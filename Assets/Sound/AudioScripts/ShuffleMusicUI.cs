using UnityEngine;

public class ShuffleMusicUI : MonoBehaviour
{
    /// <summary>
    /// Link this method to the UI Button's OnClick event in the Inspector.
    /// </summary>
    public void RequestShuffle()
    {
        if (NetworkMusicController.Instance != null)
        {
            NetworkMusicController.Instance.ShuffleFromHost();
        }
        else
        {
            Debug.LogWarning("ShuffleMusicUI: NetworkMusicController is not in the scene yet.");
        }
    }
}