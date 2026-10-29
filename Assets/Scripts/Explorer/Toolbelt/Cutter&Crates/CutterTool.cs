using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class CutterTool : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cuttingTip;

    private XRGrabInteractable grabInteractable;
    private IXRSelectInteractor holdingHand;
    private bool useRequested;

    // Public properties for other scripts to access the tool's state.
    public Transform CuttingTip
    {
        get { return cuttingTip; }
    }

    public bool IsHeldByHand
    {
        get { return isActiveAndEnabled && holdingHand != null; }
    }

    public bool IsUseRequested
    {
        // Checks whether the holding hand has activated the tool.
        // "get" lets other scripts read this value as a property.
        get
        {
            return IsHeldByHand && cuttingTip != null && useRequested;
        }
    }
    // End of the public properties.

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (cuttingTip == null)
        {
            Debug.LogWarning(
                "CutterTool: Where the heck is 'CuttingTip' O_o???",
                this
            );
        }
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
        grabInteractable.activated.AddListener(OnActivated);
        grabInteractable.deactivated.AddListener(OnDeactivated);

        // Restore the holding hand if this component was re-enabled mid-grab.
        foreach (var interactor in grabInteractable.interactorsSelecting)
        {
            if (interactor is XRSocketInteractor)
                continue;

            holdingHand = interactor;
            break;
        }
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
        grabInteractable.activated.RemoveListener(OnActivated);
        grabInteractable.deactivated.RemoveListener(OnDeactivated);

        holdingHand = null;
        useRequested = false;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // In this rig, non-socket selectors are the player's hand interactors.
        if (args.interactorObject is XRSocketInteractor)
            return;

        holdingHand = args.interactorObject;
        useRequested = false;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        // Releasing the tool always cancels use by that hand.
        if (ReferenceEquals(holdingHand, args.interactorObject))
        {
            holdingHand = null;
            useRequested = false;
        }
    }

    private void OnActivated(ActivateEventArgs args)
    {
        // Only the hand currently holding the tool may activate it.
        if (holdingHand != null &&
            ReferenceEquals(holdingHand, args.interactorObject))
        {
            useRequested = true;
        }
    }

    private void OnDeactivated(DeactivateEventArgs args)
    {
        if (ReferenceEquals(holdingHand, args.interactorObject))
            useRequested = false;
    }
}