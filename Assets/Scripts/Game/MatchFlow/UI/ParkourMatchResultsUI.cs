using UnityEngine;

public class ParkourMatchResultsUI : MonoBehaviour, IMatchResultsUI
{
    [SerializeField] private CanvasGroup resultsGroup;
    [SerializeField] private CountNumberAnimator countAnimator;

    public void SetResultsVisible(bool visible)
    {
        if (resultsGroup == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        resultsGroup.alpha = visible ? 1f : 0f;
        resultsGroup.interactable = visible;
        resultsGroup.blocksRaycasts = visible;
    }
}
