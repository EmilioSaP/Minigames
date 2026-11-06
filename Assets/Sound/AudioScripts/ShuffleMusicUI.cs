using UnityEngine;
using UnityEngine.UI;

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

    public void EnableButton(bool enable)
    {
        // set button interactable state based on the enable parameter
        var button = GetComponent<UnityEngine.UI.Button>();
        if (button != null)
        {
            button.interactable = enable;
        }
    }
}