using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ResourceContainer : MonoBehaviour
{
    [Header("Lid")]
    [SerializeField] private XRGrabInteractable lid;
    [SerializeField] private Transform lidVisual;
    [SerializeField] private BoxCollider lidCollider;

    [Header("Opening Settings")]
    [SerializeField] private float lidSizeMultiplier = 1.03125f;
    [SerializeField] private float lidLift = 0.003f;

    [Header("Contents")]
    [SerializeField] private XRGrabInteractable[] resources;

    [Header("Cutting Visuals")]
    [SerializeField] private GameObject cuttingUI;
    [SerializeField] private CuttingSeamUI seamUI;

    [Header("Cleanup")]
    [SerializeField] private float lidLifetime = 10f;
    [SerializeField] private float releaseDelay = 0.5f;

    private bool opened;
    private float deleteTime;
    private float releaseDeleteTime = -1f;

    public void Open()
    {
        // Opening must happen only once.
        if (opened)
            return;

        if (lid == null || lidVisual == null || lidCollider == null)
        {
            Debug.LogError(
                "ResourceContainer: assign all lid references.",
                this
            );

            return;
        }

        Rigidbody lidBody = lid.GetComponent<Rigidbody>();

        if (lidBody == null)
        {
            Debug.LogError(
                "ResourceContainer: the lid needs a Rigidbody.",
                this
            );

            return;
        }

        opened = true;

        // Resize only the horizontal dimensions.
        Vector3 visualScale = lidVisual.localScale;
        visualScale.x *= lidSizeMultiplier;
        visualScale.z *= lidSizeMultiplier;
        lidVisual.localScale = visualScale;

        Vector3 colliderSize = lidCollider.size;
        colliderSize.x *= lidSizeMultiplier;
        colliderSize.z *= lidSizeMultiplier;
        lidCollider.size = colliderSize;

        // Add a small clearance before enabling physics.
        lidBody.position += transform.up * lidLift;

        lidBody.useGravity = true;
        lidBody.isKinematic = false;
        lid.enabled = true;

        // Stop the UI script before hiding its canvases.
        if (seamUI != null)
            seamUI.enabled = false;

        if (cuttingUI != null)
            cuttingUI.SetActive(false);

        foreach (XRGrabInteractable resource in resources)
        {
            if (resource == null)
                continue;

            Rigidbody resourceBody = resource.GetComponent<Rigidbody>();

            if (resourceBody != null)
            {
                resourceBody.useGravity = true;
                resourceBody.isKinematic = false;
            }

            resource.enabled = true;
        }

        deleteTime = Time.time + lidLifetime;
    }

    private void Update()
    {
        if (!opened || lid == null || Time.time < deleteTime)
            return;

        // Do not remove the lid while the player is holding it.
        if (lid.isSelected)
        {
            releaseDeleteTime = -1f;
            return;
        }

        // Allow a short delay before deletion, including after release.
        if (releaseDeleteTime < 0f)
            releaseDeleteTime = Time.time + releaseDelay;

        if (Time.time >= releaseDeleteTime)
            Destroy(lid.gameObject);
    }

    private void OnDestroy()
    {
        // Also remove a detached or held lid when its crate is destroyed.
        if (opened && lid != null)
            Destroy(lid.gameObject);
    }
}