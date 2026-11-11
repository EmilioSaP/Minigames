using UnityEngine;

public class PlayRandomAudio : MonoBehaviour
{
    [SerializeField] private AudioClip[] audioClips;


    public void PlayAudio()
    {
        //randomly select an audio clip from the array
        AudioClip clip = audioClips[Random.Range(0, audioClips.Length)];

        if (clip == null)
        {
            Debug.LogWarning("PlayLocalAudio: No audio clip assigned.", this);
            return;
        }
        AudioManager.Instance.PlayLocalSFX(clip);
    }
}
