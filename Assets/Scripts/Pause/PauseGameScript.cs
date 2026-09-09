using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PauseGameScript : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;

    private DisablePlayer disablePlayer;
    private bool isPaused;

    private void Start()
    {
        pauseUI.SetActive(false);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            TogglePause();
    }

    private void OnClientConnected(ulong clientId)
    {
        // We only care about OUR client.
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        NetworkObject playerObject =
            NetworkManager.Singleton.LocalClient.PlayerObject;

        if (playerObject == null)
        {
            Debug.LogWarning("Local player object not found.");
            return;
        }

        disablePlayer = playerObject.GetComponent<DisablePlayer>();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        pauseUI.SetActive(isPaused);

        if (disablePlayer != null)
            disablePlayer.SetPlayerEnabled(!isPaused);
        // For the case the player was destroyed and disablePlayer is no longer available and mouse remained locked.
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}