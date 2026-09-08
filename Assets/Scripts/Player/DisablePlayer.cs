using UnityEngine;

public class DisablePlayer : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerTurning playerTurning;

    public void SetPlayerEnabled(bool state)
    {
        if (playerMovement != null)
            playerMovement.SetMovementEnabled(state);

        if (playerTurning != null)
            playerTurning.SetLookMode(state);
    }
}