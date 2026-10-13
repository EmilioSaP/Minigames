using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class JoinUI : MonoBehaviour
{
    [SerializeField] private UnityEvent onPlayerJoin = new UnityEvent();
    [SerializeField] private UnityEvent onPlayerLeave = new UnityEvent();
    
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


    private void OnLocalPlayerReady(DisablePlayer player)
    {
        onPlayerJoin.Invoke();
    }

    private void OnLocalPlayerLeft()
    {
        onPlayerLeave.Invoke();
    }
}
