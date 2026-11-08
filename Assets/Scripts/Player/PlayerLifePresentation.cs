
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLifePresentation : NetworkBehaviour
{
    [Header("Visuals")]
    [SerializeField] private GameObject[] playerVisuals;
    [SerializeField] private ParticleSystem deathBurst;
    [SerializeField] private NetworkPlayerAudio networkPlayerAudio;
    private PlayerMovement playerMovement;

    private PlayerHealth playerHealth;
    private DisablePlayer disablePlayer;

    private int deathRestrictionHandle;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerHealth = GetComponent<PlayerHealth>();
        disablePlayer = GetComponent<DisablePlayer>();
        playerMovement = GetComponent<PlayerMovement>();

        if (playerHealth == null)
        {
            Debug.LogError("PlayerLifePresentation requires PlayerHealth.");
            return;
        }

        playerHealth.IsDead.OnValueChanged += OnDeathStateChanged;

        // Apply current state in case this player is already dead
        // when another client starts observing it.
        ApplyDeadState(playerHealth.IsDead.Value, false);
    }

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
        {
            playerHealth.IsDead.OnValueChanged -= OnDeathStateChanged;
        }

        ReleaseDeathRestriction();
        playerMovement?.SetMotionFrozen(false);

        base.OnNetworkDespawn();
    }

    private void OnDeathStateChanged(bool previous, bool current)
    {
        ApplyDeadState(current, current);
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        if (!IsOwner)
            return;

        ReleaseDeathRestriction();
        playerMovement?.SetMotionFrozen(false);
    }

    private void ApplyDeadState(bool isDead, bool playEffect)
    {
        // Visual presentation happens on every observing client.
        if (isDead)
        {
            networkPlayerAudio.PlayFromOwner(PlayerSoundEvent.Explode);
        }
        else
        {
            networkPlayerAudio.PlayFromOwner(PlayerSoundEvent.Spawn);
        }

        if (playerVisuals != null)
        {
            foreach (var visual in playerVisuals)
            {
                if (visual != null)
                    visual.SetActive(!isDead);
            }
        }

        if (isDead && playEffect && deathBurst != null)
        {
            deathBurst.Play(true);
        }

        // Only the owning player needs their local controls restricted.
        if (!IsOwner || disablePlayer == null)
            return;

        // Stop all momentum and gravity when dead.
        if (playerMovement != null)
            playerMovement.SetMotionFrozen(isDead);

        if (disablePlayer == null)
            return;

        if (isDead)
        {
            if (deathRestrictionHandle == 0)
            {
                deathRestrictionHandle =
                    disablePlayer.AddRestriction(PlayerControlChannels.All);
            }
        }
        else
        {
            ReleaseDeathRestriction();
        }
    }

    private void ReleaseDeathRestriction()
    {
        if (deathRestrictionHandle == 0)
            return;

        if (disablePlayer != null)
        {
            disablePlayer.RemoveRestriction(deathRestrictionHandle);
        }

        deathRestrictionHandle = 0;
    }
}
