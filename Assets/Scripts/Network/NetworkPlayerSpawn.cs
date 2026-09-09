using UnityEngine;

public class NetworkPlayerSpawn : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnRadius = 5f;


    public Vector3 PlayerSpawnPoint()
    {
        Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = spawnPoint.position;
        spawnPosition += new Vector3(randomOffset.x, 0f, randomOffset.y);

        return spawnPosition;
    }
}
