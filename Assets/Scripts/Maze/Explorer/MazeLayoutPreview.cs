using UnityEngine;

[ExecuteAlways]
public class MazeLayoutPreview : MonoBehaviour
{
    [SerializeField] private MazeLayoutData layout;

    private void OnDrawGizmos()
    {
        if (layout == null || !layout.IsConfigured) return;

        float size = layout.CellSize;
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        for (int y = 0; y < layout.Height; y++)
            for (int x = 0; x <= layout.Width; x++)
            {
                Vector3 start = origin + new Vector3(x * size, 0f, y * size);
                DrawEdge(layout.GetVerticalEdge(x, y), start,
                    start + Vector3.forward * size);
            }

        for (int y = 0; y <= layout.Height; y++)
            for (int x = 0; x < layout.Width; x++)
            {
                Vector3 start = origin + new Vector3(x * size, 0f, y * size);
                DrawEdge(layout.GetHorizontalEdge(x, y), start,
                    start + Vector3.right * size);
            }

        Gizmos.color = Color.yellow;
        foreach (MazeLightData light in layout.Lights)
        {
            Vector3 pos = transform.position +
                layout.GetCellCenterLocal(light.cell.x, light.cell.y) +
                Vector3.up * 0.35f;
            Gizmos.DrawSphere(pos, size * 0.08f);
        }

        if (layout.HasExplorerSpawn)
        {
            Gizmos.color = Color.green;
            Vector3 pos = transform.position +
                layout.GetCellCenterLocal(layout.ExplorerSpawnCell.x,
                                          layout.ExplorerSpawnCell.y) +
                Vector3.up * 0.35f;
            Gizmos.DrawSphere(pos, size * 0.1f);
        }
    }

    private static void DrawEdge(MazeEdgeType edge, Vector3 start, Vector3 end)
    {
        if (edge == MazeEdgeType.Open) return;
        Gizmos.color = edge == MazeEdgeType.Door ? Color.yellow : Color.cyan;
        Gizmos.DrawLine(start, end);
    }
}