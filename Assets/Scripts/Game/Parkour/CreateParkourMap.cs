using System;
using UnityEngine;

public class CreateParkourMap : MatchPreparationTask
{
    public PlatformPiece startPrefab;
    public PlatformPiece[] platformPrefabs;
    public PlatformPiece goalPrefab;

    public Vector2 forwardRange = new Vector2(0f, 2f);
    public Vector2 upRange = new Vector2(0f, 2f);
    public float maxTurn = 30f;

    public Vector2Int countRange = new Vector2Int(9, 13);

    private bool hasGenerated;

    public override void BeginPreparation(int seed)
    {
        ResetPreparationState();

        if (hasGenerated)
        {
            MarkReady();
            return;
        }

        try
        {
            System.Random random = new System.Random(seed);

            CreateMap(random);

            hasGenerated = true;

            MarkReady();

            Debug.Log($"Parkour map generated with seed: {seed}");
        }
        catch (Exception e)
        {
            MarkFailed(e.Message);
        }
    }

    private void CreateMap(System.Random random)
    {
        if (startPrefab == null || goalPrefab == null)
            throw new Exception("Start or goal prefab is missing.");

        if (platformPrefabs == null || platformPrefabs.Length == 0)
            throw new Exception("No platform prefabs assigned.");

        PlatformPiece current =
            Instantiate(startPrefab, transform.position, transform.rotation);

        // Checkpoint numbering starts at 1.
        int checkpointOrder = 1;

        AssignCheckpointOrder(current, checkpointOrder++);

        int minCount = Mathf.Min(countRange.x, countRange.y);
        int maxCount = Mathf.Max(countRange.x, countRange.y);

        int count = random.Next(minCount, maxCount + 1);

        int last = -1;

        for (int i = 0; i < count; i++)
        {
            int index;

            do
            {
                index = random.Next(0, platformPrefabs.Length);
            }
            while (index == last && platformPrefabs.Length > 1);

            last = index;

            if (platformPrefabs[index] == null)
                throw new Exception($"Platform prefab at index {index} is missing.");

            current = SpawnNext(
                current,
                platformPrefabs[index],
                random,
                checkpointOrder++
            );
        }

        // The goal also receives a checkpoint order.
        SpawnNext(current, goalPrefab, random, checkpointOrder);
    }

    private PlatformPiece SpawnNext(
        PlatformPiece from,
        PlatformPiece prefab,
        System.Random random,
        int checkpointOrder)
    {
        if (from.exitPoints == null || from.exitPoints.Length == 0)
            throw new Exception("A platform has no exit points.");

        Transform exit =
            from.exitPoints[random.Next(0, from.exitPoints.Length)];

        float turn = Range(random, -maxTurn, maxTurn);

        Quaternion rotation =
            exit.rotation * Quaternion.Euler(0f, turn, 0f);

        Vector3 offset = new Vector3(
            0f,
            Range(random, upRange.x, upRange.y),
            Range(random, forwardRange.x, forwardRange.y)
        );

        Vector3 startPosition =
            exit.position + exit.rotation * offset;

        PlatformPiece next =
            Instantiate(prefab, startPosition, rotation);

        if (next.startPoint == null)
        {
            Destroy(next.gameObject);

            throw new Exception(
                $"Platform prefab {prefab.name} has no startPoint assigned."
            );
        }

        next.transform.position +=
            startPosition - next.startPoint.position;

        AssignCheckpointOrder(next, checkpointOrder);

        return next;
    }

    private void AssignCheckpointOrder(PlatformPiece piece, int order)
    {
        CheckpointTrigger checkpoint = piece.CheckpointTrigger;

        if (checkpoint == null)
        {
            Destroy(piece.gameObject);

            throw new Exception(
                $"Platform prefab {piece.name} has no CheckpointTrigger child."
            );
        }

        checkpoint.SetCheckpointOrder(order);
    }

    private static float Range(
        System.Random random,
        float min,
        float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}