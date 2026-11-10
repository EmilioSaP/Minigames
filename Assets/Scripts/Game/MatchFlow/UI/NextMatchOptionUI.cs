
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NextMatchOptionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text minigameNameText;
    [SerializeField] private TMP_Text voteCountText;

    [Header("Indicators")]
    [SerializeField] private GameObject selectedIndicator;
    [SerializeField] private GameObject unavailableIndicator;

    private Action<int> onSelected;
    private int minigameIndex;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void Initialize(
        int index,
        string displayName,
        bool available,
        Action<int> selectionCallback)
    {
        minigameIndex = index;
        onSelected = selectionCallback;

        if (minigameNameText != null)
            minigameNameText.text = displayName;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        SetAvailable(available);
        SetSelected(false);
        SetVoteCount(0);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        onSelected?.Invoke(minigameIndex);
    }

    public void SetAvailable(bool available)
    {
        if (button != null)
            button.interactable = available;

        if (unavailableIndicator != null)
            unavailableIndicator.SetActive(!available);
    }

    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);
    }

    public void SetVoteCount(int count)
    {
        if (voteCountText != null)
        {
            voteCountText.text =
                count == 1 ? "1 vote" : $"{count} votes";
        }
    }
}
