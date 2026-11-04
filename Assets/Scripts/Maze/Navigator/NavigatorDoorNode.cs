
using UnityEngine;

public class NavigatorDoorNode : MonoBehaviour
{
    [Header("Door Identity")]
    [SerializeField] private int edgeX;
    [SerializeField] private int edgeY;
    [SerializeField] private bool isVertical;

    public int EdgeX => edgeX;
    public int EdgeY => edgeY;
    public bool IsVertical => isVertical;

    public string DoorId =>
        $"{(isVertical ? "V" : "H")}_{edgeX}_{edgeY}";

    public void Configure(int x, int y, bool vertical)
    {
        edgeX = x;
        edgeY = y;
        isVertical = vertical;
    }
}
