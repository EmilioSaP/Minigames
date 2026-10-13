using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PauseGameScript : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;
    [SerializeField] private UnityEvent onPause = new UnityEvent();
    [SerializeField] private UnityEvent onResume = new UnityEvent();

    //[SerializeField] private UnityEvent onPlayerJoin = new UnityEvent();
    //[SerializeField] private UnityEvent onPlayerLeave = new UnityEvent();

    [SerializeField] private bool enablePause = true;

    private DisablePlayer disablePlayer;
    
    private bool isPaused;

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
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
            TogglePause();
    }

    private void OnLocalPlayerReady(DisablePlayer player)
    {
        disablePlayer = player;
        //onPlayerJoin.Invoke();
        enablePause = true;
        isPaused = false;
    }

    private void OnLocalPlayerLeft()
    {
        isPaused = true;
        Cursor.lockState = CursorLockMode.None;
        //onPlayerLeave.Invoke();
        enablePause = false;
    }

    public void TogglePause()
    {
        if (!enablePause)
        {
            return;
        }
        isPaused = !isPaused;

        pauseUI.SetActive(isPaused);

        if (isPaused)
        {
            onPause.Invoke();
        }
        else
        {
            onResume.Invoke();
        }

        if (disablePlayer != null)
            disablePlayer.SetPlayerEnabled(!isPaused);
        // For the case the player was destroyed and disablePlayer is no longer available and mouse remained locked.
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}