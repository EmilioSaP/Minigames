using UnityEngine;
using System.Collections;

public class NetworkMusicCall : MonoBehaviour
{
    private float ownDelay = 0.1f; // Delay in seconds before starting the music
    public void StartMusic()
    {
        if (NetworkMusicController.Instance != null)
        {
            NetworkMusicController.Instance.StartMusic();
        }
        else
        {
            Debug.LogWarning("NetworkMusicCall: NetworkMusicController is not in the scene yet.");
        }
    }
    public void StartMusicDelay(float delay)
    {
        StartCoroutine(WaitThenStartMusic(delay));
    }

    public void StartMusicDelay()
    {
        StartCoroutine(WaitThenStartMusic(ownDelay));
    }

    private IEnumerator WaitThenStartMusic(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartMusic();
    }
}
