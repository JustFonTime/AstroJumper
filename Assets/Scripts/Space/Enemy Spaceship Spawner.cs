using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class EnemySpaceshipSpawner : MonoBehaviour
{
    public static EnemySpaceshipSpawner Instance { get; private set; }

    public event Action<int> OnAliveEnemiesChanged; // Pass the new alive enemies count
    public event Action<int> OnWaveChanged; // Pass the new wave index (starting from 1 for better readability)

    public event Action AllWavesCompleted; // Triggered when all waves are completed (only for SetWaves mode)

    public enum SpawnType
    {
        Infinite,
        SetWaves
    }

    [SerializeField]
    private float
        initialSpawnDelay = 5f; // Time to wait before starting the first spawn, giving player time to get ready

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

    public int AliveEnemies
    {
        get { return aliveEnemies; }
    }

    private int currentWave = 0;

    public int CurrentWave
    {
        get { return currentWave; }
    }

    private void Awake()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple EnemySpaceshipSpawner instances found! Destroying duplicate.");
            Destroy(gameObject);
        }
    }

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
            pooledRoot = go.transform;
        }


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
        //Hard code wait so player has time to get ready and not get instantly overwhelmed by enemies as soon as the scene starts. Can be removed later if needed.
        // and for the hud and anyhitng using events to bind before there called 
        yield return new WaitForSeconds(initialSpawnDelay);


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
            //Go through each wave in order
            for (int w = 0; w < waves.Count; w++)
            {
                //Start the spawn process for this wave
                currentWave++;
                OnWaveChanged?.Invoke(currentWave);

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

                //Wait until all enemies from this wave are dead before starting the next one
                while (aliveEnemies > 0)
                    yield return null;

                yield return new WaitForSeconds(waves[w].timeToSpawnAfterFinalDeath);
            }

            //All waves completed
            AllWavesCompleted?.Invoke();
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
        OnAliveEnemiesChanged?.Invoke(aliveEnemies);
    }

    public void NotifyEnemyGone()
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
        OnAliveEnemiesChanged?.Invoke(aliveEnemies);
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