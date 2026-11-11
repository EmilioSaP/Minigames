using UnityEngine;

public class PlatformPiece : MonoBehaviour
{
    public Transform startPoint;
    public Transform[] exitPoints; // At least one

    [SerializeField] private CheckpointTrigger checkpointTrigger;

    public CheckpointTrigger CheckpointTrigger
    {
        get
        {
            if (checkpointTrigger == null)
                checkpointTrigger = GetComponentInChildren<CheckpointTrigger>(true);

            return checkpointTrigger;
        }
    }

    private void OnValidate()
    {
        if (checkpointTrigger == null)
            checkpointTrigger = GetComponentInChildren<CheckpointTrigger>(true);
    }
}