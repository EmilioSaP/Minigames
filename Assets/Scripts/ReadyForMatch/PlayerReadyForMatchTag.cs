using TMPro;
using UnityEngine;

public class PlayerReadyForMatchTag : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;

    private Transform localCamera;

    private void Start()
    {
        Camera cam = Camera.main;

        if (cam != null)
            localCamera = cam.transform;
    }

    private void LateUpdate()
    {
        if (localCamera == null)
            return;

        Vector3 direction = localCamera.position - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(-direction);
        }
    }

    public void SetReadyTag()
    {
        nameText.text = "[Ready]";
    }

    public void SetNotReadyTag()
    {
        nameText.text = "[Not Ready]";
    }
}