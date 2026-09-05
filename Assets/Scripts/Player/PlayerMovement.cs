using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Look Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 10f;

    private CharacterController characterController;
    private Vector3 velocity;
    private float xRotation = 0f;
    private bool isLooking = false; 

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // Only the local player should have an active camera and start in look mode
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

        HandleMovement();
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
        
        // Scale down the raw delta to make sensitivity values more manageable
        float mouseX = mouseDelta.x * mouseSensitivity * 0.1f;
        float mouseY = mouseDelta.y * mouseSensitivity * 0.1f;

        // Vertical rotation (Camera)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Prevent neck-breaking

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        // Horizontal rotation (Player Body)
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        // 1. Gravity and Ground Check
        bool isGrounded = characterController.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Slight downward force to keep grounded
        }

        // 2. Read Inputs
        Vector2 input = Vector2.zero;
        bool isSprinting = false;
        bool jumpPressed = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;

            // Sprint mapping: Shift or Q
            isSprinting = Keyboard.current.shiftKey.isPressed || Keyboard.current.qKey.isPressed;
            
            // Jump mapping: Space
            jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        // 3. Apply Movement (Relative to player's rotation)
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move.Normalize();

        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;
        characterController.Move(move * currentSpeed * Time.deltaTime);

        // 4. Apply Jump
        if (jumpPressed && isGrounded)
        {
            // Physics formula for calculating jump velocity required to reach a specific height
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // 5. Apply Gravity
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }
}