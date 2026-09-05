using UnityEngine;
using Unity.Netcode;

public class LocalPlayerCamera : MonoBehaviour
{
    private Transform target;

    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = target.position + offset;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}