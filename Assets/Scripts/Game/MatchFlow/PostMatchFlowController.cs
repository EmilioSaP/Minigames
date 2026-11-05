
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum PostMatchStage : byte
{
    None,
    FinishPresentation,
    AdditionalFinishWindow,
    Selection
}

[RequireComponent(typeof(NetworkObject))]
public class PostMatchFlowController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MatchFlowManager matchFlow;
    [SerializeField] private MatchResultsManager resultsManager;
    [SerializeField] private MatchPlayerTracker playerTracker;

    [Header("Finish Presentation")]
    [SerializeField] private float finishPresentationDuration = 2.5f;

    [Header("Additional Finish Window")]
    [SerializeField] private bool waitForRemainingPlayers = true;
    [SerializeField] private float additionalFinishWindowDuration = 10f;

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

            if (matchFlow != null &&
                matchFlow.IsSpawned &&
                resultsManager != null &&
                resultsManager.IsSpawned &&
                playerTracker != null)
            {
                break;
            }

            yield return null;
        }

        matchFlow.PhaseChanged += OnMatchPhaseChanged;

        // Handle the current state in case we subscribed late.
        OnMatchPhaseChanged(matchFlow.CurrentPhase);

        bindingCoroutine = null;
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

        currentStage.Value = PostMatchStage.Selection;

        Debug.Log("Post-match selection phase started.");

        postMatchCoroutine = null;
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

    [Rpc(SendTo.Server, RequireOwnership = false)]
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
        StageChanged?.Invoke(newStage);

        if (newStage == PostMatchStage.Selection)
        {
            SelectionPhaseStarted?.Invoke();
        }
    }
}
