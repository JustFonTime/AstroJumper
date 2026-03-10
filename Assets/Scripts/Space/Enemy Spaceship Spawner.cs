using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

[Obsolete("EnemySpaceshipSpawner is legacy. Use FleetSpawner for team-agnostic spawning.", false)]
public class EnemySpaceshipSpawner : MonoBehaviour
{
    public static EnemySpaceshipSpawner Instance { get; private set; }

    public event Action<int> OnAliveEnemiesChanged;
    public event Action<int> OnWaveChanged;
    public event Action AllWavesCompleted;
    public event Action<ReinforcementRequest> OnReinforcementRequested;

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

    [Header("Auto Formation")]
    [SerializeField] private bool autoAssignFormationSquads = true;
    [SerializeField] private int maxShipsPerAutoSquad = 10;
    [SerializeField] private EnemySquadState autoSquadInitialState = EnemySquadState.Engage;
    [SerializeField] private EnemySquadFormationType autoSquadFormationType = EnemySquadFormationType.Vee;
    [SerializeField] private float autoSquadSpacing = 5f;
    [SerializeField] private float autoSquadEngageDistance = 18f;
    [SerializeField] private float autoSquadAnchorMoveSpeed = 14f;

    [Header("Reinforcement Requests")]
    [SerializeField] private bool autoFulfillReinforcementRequests = false;
    [SerializeField] private int maxPendingReinforcementRequests = 32;

    [Header("Pooling")] [SerializeField] private int defaultPoolCapacity = 50;
    [SerializeField] private int maxPoolSize = 200;

    [Header("Hierarchy Parents ")] [SerializeField]
    private Transform activeEnemiesRoot;

    [SerializeField] private Transform pooledRoot;

    private GameObject player;
    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    private readonly Dictionary<int, List<EnemySquadController>> autoSquadsByTeam = new();
    private readonly HashSet<EnemySquadController> registeredReinforcementSquads = new();
    private readonly List<ReinforcementRequest> pendingReinforcementRequests = new(32);

    private int aliveEnemies = 0;
    private int currentWave = 0;

    public int AliveEnemies => aliveEnemies;
    public int CurrentWave => currentWave;
    public int PendingReinforcementRequestCount => pendingReinforcementRequests.Count;
    public IReadOnlyList<ReinforcementRequest> PendingReinforcementRequests => pendingReinforcementRequests;

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

    private void OnDisable()
    {
        UnregisterAllReinforcementSquads();
        pendingReinforcementRequests.Clear();
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
        int minSize = Mathf.Clamp(entry.minSquadSize, 2, 10);
        int maxSize = Mathf.Clamp(entry.maxSquadSize, minSize, 10);
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
        squad.ConfigureReinforcementPolicy(Mathf.Clamp(squadSize, 1, 10), 2f, 8f, true);
        RegisterSquadForReinforcement(squad);

        for (int i = 0; i < squadSize; i++)
        {
            Vector3 memberSpawnPos = squad.GetPreviewSlotWorldPosition(i);
            GameObject go = SpawnSingle(entry.prefab, memberSpawnPos, teamId, assignAutoSquad: false);
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

    private void TryAssignToAutoSquad(GameObject ship, int teamId)
    {
        if (ship == null)
            return;

        EnemySpaceshipAI ai = ship.GetComponent<EnemySpaceshipAI>();
        if (ai == null)
            return;

        EnemySquadMember member = ship.GetComponent<EnemySquadMember>();
        if (member == null)
            member = ship.AddComponent<EnemySquadMember>();

        if (member.Squad != null)
            return;

        EnemySquadController squad = GetOrCreateAutoSquad(teamId);
        if (squad == null)
            return;

        squad.RegisterMember(member, ResolveRole(ship, squad.MemberCount));
    }

    private EnemySquadController GetOrCreateAutoSquad(int teamId)
    {
        if (!autoSquadsByTeam.TryGetValue(teamId, out List<EnemySquadController> squads))
        {
            squads = new List<EnemySquadController>();
            autoSquadsByTeam.Add(teamId, squads);
        }

        int maxMembers = Mathf.Clamp(maxShipsPerAutoSquad, 2, 10);
        for (int i = squads.Count - 1; i >= 0; i--)
        {
            EnemySquadController candidate = squads[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.MemberCount <= 0)
            {
                squads.RemoveAt(i);
                continue;
            }

            RegisterSquadForReinforcement(candidate);
            if (candidate.MemberCount < maxMembers)
            {
                candidate.ConfigureReinforcementPolicy(maxMembers, 2f, 8f, true);
                return candidate;
            }
        }

        GameObject squadObject = new GameObject($"AutoSquad_T{teamId}_{squads.Count + 1}");
        squadObject.transform.SetParent(activeEnemiesRoot, true);
        squadObject.transform.position = player != null ? player.transform.position : Vector3.zero;

        EnemySquadController squad = squadObject.AddComponent<EnemySquadController>();
        squad.Initialize(
            player != null ? player.transform : null,
            autoSquadFormationType,
            autoSquadSpacing,
            autoSquadInitialState,
            autoSquadEngageDistance,
            autoSquadAnchorMoveSpeed);
        squad.ConfigureReinforcementPolicy(maxMembers, 2f, 8f, true);

        RegisterSquadForReinforcement(squad);
        squads.Add(squad);
        return squad;
    }

    private GameObject SpawnSingle(GameObject prefab, Vector3 spawnPosition, int teamId, bool assignAutoSquad = true)
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

        if (assignAutoSquad && autoAssignFormationSquads)
            TryAssignToAutoSquad(go, teamId);

        aliveEnemies++;
        OnAliveEnemiesChanged?.Invoke(aliveEnemies);
        return go;
    }

    public void RequestReinforcements(ReinforcementRequest request)
    {
        if (!request.IsValid)
            return;

        PrunePendingReinforcementRequests();
        EnqueueOrUpdateReinforcementRequest(request);
        OnReinforcementRequested?.Invoke(request);

        if (autoFulfillReinforcementRequests)
        {
            TryFulfillPendingRequestForSquad(request.Squad);
        }
    }

    private void TryFulfillPendingRequestForSquad(EnemySquadController squad)
    {
        if (squad == null || !squad.isActiveAndEnabled)
            return;

        int pendingIndex = FindPendingRequestIndex(squad);
        if (pendingIndex < 0)
            return;

        ReinforcementRequest request = pendingReinforcementRequests[pendingIndex];
        if (!request.IsValid || !squad.IsUnderStrength)
        {
            pendingReinforcementRequests.RemoveAt(pendingIndex);
            return;
        }

        GameObject prefab = GetDefaultReinforcementPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("EnemySpaceshipSpawner: auto-fulfill is enabled but no reinforcement prefab is configured.");
            return;
        }

        int teamId = ResolveReinforcementTeamId(request.TeamId);
        int spawnCount = Mathf.Max(0, request.MissingCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 spawnOffset = Random.insideUnitCircle * Mathf.Max(1f, autoSquadSpacing * 0.6f);
            Vector3 spawnPos = request.RallyPoint + spawnOffset;

            GameObject ship = SpawnSingle(prefab, spawnPos, teamId, assignAutoSquad: false);
            if (ship == null)
                continue;

            EnemySquadMember member = ship.GetComponent<EnemySquadMember>();
            if (member == null)
                member = ship.AddComponent<EnemySquadMember>();

            squad.RegisterMember(member, ResolveRole(ship, squad.MemberCount));
        }

        if (!squad.IsUnderStrength)
        {
            pendingReinforcementRequests.RemoveAt(pendingIndex);
            return;
        }

        ReinforcementRequest refreshedRequest = new ReinforcementRequest(
            squad,
            teamId,
            squad.DesiredMemberCount,
            squad.MemberCount,
            squad.FocusTarget,
            request.RallyPoint,
            Time.time);

        if (refreshedRequest.IsValid)
            pendingReinforcementRequests[pendingIndex] = refreshedRequest;
        else
            pendingReinforcementRequests.RemoveAt(pendingIndex);
    }

    private int ResolveReinforcementTeamId(int requestedTeamId)
    {
        if (requestedTeamId != 0)
            return requestedTeamId;

        return fallbackEnemyTeamId != 0 ? fallbackEnemyTeamId : 1;
    }

    private GameObject GetDefaultReinforcementPrefab()
    {
        if (spawnerSettings != null && spawnerSettings.enemySpaceshipPrefab != null)
            return spawnerSettings.enemySpaceshipPrefab;

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            WaveSpawnSettings wave = waves[waveIndex];
            if (wave == null || wave.enemies == null)
                continue;

            for (int entryIndex = 0; entryIndex < wave.enemies.Count; entryIndex++)
            {
                WaveEnemyEntry entry = wave.enemies[entryIndex];
                if (entry != null && entry.prefab != null)
                    return entry.prefab;
            }
        }

        return null;
    }

    private void RegisterSquadForReinforcement(EnemySquadController squad)
    {
        if (squad == null)
            return;

        if (!registeredReinforcementSquads.Add(squad))
            return;

        squad.ReinforcementRequested += OnSquadReinforcementRequested;
    }

    private void OnSquadReinforcementRequested(ReinforcementRequest request)
    {
        RequestReinforcements(request);
    }

    private void EnqueueOrUpdateReinforcementRequest(ReinforcementRequest request)
    {
        int existingIndex = FindPendingRequestIndex(request.Squad);
        if (existingIndex >= 0)
        {
            pendingReinforcementRequests[existingIndex] = request;
            return;
        }

        int maxQueue = Mathf.Max(1, maxPendingReinforcementRequests);
        while (pendingReinforcementRequests.Count >= maxQueue)
            pendingReinforcementRequests.RemoveAt(0);

        pendingReinforcementRequests.Add(request);
    }

    private int FindPendingRequestIndex(EnemySquadController squad)
    {
        for (int i = 0; i < pendingReinforcementRequests.Count; i++)
        {
            if (pendingReinforcementRequests[i].Squad == squad)
                return i;
        }

        return -1;
    }

    private void PrunePendingReinforcementRequests()
    {
        for (int i = pendingReinforcementRequests.Count - 1; i >= 0; i--)
        {
            ReinforcementRequest request = pendingReinforcementRequests[i];
            EnemySquadController squad = request.Squad;

            if (!request.IsValid || squad == null || !squad.isActiveAndEnabled || !squad.IsUnderStrength)
                pendingReinforcementRequests.RemoveAt(i);
        }
    }

    private void UnregisterAllReinforcementSquads()
    {
        foreach (EnemySquadController squad in registeredReinforcementSquads)
        {
            if (squad != null)
                squad.ReinforcementRequested -= OnSquadReinforcementRequested;
        }

        registeredReinforcementSquads.Clear();
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







