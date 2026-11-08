
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Mind the Gap: imports finite orthogonal Tiled maps
// authored with object layers.
// One Tiled tile = one logical maze cell.
// Walls and Doors use polylines on cell boundaries.
// Lights, EnemySpawns and ResourceSpawns use point objects.
public static class TiledMazeImporter
{
    private const string AssetFolder = "Assets/MazeLayouts";
    private const float PixelTolerance = 0.1f;

    [MenuItem("Tools/Mind the Gap/Import Selected Tiled JSON")]
    private static void ImportSelected()
    {
        TextAsset source = Selection.activeObject as TextAsset;

        if (source == null)
        {
            Debug.LogError(
                "Select a Tiled .json TextAsset in the Project window.");
            return;
        }

        try
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);

            if (!sourcePath.EndsWith(
                    ".json", StringComparison.OrdinalIgnoreCase))
                throw new FormatException(
                    "Select a .json export from Tiled.");

            // Validate before modifying existing assets.
            ImportedMaze maze = ReadAndValidate(source.text);
            string assetName = Path.GetFileNameWithoutExtension(sourcePath);

            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", "MazeLayouts");

            string targetPath =
                AssetFolder + "/" + assetName + ".asset";

            MazeLayoutData layout =
                AssetDatabase.LoadAssetAtPath<MazeLayoutData>(targetPath);

            bool isNew = layout == null;

            if (isNew)
            {
                layout = ScriptableObject.CreateInstance<MazeLayoutData>();
                AssetDatabase.CreateAsset(layout, targetPath);
            }
            else
            {
                Undo.RecordObject(
                    layout, "Import Tiled maze (object layers)");
            }

            layout.SetImportedData(
                maze.layoutId,
                maze.width,
                maze.height,
                maze.cellSize,
                maze.vertical,
                maze.horizontal,
                maze.lights,
                maze.enemySpawnCells,
                maze.resourceSpawnCells
            );

            layout.SetEntranceCell(maze.entranceCell);

            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();

            Selection.activeObject = layout;
            EditorGUIUtility.PingObject(layout);

            Debug.Log(
                $"{(isNew ? "Created" : "Updated")} {targetPath}: " +
                $"{maze.width}x{maze.height} cells, " +
                $"{maze.wallCount} wall edges, " +
                $"{maze.doorCount} door edges, " +
                $"{maze.lights.Length} lights, " +
                $"{maze.enemySpawnCells.Length} enemy spawns, " +
                $"{maze.resourceSpawnCells.Length} resource spawns.",
                layout
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "Tiled maze import failed: " + ex.Message);
        }
    }

    [MenuItem(
        "Tools/Mind the Gap/Import Selected Tiled JSON", true)]
    private static bool HasSelectedJson()
    {
        TextAsset a = Selection.activeObject as TextAsset;

        return a != null &&
               AssetDatabase.GetAssetPath(a)
                   .EndsWith(
                       ".json",
                       StringComparison.OrdinalIgnoreCase);
    }

    private static ImportedMaze ReadAndValidate(string json)
    {
        TiledMap map = JsonUtility.FromJson<TiledMap>(json);

        if (map == null || map.type != "map")
            throw new FormatException(
                "File is not a Tiled JSON map.");

        if (map.infinite || map.orientation != "orthogonal")
            throw new FormatException(
                "Use a finite, orthogonal Tiled map.");

        if (map.width <= 0 || map.height <= 0 ||
            map.tilewidth <= 0 || map.tileheight <= 0)
            throw new FormatException(
                "Map and tile dimensions must be positive.");

        TiledLayer walls =
            RequireObjectLayer(map, "Walls");

        TiledLayer doors =
            RequireObjectLayer(map, "Doors");

        TiledLayer lights =
            RequireObjectLayer(map, "Lights");

        TiledLayer enemySpawns =
            RequireObjectLayer(map, "EnemySpawns");

        TiledLayer resourceSpawns =
            RequireObjectLayer(map, "ResourceSpawns");
        
        TiledLayer entrances =
            RequireObjectLayer(map, "Entrances");   

        var result = new ImportedMaze
        {
            layoutId = ReadInt(map.properties, "layoutId"),
            cellSize = ReadNumber(map.properties, "cellSize"),
            width = map.width,
            height = map.height,

            vertical = new MazeEdgeType[
                (map.width + 1) * map.height],

            horizontal = new MazeEdgeType[
                map.width * (map.height + 1)]
        };

        if (result.layoutId < 0 || result.cellSize <= 0f)
            throw new FormatException(
                "layoutId must be >= 0 and cellSize must be > 0.");

        // Import walls and doors.
        ReadPolylineLayer(
            map, walls, result, MazeEdgeType.Wall);

        ReadPolylineLayer(
            map, doors, result, MazeEdgeType.Door);

        // Import lights.
        var lightList = new List<MazeLightData>();
        var usedCodes = new HashSet<int>();

        foreach (TiledObject obj in
                 lights.objects ?? Array.Empty<TiledObject>())
        {
            Vector2Int cell =
                ReadPointCell(obj, map, "Lights");

            int code = ReadInt(obj.properties, "code");

            if (!usedCodes.Add(code))
                throw new FormatException(
                    $"Duplicate light control code {code}.");

            lightList.Add(new MazeLightData
            {
                id = obj.id,
                name = obj.name,
                cell = cell,
                controlCode = code
            });
        }

        result.lights = lightList.ToArray();

        // Import spawn positions.
        result.enemySpawnCells =
            ReadSpawnCells(map, enemySpawns);

        result.resourceSpawnCells =
            ReadSpawnCells(map, resourceSpawns);


        // Import the entrance position.
        TiledObject[] entranceObjects =
            entrances.objects ?? Array.Empty<TiledObject>();

        if (entranceObjects.Length != 1)
        {
            throw new FormatException(
                "The Entrances layer must contain exactly " +
                "one BaseEntrance point."
            );
        }

        TiledObject entrance = entranceObjects[0];

        if (entrance.type != "BaseEntrance")
        {
            throw new FormatException(
                "The entrance object must have type BaseEntrance."
            );
        }

        // Reuse the same coordinate conversion as lights and spawns.
        result.entranceCell =
            ReadPointCell(entrance, map, "Entrances");


        return result;
    }

    private static TiledLayer RequireObjectLayer(
        TiledMap map, string name)
    {
        foreach (TiledLayer layer in
                 map.layers ?? Array.Empty<TiledLayer>())
        {
            if (layer.name != name)
                continue;

            if (layer.type != "objectgroup")
                throw new FormatException(
                    $"'{name}' must be an Object Layer, " +
                    $"not '{layer.type}'.");

            return layer;
        }

        throw new FormatException(
            $"Missing Object Layer named '{name}'.");
    }

    private static void ReadPolylineLayer(
        TiledMap map,
        TiledLayer layer,
        ImportedMaze result,
        MazeEdgeType type)
    {
        foreach (TiledObject obj in
                 layer.objects ?? Array.Empty<TiledObject>())
        {
            if (obj.polyline == null ||
                obj.polyline.Length < 2)
                throw new FormatException(
                    $"'{obj.name}' (id {obj.id}) on " +
                    $"{layer.name} must be a polyline.");

            if (Mathf.Abs(obj.rotation) > 0.001f)
                throw new FormatException(
                    $"'{obj.name}' (id {obj.id}) has rotation. " +
                    "Edit its points instead.");

            for (int i = 1; i < obj.polyline.Length; i++)
            {
                Vector2 a = new Vector2(
                    obj.x + obj.polyline[i - 1].x,
                    obj.y + obj.polyline[i - 1].y
                );

                Vector2 b = new Vector2(
                    obj.x + obj.polyline[i].x,
                    obj.y + obj.polyline[i].y
                );

                int x1 = GridIndex(
                    a.x, map.tilewidth, map.width,
                    $"{layer.name} object {obj.id}: endpoint A X");

                int y1 = GridIndex(
                    a.y, map.tileheight, map.height,
                    $"{layer.name} object {obj.id}: endpoint A Y");

                int x2 = GridIndex(
                    b.x, map.tilewidth, map.width,
                    $"{layer.name} object {obj.id}: endpoint B X");

                int y2 = GridIndex(
                    b.y, map.tileheight, map.height,
                    $"{layer.name} object {obj.id}: endpoint B Y");

                if ((x1 == x2) == (y1 == y2))
                    throw new FormatException(
                        $"{layer.name} object {obj.id}: " +
                        $"line from ({x1},{y1}) to ({x2},{y2}) " +
                        "must be a nonzero horizontal or vertical segment.");

                int length =
                    Mathf.Abs(x2 - x1) + Mathf.Abs(y2 - y1);

                if (type == MazeEdgeType.Door &&
                    (obj.polyline.Length != 2 || length != 1))
                    throw new FormatException(
                        $"Door object {obj.id} must be " +
                        "one straight edge, exactly one cell long.");

                if (x1 == x2)
                {
                    int top = Math.Min(y1, y2);
                    int bottom = Math.Max(y1, y2);

                    for (int tileRow = top;
                         tileRow < bottom;
                         tileRow++)
                    {
                        int unityY =
                            result.height - 1 - tileRow;

                        int index =
                            unityY * (result.width + 1) + x1;

                        if (result.vertical[index] !=
                            MazeEdgeType.Open)
                            throw new FormatException(
                                "Wall/door overlap at vertical edge " +
                                $"Tiled x={x1}, row={tileRow}.");

                        result.vertical[index] = type;
                        CountEdge(result, type);
                    }
                }
                else
                {
                    int left = Math.Min(x1, x2);
                    int right = Math.Max(x1, x2);
                    int unityY = result.height - y1;

                    for (int tileCol = left;
                         tileCol < right;
                         tileCol++)
                    {
                        int index =
                            unityY * result.width + tileCol;

                        if (result.horizontal[index] !=
                            MazeEdgeType.Open)
                            throw new FormatException(
                                "Wall/door overlap at horizontal edge " +
                                $"Tiled col={tileCol}, y={y1}.");

                        result.horizontal[index] = type;
                        CountEdge(result, type);
                    }
                }
            }
        }
    }

    private static int GridIndex(
        float coordinate,
        int tilePixels,
        int max,
        string description)
    {
        if (float.IsNaN(coordinate) ||
            float.IsInfinity(coordinate))
            throw new FormatException(
                description + " is not finite.");

        int index =
            Mathf.RoundToInt(coordinate / tilePixels);

        if (index < 0 || index > max ||
            Mathf.Abs(coordinate - index * tilePixels) >
            PixelTolerance)
            throw new FormatException(
                $"{description} at pixel {coordinate} " +
                "is not on a tile-grid line " +
                $"(step {tilePixels}, " +
                $"valid 0..{max * tilePixels}). " +
                "Enable Snap to Grid for wall/door points.");

        return index;
    }

    private static void CountEdge(
        ImportedMaze result, MazeEdgeType type)
    {
        if (type == MazeEdgeType.Door)
            result.doorCount++;
        else
            result.wallCount++;
    }

    // Read all spawn points from a Tiled Object Layer.
    private static Vector2Int[] ReadSpawnCells(
        TiledMap map, TiledLayer layer)
    {
        var cells = new List<Vector2Int>();

        foreach (TiledObject obj in
                 layer.objects ?? Array.Empty<TiledObject>())
        {
            cells.Add(
                ReadPointCell(obj, map, layer.name));
        }

        return cells.ToArray();
    }

    // Convert Tiled point coordinates into a Unity maze cell.
    // Grid-boundary points are supported.
    private static Vector2Int ReadPointCell(
        TiledObject obj,
        TiledMap map,
        string layerName)
    {
        if (!obj.point ||
            Mathf.Abs(obj.rotation) > 0.001f)
            throw new FormatException(
                $"{layerName} object '{obj.name}' " +
                "must be an unrotated Point object.");

        float mapPixelWidth =
            map.width * map.tilewidth;

        float mapPixelHeight =
            map.height * map.tileheight;

        if (float.IsNaN(obj.x) ||
            float.IsNaN(obj.y) ||
            float.IsInfinity(obj.x) ||
            float.IsInfinity(obj.y) ||
            obj.x < 0f ||
            obj.x > mapPixelWidth ||
            obj.y < 0f ||
            obj.y > mapPixelHeight)
            throw new FormatException(
                $"{layerName} object '{obj.name}' " +
                "lies outside the maze.");

        // On internal boundaries, use the cell to the
        // right/below in Tiled.
        // Clamp outer right/bottom edges to the last cell.
        int tiledCol = Mathf.Clamp(
            Mathf.FloorToInt(obj.x / map.tilewidth),
            0,
            map.width - 1
        );

        int tiledRow = Mathf.Clamp(
            Mathf.FloorToInt(obj.y / map.tileheight),
            0,
            map.height - 1
        );

        // Tiled Y increases downward.
        // Unity logical cell Y increases upward.
        int unityY =
            map.height - 1 - tiledRow;

        return new Vector2Int(tiledCol, unityY);
    }

    private static float ReadNumber(
        TiledProperty[] props, string name)
    {
        foreach (TiledProperty prop in
                 props ?? Array.Empty<TiledProperty>())
        {
            if (prop.name != name)
                continue;

            if (prop.type != "int" &&
                prop.type != "float")
                throw new FormatException(
                    $"'{name}' must be numeric.");

            if (float.IsNaN(prop.value) ||
                float.IsInfinity(prop.value))
                throw new FormatException(
                    $"'{name}' must be finite.");

            return prop.value;
        }

        throw new FormatException(
            $"Missing numeric property '{name}'.");
    }

    private static int ReadInt(
        TiledProperty[] props, string name)
    {
        float value = ReadNumber(props, name);

        if (!Mathf.Approximately(
                value, Mathf.Round(value)))
            throw new FormatException(
                $"'{name}' must be a whole number.");

        return Mathf.RoundToInt(value);
    }

    private sealed class ImportedMaze
    {
        public int layoutId;
        public int width;
        public int height;
        public float cellSize;
        public Vector2Int entranceCell;

        public MazeEdgeType[] vertical;
        public MazeEdgeType[] horizontal;

        public MazeLightData[] lights;

        public Vector2Int[] enemySpawnCells;
        public Vector2Int[] resourceSpawnCells;

        public int wallCount;
        public int doorCount;
    }

    // JsonUtility ignores unused fields in Tiled JSON.
    [Serializable]
    private sealed class TiledMap
    {
        public string type;
        public string orientation;
        public bool infinite;

        public int width;
        public int height;
        public int tilewidth;
        public int tileheight;

        public TiledLayer[] layers;
        public TiledProperty[] properties;
    }

    [Serializable]
    private sealed class TiledLayer
    {
        public string name;
        public string type;
        public TiledObject[] objects;
    }

    [Serializable]
    private sealed class TiledObject
    {
        public int id;
        public string name;
        public string type;
        public bool point;

        public float x;
        public float y;
        public float rotation;

        public TiledPoint[] polyline;
        public TiledProperty[] properties;
    }

    [Serializable]
    private struct TiledPoint
    {
        public float x;
        public float y;
    }

    [Serializable]
    private sealed class TiledProperty
    {
        public string name;
        public string type;
        public float value;
    }
}
