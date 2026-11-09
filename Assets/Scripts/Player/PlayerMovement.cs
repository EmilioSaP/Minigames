
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;

    [Header("Spectator")]
    [SerializeField, Min(0f)] private float spectatorSpeed = 12f;
    [SerializeField, Min(0f)] private float spectatorVerticalSpeed = 8f;

    [Header("Ground Movement")]
    [SerializeField] private float groundAcceleration = 22f;
    [SerializeField] private float groundDeceleration = 25f;

    [Header("Air Movement")]
    [SerializeField] private float airAcceleration = 7f;
    [SerializeField] private float airDeceleration = 2.5f;

    [Header("Jump and Gravity")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Jump Settings")]
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField, Min(0f)] private float minimumJumpHeight = 0.35f;
    [SerializeField, Min(0.01f)] private float jumpHoldDuration = 0.25f;

    private float coyoteTimer;
    private float jumpHoldTimer;
    private bool isAirborne;
    private bool jumpWasHeld;

    public bool IsAirborne => isAirborne;

    [Header("External Forces")]
    [SerializeField] private float externalVelocityDecay = 14f;
    [SerializeField] private float maxExternalSpeed = 12f;

    [Header("Audio")]
    [SerializeField] private NetworkPlayerAudio networkPlayerAudio;
    

    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsJumping { get; private set; }

    public bool IsMovementEnabled => movementEnabled;

    public bool CanReceiveMovementInput =>
        IsOwner && movementEnabled && !motionFrozen;

    // Normal movement momentum.
    private Vector3 horizontalVelocity;

    // Additional forces, independent of normal movement.
    private Vector3 externalVelocity;

    // Vertical movement and gravity.
    private float verticalVelocity;

    private CharacterController characterController;

    private bool movementEnabled = true;
    private bool motionFrozen;
    private bool isSpectator;

    public bool IsSpectator => isSpectator;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!IsOwner || characterController == null ||
            !characterController.enabled)
        {
            return;
        }

        if (motionFrozen)
        {
            // A frozen player does not receive gravity or momentum.
            ResetMotion();
            return;
        }

        HandleMovement();
    }

    // --------------------------------------------------
    // MOVEMENT RESTRICTIONS
    // --------------------------------------------------

    public void SetMovementEnabled(bool state)
    {
        movementEnabled = state;

        if (!state)
        {
            // Remove existing horizontal momentum when controls
            // are restricted, but keep gravity active.
            horizontalVelocity = Vector3.zero;
            externalVelocity = Vector3.zero;

            IsMoving = false;
            IsSprinting = false;
            IsJumping = false;
        }
    }

    // Completely freezes or resumes the movement simulation.
    // Used for death, respawning, and other full-motion restrictions.
    public void SetMotionFrozen(bool frozen)
    {
        motionFrozen = frozen;

        if (frozen)
            ResetMotion();
    }

    public void SetSpectatorMode(bool enabled)
    {
        if (isSpectator == enabled)
            return;

        isSpectator = enabled;
        ResetMotion();
    }

    // --------------------------------------------------
    // MOVEMENT RESET
    // --------------------------------------------------

    // Clears every accumulated velocity.
    // Call this when teleporting, dying, or respawning.
    public void ResetMotion()
    {
        horizontalVelocity = Vector3.zero;
        externalVelocity = Vector3.zero;
        verticalVelocity = 0f;

        IsMoving = false;
        IsSprinting = false;
        IsJumping = false;
    }

    // --------------------------------------------------
    // EXTERNAL FORCES
    // --------------------------------------------------

    // Applies an additional horizontal impulse.
    // The vector represents an instantaneous change in velocity
    // (metres per second), not a force in Newtons.
    public void AddHorizontalImpulse(Vector3 impulse)
    {
        if (!IsOwner || motionFrozen || isSpectator)
            return;

        impulse = Vector3.ProjectOnPlane(impulse, Vector3.up);

        externalVelocity += impulse;

        externalVelocity =
            Vector3.ClampMagnitude(externalVelocity, maxExternalSpeed);
    }

    // --------------------------------------------------
    // MAIN MOVEMENT
    // --------------------------------------------------

    private void HandleMovement()
    {
        float deltaTime = Time.deltaTime;
        bool isGrounded = characterController.isGrounded;

        Vector2 input = Vector2.zero;

        bool sprintInput = false;
        bool jumpHeld = Keyboard.current != null &&
                        Keyboard.current.spaceKey.isPressed;

        if (movementEnabled && Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed ||
                Keyboard.current.upArrowKey.isPressed)
                input.y += 1f;

            if (Keyboard.current.sKey.isPressed ||
                Keyboard.current.downArrowKey.isPressed)
                input.y -= 1f;

            if (Keyboard.current.aKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed)
                input.x -= 1f;

            if (Keyboard.current.dKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed)
                input.x += 1f;

            sprintInput =
                Keyboard.current.shiftKey.isPressed ||
                Keyboard.current.qKey.isPressed;
        }

        if (isSpectator)
        {
            HandleSpectatorMovement(input, deltaTime);
            return;
        }

        HandleJumpAndGravity(jumpHeld);

        // --------------------------------------------------
        // HORIZONTAL MOVEMENT
        // --------------------------------------------------

        Vector3 forward =
            Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        Vector3 right =
            Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        Vector3 moveDirection = right * input.x + forward * input.y;

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        bool hasMovementInput = moveDirection.sqrMagnitude > 0.01f;

        IsSprinting = movementEnabled && hasMovementInput && sprintInput;

        float currentSpeed = IsSprinting ? sprintSpeed : walkSpeed;

        Vector3 targetVelocity =
            movementEnabled
                ? moveDirection * currentSpeed
                : Vector3.zero;

        float acceleration;

        if (isGrounded)
        {
            acceleration = hasMovementInput
                ? groundAcceleration
                : groundDeceleration;
        }
        else
        {
            acceleration = hasMovementInput
                ? airAcceleration
                : airDeceleration;
        }

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            acceleration * deltaTime
        );

        IsMoving = horizontalVelocity.sqrMagnitude > 0.05f;

        // --------------------------------------------------
        // EXTERNAL FORCES
        // --------------------------------------------------

        // Dash and knockback gradually lose their influence.
        externalVelocity = Vector3.MoveTowards(
            externalVelocity,
            Vector3.zero,
            externalVelocityDecay * deltaTime
        );

        // --------------------------------------------------
        // APPLY ALL MOVEMENT IN ONE CALL
        // --------------------------------------------------

        Vector3 finalVelocity =
            horizontalVelocity +
            externalVelocity +
            Vector3.up * verticalVelocity;

        characterController.Move(finalVelocity * deltaTime);

        // Keep the CharacterController grounded after movement.
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
        }
    }

    private void HandleSpectatorMovement(Vector2 input, float deltaTime)
    {
        float verticalInput = 0f;

        if (movementEnabled && Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.isPressed || Keyboard.current.qKey.isPressed)
                verticalInput += 1f;

            if (Keyboard.current.eKey.isPressed)
                verticalInput -= 1f;
        }

        Vector3 forward =
            Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        Vector3 right =
            Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        Vector3 horizontalDirection =
            right * input.x + forward * input.y;

        if (horizontalDirection.sqrMagnitude > 1f)
            horizontalDirection.Normalize();

        Vector3 spectatorVelocity =
            horizontalDirection * spectatorSpeed +
            Vector3.up * verticalInput * spectatorVerticalSpeed;

        characterController.Move(
            spectatorVelocity * deltaTime
        );

        IsMoving = false;
        IsSprinting = false;
        IsJumping = false;
        isAirborne = false;
        horizontalVelocity = Vector3.zero;
        externalVelocity = Vector3.zero;
        verticalVelocity = 0f;
    }

    private void HandleJumpAndGravity(bool jumpHeld)
    {
        bool isGrounded = characterController.isGrounded;
        bool jumpPressed = jumpHeld && !jumpWasHeld;
        bool jumpReleased = !jumpHeld && jumpWasHeld;
        jumpWasHeld = jumpHeld;

        // Detect landing, regardless of whether the player jumped.
        if (isGrounded)
        {
            if (isAirborne)
            {
                isAirborne = false;
                IsJumping = false;

                if (networkPlayerAudio != null)
                {
                    networkPlayerAudio.PlayFromOwner(PlayerSoundEvent.Land);
                }
                    
            }

            // Refresh coyote time while grounded.
            coyoteTimer = coyoteTime;

            if (verticalVelocity < 0f)
                verticalVelocity = groundedGravity;
        }
        else
        {
            isAirborne = true;
            coyoteTimer -= Time.deltaTime;
        }

        // Hold-to-jump: jumping consumes the coyote window,
        // preventing another jump while still airborne.
        bool canJump = isGrounded || coyoteTimer > 0f;

        bool shouldJump = jumpPressed || (jumpHeld && isGrounded && !IsJumping);

        if (shouldJump && canJump)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);

            IsJumping = true;
            isAirborne = true;
            jumpHoldTimer = 0f;

            // Consume the available jump window.
            coyoteTimer = 0f;

            if (networkPlayerAudio != null)
                networkPlayerAudio.PlayFromOwner(PlayerSoundEvent.Jump);
        }

        if (IsJumping && jumpHeld && !jumpPressed)
            jumpHoldTimer = Mathf.Min(
                jumpHoldTimer + Time.deltaTime,
                jumpHoldDuration
            );

        if (IsJumping && jumpReleased && verticalVelocity > 0f)
        {
            float holdFraction = jumpHoldDuration > 0f
                ? Mathf.Clamp01(jumpHoldTimer / jumpHoldDuration)
                : 1f;
            float minHeight = Mathf.Min(minimumJumpHeight, jumpHeight);
            float minimumJumpVelocity = Mathf.Sqrt(minHeight * -2f * gravity);
            float maximumJumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            float targetJumpVelocity = Mathf.Lerp(
                minimumJumpVelocity,
                maximumJumpVelocity,
                holdFraction
            );
            float targetVelocity =
                targetJumpVelocity + gravity * jumpHoldTimer;

            verticalVelocity = Mathf.Min(verticalVelocity, targetVelocity);
        }

        // Apply gravity.
        verticalVelocity += gravity * Time.deltaTime;
    }
}
