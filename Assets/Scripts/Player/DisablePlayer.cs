using UnityEngine;

public class DisablePlayer : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerTurning playerTurning;
    [SerializeField] private PickupController pickupController;

    public void SetPlayerEnabled(bool state)
    {
        if (playerMovement != null)
            playerMovement.SetMovementEnabled(state);

        if (playerTurning != null)
            playerTurning.SetLookMode(state);

        if (pickupController != null)
            pickupController.SetInteractionEnabled(state);
    }
}