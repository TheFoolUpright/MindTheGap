using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class BagInventorySlot : MonoBehaviour
{
    private const int Capacity = 3;

    [Header("Interface")]
    [SerializeField] private Image slotImage;
    [SerializeField] private TMP_Text resourceCount;

    [SerializeField]
    private Color availableColor = new Color(0.55f, 1f, 0.35f, 1f);

    [Header("Resource Previews")]
    [SerializeField] private GameObject cubeSlot;
    [SerializeField] private GameObject sphereSlot;

    [Header("Storage")]
    [SerializeField] private Transform storedObjectsRoot;

    private readonly List<BagResource> storedResources = new();

    // Track every collider so compound objects are handled correctly.
    private readonly Dictionary<BagResource, HashSet<Collider>>
        overlappingResources = new();

    private readonly HashSet<BagResource> pendingReleases = new();

    private Color defaultColor;
    private BagResourceType? storedType;

    public int Count => storedResources.Count;

    private void Awake()
    {
        defaultColor = slotImage != null? slotImage.color : Color.white;

        if (storedObjectsRoot == null)
            storedObjectsRoot = transform;

        RefreshDisplay();
    }

    private void OnDisable()
    {
        foreach (BagResource resource in overlappingResources.Keys)
        {
            if (resource != null)
                resource.Released -= OnResourceReleased;
        }

        overlappingResources.Clear();
        pendingReleases.Clear();

        if (slotImage != null)
            slotImage.color = defaultColor;

        // Stored contents remain intact when the inventory is hidden.
    }

    private void OnTriggerEnter(Collider other)
    {
        TrackResource(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Also discover objects already inside when the slot is enabled.
        TrackResource(other);
    }

    private void OnTriggerExit(Collider other)
    {
        BagResource resource = other.GetComponentInParent<BagResource>();

        if (resource == null)
            return;

        if (!overlappingResources.TryGetValue(resource, out var colliders))
            return;

        colliders.Remove(other);

        if (colliders.Count == 0)
            StopTracking(resource);
    }

    private void TrackResource(Collider other)
    {
        BagResource resource = other.GetComponentInParent<BagResource>();

        if (resource == null || resource.IsStored)
            return;

        if (!overlappingResources.TryGetValue(resource, out var colliders))
        {
            colliders = new HashSet<Collider>();
            overlappingResources.Add(resource, colliders);

            resource.Released += OnResourceReleased;
        }

        colliders.Add(other);
    }

    private void StopTracking(BagResource resource)
    {
        if (resource != null)
            resource.Released -= OnResourceReleased;

        overlappingResources.Remove(resource);
        pendingReleases.Remove(resource);
    }

    private void OnResourceReleased(BagResource resource)
    {
        // Wait until XR has finished processing the release.
        pendingReleases.Add(resource);
    }

    private void LateUpdate()
    {
        ProcessReleasedResources();
        RefreshHighlight();
    }

    private void ProcessReleasedResources()
    {
        if (pendingReleases.Count == 0)
            return;

        // A copy allows storage to remove tracked entries safely.
        var releasedResources = new List<BagResource>(pendingReleases);
        pendingReleases.Clear();

        foreach (BagResource resource in releasedResources)
        {
            if (resource == null || resource.IsHeld)
                continue;

            if (!IsInsideSlot(resource) || !CanAccept(resource))
                continue;

            StoreResource(resource);
        }
    }

    private bool IsInsideSlot(BagResource resource)
    {
        if (!overlappingResources.TryGetValue(resource, out var colliders))
            return false;

        foreach (Collider resourceCollider in colliders)
        {
            if (resourceCollider != null && resourceCollider.enabled && resourceCollider.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanAccept(BagResource resource)
    {
        if (resource.IsStored || Count >= Capacity)
            return false;

        // An empty slot accepts either resource type.
        return !storedType.HasValue
            || storedType.Value == resource.ResourceType;
    }

    private void StoreResource(BagResource resource)
    {
        if (!storedType.HasValue)
            storedType = resource.ResourceType;

        storedResources.Add(resource);

        // Unsubscribe before disabling the original object.
        StopTracking(resource);
        resource.Store(storedObjectsRoot);

        RefreshDisplay();
    }

    private void RefreshHighlight()
    {
        if (slotImage == null)
            return;

        bool hasValidResource = false;

        foreach (var entry in overlappingResources)
        {
            BagResource resource = entry.Key;

            if (resource == null || !resource.IsHeld)
                continue;

            if (IsInsideSlot(resource) && CanAccept(resource))
            {
                hasValidResource = true;
                break;
            }
        }

        slotImage.color = hasValidResource? availableColor : defaultColor;
    }

    private void RefreshDisplay()
    {
        if (resourceCount != null)
        {
            resourceCount.text = Count == 0? string.Empty : Count.ToString();
        }

        if (cubeSlot != null)
        {
            cubeSlot.SetActive(
                Count > 0 && storedType == BagResourceType.Cube
            );
        }

        if (sphereSlot != null)
        {
            sphereSlot.SetActive(
                Count > 0 && storedType == BagResourceType.Sphere
            );
        }
    }
}