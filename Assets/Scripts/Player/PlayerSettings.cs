using UnityEngine;

public static class PlayerSettings
{
    public static string PlayerName
    {
        get => PlayerPrefs.GetString("PlayerName", "");
        set
        {
            PlayerPrefs.SetString("PlayerName", value);
            PlayerPrefs.Save();
        }
    }
}