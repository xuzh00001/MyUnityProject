using UnityEngine;

public class RoomLockToCamera : MonoBehaviour
{
    public Transform roomRoot;
    public Transform cameraTransform;

    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;

    private bool isLocked = false;

    void Awake()
    {
        if (roomRoot == null)
        {
            Debug.LogError("RoomRoot is not assigned!");
            return;
        }

        // save initial state
        originalParent   = roomRoot.parent;
        originalPosition = roomRoot.position;
        originalRotation = roomRoot.rotation;
        originalScale    = roomRoot.localScale;
    }

    public void LockRoomToCamera()
    {
        if (isLocked) return;

        roomRoot.SetParent(cameraTransform, worldPositionStays: true);

        isLocked = true;
        Debug.Log("Room locked to camera.");
    }

    public void UnlockRoom()
    {
        if (!isLocked) return;

        roomRoot.SetParent(originalParent, worldPositionStays: true);

        roomRoot.position = originalPosition;
        roomRoot.rotation = originalRotation;
        roomRoot.localScale = originalScale;

        isLocked = false;
        Debug.Log("Room unlocked from camera.");
    }
}
