using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class BagInventoryDisplay : MonoBehaviour
{
    [SerializeField] private GameObject inventory;

    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        SetInventoryVisible(false);
    }

    private void LateUpdate()
    {
        bool isHeld = false;

        foreach (var interactor in grabInteractable.interactorsSelecting)
        {
            // A body socket must not count as a hand holding the bag.
            if (interactor is XRSocketInteractor)
                continue;

            isHeld = true;
            break;
        }

        SetInventoryVisible(isHeld);
    }

    private void OnDisable()
    {
        SetInventoryVisible(false);
    }

    private void SetInventoryVisible(bool visible)
    {
        if (inventory == null)
            return;

        // Only change the active state when necessary.
        if (inventory.activeSelf != visible)
            inventory.SetActive(visible);
    }
}