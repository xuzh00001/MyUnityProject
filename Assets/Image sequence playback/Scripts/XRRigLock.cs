using UnityEngine;

public class XRRigLock : MonoBehaviour
{
    private Vector3 lockedPosition;
    private Quaternion lockedRotation;
    private bool locked = false;

    public void LockRig()
    {
        lockedPosition = transform.position;
        lockedRotation = transform.rotation;
        locked = true;
    }

    public void UnlockRig()
    {
        locked = false;
    }

    void LateUpdate()
    {
        if (!locked) return;

        transform.position = lockedPosition;
        transform.rotation = lockedRotation;
    }
}
