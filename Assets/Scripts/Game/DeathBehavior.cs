using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathBehavior : MonoBehaviour
{
    [SerializeField] private bool spectateOnDeath;
    [SerializeField] private bool returnToNormalOnRespawn = true;

    public bool SpectateOnDeath => spectateOnDeath;
    public bool ReturnToNormalOnRespawn => returnToNormalOnRespawn;

    public static DeathBehavior FindForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DeathBehavior behavior =
                root.GetComponentInChildren<DeathBehavior>(true);

            if (behavior != null && behavior.isActiveAndEnabled)
                return behavior;
        }

        return null;
    }
}
