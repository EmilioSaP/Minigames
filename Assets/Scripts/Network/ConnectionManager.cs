using System;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

/// <summary>
/// Manages lobby connection and Relay session setup, saving the entered
/// player name before connecting.
/// </summary>
public class ConnectionManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_InputField joinCodeTextField;  // Makes it easier to copy the join code
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private GameObject StartGameButton;
    [SerializeField] private GameObject offlineMenuRoot;

    [Header("Relay")]
    [SerializeField] private int maxConnections = 4;

    private static string currentJoinCode = string.Empty;

    public static string CurrentJoinCode => currentJoinCode;

    private bool servicesInitialized = false;
    private bool isLeaving;
    private bool isConnecting;

    private void Start()
    {
        string savedPlayerName = PlayerSettings.PlayerName;
        if (usernameInput != null &&
            !string.IsNullOrWhiteSpace(savedPlayerName))
        {
            usernameInput.text = savedPlayerName;
        }

        ApplyLobbyPresentation();

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        if (IsConnectedParty(networkManager) && statusText != null)
        {
            statusText.text = networkManager.IsHost
                ? "Hosting game!"
                : "Connected to host!";
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        isConnecting = false;

        if (statusText != null)
            statusText.text = NetworkManager.Singleton.IsHost
                ? "Hosting game!"
                : "Connected to host!";

        ApplyLobbyPresentation();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // A remote player left. Do not disconnect the host.
        if (clientId != NetworkManager.Singleton.LocalClientId)
        {
            if (NetworkManager.Singleton.IsServer && statusText != null)
                statusText.text = $"Player {clientId} disconnected.";

            return;
        }

        // Ignore callbacks caused by our own Leave Game button.
        if (isLeaving)
            return;

        isConnecting = false;
        isLeaving = true;
        currentJoinCode = string.Empty;

        if (statusText != null)
            statusText.text = "Disconnected from host. You can join another game.";

        ApplyLobbyPresentation();
        Debug.Log("Local client disconnected from the session.");
    }

    public void LeaveGame()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsListening)
        {
            ResetConnectionUI("You are not connected to a game.");
            ApplyLobbyPresentation();
            return;
        }

        // Set this before shutting down to suppress our own disconnect alert.
        isLeaving = true;
        isConnecting = false;
        currentJoinCode = string.Empty;

        bool wasHost = networkManager.IsHost;

        networkManager.Shutdown();

        ResetConnectionUI(wasHost
            ? "You stopped hosting."
            : "You left the game.");
        ApplyLobbyPresentation();
    }

    private void ResetConnectionUI(string message)
    {
        if (joinCodeTextField != null)
        {
            joinCodeTextField.text = "";
        }

        if (joinCodeText != null)
            joinCodeText.text = "";

        if (statusText != null)
            statusText.text = message;
    }

    private async Task InitializeServices()
    {
        if (servicesInitialized)
            return;

        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesInitialized = true;

            Debug.Log("Unity Services initialized.");
            Debug.Log($"Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Unity Services initialization failed: {e}");

            if (statusText != null)
                statusText.text = "Services initialization failed.";
        }
    }

    public async void HostGame()
    {
        if (isConnecting)
            return;
        
        isLeaving = false;
        isConnecting = true;
        PlayerSettings.PlayerName = GetPlayerName();
        await InitializeServices();

        try
        {
            statusText.text = "Creating Relay allocation...";

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(maxConnections);

            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport =
                NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "wss")
            );

            transport.UseWebSockets = true;

            bool started = NetworkManager.Singleton.StartHost();

            if (started)
            {
                currentJoinCode = joinCode;
                joinCodeText.text = $"Join Code: {joinCode}";
                statusText.text = "Hosting game!";
                //enable the gamobject containing the joinCodeTextField so that the player can copy the join code
                joinCodeTextField.text = joinCode; // Set the input field to the join code for easy copying
                ApplyLobbyPresentation();

                Debug.Log($"HOST STARTED");
                Debug.Log($"Join Code: {joinCode}");
            }
            else
            {
                statusText.text = "Failed to start host.";
                Debug.LogError("NetworkManager failed to start Host.");
                isConnecting = false;
            }
        }
        catch (Exception e)
        {
            statusText.text = "Failed to create Relay.";

            Debug.LogError($"Host failed: {e}");
            isConnecting = false;
        }
    }

    public async void JoinGame()
    {
        if (isConnecting)
            return;
        isLeaving = false;
        isConnecting = true;
        PlayerSettings.PlayerName = GetPlayerName();
        await InitializeServices();

        string joinCode = joinCodeInput.text.Trim();

        if (string.IsNullOrEmpty(joinCode))
        {
            statusText.text = "Enter a join code.";
            isConnecting = false;
            return;
        }

        try
        {
            statusText.text = "Joining Relay...";

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport =
                NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "wss")
            );

            transport.UseWebSockets = true;

            bool started = NetworkManager.Singleton.StartClient();

            if (started)
            {
                statusText.text = "Joining game...";
                Debug.Log($"CLIENT STARTED");
                Debug.Log($"Join Code: {joinCode}");
                currentJoinCode = joinCode;
                joinCodeTextField.text = joinCode;
                ApplyLobbyPresentation();
            }
            else
            {
                statusText.text = "Failed to start client.";
                Debug.LogError("NetworkManager failed to start Client.");
                isConnecting = false;
            }
        }
        catch (Exception e)
        {
            statusText.text = "Failed to join game.";
            isConnecting = false;
            Debug.LogError($"Join failed: {e}");
        }
    }

    public string GetPlayerName()
    {
        string playerName = usernameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName))
            return $"Player {UnityEngine.Random.Range(1000, 9999)}";

        return playerName;
    }

    public void PlayerNameEdited()
    {
        //Just set the playerpref
        PlayerSettings.PlayerName = GetPlayerName();
    }

    private void ApplyLobbyPresentation()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        bool isConnectedParty = !isLeaving && IsConnectedParty(networkManager);

        if (offlineMenuRoot != null)
            offlineMenuRoot.SetActive(!isConnectedParty);

        if (StartGameButton != null)
            StartGameButton.SetActive(
                isConnectedParty && networkManager.IsHost
            );

        if (!isConnectedParty)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (joinCodeTextField != null)
                joinCodeTextField.text = string.Empty;

            if (joinCodeText != null)
                joinCodeText.text = string.Empty;

            return;
        }

        if (!string.IsNullOrEmpty(currentJoinCode))
        {
            if (joinCodeTextField != null)
                joinCodeTextField.text = currentJoinCode;

            if (joinCodeText != null)
                joinCodeText.text = $"Join Code: {currentJoinCode}";
        }
    }

    private static bool IsConnectedParty(NetworkManager networkManager)
    {
        return networkManager != null &&
            networkManager.IsListening &&
            (networkManager.IsHost || networkManager.IsConnectedClient);
    }
}