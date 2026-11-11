
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class NextMatchControllerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NextMatchController nextMatchController;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Animator animator;

    [Header("Options")]
    [SerializeField] private RectTransform optionsContainer;
    [SerializeField] private NextMatchOptionUI optionPrefab;

    [Tooltip("Show unavailable minigames as disabled options.")]
    [SerializeField] private bool showUnavailableOptions = true;

    [Header("Vote Information")]
    //[SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text voteProgressText;

    [Header("Animation")]
    [SerializeField] private string showTrigger = "TriggerShowUI";
    [SerializeField] private string hideTrigger = "TriggerHideUI";
    [SerializeField] private CountNumberAnimator countNumberAnimator;

    private readonly List<NextMatchOptionUI> optionInstances = new();

    private readonly Dictionary<int, NextMatchOptionUI> optionLookup = new();

    private Coroutine bindingCoroutine;

    private bool isSubscribed;
    private bool isVisible;
    private bool optionsCreated;

    private int showTriggerHash;
    private int hideTriggerHash;

    private void Awake()
    {
        showTriggerHash = Animator.StringToHash(showTrigger);
        hideTriggerHash = Animator.StringToHash(hideTrigger);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        bindingCoroutine = StartCoroutine(BindToController());
    }

    private void OnDisable()
    {
        if (bindingCoroutine != null)
            StopCoroutine(bindingCoroutine);

        if (isSubscribed && nextMatchController != null)
        {
            nextMatchController.VoteCountsChanged -= RefreshVoteCounts;
        }

        isSubscribed = false;
    }

    private IEnumerator BindToController()
    {
        while (NetworkManager.Singleton == null ||
               !NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }

        while (nextMatchController == null ||
               !nextMatchController.IsSpawned)
        {
            if (nextMatchController == null)
            {
                nextMatchController =
                    FindFirstObjectByType<NextMatchController>();
            }

            yield return null;
        }

        nextMatchController.VoteCountsChanged += RefreshVoteCounts;

        isSubscribed = true;

        // Handle joining while a selection is already active.
        UpdateSelectionVisibility();

        bindingCoroutine = null;
    }

    private void Update()
    {
        if (nextMatchController == null ||
            !nextMatchController.IsSpawned)
        {
            return;
        }

        UpdateSelectionVisibility();

        if (isVisible)
        {
            RefreshTimer();
            RefreshVoteProgress();
        }
    }

    // ------------------------------------
    // VISIBILITY
    // ------------------------------------

    private void UpdateSelectionVisibility()
    {
        bool shouldBeVisible =
            nextMatchController.IsSelectionActive;

        if (shouldBeVisible == isVisible)
            return;

        SetSelectionVisible(shouldBeVisible);
    }

    private void SetSelectionVisible(bool visible)
    {
        isVisible = visible;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (animator != null)
        {
            int triggerToReset = visible
                ? hideTriggerHash
                : showTriggerHash;

            animator.ResetTrigger(triggerToReset);

            animator.SetTrigger(
                visible ? showTriggerHash : hideTriggerHash
            );
        }
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }

        if (visible)
        {
            BuildOptionsIfNeeded();
            RefreshVoteCounts();
            RefreshVoteProgress();
            RefreshTimer();
        }
    }

    // ------------------------------------
    // BUILD OPTIONS
    // ------------------------------------

    private void BuildOptionsIfNeeded()
    {
        if (optionsCreated)
            return;

        if (optionPrefab == null || optionsContainer == null)
        {
            Debug.LogError(
                "NextMatchControllerUI is missing its option prefab or container."
            );

            return;
        }

        IReadOnlyList<MinigameCatalog.MinigameOption> minigames =
            nextMatchController.Minigames;

        IReadOnlyList<int> availableIndices =
            nextMatchController.GetAvailableMinigameIndices();

        for (int i = 0; i < minigames.Count; i++)
        {
            MinigameCatalog.MinigameOption option = minigames[i];

            if (option == null)
                continue;

            bool available = IsAvailable(i, availableIndices);

            if (!available && !showUnavailableOptions)
                continue;

            NextMatchOptionUI instance =
                Instantiate(optionPrefab, optionsContainer);

            instance.Initialize(
                i,
                option.displayName,
                available,
                OnMinigameSelected
            );

            optionInstances.Add(instance);
            optionLookup.Add(i, instance);
        }

        optionsCreated = true;
    }

    private bool IsAvailable(
        int index,
        IReadOnlyList<int> availableIndices)
    {
        for (int i = 0; i < availableIndices.Count; i++)
        {
            if (availableIndices[i] == index)
                return true;
        }

        return false;
    }

    // ------------------------------------
    // VOTING
    // ------------------------------------

    private void OnMinigameSelected(int minigameIndex)
    {
        if (!isVisible ||
            nextMatchController == null ||
            !nextMatchController.IsSelectionActive)
        {
            return;
        }

        nextMatchController.VoteForMinigame(minigameIndex);

        // Immediate local visual feedback.
        RefreshVoteCounts();
    }

    private void RefreshVoteCounts()
    {
        if (nextMatchController == null)
            return;

        ulong localClientId =
            NetworkManager.Singleton.LocalClientId;

        bool hasVoted =
            nextMatchController.TryGetVoteForClient(
                localClientId,
                out int selectedIndex
            );

        foreach (KeyValuePair<int, NextMatchOptionUI> entry in optionLookup)
        {
            int index = entry.Key;
            NextMatchOptionUI option = entry.Value;

            if (option == null)
                continue;

            option.SetVoteCount(
                nextMatchController.GetVoteCount(index)
            );

            option.SetSelected(
                hasVoted && selectedIndex == index
            );
        }
    }

    // ------------------------------------
    // TIMER / PROGRESS
    // ------------------------------------

    private void RefreshTimer()
    {
        float remaining = nextMatchController.VoteTimeRemaining;

        int seconds = Mathf.CeilToInt(remaining);
        //int minutes = seconds / 60;
        //int remainder = seconds % 60;

        //timerText.text = $"{minutes:00}:{remainder:00}";
        countNumberAnimator.AnimateNumber(seconds);
    }

    private void RefreshVoteProgress()
    {
        if (voteProgressText == null)
            return;

        voteProgressText.text =
            $"{nextMatchController.VotedPlayerCount} / " +
            $"{nextMatchController.ConnectedPlayerCount} players voted";
    }
}
