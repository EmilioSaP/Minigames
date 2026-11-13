using UnityEngine;

public class HighlightSwap: MonoBehaviour
{
    [SerializeField] private AudioClip audioClip; 
    [SerializeField] private Animator animatorA;
    [SerializeField] private Animator animatorB;
    [SerializeField] private string currentObject = "A";
    [SerializeField] public string swapInTrigger = "SwapIn";
    [SerializeField] public string swapOutTrigger = "SwapOut";
    [SerializeField] private bool isVisible = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    // Update is called once per frame
    public void SwapToA()
    {
        if (!isVisible)
        {
            SwapShow();
            return;
        }
        if (currentObject == "A")
            return;

        if (currentObject == "B")
        {
            animatorA.SetTrigger(swapInTrigger);
            animatorB.SetTrigger(swapOutTrigger);
        }
        else
        {
            Debug.Log("SwappingUIError!");
        }
        if (audioClip != null)
            AudioManager.Instance.PlayLocalSFX(audioClip);
        
        currentObject = "A";
    }

    public void SwapToB()
    {
        if (!isVisible)
        {
            SwapShow();
            return;
        }

        if (currentObject == "B")
            return;

        if (currentObject == "A")
        {
            animatorB.SetTrigger(swapInTrigger);
            animatorA.SetTrigger(swapOutTrigger);
        }
        else
        {
            Debug.Log("SwappingUIError!");
        }
        if (audioClip != null)
            AudioManager.Instance.PlayLocalSFX(audioClip);

        currentObject = "B";
    }

    public void SwapHide()
    {
        if (!isVisible)
            return;

        if (currentObject == "A")
        {
            animatorA.SetTrigger(swapOutTrigger);
        }else if (currentObject == "B")
        {
            animatorB.SetTrigger(swapOutTrigger);
        }
        else
        {
            Debug.Log("SwappingUIError!");
        }
        if (audioClip != null)
            AudioManager.Instance.PlayLocalSFX(audioClip);

        isVisible = false;
    }

    public void SwapShow()
    {
        if (isVisible)
            return;
        
        if (currentObject == "A")
        {
            animatorA.SetTrigger(swapInTrigger);
        }else if (currentObject == "B")
        {
            animatorB.SetTrigger(swapInTrigger);
        }
        else
        {
            Debug.Log("SwappingUIError!");
        }
        isVisible = true;
    }
}
