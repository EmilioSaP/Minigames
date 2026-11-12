using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerReadyForMatchTag : MonoBehaviour
{
    [SerializeField] private TMP_Text readyStatusText;
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private Transform localCamera;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ReadyForMatchController.InstanceAvailable += OnControllerAvailable;
        OnControllerAvailable(ReadyForMatchController.Instance);
        SetVisible(SceneManager.GetActiveScene().name == lobbySceneName);
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        ReadyForMatchController.InstanceAvailable -= OnControllerAvailable;

        if (ReadyForMatchController.Instance != null)
            ReadyForMatchController.Instance.UnregisterPlayerReadyTag(this);
    }

    private void Start()
    {
        Camera cam = Camera.main;

        if (cam != null)
            localCamera = cam.transform;
    }

    private void LateUpdate()
    {
        if (localCamera == null)
            return;

        Vector3 direction = localCamera.position - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(-direction);
        }
    }

    public void SetReadyTag()
    {
        if (readyStatusText != null)
            readyStatusText.text = "[Ready]";
    }

    public void SetNotReadyTag()
    {
        if (readyStatusText != null)
            readyStatusText.text = "[Not Ready]";
    }

    public void SetVisible(bool visible)
    {
        if (readyStatusText != null)
            readyStatusText.enabled = visible;
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        bool isLobby = newScene.name == lobbySceneName;
        SetVisible(isLobby);

        if (isLobby)
            SetNotReadyTag();
    }

    private void OnControllerAvailable(
        ReadyForMatchController controller)
    {
        if (controller != null)
            controller.RegisterPlayerReadyTag(this);
    }
}