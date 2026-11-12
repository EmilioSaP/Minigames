using UnityEngine;

public class PlayerFOVController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Camera playerCamera;

    [Header("FOV Settings")]
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float sprintFOV = 75f;
    [SerializeField] private float transitionSpeed = 10f;

    private void Awake()
    {
        // Auto-assign references if they aren't set in the inspector
        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (playerMovement == null || playerCamera == null) return;
        
        // Ensure we only modify the FOV for the local player
        if (!playerMovement.IsOwner) return;

        // Determine target FOV based on the IsSprinting state
        float targetFOV = playerMovement.IsSprinting ? sprintFOV : normalFOV;

        // Smoothly transition the camera's field of view
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView, 
            targetFOV, 
            Time.deltaTime * transitionSpeed
        );
    }
}