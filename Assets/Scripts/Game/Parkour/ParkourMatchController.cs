
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ParkourMatchController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MatchFlowManager matchFlow;
    [SerializeField] private MatchResultsManager resultsManager;
    [SerializeField] private PostMatchFlowController postMatchFlow;

    public bool RegisterGoalReachedServer(NetworkObject playerObject)
    {
        if (!IsServer || !IsSpawned)
            return false;

        if (playerObject == null || !playerObject.IsPlayerObject)
            return false;

        if (matchFlow == null ||
            resultsManager == null ||
            postMatchFlow == null)
        {
            Debug.LogError("ParkourMatchController is missing references.");
            return false;
        }

        ulong clientId = playerObject.OwnerClientId;

        // FIRST FINISHER
        if (matchFlow.CurrentPhase == MatchPhase.InProgress)
        {
            if (resultsManager.IsPlayerResolved(clientId))
                return false;

            // Register the winner before completing the player.
            if (!resultsManager.TryRegisterWinnerServer(clientId))
                return false;

            if (!resultsManager.TryRegisterCompletedPlayerServer(clientId))
                return false;

            // The generic manager only changes the match state.
            bool finished = matchFlow.TryFinishMatch();

            if (!finished)
            {
                Debug.LogError(
                    "Could not finish the match after registering the winner."
                );

                return false;
            }

            Debug.Log($"Parkour winner: Client {clientId}");

            return true;
        }

        // SUBSEQUENT FINISHERS
        if (matchFlow.CurrentPhase == MatchPhase.Finished &&
            postMatchFlow.CanRegisterAdditionalFinishers)
        {
            bool registered =
                resultsManager.TryRegisterCompletedPlayerServer(clientId);

            if (registered)
            {
                Debug.Log($"Additional parkour finisher: Client {clientId}");
            }

            return registered;
        }

        return false;
    }
}
