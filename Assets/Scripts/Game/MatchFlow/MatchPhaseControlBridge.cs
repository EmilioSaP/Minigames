
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MatchPhaseControlBridge : MonoBehaviour
{
    private MatchFlowManager matchFlow;
    private NetworkPlayerSpawnHandler localSpawnHandler;

    private bool isSubscribed;

    private IEnumerator Start()
    {
        // Wait until networking is available.
        while (NetworkManager.Singleton == null ||
               !NetworkManager.Singleton.IsClient)
        {
            yield return null;
        }

        NetworkManager networkManager = NetworkManager.Singleton;

        // Wait for our own player object.
        while (networkManager.LocalClient == null ||
               networkManager.LocalClient.PlayerObject == null)
        {
            yield return null;
        }

        NetworkObject localPlayer =
            networkManager.LocalClient.PlayerObject;

        localSpawnHandler =
            localPlayer.GetComponent<NetworkPlayerSpawnHandler>();

        if (localSpawnHandler == null)
        {
            Debug.LogError(
                "Local player is missing NetworkPlayerSpawnHandler."
            );
            yield break;
        }

        // Wait until the scene's networked MatchFlowManager is spawned.
        while (matchFlow == null || !matchFlow.IsSpawned)
        {
            matchFlow =
                FindFirstObjectByType<MatchFlowManager>();

            yield return null;
        }

        matchFlow.PhaseChanged += OnPhaseChanged;
        isSubscribed = true;

        // Handle the current phase too, in case it changed
        // before this component subscribed.
        OnPhaseChanged(matchFlow.CurrentPhase);
    }

    private void OnDisable()
    {
        if (isSubscribed && matchFlow != null)
        {
            matchFlow.PhaseChanged -= OnPhaseChanged;
        }

        isSubscribed = false;
    }

    private void OnPhaseChanged(MatchPhase phase)
    {
        switch (phase)
        {
            case MatchPhase.InProgress:

                // The countdown has ended.
                // Remove only the spawn-specific restriction.
                localSpawnHandler?.ReleaseSpawnRestriction();

                Debug.Log("Spawn restriction released. Gameplay enabled.");
                break;

            case MatchPhase.PreparationFailed:

                // Keep the player immobilized.
                Debug.LogWarning(
                    "Match preparation failed. Player remains restricted."
                );
                break;
        }
    }
}
