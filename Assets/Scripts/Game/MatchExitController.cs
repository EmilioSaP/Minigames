
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchExitController : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private NetworkManager networkManager;
    private bool isReturningToLobby;
    private bool isSubscribed;

    private void OnEnable()
    {
        SubscribeToConnectionEvents();
    }

    private void Start()
    {
        // Retry in case the NetworkManager wasn't ready during OnEnable.
        SubscribeToConnectionEvents();
    }

    private void OnDisable()
    {
        if (isSubscribed && networkManager != null)
        {
            networkManager.OnConnectionEvent -= HandleConnectionEvent;
        }

        isSubscribed = false;
    }

    private void SubscribeToConnectionEvents()
    {
        if (isSubscribed)
            return;

        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return;

        networkManager.OnConnectionEvent += HandleConnectionEvent;
        isSubscribed = true;
    }

    public void LeaveMatch()
    {
        if (isReturningToLobby)
            return;

        isReturningToLobby = true;

        Debug.Log("Leaving match...");

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        LoadLobbyLocally();
    }

    private void HandleConnectionEvent(
        NetworkManager manager,
        ConnectionEventData eventData)
    {
        // We only care when OUR local connection is lost.
        if (eventData.EventType != ConnectionEvent.ClientDisconnected)
            return;

        if (eventData.ClientId != manager.LocalClientId)
            return;

        if (isReturningToLobby)
            return;

        isReturningToLobby = true;

        Debug.LogWarning(
            "Lost connection to the match. Returning to LobbyScene."
        );

        LoadLobbyLocally();
    }

    private void LoadLobbyLocally()
    {
        if (SceneManager.GetActiveScene().name == lobbySceneName)
            return;

        SceneManager.LoadScene(
            lobbySceneName,
            LoadSceneMode.Single
        );
    }
}
