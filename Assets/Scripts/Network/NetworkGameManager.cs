using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        NetworkObject player = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;

        if (player == null)
            return;

        int spawnIndex = (int)(clientId % (ulong)spawnPoints.Length);

        player.transform.position = spawnPoints[spawnIndex].position;
    }
}