using TMPro;
using UnityEngine;
//General counting up or down animator. Controls the text and the animator. It also plays the audio clips for each number. Manually fades the text in and out. The animator is responsible for the fade out.
public class CountNumberAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Animator countdownAnimator;
    [SerializeField] private FadeCanvasGroupScript fadeCanvasGroup;

    [Header("Audios")] 
    [SerializeField] private AudioClip moreThanThreeAudioClip;    
    [SerializeField] private AudioClip threeAudioClip;    
    [SerializeField] private AudioClip twoAudioClip;
    [SerializeField] private AudioClip oneAudioClip;
    [SerializeField] private AudioClip goAudioClip;

    public void AnimateNumber(int seconds)
    {
        ShowText(seconds.ToString());

        if (seconds > 3)
        {
            AudioManager.Instance.PlayLocalSFX(moreThanThreeAudioClip);
        }
        else if (seconds == 3)
        {
            AudioManager.Instance.PlayLocalSFX(threeAudioClip);
        }
        else if (seconds == 2)
        {
            AudioManager.Instance.PlayLocalSFX(twoAudioClip);
        }
        else if (seconds == 1)
        {
            AudioManager.Instance.PlayLocalSFX(oneAudioClip);
        }

        if (countdownAnimator != null)
            countdownAnimator.SetTrigger("Countdown");
    }

    public void AnimateGo(string value)
    {
        ShowText(value);

        if (goAudioClip != null)
            AudioManager.Instance.PlayLocalSFX(goAudioClip);

        if (countdownAnimator != null)
            countdownAnimator.SetTrigger("Go");
    }

    public void Hide()
    {
        if (countdownText == null)
            return;

        // fadeCanvasGroup.FadeOut(); done by animator
        // countdownText.enabled = false;
    }

    private void ShowText(string value)
    {
        // fadeCanvasGroup.SetAlpha(1f); done by animator
        if (countdownText == null)
            return;

        countdownText.text = value;
        countdownText.enabled = true;
    }
}