using UnityEngine;

public class PlayMusicInterrupt : MonoBehaviour
{
    [SerializeField] private AudioClip audioClip;


    public void PlayMusicInterruptPauseMusic()
    {
        if (audioClip == null)
        {
            Debug.LogWarning("PlayMusicInterrupt: No audio clip assigned.", this);
            return;
        }
        AudioManager.Instance.PlayMusic(audioClip, true, true);
    }
}