using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTurning : NetworkBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 10f;

    private float xRotation = 0f;
    private bool isLooking = false; 

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            SetLookMode(true);
        }
        else if (cameraTransform != null)
        {
            cameraTransform.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleCursorToggle();

        if (isLooking)
        {
            HandleLook();
        }
    }

    private void HandleCursorToggle()
    {
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            SetLookMode(!isLooking);
        }
    }

    private void SetLookMode(bool state)
    {
        isLooking = state;
        Cursor.lockState = isLooking ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLooking;
    }

    private void HandleLook()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        float mouseX = mouseDelta.x * mouseSensitivity * 0.1f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.1f;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); 

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseX);
    }
}