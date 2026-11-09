using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RespawnUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator deathAnimator;
    [SerializeField] private CountNumberAnimator countAnimator;
    [SerializeField] private Button respawnButton;
    [SerializeField] private Animator screenFadeAnimator;

    [Header("Animation Triggers")]
    [SerializeField] private string deathNoRespawnTrigger = "DeathNoRespawn";
    [SerializeField] private string deathWaitRespawnTrigger = "DeathWaitRespawn";
    [SerializeField] private string deathWaitRemoveTrigger = "DeathWaitRemove";
    [SerializeField] private string fadeInTrigger = "FadeIn";
    [SerializeField] private string fadeOutTrigger = "FadeOut";

    [Header("Timing")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    private NetworkPlayerSpawnHandler localSpawnHandler;
    private DisablePlayer localDisablePlayer;
    private int fadeMovementRestriction;
    private Coroutine fadeOutCoroutine;
    private int currentCountdownValue = -1;

    public static RespawnUI FindForScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            RespawnUI ui = root.GetComponentInChildren<RespawnUI>(true);

            if (ui != null)
                return ui;
        }

        return null;
    }

    private void OnEnable()
    {
        if (respawnButton != null)
        {
            respawnButton.onClick.AddListener(RequestRespawn);
            SetRespawnButtonVisible(false);
        }
    }

    private void OnDisable()
    {
        if (respawnButton != null)
            respawnButton.onClick.RemoveListener(RequestRespawn);

        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }

        ReleaseFadeMovementRestriction();
    }

    public void ShowDeathState(
        bool canRespawn,
        bool shouldAutoRespawn,
        float delay,
        NetworkPlayerSpawnHandler spawnHandler,
        DisablePlayer disablePlayer)
    {
        localSpawnHandler = spawnHandler;
        localDisablePlayer = disablePlayer;

        if (canRespawn)
        {
            currentCountdownValue = -1;
            DeathWaitRespawn();

            bool showButton = !shouldAutoRespawn;
            SetRespawnButtonVisible(showButton);

            if (shouldAutoRespawn && delay > 0f)
                UpdateCountdown(Mathf.CeilToInt(delay));
        }
        else
        {
            DeathNoRespawn();
            SetRespawnButtonVisible(false);
        }
    }

    public void DeathNoRespawn()
    {
        if (deathAnimator != null && !string.IsNullOrEmpty(deathNoRespawnTrigger))
            deathAnimator.SetTrigger(deathNoRespawnTrigger);
    }

    public void DeathWaitRespawn()
    {
        if (deathAnimator != null && !string.IsNullOrEmpty(deathWaitRespawnTrigger))
            deathAnimator.SetTrigger(deathWaitRespawnTrigger);
    }

    public void UpdateCountdown(int seconds)
    {
        if (countAnimator == null || seconds == currentCountdownValue)
            return;

        currentCountdownValue = seconds;
        countAnimator.AnimateNumber(seconds);
    }

    public void BeginRespawnFadeIn(DisablePlayer disablePlayer)
    {
        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }

        localDisablePlayer = disablePlayer;

        if (localDisablePlayer == null)
        {
            Debug.LogError(
                "RespawnUI cannot keep the player still during the fade: " +
                "the player is missing DisablePlayer."
            );
        }
        else if (fadeMovementRestriction == 0)
        {
            fadeMovementRestriction =
                localDisablePlayer.AddRestriction(PlayerControlChannels.Movement);
        }

        if (screenFadeAnimator == null)
        {
            Debug.LogError("RespawnUI requires a screen fade Animator.");
        }

        if (screenFadeAnimator != null && !string.IsNullOrEmpty(fadeInTrigger))
            screenFadeAnimator.SetTrigger(fadeInTrigger);
    }

    public void BeginRespawnFadeOut()
    {
        if (deathAnimator != null && !string.IsNullOrEmpty(deathWaitRemoveTrigger))
            deathAnimator.SetTrigger(deathWaitRemoveTrigger);
            
        if (screenFadeAnimator != null && !string.IsNullOrEmpty(fadeOutTrigger))
            screenFadeAnimator.SetTrigger(fadeOutTrigger);

        if (fadeOutCoroutine != null)
            StopCoroutine(fadeOutCoroutine);

        fadeOutCoroutine = StartCoroutine(ReleaseMovementAfterFade());
    }

    private IEnumerator ReleaseMovementAfterFade()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, fadeOutDuration));

        ReleaseFadeMovementRestriction();
        fadeOutCoroutine = null;
    }

    private void RequestRespawn()
    {
        if (localSpawnHandler == null || !localSpawnHandler.IsSpawned)
            return;

        if (respawnButton != null)
            respawnButton.interactable = false;

        localSpawnHandler.RequestRespawnRpc();
    }

    private void SetRespawnButtonVisible(bool visible)
    {
        if (respawnButton == null)
            return;

        respawnButton.gameObject.SetActive(visible);
        respawnButton.interactable = visible;
    }

    private void ReleaseFadeMovementRestriction()
    {
        if (fadeMovementRestriction == 0)
            return;

        if (localDisablePlayer != null)
            localDisablePlayer.RemoveRestriction(fadeMovementRestriction);

        fadeMovementRestriction = 0;
    }
}
