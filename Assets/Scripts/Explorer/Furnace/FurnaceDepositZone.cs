
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FurnaceDepositZone : MonoBehaviour
{
    [SerializeField]
    private FurnaceController furnace;

    private void OnTriggerEnter(Collider other)
    {
        FurnaceResource resource =
            other.GetComponentInParent<FurnaceResource>();

        if (resource != null)
            furnace.AddResource(resource);
    }

    private void OnTriggerExit(Collider other)
    {
        FurnaceResource resource =
            other.GetComponentInParent<FurnaceResource>();

        if (resource != null)
            furnace.RemoveResource(resource);
    }
}
