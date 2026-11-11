using UnityEngine;

public static class GameSettings
{
    // Define your keys here once to avoid typos
    private const string PREF_SCREENSHAKE = "AllowScreenshake";

    // A public property that automatically handles the saving/loading
    public static bool AllowScreenshake
    {
        get 
        { 
            // Default to 1 (true) if the key doesn't exist yet
            return PlayerPrefs.GetInt(PREF_SCREENSHAKE, 1) == 1; 
        }
        set 
        { 
            PlayerPrefs.SetInt(PREF_SCREENSHAKE, value ? 1 : 0);
            PlayerPrefs.Save(); 
        }
    }
}