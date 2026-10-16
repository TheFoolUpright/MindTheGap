using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum BagResourceType
{
    Cube = 0,
    Pyramid = 1
}

[RequireComponent(typeof(XRGrabInteractable))]
public class BagResource : MonoBehaviour
{
    [SerializeField] private BagResourceType resourceType;

    private XRGrabInteractable grabInteractable;
    private Rigidbody body;
    private Vector3 storedWorldScale;

    public BagResourceType ResourceType => resourceType;
    public XRGrabInteractable GrabInteractable => grabInteractable;
    public bool IsHeld => grabInteractable.isSelected;
    public bool IsStored { get; private set; }

    public event Action<BagResource> Released;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        body = GetComponent<Rigidbody>();
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
        // Ignore releases caused by disabling or destroying the object.
        if (args.isCanceled || grabInteractable.isSelected)
            return;

        Released?.Invoke(this);
    }

    public void Store(Transform storageParent)
    {
        // Remember the size before inheriting the bag's transform.
        storedWorldScale = transform.lossyScale;

        IsStored = true;

        transform.SetParent(storageParent, true);
        gameObject.SetActive(false);
    }

    public void RestoreFromStorage(Vector3 position, Quaternion rotation)
    {
        // Detach before activation so the bag cannot become its parent again.
        transform.SetParent(null, true);
        transform.localScale = storedWorldScale;
        transform.SetPositionAndRotation(position, rotation);

        IsStored = false;
        gameObject.SetActive(true);

        // Remove any motion left over from the previous release.
        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}