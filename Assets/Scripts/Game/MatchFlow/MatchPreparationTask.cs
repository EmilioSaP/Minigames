
using UnityEngine;

public abstract class MatchPreparationTask : MonoBehaviour
{
    public bool IsReady { get; private set; }
    public bool HasFailed { get; private set; }
    public string FailureReason { get; private set; }

    public string TaskName => name;

    // Called by MatchFlowManager on every client.
    // The same server-generated seed is passed to every client.
    public abstract void BeginPreparation(int seed);

    protected void ResetPreparationState()
    {
        IsReady = false;
        HasFailed = false;
        FailureReason = null;
    }

    protected void MarkReady()
    {
        IsReady = true;
        HasFailed = false;
    }

    protected void MarkFailed(string reason)
    {
        IsReady = false;
        HasFailed = true;
        FailureReason = reason;

        Debug.LogError($"Preparation failed ({TaskName}): {reason}");
    }
}

public interface IMatchSpawnReadiness
{
    bool IsLocalPlayerSpawned { get; }
}
