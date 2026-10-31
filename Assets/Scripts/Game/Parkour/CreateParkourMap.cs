using UnityEngine;

public class CreateParkourMap : MonoBehaviour
{
    public PlatformPiece startPrefab;
    public PlatformPiece[] platformPrefabs;
    public PlatformPiece goalPrefab;

    public Vector2 forwardRange = new Vector2(0f, 2f);
    public Vector2 upRange = new Vector2(0f, 2f);
    public float maxTurn = 30f;
    public Vector2Int countRange = new Vector2Int(9, 13); // inclusive

    void Start()
    {
        CreateMap();
    }
    public void CreateMap()
    {
        var current = Instantiate(startPrefab, transform.position, transform.rotation);
        int count = Random.Range(countRange.x, countRange.y + 1);
        int last = -1;

        for (int i = 0; i < count; i++)
        {
            // Pick a different prefab than last time
            int idx;
            do { idx = Random.Range(0, platformPrefabs.Length); }
            while (idx == last && platformPrefabs.Length > 1);
            last = idx;

            current = SpawnNext(current, platformPrefabs[idx]);
        }

        SpawnNext(current, goalPrefab);
    }

    PlatformPiece SpawnNext(PlatformPiece from, PlatformPiece prefab)
    {
        Transform exit = from.exitPoints[Random.Range(0, from.exitPoints.Length)];

        Quaternion rot = exit.rotation * Quaternion.Euler(0f, Random.Range(-maxTurn, maxTurn), 0f);
        Vector3 offset = new Vector3(0f, Random.Range(upRange.x, upRange.y),
                                     Random.Range(forwardRange.x, forwardRange.y));
        Vector3 pos = exit.position + exit.rotation * offset;

        return Instantiate(prefab, pos, rot);
    }
}
