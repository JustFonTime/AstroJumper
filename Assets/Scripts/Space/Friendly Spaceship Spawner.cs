using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

[Obsolete("FriendlySpaceshipSpawner is legacy. Use FleetSpawner for team-agnostic spawning.", false)]
public class FriendlySpaceshipSpawner : MonoBehaviour
{
    public static FriendlySpaceshipSpawner Instance { get; private set; }

    public event Action<int> OnAliveFriendliesChanged;
    public event Action<ReinforcementRequest> OnReinforcementRequested;

    [SerializeField] private GameObject[] teamPrefabs;
    [SerializeField] private int[] teamSpawnWeights;

    private GameObject player;
    private int playerTeamId;
    private int aliveFriendlies;

    [SerializeField] private float minSpawnRadius = 15f;
    [SerializeField] private float maxSpawnRadius = 30f;
    [SerializeField] private float initialSpawnDelay = 5f;

    [Header("Pooling")]
    [SerializeField] private int defaultPoolCapacity = 30;
    [SerializeField] private int maxPoolSize = 70;

    [Header("Auto Formation")]
    [SerializeField] private bool autoAssignFormationSquad = true;
    [SerializeField] private int maxShipsPerFriendlySquad = 10;
    [SerializeField] private EnemySquadState friendlySquadInitialState = EnemySquadState.FormUp;
    [SerializeField] private EnemySquadFormationType friendlyAutoFormationType = EnemySquadFormationType.Vee;
    [SerializeField] private float friendlySquadSpacing = 5f;
    [SerializeField] private float friendlySquadEngageDistance = 16f;
    [SerializeField] private float friendlySquadAnchorMoveSpeed = 13f;

    [Header("Friendly Reinforcement Requests")]
    [SerializeField] private bool enableFriendlyReinforcementRequests = true;
    [SerializeField] private bool autoFulfillFriendlyReinforcementRequests = false;
    [SerializeField] private int maxPendingFriendlyReinforcementRequests = 32;

    [Header("Hierarchy Parents")]
    [FormerlySerializedAs("activeTeamatesRoot")]
    [SerializeField] private Transform activeTeammatesRoot;
    [SerializeField] private Transform pooledRoot;

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    private readonly List<EnemySquadController> friendlyAutoSquads = new List<EnemySquadController>(8);
    private readonly HashSet<EnemySquadController> registeredReinforcementSquads = new();
    private readonly List<ReinforcementRequest> pendingReinforcementRequests = new(32);

    public int AliveFriendlies => aliveFriendlies;
    public int PendingFriendlyReinforcementRequestCount => pendingReinforcementRequests.Count;
    public IReadOnlyList<ReinforcementRequest> PendingFriendlyReinforcementRequests => pendingReinforcementRequests;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player not found in scene. Friendly spawner disabled.");
            enabled = false;
            return;
        }

        TeamAgent playerTeamAgent = player.GetComponent<TeamAgent>();
        playerTeamId = playerTeamAgent != null ? playerTeamAgent.TeamId : 0;

        if (activeTeammatesRoot == null)
        {
            GameObject go = new GameObject("Teammates_Active");
            activeTeammatesRoot = go.transform;
        }

        if (pooledRoot == null)
        {
            GameObject go = new GameObject("Teammates_Pooled");
            go.SetActive(false);
            pooledRoot = go.transform;
        }

        PrewarmConfiguredPrefabs();
        StartCoroutine(RunSpawner());
    }

    private void OnDisable()
    {
        UnregisterAllFriendlyReinforcementSquads();
        pendingReinforcementRequests.Clear();
    }

    private void PrewarmConfiguredPrefabs()
    {
        if (teamPrefabs == null)
            return;

        for (int i = 0; i < teamPrefabs.Length; i++)
        {
            GameObject prefab = teamPrefabs[i];
            if (prefab != null)
                Prewarm(prefab, Mathf.Max(0, defaultPoolCapacity));
        }
    }

    private IEnumerator RunSpawner()
    {
        yield return new WaitForSeconds(initialSpawnDelay);

        if (teamPrefabs == null || teamPrefabs.Length == 0)
            yield break;

        int weightCount = teamSpawnWeights != null ? teamSpawnWeights.Length : 0;

        for (int i = 0; i < teamPrefabs.Length; i++)
        {
            GameObject prefab = teamPrefabs[i];
            if (prefab == null)
                continue;

            int weight = i < weightCount ? Mathf.Max(0, teamSpawnWeights[i]) : 1;
            for (int j = 0; j < weight; j++)
            {
                Spawn(prefab);
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private GameObject Spawn(GameObject prefab, Vector3? overridePosition = null, bool assignAutoSquad = true)
    {
        if (prefab == null)
            return null;

        Vector3 spawnPosition = overridePosition ?? GetRandomSpawnPosition();
        ObjectPool<GameObject> pool = GetPool(prefab);
        GameObject go = pool.Get();

        go.transform.SetParent(activeTeammatesRoot, true);
        go.transform.position = spawnPosition;
        go.transform.rotation = Quaternion.identity;
        go.SetActive(true);

        if (go.TryGetComponent<TeamAgent>(out TeamAgent agent))
            agent.SetTeam(playerTeamId);

        if (go.TryGetComponent<EnemySpaceshipAI>(out EnemySpaceshipAI ai))
            ai.ResetForSpawn(player);

        if (go.TryGetComponent<EnemySpaceshipCombatAI>(out EnemySpaceshipCombatAI combat))
            combat.ResetForSpawn();

        if (assignAutoSquad && autoAssignFormationSquad)
            TryAssignToFriendlySquad(go);

        aliveFriendlies++;
        OnAliveFriendliesChanged?.Invoke(aliveFriendlies);
        return go;
    }

    private void TryAssignToFriendlySquad(GameObject ship)
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

        EnemySquadController squad = GetOrCreateFriendlySquad();
        if (squad == null)
            return;

        squad.RegisterMember(member, ResolveFriendlyRole(ship, squad.MemberCount));
    }

    private EnemySquadController GetOrCreateFriendlySquad()
    {
        int maxMembers = Mathf.Clamp(maxShipsPerFriendlySquad, 2, 10);

        for (int i = friendlyAutoSquads.Count - 1; i >= 0; i--)
        {
            EnemySquadController candidate = friendlyAutoSquads[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.MemberCount <= 0)
            {
                friendlyAutoSquads.RemoveAt(i);
                continue;
            }

            RegisterFriendlySquadForReinforcement(candidate);
            if (candidate.MemberCount < maxMembers)
            {
                candidate.ConfigureReinforcementPolicy(maxMembers, 2f, 8f, enableFriendlyReinforcementRequests);
                return candidate;
            }
        }

        GameObject squadObject = new GameObject($"FriendlyAutoSquad_{friendlyAutoSquads.Count + 1}");
        squadObject.transform.SetParent(activeTeammatesRoot, true);
        squadObject.transform.position = player != null ? player.transform.position : Vector3.zero;

        EnemySquadController squad = squadObject.AddComponent<EnemySquadController>();
        squad.Initialize(
            player != null ? player.transform : null,
            friendlyAutoFormationType,
            friendlySquadSpacing,
            friendlySquadInitialState,
            friendlySquadEngageDistance,
            friendlySquadAnchorMoveSpeed);
        squad.ConfigureReinforcementPolicy(maxMembers, 2f, 8f, enableFriendlyReinforcementRequests);

        RegisterFriendlySquadForReinforcement(squad);
        friendlyAutoSquads.Add(squad);
        return squad;
    }

    private void RegisterFriendlySquadForReinforcement(EnemySquadController squad)
    {
        if (squad == null || !enableFriendlyReinforcementRequests)
            return;

        if (!registeredReinforcementSquads.Add(squad))
            return;

        squad.ReinforcementRequested += OnFriendlySquadReinforcementRequested;
    }

    private void OnFriendlySquadReinforcementRequested(ReinforcementRequest request)
    {
        RequestFriendlyReinforcements(request);
    }

    public void RequestFriendlyReinforcements(ReinforcementRequest request)
    {
        if (!enableFriendlyReinforcementRequests || !request.IsValid)
            return;

        PrunePendingFriendlyReinforcementRequests();
        EnqueueOrUpdateFriendlyReinforcementRequest(request);
        OnReinforcementRequested?.Invoke(request);

        if (autoFulfillFriendlyReinforcementRequests)
            TryFulfillPendingFriendlyRequest(request.Squad);
    }

    private void TryFulfillPendingFriendlyRequest(EnemySquadController squad)
    {
        if (squad == null || !squad.isActiveAndEnabled)
            return;

        int pendingIndex = FindPendingFriendlyRequestIndex(squad);
        if (pendingIndex < 0)
            return;

        ReinforcementRequest request = pendingReinforcementRequests[pendingIndex];
        if (!request.IsValid || !squad.IsUnderStrength)
        {
            pendingReinforcementRequests.RemoveAt(pendingIndex);
            return;
        }

        GameObject prefab = GetDefaultFriendlyReinforcementPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("FriendlySpaceshipSpawner: no prefab available for friendly reinforcement fulfill.");
            return;
        }

        int teamId = ResolveFriendlyTeamId(request.TeamId);
        int spawnCount = Mathf.Max(0, request.MissingCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 spawnOffset = Random.insideUnitCircle * Mathf.Max(1f, friendlySquadSpacing * 0.6f);
            Vector3 spawnPos = request.RallyPoint + spawnOffset;

            GameObject ship = Spawn(prefab, spawnPos, assignAutoSquad: false);
            if (ship == null)
                continue;

            TeamAgent agent = ship.GetComponent<TeamAgent>();
            if (agent != null)
                agent.SetTeam(teamId);

            EnemySquadMember member = ship.GetComponent<EnemySquadMember>();
            if (member == null)
                member = ship.AddComponent<EnemySquadMember>();

            squad.RegisterMember(member, ResolveFriendlyRole(ship, squad.MemberCount));

            if (!squad.IsUnderStrength)
                break;
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

    private int ResolveFriendlyTeamId(int requestedTeamId)
    {
        if (requestedTeamId == playerTeamId)
            return requestedTeamId;

        return playerTeamId;
    }

    private GameObject GetDefaultFriendlyReinforcementPrefab()
    {
        if (teamPrefabs == null || teamPrefabs.Length == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < teamPrefabs.Length; i++)
        {
            if (teamPrefabs[i] == null)
                continue;

            int weight = GetSpawnWeightAt(i);
            if (weight > 0)
                totalWeight += weight;
        }

        if (totalWeight <= 0)
        {
            for (int i = 0; i < teamPrefabs.Length; i++)
            {
                if (teamPrefabs[i] != null)
                    return teamPrefabs[i];
            }

            return null;
        }

        int pick = Random.Range(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < teamPrefabs.Length; i++)
        {
            GameObject prefab = teamPrefabs[i];
            if (prefab == null)
                continue;

            int weight = GetSpawnWeightAt(i);
            if (weight <= 0)
                continue;

            cumulative += weight;
            if (pick < cumulative)
                return prefab;
        }

        return null;
    }

    private int GetSpawnWeightAt(int index)
    {
        if (teamSpawnWeights != null && index >= 0 && index < teamSpawnWeights.Length)
            return Mathf.Max(0, teamSpawnWeights[index]);

        return 1;
    }

    private void EnqueueOrUpdateFriendlyReinforcementRequest(ReinforcementRequest request)
    {
        int existingIndex = FindPendingFriendlyRequestIndex(request.Squad);
        if (existingIndex >= 0)
        {
            pendingReinforcementRequests[existingIndex] = request;
            return;
        }

        int maxQueue = Mathf.Max(1, maxPendingFriendlyReinforcementRequests);
        while (pendingReinforcementRequests.Count >= maxQueue)
            pendingReinforcementRequests.RemoveAt(0);

        pendingReinforcementRequests.Add(request);
    }

    private int FindPendingFriendlyRequestIndex(EnemySquadController squad)
    {
        for (int i = 0; i < pendingReinforcementRequests.Count; i++)
        {
            if (pendingReinforcementRequests[i].Squad == squad)
                return i;
        }

        return -1;
    }

    private void PrunePendingFriendlyReinforcementRequests()
    {
        for (int i = pendingReinforcementRequests.Count - 1; i >= 0; i--)
        {
            ReinforcementRequest request = pendingReinforcementRequests[i];
            EnemySquadController squad = request.Squad;

            if (!request.IsValid || squad == null || !squad.isActiveAndEnabled || !squad.IsUnderStrength)
                pendingReinforcementRequests.RemoveAt(i);
        }
    }

    private void UnregisterAllFriendlyReinforcementSquads()
    {
        foreach (EnemySquadController squad in registeredReinforcementSquads)
        {
            if (squad != null)
                squad.ReinforcementRequested -= OnFriendlySquadReinforcementRequested;
        }

        registeredReinforcementSquads.Clear();
    }

    private static EnemySquadRole ResolveFriendlyRole(GameObject ship, int slotIndex)
    {
        if (ship == null)
            return slotIndex == 0 ? EnemySquadRole.Leader : EnemySquadRole.Wingman;

        if (ship.GetComponent<ShieldGiver>() != null)
            return EnemySquadRole.ShieldSupport;

        if (ship.GetComponent<Kamakazy>() != null)
            return EnemySquadRole.Kamikaze;

        return slotIndex == 0 ? EnemySquadRole.Leader : EnemySquadRole.Wingman;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        if (player == null)
            return Vector3.zero;

        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(minSpawnRadius, maxSpawnRadius);
        return player.transform.position + (Vector3)(randomDirection * randomDistance);
    }

    public void NotifyTeamateGone()
    {
        aliveFriendlies = Mathf.Max(0, aliveFriendlies - 1);
        OnAliveFriendliesChanged?.Invoke(aliveFriendlies);
    }

    private void Prewarm(GameObject prefab, int count)
    {
        ObjectPool<GameObject> pool = GetPool(prefab);
        for (int i = 0; i < count; i++)
        {
            GameObject go = pool.Get();
            pool.Release(go);
        }
    }

    private ObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<GameObject> existing))
            return existing;

        ObjectPool<GameObject> pool = null;

        pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                GameObject go = Instantiate(prefab, pooledRoot);
                go.SetActive(false);

                PooledTeamate pooled = go.GetComponent<PooledTeamate>();
                if (pooled == null)
                    pooled = go.AddComponent<PooledTeamate>();

                pooled.Init(this, pool);
                return go;
            },
            actionOnGet: go =>
            {
                if (go.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
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
}







