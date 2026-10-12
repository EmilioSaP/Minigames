using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PauseGameScript : MonoBehaviour
{
    [SerializeField] private GameObject pauseUI;
    [SerializeField] private UnityEvent onPause = new UnityEvent();
    [SerializeField] private UnityEvent onResume = new UnityEvent();

    private DisablePlayer disablePlayer;
    private bool isPaused;

    private void OnEnable()
    {
        NetworkGameManager.LocalPlayerReady += OnLocalPlayerReady;
    }
    private void OnDisable()
    {
        NetworkGameManager.LocalPlayerReady -= OnLocalPlayerReady;
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
        disablePlayer.SetPlayerEnabled(!isPaused);
        onResume.Invoke();
    }

    public void TogglePause()
    {
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