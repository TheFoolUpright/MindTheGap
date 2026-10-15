using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum BagResourceType
{
    Cube,
    Sphere
}

[RequireComponent(typeof(XRGrabInteractable))]
public class BagResource : MonoBehaviour
{
    [SerializeField] private BagResourceType resourceType;

    private XRGrabInteractable grabInteractable;

    public BagResourceType ResourceType => resourceType;
    public bool IsHeld => grabInteractable.isSelected;
    public bool IsStored { get; private set; }

    public event Action<BagResource> Released;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        // Ignore cancellations caused by disabling or destroying the object.
        if (args.isCanceled)
            return;

        // Another interactor may still be holding this object.
        if (grabInteractable.isSelected)
            return;

        Released?.Invoke(this);
    }

    public void Store(Transform storageParent)
    {
        IsStored = true;

        // Keep the original object for future retrieval.
        transform.SetParent(storageParent, true);
        gameObject.SetActive(false);
    }
}