using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

[System.Serializable]
public class bodySocket
{
    public GameObject gameObject;
    [Range(0.01f, 1f)]
    public float heightRatio;
}

// Keeps body sockets (toolbelt, bag) at a fraction of the player's head
// height, measured from the XR Origin's floor in world space. Works in both
// Floor and Device tracking modes and with any Camera Y Offset, unlike the
// original version, which used the head's local height and put the belt at
// face level in Device mode.
public class BodySocketInventory : MonoBehaviour
{
    public GameObject HMD;
    public bodySocket[] bodySockets;

    [Tooltip("The rig's floor. Defaults to the XR Origin this belongs to.")]
    public Transform floorReference;

    private void Start()
    {
        if (floorReference == null)
        {
            XROrigin origin = GetComponentInParent<XROrigin>();
            floorReference = origin != null ? origin.transform : transform.parent;
        }
    }

    void Update()
    {
        if (HMD == null)
            return;

        Vector3 head = HMD.transform.position;
        float floorY = floorReference != null ? floorReference.position.y : 0f;
        float headHeight = Mathf.Max(0f, head.y - floorY);

        // Follow the head horizontally, stay on the floor vertically.
        transform.position = new Vector3(head.x, floorY, head.z);

        // Face the way the head faces, ignoring pitch and roll.
        Vector3 forward = Vector3.ProjectOnPlane(HMD.transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

        foreach (var bodySocket in bodySockets)
        {
            if (bodySocket == null || bodySocket.gameObject == null)
                continue;

            Transform socket = bodySocket.gameObject.transform;
            Vector3 position = socket.position;
            position.y = floorY + headHeight * bodySocket.heightRatio;
            socket.position = position;
        }
    }
}
