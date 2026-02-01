using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemySpaceshipSpawner : MonoBehaviour
{
    [SerializeField] private EnemySpaceshipSpawnerSettingsSO spawnerSettings;
    private GameObject player;

    private void Start()
    {
        player = GameObject.FindWithTag("Player");
        StartCoroutine(SpawnEnemiesTimer());
    }

    IEnumerator SpawnEnemiesTimer()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnerSettings.spawnInterval);
            SpawnEnemies();
        }
    }

    private void SpawnEnemies()
    {
        for (int i = 0; i < spawnerSettings.enemiesPerSpawn; i++)
        {
            Vector3 spawnPosition = GetRandomSpawnPosition();
            Instantiate(spawnerSettings.enemySpaceshipPrefab, spawnPosition, Quaternion.identity);
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float spawnAreaWidth = Random.Range(spawnerSettings.minSpawnAreaSize.x, spawnerSettings.maxSpawnAreaSize.x);
        float spawnAreaHeight = Random.Range(spawnerSettings.minSpawnAreaSize.y, spawnerSettings.maxSpawnAreaSize.y);

        float randomX = Random.Range(-spawnAreaWidth / 2, spawnAreaWidth / 2);
        float randomZ = Random.Range(-spawnAreaHeight / 2, spawnAreaHeight / 2);

        Vector3 spawnPosition = new Vector3(randomX, 0, randomZ) + player.transform.position;
        return spawnPosition;
    }
}