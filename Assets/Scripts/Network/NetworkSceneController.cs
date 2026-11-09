
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSceneController : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private NextMatchController nextMatchController;

    private bool isLoadingScene;

    private void Awake()
    {
        RemoveDuplicateNetworkManagers();
    }

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

        if (nextMatchController == null)
            nextMatchController = FindFirstObjectByType<NextMatchController>();

        if (nextMatchController == null)
        {
            Debug.LogError(
                "Cannot start game selection: NextMatchController was not found."
            );
            return;
        }

        nextMatchController.BeginSelectionServer(false);
    }

    public void ReturnPartyToLobby()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
        {
            Debug.LogWarning("Cannot return the party: no active network session.");
            return;
        }

        if (!networkManager.IsHost)
        {
            Debug.LogWarning("Only the host can return the party to the lobby.");
            return;
        }

        LoadPartyScene(networkManager, lobbySceneName);
    }

    public bool LoadPartyScene(string sceneName)
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
        {
            Debug.LogWarning($"Cannot load {sceneName}: no active network session.");
            return false;
        }

        if (!networkManager.IsHost)
        {
            Debug.LogWarning($"Only the host can load {sceneName} for the party.");
            return false;
        }

        return LoadPartyScene(networkManager, sceneName);
    }

    private bool LoadPartyScene(NetworkManager networkManager, string sceneName)
    {
        if (isLoadingScene)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Cannot load a scene with an empty name.");
            return false;
        }

        if (!networkManager.NetworkConfig.EnableSceneManagement)
        {
            Debug.LogError(
                $"Cannot load {sceneName}: NGO scene management is disabled."
            );
            return false;
        }

        isLoadingScene = true;

        SceneEventProgressStatus status =
            networkManager.SceneManager.LoadScene(
                sceneName,
                LoadSceneMode.Single
            );

        if (status != SceneEventProgressStatus.Started)
        {
            isLoadingScene = false;
            Debug.LogError(
                $"Failed to load scene {sceneName}. Status: {status}"
            );
            return false;
        }

        Debug.Log($"Loading scene for party: {sceneName}");
        return true;
    }

    private static void RemoveDuplicateNetworkManagers()
    {
        NetworkManager singleton = NetworkManager.Singleton;
        if (singleton == null)
            return;

        foreach (NetworkManager manager in
                 FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
        {
            if (manager == singleton)
                continue;

            manager.gameObject.SetActive(false);
            Destroy(manager.gameObject);
            Debug.LogWarning(
                "Removed a duplicate NetworkManager loaded with the scene.");
        }
    }
}
