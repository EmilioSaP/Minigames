using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;

    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsJumping { get; private set; }

    private CharacterController characterController;
    private Vector3 verticalVelocity; // Renamed to clarify it only handles Up/Down
    private bool sprintToggled;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        // 1. Check ground state
        bool isGrounded = characterController.isGrounded;
        if (isGrounded && verticalVelocity.y < 0)
        {
            // Push the player slightly into the ground to ensure they stay grounded on slopes
            verticalVelocity.y = -2f; 
            IsJumping = false;
        }

        // 2. Read Inputs
        Vector2 input = Vector2.zero;
        IsSprinting = false;
        bool jumpPressed = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;

            bool sprintPressedThisFrame = Keyboard.current.shiftKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame;
            if (sprintPressedThisFrame)
            {
                sprintToggled = !sprintToggled;
            }

            IsSprinting = sprintToggled;
            jumpPressed = Keyboard.current.spaceKey.isPressed;
        }

        // 3. Calculate Horizontal Movement
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move.Normalize();
        
        IsMoving = move.sqrMagnitude > 0.01f;
        float currentSpeed = IsSprinting ? sprintSpeed : walkSpeed;
        Vector3 horizontalVelocity = move * currentSpeed;

        // 4. Calculate Jump
        if (jumpPressed && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            IsJumping = true;
        }

        // 5. Apply Gravity
        verticalVelocity.y += gravity * Time.deltaTime;

        // 6. COMBINE ALL MOVEMENT INTO A SINGLE MOVE CALL
        // This prevents the "micro-bounce" that causes isGrounded to flicker on WebGL
        Vector3 finalMovement = horizontalVelocity + verticalVelocity;
        characterController.Move(finalMovement * Time.deltaTime);
    }
}