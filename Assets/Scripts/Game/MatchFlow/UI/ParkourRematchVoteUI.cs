
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ParkourRematchVoteUI : MonoBehaviour, IRematchVoteUI
{
    [Header("References")]
    [SerializeField] private RematchController rematchController;

    [Header("Canvas")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Animator animator;

    [Header("Buttons")]
    [SerializeField] private Button rematchButton;
    [SerializeField] private Button nextMinigameButton;

    [Header("Selection Indicators")]
    [SerializeField] private GameObject rematchSelected;
    [SerializeField] private GameObject nextMinigameSelected;

    [Header("Vote Information")]
    //[SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text playerVoteText;
    [SerializeField] private TMP_Text rematchVoteCountText;
    [SerializeField] private TMP_Text nextMinigameVoteCountText;

    [Header("Animation")]
    [SerializeField] private string showTrigger = "TriggerShowUI";
    [SerializeField] private string hideTrigger = "TriggerHideUI";
    [SerializeField] private CountNumberAnimator countNumberAnimator;

    private Coroutine bindingCoroutine;

    private bool isSubscribed;
    private bool isVisible;

    private int showTriggerHash;
    private int hideTriggerHash;
    private int currentSecond = -1;

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

        SetButtonsInteractable(false);
        RefreshSelectionIndicators(false);
    }

    private void OnEnable()
    {
        bindingCoroutine = StartCoroutine(BindToRematchController());
    }

    private void OnDisable()
    {
        if (bindingCoroutine != null)
            StopCoroutine(bindingCoroutine);

        if (isSubscribed && rematchController != null)
        {
            rematchController.VoteCountsChanged -= RefreshUI;
        }

        isSubscribed = false;
    }

    private IEnumerator BindToRematchController()
    {
        while (NetworkManager.Singleton == null ||
               !NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }

        while (rematchController == null ||
               !rematchController.IsSpawned)
        {
            if (rematchController == null)
            {
                rematchController =
                    FindFirstObjectByType<RematchController>();
            }

            yield return null;
        }

        rematchController.VoteCountsChanged += RefreshUI;

        isSubscribed = true;

        RefreshUI();

        bindingCoroutine = null;
    }

    private void Update()
    {
        if (!isVisible || rematchController == null ||
            !rematchController.IsSpawned)
        {
            return;
        }

        RefreshTimer();
        RefreshVoteAvailability();
    }

    // ------------------------------------
    // IRematchVoteUI
    // ------------------------------------

    public void SetRematchVoteVisible(bool visible)
    {
        if (isVisible == visible)
            return;

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
            RefreshUI();
    }

    // ------------------------------------
    // BUTTONS
    // ------------------------------------

    public void VoteRematch()
    {
        SubmitVote(RematchVoteChoice.Rematch);
    }

    public void VoteNextMinigame()
    {
        SubmitVote(RematchVoteChoice.NextMinigame);
    }

    public void LeaveParty()
    {
        SubmitVote(RematchVoteChoice.LeaveParty);
    }

    private void SubmitVote(RematchVoteChoice choice)
    {
        if (!isVisible ||
            rematchController == null ||
            !rematchController.IsVoteActive)
        {
            return;
        }

        rematchController.SubmitVote(choice);

        // Refresh immediately for responsive local feedback.
        RefreshUI();
    }

    // ------------------------------------
    // UI UPDATES
    // ------------------------------------

    private void RefreshUI()
    {
        if (rematchController == null ||
            !rematchController.IsSpawned)
        {
            return;
        }

        int rematchVotes =
            rematchController.GetVoteCount(RematchVoteChoice.Rematch);

        int nextMinigameVotes =
            rematchController.GetVoteCount(RematchVoteChoice.NextMinigame);

        if (rematchVoteCountText != null)
            rematchVoteCountText.text = rematchVotes.ToString();

        if (nextMinigameVoteCountText != null)
            nextMinigameVoteCountText.text = nextMinigameVotes.ToString();

        if (playerVoteText != null)
        {
            playerVoteText.text =
                $"{rematchController.VotedPlayerCount} / " +
                $"{rematchController.ConnectedPlayerCount} players voted";
        }

        RefreshTimer();
        RefreshVoteAvailability();
        RefreshLocalSelection();
    }

    private void RefreshTimer()
    {
        if (rematchController == null)
            return;

        float remaining = rematchController.VoteTimeRemaining;

        int seconds = Mathf.CeilToInt(remaining);
        //int minutes = seconds / 60;
        //int remainder = seconds % 60;

        //timerText.text = $"{minutes:00}:{remainder:00}";
        if (seconds != currentSecond)
        {
            currentSecond = seconds;
            countNumberAnimator.AnimateNumber(seconds);
        }
    }

    private void RefreshVoteAvailability()
    {
        bool canVote =
            isVisible &&
            rematchController != null &&
            rematchController.IsVoteActive;

        SetButtonsInteractable(canVote);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (rematchButton != null)
            rematchButton.interactable = interactable;

        if (nextMinigameButton != null)
            nextMinigameButton.interactable = interactable;
    }

    private void RefreshLocalSelection()
    {
        if (NetworkManager.Singleton == null)
            return;

        bool hasVoted =
            rematchController.TryGetVoteForClient(
                NetworkManager.Singleton.LocalClientId,
                out RematchVoteChoice choice
            );

        if (hasVoted)
        {
            RefreshSelectionIndicators(true, choice);
        }
        else
        {
            RefreshSelectionIndicators(false);
        }
    }

    private void RefreshSelectionIndicators(bool hasVoted)
    {
        RefreshSelectionIndicators(
            hasVoted,
            RematchVoteChoice.Rematch
        );
    }

    private void RefreshSelectionIndicators(
        bool hasVoted,
        RematchVoteChoice choice)
    {
        if (rematchSelected != null)
        {
            rematchSelected.SetActive(
                hasVoted && choice == RematchVoteChoice.Rematch
            );
        }

        if (nextMinigameSelected != null)
        {
            nextMinigameSelected.SetActive(
                hasVoted && choice == RematchVoteChoice.NextMinigame
            );
        }
    }
}
