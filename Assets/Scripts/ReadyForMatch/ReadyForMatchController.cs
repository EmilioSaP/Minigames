using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public class ReadyForMatchController : NetworkBehaviour
{
    public static ReadyForMatchController Instance { get; private set; }
    public static event Action<ReadyForMatchController> InstanceAvailable;

    [SerializeField] private ReadyForMatchUI readyForMatchUI;
    [SerializeField] private NextMatchController nextMatchController;
    [SerializeField] private NetworkSceneController networkSceneController;
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private readonly NetworkList<ulong> readyClientIds = new();
    private readonly HashSet<PlayerReadyForMatchTag> playerReadyTags = new();

    private NetworkManager networkManager;
    private bool startRequested;
    private bool wasSelectionActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "Only one ReadyForMatchController can be active at a time.",
                this
            );
            enabled = false;
            return;
        }

        Instance = this;
        readyForMatchUI?.SetVisible(false);
        InstanceAvailable?.Invoke(this);
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        startRequested = false;
        readyForMatchUI?.SetVisible(true);
        NetworkGameManager.LocalPlayerLeft += OnLocalPlayerLeft;

        networkManager = NetworkManager.Singleton;
        readyClientIds.OnListChanged += OnReadyClientsChanged;

        if (networkManager != null && IsServer)
        {
            readyClientIds.Clear();
            networkManager.OnConnectionEvent += OnConnectionEvent;
        }

        if (networkManager != null)
        {
            networkManager.OnClientStopped += OnNetworkStopped;
            networkManager.OnServerStopped += OnNetworkStopped;
        }

        if (nextMatchController == null)
            nextMatchController = FindFirstObjectByType<NextMatchController>();
        wasSelectionActive =
            nextMatchController != null &&
            nextMatchController.IsSelectionActive;

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        RefreshReadiness();
    }

    public void RegisterPlayerReadyTag(PlayerReadyForMatchTag tag)
    {
        if (tag == null || !playerReadyTags.Add(tag))
            return;

        RefreshPlayerReadyTag(tag);
    }

    public void UnregisterPlayerReadyTag(PlayerReadyForMatchTag tag)
    {
        if (tag != null)
            playerReadyTags.Remove(tag);
    }

    public override void OnNetworkDespawn()
    {
        readyClientIds.OnListChanged -= OnReadyClientsChanged;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        NetworkGameManager.LocalPlayerLeft -= OnLocalPlayerLeft;

        if (IsServer && networkManager != null)
            networkManager.OnConnectionEvent -= OnConnectionEvent;
        if (networkManager != null)
        {
            networkManager.OnClientStopped -= OnNetworkStopped;
            networkManager.OnServerStopped -= OnNetworkStopped;
        }

        startRequested = false;
        wasSelectionActive = false;
        readyForMatchUI?.SetVisible(false);

        base.OnNetworkDespawn();
    }

    private void OnLocalPlayerLeft()
    {
        startRequested = false;
        wasSelectionActive = false;
        readyForMatchUI?.SetVisible(false);
    }

    private void OnNetworkStopped(bool wasHost)
    {
        OnLocalPlayerLeft();
    }

    private void Update()
    {
        if (nextMatchController == null)
            nextMatchController = FindFirstObjectByType<NextMatchController>();

        bool isSelectionActive =
            nextMatchController != null &&
            nextMatchController.IsSelectionActive;
        if (wasSelectionActive && !isSelectionActive && IsServer)
            ResetReadiness();
        wasSelectionActive = isSelectionActive;

        if (!IsClient ||
            SceneManager.GetActiveScene().name != lobbySceneName ||
            isSelectionActive ||
            Keyboard.current == null ||
            !Keyboard.current.rKey.wasPressedThisFrame)
        {
            return;
        }

        ToggleReadyRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleReadyRpc(RpcParams rpcParams = default)
    {
        if (!IsServer ||
            networkManager == null ||
            !networkManager.IsListening ||
            SceneManager.GetActiveScene().name != lobbySceneName ||
            (nextMatchController != null &&
             nextMatchController.IsSelectionActive))
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!IsConnectedClient(clientId))
            return;

        int readyIndex = FindReadyClientIndex(clientId);
        if (readyIndex >= 0)
            readyClientIds.RemoveAt(readyIndex);
        else
            readyClientIds.Add(clientId);
    }

    private void OnConnectionEvent(
        NetworkManager manager,
        ConnectionEventData eventData)
    {
        if (eventData.EventType != ConnectionEvent.ClientDisconnected &&
            eventData.EventType != ConnectionEvent.PeerDisconnected)
        {
            return;
        }

        int readyIndex = FindReadyClientIndex(eventData.ClientId);
        if (readyIndex >= 0)
            readyClientIds.RemoveAt(readyIndex);
    }

    private void OnReadyClientsChanged(NetworkListEvent<ulong> change)
    {
        RefreshReadiness();
        TryStartMatch();
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        if (newScene.name != lobbySceneName)
            return;

        if (IsServer)
            ResetReadiness();

        RefreshReadiness();
    }

    private void ResetReadiness()
    {
        startRequested = false;
        readyClientIds.Clear();
        RefreshReadiness();
    }

    private void RefreshReadiness()
    {
        foreach (PlayerReadyForMatchTag tag in playerReadyTags)
        {
            RefreshPlayerReadyTag(tag);
        }

        if (readyForMatchUI == null)
            return;

        if (AreAllConnectedClientsReady())
            readyForMatchUI.AllPlayersReady();
        else if (IsLocalPlayerReady())
            readyForMatchUI.SetReadyUI();
        else
            readyForMatchUI.SetNotReadyUI();
    }

    private void RefreshPlayerReadyTag(PlayerReadyForMatchTag tag)
    {
        if (tag == null)
            return;

        bool isLobby = SceneManager.GetActiveScene().name == lobbySceneName;
        tag.SetVisible(isLobby);
        if (!isLobby)
            return;

        NetworkObject playerObject =
            tag.GetComponentInParent<NetworkObject>();
        if (playerObject != null && IsClientReady(playerObject.OwnerClientId))
            tag.SetReadyTag();
        else
            tag.SetNotReadyTag();
    }

    private void TryStartMatch()
    {
        if (!IsServer ||
            startRequested ||
            networkManager == null ||
            !networkManager.IsListening ||
            SceneManager.GetActiveScene().name != lobbySceneName ||
            (nextMatchController != null &&
             nextMatchController.IsSelectionActive))
        {
            return;
        }

        if (!AreAllConnectedClientsReady())
            return;

        if (nextMatchController == null)
            nextMatchController = FindFirstObjectByType<NextMatchController>();

        if (networkSceneController == null)
            networkSceneController =
                FindFirstObjectByType<NetworkSceneController>();

        if (nextMatchController == null || networkSceneController == null)
        {
            Debug.LogError(
                "Cannot start the match: NextMatchController or " +
                "NetworkSceneController was not found.",
                this
            );
            return;
        }

        startRequested = true;
        readyForMatchUI?.AllPlayersReady();
        networkSceneController.StartGame();
    }

    private bool IsLocalPlayerReady()
    {
        return networkManager != null &&
               networkManager.IsClient &&
               IsClientReady(networkManager.LocalClientId);
    }

    private bool AreAllConnectedClientsReady()
    {
        if (networkManager == null)
            return false;

        int connectedPlayerCount = networkManager.ConnectedClientsIds.Count;
        if (connectedPlayerCount == 0 ||
            readyClientIds.Count != connectedPlayerCount)
        {
            return false;
        }

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (!IsClientReady(clientId))
                return false;
        }

        return true;
    }

    private bool IsClientReady(ulong clientId)
    {
        return FindReadyClientIndex(clientId) >= 0;
    }

    private int FindReadyClientIndex(ulong clientId)
    {
        for (int i = 0; i < readyClientIds.Count; i++)
        {
            if (readyClientIds[i] == clientId)
                return i;
        }

        return -1;
    }

    private bool IsConnectedClient(ulong clientId)
    {
        if (networkManager == null)
            return false;

        foreach (ulong connectedClientId in networkManager.ConnectedClientsIds)
        {
            if (connectedClientId == clientId)
                return true;
        }

        return false;
    }
}
