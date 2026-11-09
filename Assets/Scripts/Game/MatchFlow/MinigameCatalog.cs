using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MinigameCatalog",
    menuName = "Minigames/Minigame Catalog"
)]
public class MinigameCatalog : ScriptableObject
{
    [Serializable]
    public class MinigameOption
    {
        public string displayName;
        public string sceneName;
        [Min(1)] public int minimumPlayers = 1;
        [Min(0)] public int maximumPlayers;
    }

    [SerializeField] private List<MinigameOption> minigames = new();

    public IReadOnlyList<MinigameOption> Minigames => minigames;

    public List<int> GetAvailableOptionIndices(int playerCount)
    {
        List<int> availableOptions = new();
        for (int i = 0; i < minigames.Count; i++)
        {
            MinigameOption option = minigames[i];
            if (option == null || string.IsNullOrWhiteSpace(option.sceneName))
                continue;

            if (playerCount < option.minimumPlayers ||
                (option.maximumPlayers > 0 &&
                 playerCount > option.maximumPlayers))
            {
                continue;
            }

            availableOptions.Add(i);
        }

        return availableOptions;
    }
}
