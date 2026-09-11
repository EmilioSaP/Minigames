using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkObjectSpawn : MonoBehaviour
{
    [SerializeField] private NetworkObject[] networkObjectPrefabs;

    private bool hasSpawnedSessionObjects;
    private bool isSubscribedToServerStarted;

    private void OnEnable()
    {
        SubscribeToServerStarted();
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null && isSubscribedToServerStarted)
            NetworkManager.Singleton.OnServerStarted -= SpawnSessionObjects;

        isSubscribedToServerStarted = false;
    }

    private void Update()
    {
        SubscribeToServerStarted();

        if (!IsHost() || Keyboard.current == null || !Keyboard.current.lKey.wasPressedThisFrame)
            return;

        SpawnFirstObject();
    }

    private void SubscribeToServerStarted()
    {
        if (isSubscribedToServerStarted || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnServerStarted += SpawnSessionObjects;
        isSubscribedToServerStarted = true;

        if (NetworkManager.Singleton.IsServer)
            SpawnSessionObjects();
    }

    private void SpawnSessionObjects()
    {
        if (hasSpawnedSessionObjects || !IsHost())
            return;

        hasSpawnedSessionObjects = true;

        if (networkObjectPrefabs == null)
            return;

        foreach (NetworkObject prefab in networkObjectPrefabs)
            SpawnObject(prefab);
    }

    private void SpawnFirstObject()
    {
        if (networkObjectPrefabs != null && networkObjectPrefabs.Length > 0)
            SpawnObject(networkObjectPrefabs[0]);
    }

    private static void SpawnObject(NetworkObject prefab)
    {
        if (prefab == null)
            return;

        NetworkObject spawnedObject = Instantiate(prefab);
        spawnedObject.Spawn();
    }

    private static bool IsHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }
}
