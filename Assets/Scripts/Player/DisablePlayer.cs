
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DisablePlayer : MonoBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerTurning playerTurning;
    [SerializeField] private PickupController pickupController;
    [SerializeField] private CameraViewToggle cameraViewToggle;

    // Each restriction gets a unique handle.
    private readonly Dictionary<int, PlayerControlChannels> restrictions =
        new Dictionary<int, PlayerControlChannels>();

    private readonly HashSet<int> sceneScopedRestrictionHandles = new();

    private int nextRestrictionHandle = 1;

    // Compatibility handles for existing systems.
    private int pauseRestrictionHandle;
    private int spawnRestrictionHandle;

    private void Awake()
    {
        ApplyRestrictions();
    }

    // --------------------------------------------------
    // GENERAL RESTRICTION API
    // --------------------------------------------------

    public int AddRestriction(PlayerControlChannels channels)
    {
        if (channels == PlayerControlChannels.None)
            return 0;

        int handle = nextRestrictionHandle++;

        restrictions.Add(handle, channels);

        ApplyRestrictions();

        return handle;
    }

    public int AddSceneScopedRestriction(PlayerControlChannels channels)
    {
        int handle = AddRestriction(channels);
        if (handle == 0)
            return 0;

        sceneScopedRestrictionHandles.Add(handle);
        if (sceneScopedRestrictionHandles.Count == 1)
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

        return handle;
    }

    public bool RemoveRestriction(int handle)
    {
        if (handle == 0)
            return false;

        bool removed = restrictions.Remove(handle);

        if (removed)
        {
            sceneScopedRestrictionHandles.Remove(handle);
            if (sceneScopedRestrictionHandles.Count == 0)
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            ApplyRestrictions();
        }

        return removed;
    }

    public bool IsRestricted(PlayerControlChannels channels)
    {
        PlayerControlChannels blocked = GetBlockedChannels();

        return (blocked & channels) != PlayerControlChannels.None;
    }

    // --------------------------------------------------
    // EXISTING PAUSE SYSTEM
    // --------------------------------------------------

    // state = true means controls are allowed by the pause system.
    public void SetPlayerEnabled(bool state)
    {
        if (!state)
        {
            if (pauseRestrictionHandle == 0)
            {
                pauseRestrictionHandle =
                    AddRestriction(PlayerControlChannels.All);
            }
        }
        else
        {
            RemoveRestriction(pauseRestrictionHandle);
            pauseRestrictionHandle = 0;
        }
    }

    // --------------------------------------------------
    // SPAWN RESTRICTION
    // --------------------------------------------------

    public void SpawnRestrict(bool restrict)
    {
        if (restrict)
        {
            if (spawnRestrictionHandle == 0)
            {
                spawnRestrictionHandle =
                    AddRestriction(PlayerControlChannels.Movement);
            }
        }
        else
        {
            RemoveRestriction(spawnRestrictionHandle);
            spawnRestrictionHandle = 0;
        }
    }

    // --------------------------------------------------
    // INTERNAL CONTROL EVALUATION
    // --------------------------------------------------

    private PlayerControlChannels GetBlockedChannels()
    {
        PlayerControlChannels blocked = PlayerControlChannels.None;

        foreach (PlayerControlChannels restriction in restrictions.Values)
        {
            blocked |= restriction;
        }

        return blocked;
    }

    private void ApplyRestrictions()
    {
        PlayerControlChannels blocked = GetBlockedChannels();

        bool movementAllowed =
            (blocked & PlayerControlChannels.Movement) == 0;

        bool lookAllowed =
            (blocked & PlayerControlChannels.Look) == 0;

        bool interactionAllowed =
            (blocked & PlayerControlChannels.Interaction) == 0;

        bool cameraToggleAllowed =
            (blocked & PlayerControlChannels.CameraToggle) == 0;

        if (playerMovement != null)
            playerMovement.SetMovementEnabled(movementAllowed);

        if (playerTurning != null)
            playerTurning.SetLookMode(lookAllowed);

        if (pickupController != null)
            pickupController.SetInteractionEnabled(interactionAllowed);

        if (cameraViewToggle != null)
            cameraViewToggle.SetCameraToggable(cameraToggleAllowed);
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        foreach (int handle in new List<int>(sceneScopedRestrictionHandles))
            RemoveRestriction(handle);
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
}
