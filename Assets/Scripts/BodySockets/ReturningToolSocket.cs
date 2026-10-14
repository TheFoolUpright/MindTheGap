using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ReturningToolSocket : XRSocketInteractor
{
    private XRGrabInteractable homeTool;
    private Coroutine returnRoutine;

    // "protected" allows this class and its subclasses to access the method.
    // "override" extends the virtual OnEnable method inherited from XRSocketInteractor instead of hiding it with a separate method.
    protected override void OnEnable()
    {
        // Run the original XRSocketInteractor initialization first.
        // This preserves its built-in setup and registration logic.
        base.OnEnable();

        // "as" attempts to treat the assigned interactable as a grab interactable.
        // If it is missing or has a different type, the result is null.
        homeTool = startingSelectedInteractable as XRGrabInteractable;

        if (homeTool == null)
        {
            Debug.LogWarning(
                $"{name}: assign an XR Grab Interactable " +
                "to Starting Selected Interactable.",
                this);
            // Stop initialization because there is no valid tool to monitor.
            return;
        }

        // Subscribe to the tool's selection-exit event.
        // Unity calls OnToolReleased whenever an interactor stops selecting it.
        homeTool.selectExited.AddListener(OnToolReleased);
    }

    protected override void OnDisable()
    {
        // Unsubscribe when this socket is disabled.
        // This prevents the disabled socket from reacting to TOOL EVENTS.
        if (homeTool != null)
            homeTool.selectExited.RemoveListener(OnToolReleased);

        // Cancel any pending return operation.
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        // Preserve the original socket cleanup and unregistration logic.
        base.OnDisable();
    }

    private void OnToolReleased(SelectExitEventArgs args) // "args" short for "arguments".
    {
        // SelectExitEventArgs contains information about this specific event.

        // args.interactorObject identifies the interactor that stopped
        // selecting the tool, such as a hand or this socket.

        // "this" refers to this ReturningToolSocket component.
        // ReferenceEquals checks whether both references identify
        // the exact same instance.
        if (ReferenceEquals(args.interactorObject, this))
        {
            // Taking the tool out of this socket must not trigger a return.
            return;
        }

        // Replace any previous pending return with a new one.
        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        // StartCoroutine runs the IEnumerator over multiple frames.
        returnRoutine = StartCoroutine(ReturnTool());
    }

    private IEnumerator ReturnTool()
    {
        // Wait until the next frame so XR Toolkit can finish processing
        // the release or a transfer to another hand.
        yield return null;

        // Also allow a physics update before attempting to reattach the tool.
        yield return new WaitForFixedUpdate();

        // Return only if:
        // - the assigned tool still exists and is enabled;
        // - no hand or other interactor is selecting it;
        // - this socket is active and empty;
        // - an interaction manager is available;
        // - the socket's selection rules accept the tool.
        if (homeTool != null && homeTool.isActiveAndEnabled && !homeTool.isSelected && socketActive && !hasSelection && interactionManager != null && CanSelect((IXRSelectInteractable)homeTool))
        {
            Rigidbody toolBody = homeTool.GetComponent<Rigidbody>();

            // Remove release momentum before reattaching the tool.
            // Kinematic rigidbodies do not need their velocity reset here.
            if (toolBody != null && !toolBody.isKinematic)
            {
                toolBody.linearVelocity = Vector3.zero;
                toolBody.angularVelocity = Vector3.zero;
            }

            
            // The casts explicitly use the interface-based SelectEnter overload.
            // They do not create or replace objects; they use existing instances.
            
            // SelectEnter starts a selection through XR Interaction Manager.
            // The socket's standard grab logic then attaches the tool
            // to its current Attach Transform.
            interactionManager.SelectEnter((IXRSelectInteractor)this,(IXRSelectInteractable)homeTool);
        }

        // The return operation has finished.
        returnRoutine = null;
    }
}