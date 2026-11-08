using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(MazeRefresh))]
public class ResourceSpawn : MonoBehaviour
{
    private const int CubeThreshold = 35;
    private const int PyramidThreshold = 70;

    [SerializeField] private BagResource cubePrefab;
    [SerializeField] private BagResource pyramidPrefab;
    [SerializeField] private CutterTool cutter;

    private MazeRefresh mazeRefresh;

    private void Awake()
    {
        mazeRefresh = GetComponent<MazeRefresh>();

        if (cutter == null)
            Debug.LogWarning("ResourceSpawn: assign the scene's Cutter Tool.", this);
    }

    private void OnEnable()
    {
        mazeRefresh.LayoutWillRefresh += RemoveLooseResources;
    }

    private void OnDisable()
    {
        mazeRefresh.LayoutWillRefresh -= RemoveLooseResources;
    }

    public void Populate(GameObject spawner)
    {
        if (cutter != null)
        {
            foreach (CuttingProgress cutting in spawner.GetComponentsInChildren<CuttingProgress>())
                cutting.SetCutter(cutter);
        }

        if (cubePrefab == null || pyramidPrefab == null)
        {
            Debug.LogWarning("ResourceSpawn: assign both resource prefabs.", this);
            return;
        }

        foreach (ResourceContainer container in spawner.GetComponentsInChildren<ResourceContainer>())
        {
            Transform points = container.transform.Find("Resources");
            if (points == null)
            {
                Debug.LogWarning("ResourceSpawn: missing Resources child.", container);
                continue;
            }

            foreach (Transform point in points)
            {
                if (!point.name.StartsWith("ResourcePoint", System.StringComparison.Ordinal))
                    continue;

                BagResource prefab = RollResource();
                if (prefab == null)
                    continue;

                BagResource resource = Instantiate(prefab, point.position, point.rotation, point);
                XRGrabInteractable grab = resource.GrabInteractable;
                grab.enabled = false;

                Rigidbody body = resource.GetComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                container.RegisterSpawnedResource(grab);
            }
        }
    }

    private BagResource RollResource()
    {
        int roll = Random.Range(0, 100);

        if (roll < CubeThreshold)
            return cubePrefab;

        if (roll < PyramidThreshold)
            return pyramidPrefab;

        return null;
    }

    private void RemoveLooseResources(Transform previousLayout)
    {
        BagResource[] resources = FindObjectsByType<BagResource>(
            FindObjectsInactive.Include, FindObjectsSortMode.None
        );

        foreach (BagResource resource in resources)
        {
            if (resource.gameObject.scene != gameObject.scene || resource.IsStored)
                continue;

            if (IsHeldByHand(resource.GrabInteractable))
            {
                if (resource.transform.IsChildOf(previousLayout))
                    resource.transform.SetParent(null, true);

                continue;
            }

            resource.gameObject.SetActive(false);
            Destroy(resource.gameObject);
        }
    }

    private static bool IsHeldByHand(XRGrabInteractable grab)
    {
        if (grab == null)
            return false;

        foreach (var interactor in grab.interactorsSelecting)
        {
            if (interactor is not XRSocketInteractor)
                return true;
        }
    }
}