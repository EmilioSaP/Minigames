using UnityEngine;

public class OnEnableAction : MonoBehaviour
{
    // Every time this object is enabled, it will call the action.
    [SerializeField] private UnityEngine.Events.UnityEvent onEnableAction;
    private void OnEnable()
    {
        onEnableAction?.Invoke();
    }

    private void Start()
    {
        if (gameObject.activeInHierarchy)
        {
            onEnableAction?.Invoke();
        }
    }

    
}
