using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
    [SerializeField] private GameObject pyramidSlot;

    [Header("Storage")]
    [SerializeField] private Transform storedObjectsRoot;

    private readonly List<BagResource> storedResources = new();

    // Track every collider so compound objects are handled correctly.
    private readonly Dictionary<BagResource, HashSet<Collider>>
        overlappingResources = new();

    private readonly HashSet<BagResource> pendingReleases = new();

    private Color defaultColor;
    private BagResourceType? storedType;
    private BagSlotInteractable slotInteraction;

    public int Count => storedResources.Count;

    private void Awake()
    {
        slotInteraction = GetComponent<BagSlotInteractable>();
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

            Debug.Log($"{name}: tracking {resource.name}",this);
  
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
        Debug.Log(
            $"{name}: released {resource.name}, held={resource.IsHeld}",
            this
        );
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

    public bool TryTakeResource(XRBaseInputInteractor hand, XRInteractionManager manager)
    {
        if (!isActiveAndEnabled || Count == 0)
            return false;

        if (hand == null || !hand.isActiveAndEnabled || hand.hasSelection)
            return false;

        if (manager == null || hand.interactionManager != manager)
            return false;

        int resourceIndex = Count - 1;
        BagResource resource = storedResources[resourceIndex];

        if (resource == null)
            return false;

        XRGrabInteractable grab = resource.GrabInteractable;

        // Restore the original object close to the hand's attachment point.
        Transform handAttach = hand.GetAttachTransform(grab);

        resource.RestoreFromStorage(
            handAttach.position,
            handAttach.rotation
        );

        bool layersMatch =
            (hand.interactionLayers.value & grab.interactionLayers.value) != 0;

        bool canSelect = grab.interactionManager == manager
            && layersMatch
            && manager.CanSelect(
                (IXRSelectInteractor)hand,
                (IXRSelectInteractable)grab
            );

        if (canSelect)
        {
            manager.SelectEnter(
                (IXRSelectInteractor)hand,
                (IXRSelectInteractable)grab
            );
        }

        if (!grab.IsSelected((IXRSelectInteractor)hand))
        {
            // Keep the inventory unchanged if the hand cannot take the object.
            resource.Store(storedObjectsRoot);
            return false;
        }

        // Remove the entry only after the hand successfully receives it.
        storedResources.RemoveAt(resourceIndex);

        if (Count == 0)
            storedType = null;

        RefreshDisplay();
        return true;
    }

    private void RefreshHighlight()
    {
        if (slotImage == null)
            return;

        bool canStoreResource = false;

        foreach (var entry in overlappingResources)
        {
            BagResource resource = entry.Key;

            if (resource == null || !resource.IsHeld)
                continue;

            if (IsInsideSlot(resource) && CanAccept(resource))
            {
                canStoreResource = true;
                break;
            }
        }

        bool canTakeResource = slotInteraction != null && slotInteraction.HasFreeHandHover();

        // Both available actions currently share the same highlight color.
        if (canStoreResource || canTakeResource)
            slotImage.color = availableColor;
        else
            slotImage.color = defaultColor;
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

        if (pyramidSlot != null)
        {
            pyramidSlot.SetActive(
                Count > 0 && storedType == BagResourceType.Pyramid
            );
        }
    }
}