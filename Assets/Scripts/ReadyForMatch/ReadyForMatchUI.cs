using UnityEngine;
using TMPro;

public class ReadyForMatchUI : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text instructionText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void SetReadyUI()
    {
        statusText.text = "[Ready]";
        instructionText.text = "Press [R] to mark as ready";
    }

    public void SetNotReadyUI()
    {
        statusText.text = "[Not Ready]";
        instructionText.text = "Press [R] to mark as not ready";
    }
}
