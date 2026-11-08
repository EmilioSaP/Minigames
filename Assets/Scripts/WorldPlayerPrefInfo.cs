using System.ComponentModel;
using UnityEngine;
// unused Singleton that contains the player preferences for the world scene. This is used to store the player's username and color, so that it can be accessed by other scripts in the world scene.
public class WorldPlayerPrefInfo : MonoBehaviour
{
    public static WorldPlayerPrefInfo Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    

    private void Start()
    {
        // If the player has not set a color, set it to white
        if (!PlayerPrefs.HasKey("Color"))
        {
            PlayerPrefs.SetString("Color", "#FFFFFF");
        }
    }
}
