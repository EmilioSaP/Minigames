using UnityEngine;

/// <summary>
/// Provides typed access to locally persisted player preferences.
/// Use this for preferences that should survive sessions, not replicated player state.
/// </summary>
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