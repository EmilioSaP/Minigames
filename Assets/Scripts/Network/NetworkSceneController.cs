
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSceneController : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string gameplaySceneName = "GameScene";

    private bool isLoadingGame;

    public void StartGame()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        // Make sure a network session exists.
        if (networkManager == null || !networkManager.IsListening)
        {
            Debug.LogWarning("Cannot start the game: no active network session.");
            return;
        }

        // Only the host is allowed to start the game.
        if (!networkManager.IsHost)
        {
            Debug.LogWarning("Only the host can start the game.");
            return;
        }

        // Prevent repeated clicks from requesting multiple scene loads.
        if (isLoadingGame)
            return;

        if (!networkManager.NetworkConfig.EnableSceneManagement)
        {
            Debug.LogError(
                "Cannot start the game: NGO scene management is disabled."
            );
            return;
        }

        isLoadingGame = true;

        // The host requests the scene transition for the entire session.
        SceneEventProgressStatus status =
            networkManager.SceneManager.LoadScene(
                gameplaySceneName,
                LoadSceneMode.Single
            );

        if (status != SceneEventProgressStatus.Started)
        {
            isLoadingGame = false;

            Debug.LogError(
                $"Failed to start scene transition. Status: {status}"
            );
            return;
        }

        Debug.Log($"Loading gameplay scene: {gameplaySceneName}");
    }
}
