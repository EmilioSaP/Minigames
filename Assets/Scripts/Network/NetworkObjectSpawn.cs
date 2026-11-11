using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NetworkObjectSpawn : MonoBehaviour
{
    [SerializeField] private NetworkObject[] networkObjectPrefabs;

    private NetworkManager networkManager;
    private NetworkSceneManager networkSceneManager;
    private bool hasSpawnedSessionObjects;
    private bool isSubscribedToServerLifecycle;
    private bool isSubscribedToSceneEvents;

    private void OnEnable()
    {
        SubscribeToServerStarted();
    }

    private void OnDisable()
    {
        if (isSubscribedToServerLifecycle && networkManager != null)
        {
            networkManager.OnServerStarted -= OnServerStarted;
            networkManager.OnServerStopped -= ResetSessionObjects;
        }

        isSubscribedToServerLifecycle = false;
        UnsubscribeFromSceneEvents();
        networkManager = null;
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
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        if (!isSubscribedToServerLifecycle)
        {
            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnServerStopped += ResetSessionObjects;
            isSubscribedToServerLifecycle = true;
        }

        SubscribeToSceneEvents();
    }

    private void OnServerStarted()
    {
        SpawnSessionObjects();
        SubscribeToSceneEvents();
    }

    private void SubscribeToSceneEvents()
    {
        if (isSubscribedToSceneEvents ||
            networkManager == null ||
            !networkManager.IsListening ||
            !networkManager.NetworkConfig.EnableSceneManagement)
        {
            return;
        }

        networkSceneManager = networkManager.SceneManager;
        if (networkSceneManager == null)
            return;

        networkSceneManager.OnLoadEventCompleted += HandleSceneLoadCompleted;
        isSubscribedToSceneEvents = true;
    }

    private void UnsubscribeFromSceneEvents()
    {
        if (isSubscribedToSceneEvents && networkSceneManager != null)
            networkSceneManager.OnLoadEventCompleted -= HandleSceneLoadCompleted;

        networkSceneManager = null;
        isSubscribedToSceneEvents = false;
    }

    private void HandleSceneLoadCompleted(
        string sceneName,
        LoadSceneMode _,
        List<ulong> __,
        List<ulong> ___)
    {
        if (sceneName == gameObject.scene.name)
            SpawnSessionObjects();
    }

    private void ResetSessionObjects(bool _)
    {
        hasSpawnedSessionObjects = false;
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
        spawnedObject.Spawn(destroyWithScene: true);
    }

    private static bool IsHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }
}
