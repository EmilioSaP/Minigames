using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTurning : NetworkBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float mouseSensitivity = 10f;
    [SerializeField] private float verticalLookLimit = 90f;

    private float xRotation;
    private bool isLooking;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        SetLookMode(true);
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        HandleCursorToggle();

        if (isLooking)
        {
            HandleLook();
        }
    }

    private void HandleCursorToggle()
    {
        if (Keyboard.current == null)
            return;
    }

    public void SetLookMode(bool state)
    {
        isLooking = state;

        Cursor.lockState = state
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !state;
    }

    private void HandleLook()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity * 0.1f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.1f;

        // Vertical look
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(
            xRotation,
            -verticalLookLimit,
            verticalLookLimit
        );

        if (cameraPivot != null)
        {
            cameraPivot.localRotation =
                Quaternion.Euler(xRotation, 0f, 0f);
        }

        // Horizontal look
        transform.Rotate(Vector3.up * mouseX);
    }
}