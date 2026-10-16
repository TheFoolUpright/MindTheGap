using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(BagInventorySlot))]
[RequireComponent(typeof(BoxCollider))]
public class BagSlotInteractable : XRSimpleInteractable
{
    private BagInventorySlot inventorySlot;
    private BoxCollider slotTrigger;
    private XRBaseInputInteractor pendingHand;

    protected override void Awake()
    {
        // Initialize the standard XR simple interactable behaviour.
        base.Awake();

        inventorySlot = GetComponent<BagInventorySlot>();
        slotTrigger = GetComponent<BoxCollider>();
    }

    protected override void OnDisable()
    {
        pendingHand = null;
        base.OnDisable();
    }

    private bool IsHandInside(XRBaseInputInteractor hand)
    {
        if (hand == null || !hand.isActiveAndEnabled)
            return false;

        // Check the actual hand position, not a distant ray hit.
        Vector3 localPosition = slotTrigger.transform.InverseTransformPoint(
            hand.transform.position
        );

        Vector3 offset = localPosition - slotTrigger.center;
        Vector3 halfSize = slotTrigger.size * 0.5f;

        return Mathf.Abs(offset.x) <= halfSize.x
            && Mathf.Abs(offset.y) <= halfSize.y
            && Mathf.Abs(offset.z) <= halfSize.z;
    }

    public bool HasFreeHandHover()
    {
        if (inventorySlot.Count == 0)
            return false;

        foreach (var interactor in interactorsHovering)
        {
            if (interactor is not XRBaseInputInteractor hand)
                continue;

            if (!hand.hasSelection && IsHandInside(hand))
                return true;
        }

        return false;
    }

    public override bool IsHoverableBy(IXRHoverInteractor interactor)
    {
        if (interactor is not XRBaseInputInteractor hand)
            return false;

        return IsHandInside(hand) && base.IsHoverableBy(interactor);
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        if (interactor is not XRBaseInputInteractor hand)
            return false;

        if (inventorySlot.Count == 0 || !IsHandInside(hand))
            return false;

        // Allow an existing slot selection to remain valid until transfer.
        if (hand.hasSelection && !IsSelected(interactor))
            return false;

        return base.IsSelectableBy(interactor);
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        // Process the transfer after XR finishes its selection callbacks.
        pendingHand = args.interactorObject as XRBaseInputInteractor;
    }

    private void LateUpdate()
    {
        XRBaseInputInteractor hand = pendingHand;
        pendingHand = null;

        if (hand == null || !IsSelected((IXRSelectInteractor)hand))
            return;

        bool canTransfer = IsHandInside(hand)
            && hand.interactablesSelected.Count == 1;

        XRInteractionManager manager = interactionManager;

        // Release the slot before asking the hand to hold a resource.
        manager.SelectExit(
            (IXRSelectInteractor)hand,
            (IXRSelectInteractable)this
        );

        if (canTransfer && !hand.hasSelection)
            inventorySlot.TryTakeResource(hand, manager);
    }
}