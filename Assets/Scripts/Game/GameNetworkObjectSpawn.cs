using Unity.Netcode;
using UnityEngine;

public class GameNetworkObjectSpawn : MonoBehaviour
{
    [SerializeField] private NetworkObject networkObjectPrefab;

    private void Start()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsHost)
        {
            return;
        }

        if (networkObjectPrefab == null)
            return;

        SpawnObject(networkObjectPrefab);
    }

    private void SpawnObject(NetworkObject prefab)
    {
        NetworkObject spawnedObject = Instantiate(prefab, transform.position, transform.rotation);
        spawnedObject.Spawn(destroyWithScene: true);
    }
}