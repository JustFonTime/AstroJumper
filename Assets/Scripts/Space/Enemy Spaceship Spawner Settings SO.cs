using UnityEngine;


[CreateAssetMenu(fileName = "EnemySpaceshipSpawnerSettingsSO",
    menuName = "Scriptable Objects/EnemySpaceshipSpawnerSettingsSO")]
public class EnemySpaceshipSpawnerSettingsSO : ScriptableObject
{
    [Header("Spawn Settings")]
    public GameObject enemySpaceshipPrefab;
    public float spawnInterval = 5f;
    public int enemiesPerSpawn = 1;
    public Vector2 minSpawnAreaSize = new Vector2(50f, 50f);
    public Vector2 maxSpawnAreaSize = new Vector2(100f, 100f);
}