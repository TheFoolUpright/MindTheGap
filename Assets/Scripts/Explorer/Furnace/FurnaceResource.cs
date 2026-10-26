
using UnityEngine;

public enum FurnaceResourceType
{
    Cube,
    Pyramid
}

public class FurnaceResource : MonoBehaviour
{
    [SerializeField]
    private FurnaceResourceType resourceType;

    public FurnaceResourceType ResourceType => resourceType;
}
