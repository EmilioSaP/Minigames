using UnityEngine;

public class ReadyForMatchController : MonoBehaviour
{
    [SerializeField] private ReadyForMatchUI readyForMatchUI;
    [SerializeField] private PlayerReadyForMatchTag playerReadyForMatchTag;
    [SerializeField] private NextMatchController nextMatchController;
    [SerializeField] private NetworkSceneController networkSceneController;
    // Controller Script that checks that all players are ready to start the match and then calls NetworkSceneController's StartGame()
    // If currently choosing nextmatchController, StartGame() should not be called, only when the nextmatchController has finished its selection or has been cancelled
    // When the player presses the "Ready" button (R), it should call PlayerReadyForMatchTag and ReadyForMatchUI to update their UI.
}
