using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class EnemySpaceshipSpawner : MonoBehaviour
{
    public enum SpawnType
    {
        Infinite,
        SetWaves
    }

    [Header("Mode")] [SerializeField] private SpawnType spawnType = SpawnType.Infinite;

    [Header("Infinite Settings")] [SerializeField]
    private EnemySpaceshipSpawnerSettingsSO spawnerSettings;

    [Header("Wave Settings")] [SerializeField]
    private List<WaveSpawnSettings> waves = new List<WaveSpawnSettings>();

    [Header("Teams (any number)")] [SerializeField]
    private bool assignTeams = true;

    [Tooltip("Enemies will be randomly assigned one of these team IDs. Example: [1,2]. Player should be team 0.")]
    [SerializeField]
    private List<int> enemyTeamIds = new List<int> { 1, 2 };

    [SerializeField] private int fallbackEnemyTeamId = 1;

    [Header("Pooling")] [SerializeField] private int defaultPoolCapacity = 50;
    [SerializeField] private int maxPoolSize = 200;

    [Header("Hierarchy Parents ")] [SerializeField]
    private Transform activeEnemiesRoot;

    [SerializeField] private Transform pooledRoot;

    private GameObject player;

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    private int aliveEnemies = 0;

    private void Start()
    {
        player = GameObject.FindWithTag("Player");

        if (activeEnemiesRoot == null)
        {
            var go = new GameObject("Enemies_Active");
            activeEnemiesRoot = go.transform;
        }

        if (pooledRoot == null)
        {
            var go = new GameObject("Enemies_Pooled");
            pooledRoot = go.transform; // IMPORTANT: set BEFORE prewarm/pool creation
        }

        // prewarm the actual enemy prefab pool (not the pooledRoot object)
        if (spawnerSettings != null && spawnerSettings.enemySpaceshipPrefab != null)
            Prewarm(spawnerSettings.enemySpaceshipPrefab, Mathf.Max(0, defaultPoolCapacity));

        StartCoroutine(RunSpawner());
    }

    private int PickEnemyTeamId()
    {
        if (!assignTeams) return fallbackEnemyTeamId;
        if (enemyTeamIds == null || enemyTeamIds.Count == 0) return fallbackEnemyTeamId;
        return enemyTeamIds[Random.Range(0, enemyTeamIds.Count)];
    }

    private void Prewarm(GameObject prefab, int count)
    {
        var pool = GetPool(prefab);
        for (int i = 0; i < count; i++)
        {
            var go = pool.Get();
            pool.Release(go);
        }
    }

    private IEnumerator RunSpawner()
    {
        if (spawnType == SpawnType.Infinite)
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnerSettings.spawnInterval);
                for (int i = 0; i < spawnerSettings.enemiesPerSpawn; i++)
                    Spawn(spawnerSettings.enemySpaceshipPrefab);
            }
        }
        else // SetWaves
        {
            for (int w = 0; w < waves.Count; w++)
            {
                foreach (var entry in waves[w].enemies)
                {
                    if (entry.prefab == null || entry.count <= 0) continue;

                    for (int i = 0; i < entry.count; i++)
                    {
                        Spawn(entry.prefab);
                        if (waves[w].spawnSpacing > 0f)
                            yield return new WaitForSeconds(waves[w].spawnSpacing);
                    }
                }

                while (aliveEnemies > 0)
                    yield return null;

                yield return new WaitForSeconds(waves[w].timeToSpawnAfterFinalDeath);
            }
        }
    }

    private void Spawn(GameObject prefab)
    {
        if (prefab == null) return;

        Vector3 spawnPosition = GetRandomSpawnPosition();
        var pool = GetPool(prefab);
        var go = pool.Get();

        go.transform.SetParent(activeEnemiesRoot, true);
        go.transform.position = spawnPosition;
        go.transform.rotation = Quaternion.identity;

        // Assign a team (so enemies can fight each other)
        if (go.TryGetComponent<TeamAgent>(out var agent))
            agent.SetTeam(PickEnemyTeamId());

        // Re-init AI every spawn (pool-safe)
        if (go.TryGetComponent<EnemySpaceshipAI>(out var ai))
            ai.ResetForSpawn(player);

        if (go.TryGetComponent<EnemySpaceshipCombatAI>(out var combat))
            combat.ResetForSpawn();

        aliveEnemies++;
    }

    public void NotifyEnemyGone()
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
    }

    private ObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out var existing))
            return existing;

        ObjectPool<GameObject> pool = null;

        pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                var go = Instantiate(prefab, pooledRoot);
                go.SetActive(false);

                var pe = go.GetComponent<PooledEnemy>();
                if (pe == null) pe = go.AddComponent<PooledEnemy>();
                pe.Init(this, pool);

                return go;
            },
            actionOnGet: go =>
            {
                go.SetActive(true);

                if (go.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            },
            actionOnRelease: go =>
            {
                go.SetActive(false);
                go.transform.SetParent(pooledRoot, true);
            },
            actionOnDestroy: go => Destroy(go),
            collectionCheck: false,
            defaultCapacity: defaultPoolCapacity,
            maxSize: maxPoolSize
        );

        pools[prefab] = pool;
        return pool;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float spawnAreaWidth = Random.Range(spawnerSettings.minSpawnAreaSize.x, spawnerSettings.maxSpawnAreaSize.x);
        float spawnAreaHeight = Random.Range(spawnerSettings.minSpawnAreaSize.y, spawnerSettings.maxSpawnAreaSize.y);

        float randomX = Random.Range(-spawnAreaWidth / 2f, spawnAreaWidth / 2f);
        float randomY = Random.Range(-spawnAreaHeight / 2f, spawnAreaHeight / 2f);
        if (player == null)
        {
            return new Vector3(randomX, randomY, 0);
        }

        return new Vector3(
            randomX + player.transform.position.x,
            randomY + player.transform.position.y,
            player.transform.position.z
        );
    }
}