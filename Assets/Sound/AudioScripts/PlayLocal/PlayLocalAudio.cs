using UnityEngine;

public class PlayLocalAudio : MonoBehaviour
{
    [SerializeField] private AudioClip audioClip;


    public void PlayAudio()
    {
        if (audioClip == null)
        {
            Debug.LogWarning("PlayLocalAudio: No audio clip assigned.", this);
            return;
        }
        AudioManager.Instance.PlayLocalSFX(audioClip);
    }
}