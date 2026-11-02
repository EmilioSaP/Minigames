using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

public class PauseGameScript : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;
    [SerializeField] private TMP_InputField joinCodeTextField;  // Makes it easier to copy the join code
    [SerializeField] private UnityEvent onPause = new UnityEvent();
    [SerializeField] private UnityEvent onResume = new UnityEvent();

    //[SerializeField] private UnityEvent onPlayerJoin = new UnityEvent();
    //[SerializeField] private UnityEvent onPlayerLeave = new UnityEvent();

    [SerializeField] private bool enablePause = true;

    private DisablePlayer disablePlayer;
    
    private bool isPaused;
    public bool HasLocalPlayer => disablePlayer != null;

    private void OnEnable()
    {
        NetworkGameManager.LocalPlayerReady += OnLocalPlayerReady;
        NetworkGameManager.LocalPlayerLeft += OnLocalPlayerLeft;
    }
    private void OnDisable()
    {
        NetworkGameManager.LocalPlayerReady -= OnLocalPlayerReady;
        NetworkGameManager.LocalPlayerLeft -= OnLocalPlayerLeft;
    }

    private void Start()
    {
        pauseUI.SetActive(false);

        if (joinCodeTextField != null &&
            !string.IsNullOrEmpty(ConnectionManager.CurrentJoinCode))
        {
            joinCodeTextField.text =
                $"{ConnectionManager.CurrentJoinCode}";
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsClient)
            return;

        NetworkObject localPlayerObject =
            networkManager.LocalClient.PlayerObject;

        if (localPlayerObject == null)
            return;

        DisablePlayer localPlayer =
            localPlayerObject.GetComponent<DisablePlayer>();

        if (localPlayer == null)
        {
            Debug.LogWarning(
                "Local player is missing the DisablePlayer component.");
            return;
        }

        OnLocalPlayerReady(localPlayer);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Pause();
            return;
        }
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
            TogglePause();
    }

    private void OnLocalPlayerReady(DisablePlayer player)
    {
        disablePlayer = player;
        //onPlayerJoin.Invoke();
        enablePause = true;
        isPaused = false;
        pauseUI.SetActive(false);
    }

    private void OnLocalPlayerLeft()
    {
        isPaused = true;
        Cursor.lockState = CursorLockMode.None;
        //onPlayerLeave.Invoke();
        enablePause = false;
        isPaused = false;
        pauseUI.SetActive(false);
    }

    public void TogglePause()
    {
        if (!enablePause){return;}

        isPaused = !isPaused;

        PauseActions(isPaused);
    }

    public void UnPause()
    {
        if (!enablePause){return;}
        isPaused = false;
        PauseActions(isPaused);
    }

    public void Pause()
    {
        if (!enablePause){return;}
        isPaused = true;
        PauseActions(isPaused);
    }

    public void PauseActions(bool paused)
    {
        pauseUI.SetActive(paused);
        if (paused)
        {
            onPause.Invoke();
        }
        else
        {
            onResume.Invoke();
        }

        if (disablePlayer != null)
            disablePlayer.SetPlayerEnabled(!paused);
        // For the case the player was destroyed and disablePlayer is no longer available and mouse remained locked.
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}