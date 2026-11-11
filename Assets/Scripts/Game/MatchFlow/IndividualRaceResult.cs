
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IndividualRaceResult : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TMP_Text placementText;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text finishTimeText;

    [Header("Background")]
    [SerializeField] private Image background;

    [Tooltip("Index 0 = 1 player, index 7 = 8 players.")]
    [SerializeField] private Sprite[] backgroundsByPlayerCount;

    [SerializeField] private Sprite winnerBackground;
    [SerializeField] private Sprite dnfBackground;

    [Header("Optional Indicators")]
    [SerializeField] private GameObject winnerIndicator;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [SerializeField] private string entranceTrigger = "TriggerEntranceUI";
    [SerializeField] private string exitTrigger = "TriggerExitUI";

    [Header("Layout")]
    [SerializeField] private LayoutElement layoutElement;

    private int entranceTriggerHash;
    private int exitTriggerHash;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (layoutElement == null)
            layoutElement = GetComponent<LayoutElement>();

        entranceTriggerHash =
            Animator.StringToHash(entranceTrigger);

        exitTriggerHash =
            Animator.StringToHash(exitTrigger);
    }

    // ------------------------------------
    // DATA
    // ------------------------------------

    public void SetResult(
        int placement,
        string username,
        string finishTime,
        bool didNotFinish,
        bool isWinner,
        int totalPlayers,
        float preferredHeight)
    {
        if (placementText != null)
        {
            placementText.text = didNotFinish
                ? "DNF"
                : FormatPlacement(placement);
        }

        if (usernameText != null)
        {
            usernameText.text = username;
        }

        if (finishTimeText != null)
        {
            finishTimeText.text = didNotFinish
                ? "DNF"
                : finishTime;
        }

        if (winnerIndicator != null)
        {
            winnerIndicator.SetActive(isWinner);
        }

        SetBackground(totalPlayers, didNotFinish, isWinner);

        if (layoutElement != null)
        {
            layoutElement.preferredHeight = preferredHeight;
        }
    }

    private void SetBackground(
        int totalPlayers,
        bool didNotFinish,
        bool isWinner)
    {
        if (background == null)
            return;

        Sprite selectedBackground = null;

        // Status-specific backgrounds take priority.
        if (didNotFinish && dnfBackground != null)
        {
            selectedBackground = dnfBackground;
        }
        else if (isWinner && winnerBackground != null)
        {
            selectedBackground = winnerBackground;
        }
        else if (backgroundsByPlayerCount != null &&
                 backgroundsByPlayerCount.Length > 0)
        {
            int index = Mathf.Clamp(
                totalPlayers - 1,
                0,
                backgroundsByPlayerCount.Length - 1
            );

            selectedBackground = backgroundsByPlayerCount[index];
        }

        if (selectedBackground != null)
        {
            background.sprite = selectedBackground;
        }
    }

    private string FormatPlacement(int placement)
    {
        int lastTwoDigits = placement % 100;

        if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
            return $"{placement}th";

        switch (placement % 10)
        {
            case 1:
                return $"{placement}st";

            case 2:
                return $"{placement}nd";

            case 3:
                return $"{placement}rd";

            default:
                return $"{placement}th";
        }
    }

    // ------------------------------------
    // ANIMATION
    // ------------------------------------

    public void PlayEntrance()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(exitTriggerHash);
        animator.SetTrigger(entranceTriggerHash);
    }

    public void PlayExit()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(entranceTriggerHash);
        animator.SetTrigger(exitTriggerHash);
    }
}
