
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum PostMatchStage : byte
{
    None,
    FinishPresentation,
    AdditionalFinishWindow,
    ResultsScreen,
    RematchVote,
    Selection
}

[RequireComponent(typeof(NetworkObject))]
public class PostMatchFlowController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MatchFlowManager matchFlow;
    [SerializeField] private MatchResultsManager resultsManager;
    [SerializeField] private MatchPlayerTracker playerTracker;
    [SerializeField] private RematchController rematchController;
    [SerializeField] private NextMatchController nextMatchController;
    [SerializeField] private MonoBehaviour resultsScreenUIComponent;

    [Header("Finish Presentation")]
    [SerializeField] private float finishPresentationDuration = 2.5f;

    [Header("Additional Finish Window")]
    [SerializeField] private bool waitForRemainingPlayers = true;
    [SerializeField] private float additionalFinishWindowDuration = 10f;

    [Header("Results Screen")]
    [SerializeField] private bool showResultsScreen = true;
    [SerializeField] private bool spectateOnResultsScreen = true;
    [SerializeField] private float resultsScreenDuration = 10f;
    
    [Header("Rematch Vote UI")]
    [SerializeField] private MonoBehaviour rematchVoteUIComponent;

    private IRematchVoteUI rematchVoteUI;
    

    // Synchronized post-match state.
    private readonly NetworkVariable<PostMatchStage> currentStage =
        new NetworkVariable<PostMatchStage>(
            PostMatchStage.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<bool> acceptingAdditionalFinishers =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private readonly NetworkVariable<double> finishWindowEndServerTime =
        new NetworkVariable<double>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Participants captured at the beginning of gameplay.
    private readonly HashSet<ulong> matchParticipants = new();

    private bool participantsCaptured;

    private Coroutine bindingCoroutine;
    private Coroutine postMatchCoroutine;
    private IMatchResultsUI resultsScreenUI;
    private bool rematchRestrictionApplied;

    public event System.Action<PostMatchStage> StageChanged;
    public event System.Action SelectionPhaseStarted;

    public PostMatchStage CurrentStage => currentStage.Value;

    public bool CanRegisterAdditionalFinishers =>
        acceptingAdditionalFinishers.Value;

    public bool CanRequestConcede =>
        IsSpawned &&
        IsClient &&
        CurrentStage == PostMatchStage.AdditionalFinishWindow &&
        acceptingAdditionalFinishers.Value;

    public float AdditionalFinishTimeRemaining
    {
        get
        {
            if (CurrentStage != PostMatchStage.AdditionalFinishWindow ||
                NetworkManager.Singleton == null)
            {
                return 0f;
            }

            double remaining =
                finishWindowEndServerTime.Value -
                NetworkManager.Singleton.ServerTime.Time;

            return Mathf.Max(0f, (float)remaining);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentStage.OnValueChanged += OnStageChanged;
        BindResultsUI();
        BindRematchVoteUI();

        if (CurrentStage == PostMatchStage.RematchVote)
            ApplyRematchRestriction();

        if (CurrentStage == PostMatchStage.ResultsScreen)
            SpectateLocalPlayerOnResultsScreen();

        // Only the server coordinates the post-match sequence.
        if (IsServer)
        {
            bindingCoroutine = StartCoroutine(BindServerDependencies());
        }
    }

    public override void OnNetworkDespawn()
    {
        currentStage.OnValueChanged -= OnStageChanged;

        if (bindingCoroutine != null)
            StopCoroutine(bindingCoroutine);

        if (postMatchCoroutine != null)
            StopCoroutine(postMatchCoroutine);

        if (matchFlow != null)
            matchFlow.PhaseChanged -= OnMatchPhaseChanged;

        if (rematchController != null)
            rematchController.VoteCompleted -= OnRematchVoteCompleted;

        base.OnNetworkDespawn();
    }

    // -------------------------
    // Dependency binding
    // -------------------------

    private IEnumerator BindServerDependencies()
    {
        while (true)
        {
            if (matchFlow == null)
                matchFlow = FindFirstObjectByType<MatchFlowManager>();

            if (resultsManager == null)
                resultsManager = FindFirstObjectByType<MatchResultsManager>();

            if (playerTracker == null)
                playerTracker = FindFirstObjectByType<MatchPlayerTracker>();

            if (rematchController == null)
                rematchController = FindFirstObjectByType<RematchController>();

            if (nextMatchController == null)
                nextMatchController = FindFirstObjectByType<NextMatchController>();

            if (matchFlow != null &&
                matchFlow.IsSpawned &&
                resultsManager != null &&
                resultsManager.IsSpawned &&
                playerTracker != null &&
                rematchController != null &&
                rematchController.IsSpawned &&
                nextMatchController != null &&
                nextMatchController.IsSpawned)
            {
                break;
            }

            yield return null;
        }

        matchFlow.PhaseChanged += OnMatchPhaseChanged;
        rematchController.VoteCompleted += OnRematchVoteCompleted;

        // Handle the current state in case we subscribed late.
        OnMatchPhaseChanged(matchFlow.CurrentPhase);

        bindingCoroutine = null;
    }

    private void BindResultsUI()
    {
        if (resultsScreenUIComponent == null)
        {
            foreach (MonoBehaviour behaviour in
                     FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is IMatchResultsUI)
                {
                    resultsScreenUIComponent = behaviour;
                    break;
                }
            }
        }

        if (resultsScreenUIComponent != null)
        {
            resultsScreenUI = resultsScreenUIComponent as IMatchResultsUI;
            if (resultsScreenUI == null)
            {
                Debug.LogError(
                    $"{resultsScreenUIComponent.name} does not implement IMatchResultsUI."
                );
            }
        }

        /*resultsScreenUI?.SetResultsVisible(
            CurrentStage == PostMatchStage.ResultsScreen
        ); //starts with 0 alpha, uneccessary*/
    }


    private void BindRematchVoteUI()
    {
        if (rematchVoteUIComponent == null)
        {
            foreach (MonoBehaviour behaviour in
                    FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is IRematchVoteUI)
                {
                    rematchVoteUIComponent = behaviour;
                    break;
                }
            }
        }

        if (rematchVoteUIComponent != null)
        {
            rematchVoteUI =
                rematchVoteUIComponent as IRematchVoteUI;

            if (rematchVoteUI == null)
            {
                Debug.LogError(
                    $"{rematchVoteUIComponent.name} does not implement IRematchVoteUI."
                );
            }
        }

        /*rematchVoteUI?.SetRematchVoteVisible(
            CurrentStage == PostMatchStage.RematchVote
        );//Starts with 0 alpha, unnecesary */
    }

    // -------------------------
    // Match lifecycle
    // -------------------------

    private void OnMatchPhaseChanged(MatchPhase phase)
    {
        if (!IsServer)
            return;

        switch (phase)
        {
            case MatchPhase.InProgress:

                CaptureParticipants();
                break;

            case MatchPhase.Finished:

                BeginPostMatchSequence();
                break;
        }
    }

    private void CaptureParticipants()
    {
        if (!IsServer || participantsCaptured)
            return;

        matchParticipants.Clear();

        foreach (ulong clientId in playerTracker.ConnectedClientIds)
        {
            matchParticipants.Add(clientId);
        }

        participantsCaptured = true;

        Debug.Log(
            $"Post-match participant snapshot captured: {matchParticipants.Count}"
        );
    }

    // -------------------------
    // Post-match sequence
    // -------------------------

    private void BeginPostMatchSequence()
    {
        if (!IsServer || postMatchCoroutine != null)
            return;

        // Fallback if this component subscribed after gameplay started.
        if (!participantsCaptured)
            CaptureParticipants();

        postMatchCoroutine = StartCoroutine(RunPostMatchSequence());
    }

    private IEnumerator RunPostMatchSequence()
    {
        bool allowAdditionalFinishers =
            waitForRemainingPlayers &&
            additionalFinishWindowDuration > 0f;

        // Allow subsequent finishers even during the presentation.
        acceptingAdditionalFinishers.Value = allowAdditionalFinishers;

        currentStage.Value = PostMatchStage.FinishPresentation;

        Debug.Log("Post-match finish presentation started.");

        // First show the finish presentation.
        double presentationEnd =
            GetServerTime() + Mathf.Max(0f, finishPresentationDuration);

        while (GetServerTime() < presentationEnd)
        {
            yield return null;
        }

        // Only open the additional window if unresolved players remain.
        if (allowAdditionalFinishers &&
            HasUnresolvedConnectedParticipants())
        {
            currentStage.Value = PostMatchStage.AdditionalFinishWindow;

            finishWindowEndServerTime.Value =
                GetServerTime() + additionalFinishWindowDuration;

            Debug.Log(
                $"Additional finish window opened: {additionalFinishWindowDuration} seconds."
            );

            while (
                GetServerTime() < finishWindowEndServerTime.Value &&
                HasUnresolvedConnectedParticipants()
            )
            {
                yield return null;
            }
        }

        // No additional finishers can be registered after this point.
        acceptingAdditionalFinishers.Value = false;
        
        // Finalize all results.
        // Remaining unresolved players become DNF.
        resultsManager.FinalizeResultsServer();
        RegisterRemainingPlayersAsConceded();

        if (showResultsScreen)
        {
            currentStage.Value = PostMatchStage.ResultsScreen;
            Debug.Log("Post-match results screen started.");

            double resultsEnd =
                GetServerTime() + Mathf.Max(0f, resultsScreenDuration);

            while (GetServerTime() < resultsEnd)
            {
                yield return null;
            }
        }

        currentStage.Value = PostMatchStage.RematchVote;
        Debug.Log("Rematch vote started.");
        rematchController.BeginVoteServer();

        while (!rematchController.IsVoteComplete)
        {
            yield return null;
        }

        StartNextMatch(rematchController.WasRematchUnanimous);

        postMatchCoroutine = null;
    }

    private void RegisterRemainingPlayersAsConceded()
    {
        foreach (ulong clientId in matchParticipants)
        {
            if (!resultsManager.IsPlayerResolved(clientId))
            {
                resultsManager.TryRegisterConcededPlayerServer(clientId);
            }
        }
    }

    private void StartNextMatch(bool rematchApproved)
    {
        currentStage.Value = PostMatchStage.Selection;
        Debug.Log("Post-match selection phase started.");
        nextMatchController.BeginSelectionServer(rematchApproved);
    }

    private void OnRematchVoteCompleted(bool rematchApproved)
    {
        if (!IsServer || CurrentStage != PostMatchStage.RematchVote)
            return;

        Debug.Log(
            rematchApproved
                ? "All connected players voted to rematch."
                : "Rematch vote did not reach unanimous agreement."
        );
    }

    private bool HasUnresolvedConnectedParticipants()
    {
        if (!participantsCaptured)
            return false;

        foreach (ulong clientId in matchParticipants)
        {
            // Disconnected players no longer need to be awaited.
            if (!playerTracker.IsPlayerConnected(clientId))
                continue;

            // Completed or conceded players are resolved.
            if (resultsManager.IsPlayerResolved(clientId))
                continue;

            return true;
        }

        // No unresolved connected participants remain.
        return false;
    }

    private double GetServerTime()
    {
        return NetworkManager.Singleton.ServerTime.Time;
    }

    // -------------------------
    // Concession
    // -------------------------

    // Called by the local player's future concede button.
    public void RequestConcede()
    {
        if (!CanRequestConcede)
            return;

        RequestConcedeRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestConcedeRpc(RpcParams rpcParams = default)
    {
        if (!IsServer ||
            CurrentStage != PostMatchStage.AdditionalFinishWindow ||
            !acceptingAdditionalFinishers.Value)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;

        // Only original match participants may concede.
        if (!matchParticipants.Contains(clientId))
            return;

        if (!playerTracker.IsPlayerConnected(clientId))
            return;

        if (resultsManager.TryRegisterConcededPlayerServer(clientId))
        {
            Debug.Log($"Player {clientId} conceded.");
        }
    }

    // -------------------------
    // Stage synchronization
    // -------------------------

    private void OnStageChanged(
        PostMatchStage previousStage,
        PostMatchStage newStage)
    {
        Debug.Log($"Post-match stage changed: {previousStage} -> {newStage}");
        StageChanged?.Invoke(newStage);

        if (IsServer &&
            newStage == PostMatchStage.ResultsScreen &&
            spectateOnResultsScreen)
        {
            SetConnectedPlayersInvulnerable();
        }

        if (newStage == PostMatchStage.ResultsScreen)
            SpectateLocalPlayerOnResultsScreen();

        bool isResultsScreenVisible =
            newStage == PostMatchStage.ResultsScreen;

        if (previousStage == PostMatchStage.ResultsScreen ||
            isResultsScreenVisible)
        {
            resultsScreenUI?.SetResultsVisible(isResultsScreenVisible);
            ResultsScreenVisibilityChanged?.Invoke(isResultsScreenVisible);
        }

        bool isRematchVoteVisible =
            newStage == PostMatchStage.RematchVote;

        if (isRematchVoteVisible)
            ApplyRematchRestriction();

        if (previousStage == PostMatchStage.RematchVote ||
            isRematchVoteVisible)
        {
            rematchVoteUI?.SetRematchVoteVisible(isRematchVoteVisible);
        }

        if (newStage == PostMatchStage.Selection)
        {
            SelectionPhaseStarted?.Invoke();
        }
    }

    private void ApplyRematchRestriction()
    {
        Cursor.lockState = CursorLockMode.None;

        if (rematchRestrictionApplied)
            return;

        NetworkObject localPlayerObject =
            NetworkManager.Singleton?.LocalClient?.PlayerObject;

        DisablePlayer disablePlayer =
            localPlayerObject != null
                ? localPlayerObject.GetComponent<DisablePlayer>()
                : null;

        if (disablePlayer == null)
        {
            Debug.LogWarning(
                "Cannot restrict the local player for rematch voting: " +
                "the local player or DisablePlayer component is unavailable.",
                this
            );
            return;
        }

        disablePlayer.AddSceneScopedRestriction(PlayerControlChannels.All);
        rematchRestrictionApplied = true;
    }

    public event System.Action<bool> ResultsScreenVisibilityChanged;

    private void SetConnectedPlayersInvulnerable()
    {
        if (NetworkManager.Singleton == null)
            return;

        foreach (NetworkClient client in
                 NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerHealth health =
                client.PlayerObject != null
                    ? client.PlayerObject.GetComponent<PlayerHealth>()
                    : null;

            if (health == null)
            {
                Debug.LogError(
                    $"Player {client.ClientId} has no PlayerHealth component.",
                    this
                );
                continue;
            }

            health.SetInvulnerableServer(true);
        }
    }

    private void SpectateLocalPlayerOnResultsScreen()
    {
        if (!spectateOnResultsScreen || NetworkManager.Singleton == null)
            return;

        NetworkObject localPlayerObject =
            NetworkManager.Singleton.LocalClient?.PlayerObject;

        if (localPlayerObject == null)
            return;

        PlayerLifePresentation lifePresentation =
            localPlayerObject.GetComponent<PlayerLifePresentation>();

        if (lifePresentation == null)
        {
            Debug.LogError(
                "PostMatchFlowController requires PlayerLifePresentation " +
                "when spectateOnResultsScreen is enabled.",
                this
            );
            return;
        }

        lifePresentation.SpectateOnResultsScreen();
    }
}
