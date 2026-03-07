using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class EnemySpaceshipSpawner : MonoBehaviour
{
    public static EnemySpaceshipSpawner Instance { get; private set; }

    public event Action<int> OnAliveEnemiesChanged;
    public event Action<int> OnWaveChanged;
    public event Action AllWavesCompleted;

    public enum SpawnType
    {
        Infinite,
        SetWaves
    }

    [SerializeField]
    private float initialSpawnDelay = 5f;

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

    [Header("Fallback Spawn Area")] [SerializeField] private Vector2 fallbackMinSpawnAreaSize = new Vector2(40f, 40f);
    [SerializeField] private Vector2 fallbackMaxSpawnAreaSize = new Vector2(80f, 80f);

    [Header("Pooling")] [SerializeField] private int defaultPoolCapacity = 50;
    [SerializeField] private int maxPoolSize = 200;

    [Header("Hierarchy Parents ")] [SerializeField]
    private Transform activeEnemiesRoot;

    [SerializeField] private Transform pooledRoot;

    private GameObject player;
    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    private int aliveEnemies = 0;
    private int currentWave = 0;

    public int AliveEnemies => aliveEnemies;
    public int CurrentWave => currentWave;

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

        PrewarmConfiguredPrefabs();
        StartCoroutine(RunSpawner());
    }

    private void PrewarmConfiguredPrefabs()
    {
        HashSet<GameObject> prefabs = new HashSet<GameObject>();

        if (spawnerSettings != null && spawnerSettings.enemySpaceshipPrefab != null)
            prefabs.Add(spawnerSettings.enemySpaceshipPrefab);

        foreach (var wave in waves)
        {
            if (wave == null || wave.enemies == null)
                continue;

            foreach (var entry in wave.enemies)
            {
                if (entry != null && entry.prefab != null)
                    prefabs.Add(entry.prefab);
            }
        }

        foreach (var prefab in prefabs)
            Prewarm(prefab, Mathf.Max(0, defaultPoolCapacity));
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
        yield return new WaitForSeconds(initialSpawnDelay);

        if (spawnType == SpawnType.Infinite)
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnerSettings.spawnInterval);
                for (int i = 0; i < spawnerSettings.enemiesPerSpawn; i++)
                    SpawnSingle(spawnerSettings.enemySpaceshipPrefab, GetRandomSpawnPosition(), PickEnemyTeamId());
            }
        }
        else
        {
            for (int w = 0; w < waves.Count; w++)
            {
                currentWave++;
                OnWaveChanged?.Invoke(currentWave);

                foreach (var entry in waves[w].enemies)
                {
                    if (entry == null || entry.prefab == null || entry.count <= 0)
                        continue;

                    if (entry.spawnAsSquad)
                    {
                        int remaining = entry.count;
                        while (remaining > 0)
                        {
                            int squadSize = PickSquadSize(entry, remaining);
                            SpawnSquad(entry, squadSize);
                            remaining -= squadSize;

                            if (waves[w].spawnSpacing > 0f)
                                yield return new WaitForSeconds(waves[w].spawnSpacing);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < entry.count; i++)
                        {
                            SpawnSingle(entry.prefab, GetRandomSpawnPosition(), PickEnemyTeamId());
                            if (waves[w].spawnSpacing > 0f)
                                yield return new WaitForSeconds(waves[w].spawnSpacing);
                        }
                    }
                }

                while (aliveEnemies > 0)
                    yield return null;

                yield return new WaitForSeconds(waves[w].timeToSpawnAfterFinalDeath);
            }

            AllWavesCompleted?.Invoke();
        }
    }

    private int PickSquadSize(WaveEnemyEntry entry, int remaining)
    {
        int minSize = Mathf.Clamp(entry.minSquadSize, 2, 5);
        int maxSize = Mathf.Clamp(entry.maxSquadSize, minSize, 5);
        int requested = Random.Range(minSize, maxSize + 1);

        if (remaining >= 2)
            return Mathf.Min(requested, remaining);

        return 1;
    }

    private void SpawnSquad(WaveEnemyEntry entry, int squadSize)
    {
        if (entry == null || entry.prefab == null || squadSize <= 0)
            return;

        Vector3 spawnPosition = GetRandomSpawnPosition();
        int teamId = PickEnemyTeamId();

        GameObject squadObject = new GameObject($"{entry.prefab.name}_Squad");
        squadObject.transform.SetParent(activeEnemiesRoot, true);
        squadObject.transform.position = spawnPosition;

        var squad = squadObject.AddComponent<EnemySquadController>();
        squad.Initialize(
            player != null ? player.transform : null,
            entry.formationType,
            entry.squadSpacing,
            entry.initialSquadState,
            entry.squadEngageDistance,
            entry.squadAnchorMoveSpeed);

        for (int i = 0; i < squadSize; i++)
        {
            Vector3 memberSpawnPos = squad.GetPreviewSlotWorldPosition(i);
            GameObject go = SpawnSingle(entry.prefab, memberSpawnPos, teamId);
            if (go == null)
                continue;

            var member = go.GetComponent<EnemySquadMember>();
            if (member == null)
                member = go.AddComponent<EnemySquadMember>();

            squad.RegisterMember(member, ResolveRole(go, i));
        }
    }

    private EnemySquadRole ResolveRole(GameObject ship, int slotIndex)
    {
        if (ship == null)
            return slotIndex == 0 ? EnemySquadRole.Leader : EnemySquadRole.Wingman;

        if (ship.GetComponent<ShieldGiver>() != null)
            return EnemySquadRole.ShieldSupport;

        if (ship.GetComponent<Kamakazy>() != null)
            return EnemySquadRole.Kamikaze;

        return slotIndex == 0 ? EnemySquadRole.Leader : EnemySquadRole.Wingman;
    }

    private GameObject SpawnSingle(GameObject prefab, Vector3 spawnPosition, int teamId)
    {
        if (prefab == null) return null;

        var pool = GetPool(prefab);
        var go = pool.Get();

        go.transform.SetParent(activeEnemiesRoot, true);
        go.transform.position = spawnPosition;
        go.transform.rotation = Quaternion.identity;

        if (go.TryGetComponent<TeamAgent>(out var agent))
            agent.SetTeam(teamId);

        if (go.TryGetComponent<EnemySpaceshipAI>(out var ai))
            ai.ResetForSpawn(player);

        if (go.TryGetComponent<EnemySpaceshipCombatAI>(out var combat))
            combat.ResetForSpawn();

        aliveEnemies++;
        OnAliveEnemiesChanged?.Invoke(aliveEnemies);
        return go;
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
        Vector2 minSpawnArea = spawnerSettings != null ? spawnerSettings.minSpawnAreaSize : fallbackMinSpawnAreaSize;
        Vector2 maxSpawnArea = spawnerSettings != null ? spawnerSettings.maxSpawnAreaSize : fallbackMaxSpawnAreaSize;
        float spawnAreaWidth = Random.Range(minSpawnArea.x, maxSpawnArea.x);
        float spawnAreaHeight = Random.Range(minSpawnArea.y, maxSpawnArea.y);

        float randomX = Random.Range(-spawnAreaWidth / 2f, spawnAreaWidth / 2f);
        float randomY = Random.Range(-spawnAreaHeight / 2f, spawnAreaHeight / 2f);
        if (player == null)
            return new Vector3(randomX, randomY, 0);

        return new Vector3(
            randomX + player.transform.position.x,
            randomY + player.transform.position.y,
            player.transform.position.z
        );
    }
}




