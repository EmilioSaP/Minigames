using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class PlayerIdentity : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> PlayerName =
        new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    [SerializeField] private PlayerNameTag nameTag;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        PlayerName.OnValueChanged += OnPlayerNameChanged;

        // Apply current value immediately.
        UpdateNameTag(PlayerName.Value.ToString());

        if (IsOwner)
        {
            SetPlayerName();
        }
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= OnPlayerNameChanged;

        base.OnNetworkDespawn();
    }

    private void SetPlayerName()
    {
        PlayerName.Value = $"Player {OwnerClientId + 1}";
    }

    private void OnPlayerNameChanged(
        FixedString64Bytes oldName,
        FixedString64Bytes newName)
    {
        UpdateNameTag(newName.ToString());
    }

    private void UpdateNameTag(string playerName)
    {
        if (nameTag != null)
        {
            nameTag.SetName(playerName);
        }
    }
}

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