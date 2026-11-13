using UnityEngine;
using TMPro;

public class ReadyForMatchUI : MonoBehaviour
{
    [SerializeField] private HighlightSwap swapUI;
    [SerializeField] private TMP_Text instructionText;

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            swapUI.SwapShow();
            instructionText.text = "Press [R] to mark as ready";
        }
        else
        {
            swapUI.SwapHide();
            instructionText.text = "";
        }

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void SetReadyUI()
    {
        swapUI.SwapToB();
        instructionText.text = "Press [R] to mark as ready";
    }

    public void SetNotReadyUI()
    {
        swapUI.SwapToA();
        instructionText.text = "Press [R] to mark as not ready";
    }
    public void AllPlayersReady()
    {
        SetVisible(false);
        instructionText.text = "";
    }
}
