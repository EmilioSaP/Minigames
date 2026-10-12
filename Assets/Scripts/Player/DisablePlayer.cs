using UnityEngine;

public class DisablePlayer : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerTurning playerTurning;
    [SerializeField] private PickupController pickupController;
    [SerializeField] private CameraViewToggle cameraViewToggle;

    public void SetPlayerEnabled(bool state)
    {
        if (playerMovement != null)
            playerMovement.SetMovementEnabled(state);

        if (playerTurning != null)
            playerTurning.SetLookMode(state);

        if (pickupController != null)
            pickupController.SetInteractionEnabled(state);

        if (cameraViewToggle != null)
            cameraViewToggle.SetCameraToggable(state);
    }
}