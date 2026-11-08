
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerDash : NetworkBehaviour
{
    [Header("Dash Settings")]
    [SerializeField] private float dashImpulse = 6f;
    [SerializeField] private float cooldown = 1.5f;

    [Tooltip("Assign the player's camera or view transform. " +
             "If empty, the player's forward direction is used.")]
    [SerializeField] private Transform directionSource;

    private PlayerMovement playerMovement;

    private float nextDashTime;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        // Only the owning player reads dash input.
        if (!IsOwner || Keyboard.current == null)
            return;

        if (!playerMovement.CanReceiveMovementInput ||
            playerMovement.IsSpectator)
            return;

        if (!Keyboard.current.eKey.wasPressedThisFrame)
            return;

        TryDash();
    }

    private void TryDash()
    {
        if (Time.time < nextDashTime)
            return;

        Transform source =
            directionSource != null ? directionSource : transform;

        // Project the look direction onto the horizontal plane.
        // This removes vertical camera pitch.
        Vector3 dashDirection =
            Vector3.ProjectOnPlane(source.forward, Vector3.up).normalized;

        // Fallback if the view is pointing almost perfectly vertically.
        if (dashDirection.sqrMagnitude < 0.001f)
        {
            dashDirection =
                Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }

        if (dashDirection.sqrMagnitude < 0.001f)
            return;

        playerMovement.AddHorizontalImpulse(
            dashDirection * dashImpulse
        );

        nextDashTime = Time.time + cooldown;

        Debug.Log("Player dashed.");
    }

    public void ResetCooldown()
    {
        nextDashTime = 0f;
    }
}
