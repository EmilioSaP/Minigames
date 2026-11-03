
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchExitController : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private NetworkSceneController networkSceneController;
    [SerializeField] private AudioClip lobbyClip;

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

    public void LeaveParty()
    {
        if (isReturningToLobby)
            return;

        NetworkManager manager = NetworkManager.Singleton;

        if (manager != null && manager.IsListening && manager.IsHost)
        {
            if (networkSceneController == null)
            {
                Debug.LogError(
                    "Cannot return the party: NetworkSceneController is not assigned."
                );
                return;
            }

            networkSceneController.ReturnPartyToLobby();
            return;
        }

        isReturningToLobby = true;

        Debug.Log("Leaving match...");

        if (manager != null && manager.IsListening)
        {
            manager.Shutdown();
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
        //AudioManager.Instance.PlayMusic(lobbyClip, true, false);
        if (SceneManager.GetActiveScene().name == lobbySceneName)
            return;

        SceneManager.LoadScene(
            lobbySceneName,
            LoadSceneMode.Single
        );
    }
}
