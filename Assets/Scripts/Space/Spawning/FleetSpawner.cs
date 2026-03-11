using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[AddComponentMenu("Space/Spawning/Fleet Spawner")]
public class FleetSpawner : MonoBehaviour
{
    [Serializable]
    public class ShipPrefabOption
    {
        public GameObject prefab;
        [Min(1)] public int weight = 1;
    }

    [Serializable]
    public class TeamSpawnConfig
    {
        public string label = "Team";
        public int teamId = 1;
        [Range(2, 10)] public int autoSquadMaxMembers = 10;
        public EnemySquadFormationType autoFormationType = EnemySquadFormationType.Vee;
        public EnemySquadState autoSquadState = EnemySquadState.Engage;
        public float squadSpacing = 5f;
        public float squadEngageDistance = 18f;
        public float squadAnchorMoveSpeed = 14f;
        public Transform defaultFocusTarget;
        public List<ShipPrefabOption> shipPrefabs = new List<ShipPrefabOption>();
    }

    [Serializable]
    public class FleetWaveEntry
    {
        public int teamId = 1;
        [Min(1)] public int count = 5;

        [Header("Squad Spawning")]
        public bool spawnAsSquad = false;
        [Range(2, 10)] public int minSquadSize = 2;
        [Range(2, 10)] public int maxSquadSize = 4;
        public EnemySquadFormationType formationType = EnemySquadFormationType.Vee;
        public EnemySquadState initialSquadState = EnemySquadState.Engage;
        public float squadSpacing = 5f;
        public float squadEngageDistance = 18f;
        public float squadAnchorMoveSpeed = 14f;

        [Header("Overrides")]
        public Transform focusTarget;
        public GameObject prefabOverride;
    }

    [Serializable]
    public class FleetWave
    {
        public List<FleetWaveEntry> entries = new List<FleetWaveEntry>();
        public float spawnSpacing = 0f;
        public float timeToNextWaveAfterClear = 2f;
    }

    public enum SpawnMode
    {
        Manual,
        SetWaves
    }

    public static FleetSpawner Instance { get; private set; }

    public event Action<int, int> OnTeamAliveCountChanged;
    public event Action<int> OnAliveEnemiesChanged;
    public event Action<int> OnWaveChanged;
    public event Action AllWavesCompleted;
    public event Action<ReinforcementRequest> OnReinforcementRequested;

    [Header("Team Config")]
    [SerializeField] private List<TeamSpawnConfig> teamConfigs = new List<TeamSpawnConfig>();
    [SerializeField] private GameObject fallbackShipPrefab;
    [SerializeField] private Transform defaultFocusTarget;

    [Header("Auto Squad")]
    [SerializeField] private bool autoAssignShipsToSquads = true;
    [SerializeField] [Range(2, 10)] private int defaultAutoSquadMaxMembers = 10;
    [SerializeField] private EnemySquadFormationType defaultAutoFormationType = EnemySquadFormationType.Vee;
    [SerializeField] private EnemySquadState defaultAutoSquadState = EnemySquadState.Engage;
    [SerializeField] private float defaultAutoSquadSpacing = 5f;
    [SerializeField] private float defaultAutoSquadEngageDistance = 18f;
    [SerializeField] private float defaultAutoSquadAnchorMoveSpeed = 14f;
    [SerializeField] private float squadSeparationDistance = 12f;

    [Header("Reinforcement Requests")]
    [SerializeField] private bool enableReinforcementRequests = true;
    [SerializeField] private bool autoFulfillReinforcementRequests = false;
    [SerializeField] private int maxPendingReinforcementRequests = 64;
    [SerializeField] private float defaultRequestDelaySeconds = 2f;
    [SerializeField] private float defaultRequestCooldownSeconds = 8f;

    [Header("Wave Mode (Optional)")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Manual;
    [SerializeField] private float initialWaveSpawnDelay = 2f;
    [SerializeField] private bool waitForTrackedEnemyTeamsToClear = true;
    [SerializeField] private List<int> trackedEnemyTeamIds = new List<int> { 1 };
    [SerializeField] private List<FleetWave> waves = new List<FleetWave>();

    [Header("Startup Spawn (Manual Mode)")]
    [SerializeField] private bool autoSpawnConfiguredTeamsOnStart = false;
    [SerializeField] private bool startupSpawnAsSquads = true;
    [SerializeField] [Range(1, 10)] private int startupShipsPerTeam = 5;
    [SerializeField] private bool includePlayerTeamInStartup = false;
    [SerializeField] private float startupTeamSpacing = 20f;
    [SerializeField] private Vector2 startupShipJitter = new Vector2(6f, 6f);

    [Header("Spawn Area")]
    [SerializeField] private Vector2 fallbackMinSpawnAreaSize = new Vector2(40f, 40f);
    [SerializeField] private Vector2 fallbackMaxSpawnAreaSize = new Vector2(80f, 80f);

    [Header("Pooling")]
    [SerializeField] private int defaultPoolCapacity = 40;
    [SerializeField] private int maxPoolSize = 200;

    [Header("Hierarchy Parents")]
    [SerializeField] private Transform activeShipsRoot;
    [SerializeField] private Transform pooledRoot;

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    private readonly Dictionary<int, List<EnemySquadController>> autoSquadsByTeam = new();
    private readonly Dictionary<int, int> teamAliveCounts = new();
    private readonly Dictionary<int, Transform> teamFocusOverrides = new();
    private readonly HashSet<EnemySquadController> registeredReinforcementSquads = new();
    private readonly List<ReinforcementRequest> pendingReinforcementRequests = new(64);

    private GameObject player;
    private Coroutine waveRoutine;
    private int currentWave;
    private int cachedTrackedEnemyCount;

    public int PendingReinforcementRequestCount => pendingReinforcementRequests.Count;
    public IReadOnlyList<ReinforcementRequest> PendingReinforcementRequests => pendingReinforcementRequests;
    public int CurrentWave => currentWave;
    public int AliveTrackedEnemies => cachedTrackedEnemyCount;

    private void Awake()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple FleetSpawner instances found. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        EnsureRoots();
    }

    private void Start()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        EmitTrackedEnemyCount(force: true);
        TryStartWaveMode();
        TryStartupSpawnFromTeamConfigs();
    }

    private void OnDisable()
    {
        StopWaveMode();
        UnregisterAllReinforcementSquads();
        pendingReinforcementRequests.Clear();
    }

    public void SetPlayerReference(GameObject playerObject)
    {
        player = playerObject;
    }

    public void SetTeamFocusTarget(int teamId, Transform focusTarget)
    {
        if (focusTarget == null)
        {
            teamFocusOverrides.Remove(teamId);
            return;
        }

        teamFocusOverrides[teamId] = focusTarget;
    }

    public int GetAliveCountForTeam(int teamId)
    {
        return teamAliveCounts.TryGetValue(teamId, out int count) ? count : 0;
    }

    public int GetAliveCountForTrackedEnemyTeams()
    {
        return ComputeTrackedEnemyCount();
    }

    public void SetTrackedEnemyTeams(List<int> teamIds)
    {
        trackedEnemyTeamIds = teamIds ?? new List<int>();
        EmitTrackedEnemyCount(force: true);
    }

    public void StartWaveMode(bool restart = false)
    {
        if (spawnMode != SpawnMode.SetWaves)
            spawnMode = SpawnMode.SetWaves;

        if (waveRoutine != null)
        {
            if (!restart)
                return;

            StopCoroutine(waveRoutine);
            waveRoutine = null;
        }

        currentWave = 0;
        waveRoutine = StartCoroutine(RunWaves());
    }

    public void StopWaveMode()
    {
        if (waveRoutine == null)
            return;

        StopCoroutine(waveRoutine);
        waveRoutine = null;
    }

    public GameObject SpawnShipForTeam(
        int teamId,
        Vector3 worldPosition,
        GameObject prefabOverride = null,
        bool assignAutoSquad = true,
        Transform focusTargetOverride = null)
    {
        GameObject prefab = ResolveShipPrefab(teamId, prefabOverride);
        if (prefab == null)
        {
            Debug.LogWarning($"FleetSpawner: no ship prefab found for team {teamId}.");
            return null;
        }

        ObjectPool<GameObject> pool = GetPool(prefab);
        GameObject go = pool.Get();

        go.transform.SetParent(activeShipsRoot, true);
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.identity;

        if (go.TryGetComponent<TeamAgent>(out TeamAgent agent))
            agent.SetTeam(teamId);

        if (go.TryGetComponent<EnemySpaceshipAI>(out EnemySpaceshipAI ai))
        {
            ai.enabled = true;
            ai.ResetForSpawn(player);
        }

        if (go.TryGetComponent<EnemySpaceshipCombatAI>(out EnemySpaceshipCombatAI combat))
        {
            combat.enabled = true;
            combat.ResetForSpawn();
        }

        if (assignAutoSquad && autoAssignShipsToSquads)
            TryAssignToAutoSquad(go, teamId, focusTargetOverride);

        NotifyShipSpawned(teamId);
        return go;
    }

    public EnemySquadController SpawnSquadForTeam(
        int teamId,
        int memberCount,
        Vector3 anchorPosition,
        Transform focusTarget = null,
        GameObject prefabOverride = null)
    {
        TeamSpawnConfig config = GetTeamConfig(teamId);
        EnemySquadFormationType formation = config != null ? config.autoFormationType : defaultAutoFormationType;
        float spacing = config != null ? config.squadSpacing : defaultAutoSquadSpacing;
        EnemySquadState state = config != null ? config.autoSquadState : defaultAutoSquadState;
        float engageDistance = config != null ? config.squadEngageDistance : defaultAutoSquadEngageDistance;
        float anchorMoveSpeed = config != null ? config.squadAnchorMoveSpeed : defaultAutoSquadAnchorMoveSpeed;

        return SpawnSquadForTeam(
            teamId,
            memberCount,
            anchorPosition,
            formation,
            spacing,
            state,
            engageDistance,
            anchorMoveSpeed,
            focusTarget,
            prefabOverride);
    }

    public EnemySquadController SpawnSquadForTeam(
        int teamId,
        int memberCount,
        Vector3 anchorPosition,
        EnemySquadFormationType formationType,
        float spacing,
        EnemySquadState initialState,
        float engageDistance,
        float anchorMoveSpeed,
        Transform focusTarget = null,
        GameObject prefabOverride = null)
    {
        int clampedCount = Mathf.Clamp(memberCount, 1, 10);
        GameObject prefab = ResolveShipPrefab(teamId, prefabOverride);
        if (prefab == null)
        {
            Debug.LogWarning($"FleetSpawner: no ship prefab found for squad spawn (team {teamId}).");
            return null;
        }

        GameObject squadObject = new GameObject($"FleetSquad_T{teamId}_{Guid.NewGuid():N}");
        squadObject.transform.SetParent(activeShipsRoot, true);
        squadObject.transform.position = anchorPosition;

        EnemySquadController squad = squadObject.AddComponent<SquadController>();
        Transform resolvedFocus = ResolveFocusTarget(teamId, focusTarget);
        squad.Initialize(
            resolvedFocus,
            formationType,
            spacing,
            initialState,
            engageDistance,
            anchorMoveSpeed);
        squad.ConfigureReinforcementPolicy(
            clampedCount,
            defaultRequestDelaySeconds,
            defaultRequestCooldownSeconds,
            enableReinforcementRequests);

        RegisterSquadForReinforcement(squad);

        for (int i = 0; i < clampedCount; i++)
        {
            Vector3 memberSpawnPos = squad.GetPreviewSlotWorldPosition(i);
            GameObject ship = SpawnShipForTeam(
                teamId,
                memberSpawnPos,
                prefab,
                assignAutoSquad: false,
                focusTargetOverride: resolvedFocus);
            if (ship == null)
                continue;

            EnemySquadMember member = ship.GetComponent<EnemySquadMember>();
            if (member == null)
                member = ship.AddComponent<SquadMember>();

            squad.RegisterMember(member, ResolveRole(ship, i));
        }

        return squad;
    }

    public List<EnemySquadController> SpawnSquadronsForTeam(
        int teamId,
        int totalShips,
        Vector3 centerPosition,
        Transform focusTarget = null,
        GameObject prefabOverride = null)
    {
        List<EnemySquadController> squads = new List<EnemySquadController>();
        int remaining = Mathf.Max(0, totalShips);
        if (remaining == 0)
            return squads;

        int maxMembers = GetMaxAutoMembersForTeam(teamId);
        int squadIndex = 0;

        while (remaining > 0)
        {
            int squadSize = Mathf.Min(maxMembers, remaining);
            Vector3 anchorPos = centerPosition + ComputeSquadOffset(squadIndex);

            EnemySquadController squad = SpawnSquadForTeam(
                teamId,
                squadSize,
                anchorPos,
                focusTarget,
                prefabOverride);
            if (squad != null)
                squads.Add(squad);

            remaining -= squadSize;
            squadIndex++;
        }

        return squads;
    }

    public EnemySquadController SpawnPlayerSupportSquad(
        int memberCount,
        Transform playerTransform = null,
        GameObject prefabOverride = null)
    {
        Transform supportAnchor = playerTransform != null ? playerTransform : (player != null ? player.transform : null);
        int teamId = ResolvePlayerTeamId();
        Vector3 center = supportAnchor != null
            ? supportAnchor.position + (Vector3)(Random.insideUnitCircle * Mathf.Max(2f, squadSeparationDistance * 0.35f))
            : Vector3.zero;

        return SpawnSquadForTeam(teamId, memberCount, center, supportAnchor, prefabOverride);
    }

    public List<EnemySquadController> SpawnPlayerSupportSquadrons(
        int totalShips,
        Transform playerTransform = null,
        GameObject prefabOverride = null)
    {
        Transform supportAnchor = playerTransform != null ? playerTransform : (player != null ? player.transform : null);
        int teamId = ResolvePlayerTeamId();
        Vector3 center = supportAnchor != null
            ? supportAnchor.position + (Vector3)(Random.insideUnitCircle * Mathf.Max(2f, squadSeparationDistance * 0.35f))
            : Vector3.zero;

        return SpawnSquadronsForTeam(teamId, totalShips, center, supportAnchor, prefabOverride);
    }

    public void SpawnReinforcementsNow(
        int teamId,
        int shipCount,
        Vector2 rallyPoint,
        Transform focusTarget = null,
        GameObject prefabOverride = null)
    {
        SpawnSquadronsForTeam(teamId, shipCount, rallyPoint, focusTarget, prefabOverride);
    }

    public void RequestReinforcements(ReinforcementRequest request)
    {
        if (!enableReinforcementRequests || !request.IsValid)
            return;

        PrunePendingReinforcementRequests();
        EnqueueOrUpdateReinforcementRequest(request);
        OnReinforcementRequested?.Invoke(request);

        if (autoFulfillReinforcementRequests)
            TryFulfillPendingRequestForSquad(request.Squad);
    }

    public void SetAutoFulfillReinforcementRequests(bool enabled)
    {
        autoFulfillReinforcementRequests = enabled;
    }

    public void RemovePendingReinforcementRequest(EnemySquadController squad)
    {
        if (squad == null)
            return;

        int index = FindPendingRequestIndex(squad);
        if (index >= 0)
            pendingReinforcementRequests.RemoveAt(index);
    }

    public void NotifyShipGone(int teamId)
    {
        int current = GetAliveCountForTeam(teamId);
        int next = Mathf.Max(0, current - 1);
        teamAliveCounts[teamId] = next;
        OnTeamAliveCountChanged?.Invoke(teamId, next);
        EmitTrackedEnemyCount();
    }

    private void NotifyShipSpawned(int teamId)
    {
        int current = GetAliveCountForTeam(teamId);
        int next = current + 1;
        teamAliveCounts[teamId] = next;
        OnTeamAliveCountChanged?.Invoke(teamId, next);
        EmitTrackedEnemyCount();
    }

    private void TryAssignToAutoSquad(GameObject ship, int teamId, Transform focusTargetOverride)
    {
        if (ship == null)
            return;

        EnemySpaceshipAI ai = ship.GetComponent<EnemySpaceshipAI>();
        if (ai == null)
            return;

        EnemySquadMember member = ship.GetComponent<EnemySquadMember>();
        if (member == null)
            member = ship.AddComponent<SquadMember>();

        if (member.Squad != null)
            return;

        EnemySquadController squad = GetOrCreateAutoSquad(teamId, focusTargetOverride);
        if (squad == null)
            return;

        squad.RegisterMember(member, ResolveRole(ship, squad.MemberCount));
    }

    private EnemySquadController GetOrCreateAutoSquad(int teamId, Transform focusTargetOverride)
    {
        if (!autoSquadsByTeam.TryGetValue(teamId, out List<EnemySquadController> squads))
        {
            squads = new List<EnemySquadController>();
            autoSquadsByTeam.Add(teamId, squads);
        }

        int maxMembers = GetMaxAutoMembersForTeam(teamId);
        Transform resolvedFocus = ResolveFocusTarget(teamId, focusTargetOverride);

        for (int i = squads.Count - 1; i >= 0; i--)
        {
            EnemySquadController candidate = squads[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.MemberCount <= 0)
            {
                squads.RemoveAt(i);
                continue;
            }

            candidate.SetFocusTarget(resolvedFocus);
            candidate.ConfigureReinforcementPolicy(
                maxMembers,
                defaultRequestDelaySeconds,
                defaultRequestCooldownSeconds,
                enableReinforcementRequests);

            RegisterSquadForReinforcement(candidate);
            if (candidate.MemberCount < maxMembers)
                return candidate;
        }

        TeamSpawnConfig config = GetTeamConfig(teamId);
        EnemySquadFormationType formation = config != null ? config.autoFormationType : defaultAutoFormationType;
        EnemySquadState state = config != null ? config.autoSquadState : defaultAutoSquadState;
        float spacing = config != null ? config.squadSpacing : defaultAutoSquadSpacing;
        float engageDistance = config != null ? config.squadEngageDistance : defaultAutoSquadEngageDistance;
        float anchorMoveSpeed = config != null ? config.squadAnchorMoveSpeed : defaultAutoSquadAnchorMoveSpeed;

        Vector3 anchorPos = player != null
            ? player.transform.position + (Vector3)(Random.insideUnitCircle * Mathf.Max(1f, squadSeparationDistance * 0.25f))
            : Vector3.zero;

        GameObject squadObject = new GameObject($"FleetAutoSquad_T{teamId}_{squads.Count + 1}");
        squadObject.transform.SetParent(activeShipsRoot, true);
        squadObject.transform.position = anchorPos;

        EnemySquadController squad = squadObject.AddComponent<SquadController>();
        squad.Initialize(
            resolvedFocus,
            formation,
            spacing,
            state,
            engageDistance,
            anchorMoveSpeed);
        squad.ConfigureReinforcementPolicy(
            maxMembers,
            defaultRequestDelaySeconds,
            defaultRequestCooldownSeconds,
            enableReinforcementRequests);

        RegisterSquadForReinforcement(squad);
        squads.Add(squad);
        return squad;
    }

    private void RegisterSquadForReinforcement(EnemySquadController squad)
    {
        if (squad == null || !enableReinforcementRequests)
            return;

        if (!registeredReinforcementSquads.Add(squad))
            return;

        squad.ReinforcementRequested += OnSquadReinforcementRequested;
    }

    private void OnSquadReinforcementRequested(ReinforcementRequest request)
    {
        RequestReinforcements(request);
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

        GameObject prefab = ResolveShipPrefab(request.TeamId, null);
        if (prefab == null)
        {
            Debug.LogWarning($"FleetSpawner: no prefab available to fulfill reinforcement for team {request.TeamId}.");
            return;
        }

        int spawnCount = Mathf.Max(0, request.MissingCount);
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 spawnOffset = Random.insideUnitCircle * Mathf.Max(1f, defaultAutoSquadSpacing * 0.6f);
            Vector3 spawnPos = request.RallyPoint + spawnOffset;
            GameObject ship = SpawnShipForTeam(
                request.TeamId,
                spawnPos,
                prefab,
                assignAutoSquad: false,
                focusTargetOverride: request.FocusTarget);
            if (ship == null)
                continue;

            EnemySquadMember member = ship.GetComponent<EnemySquadMember>();
            if (member == null)
                member = ship.AddComponent<SquadMember>();

            squad.RegisterMember(member, ResolveRole(ship, squad.MemberCount));
            if (!squad.IsUnderStrength)
                break;
        }

        if (!squad.IsUnderStrength)
        {
            pendingReinforcementRequests.RemoveAt(pendingIndex);
            return;
        }

        ReinforcementRequest refreshed = new ReinforcementRequest(
            squad,
            request.TeamId,
            squad.DesiredMemberCount,
            squad.MemberCount,
            request.FocusTarget,
            request.RallyPoint,
            Time.time);

        if (refreshed.IsValid)
            pendingReinforcementRequests[pendingIndex] = refreshed;
        else
            pendingReinforcementRequests.RemoveAt(pendingIndex);
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

    private Transform ResolveFocusTarget(int teamId, Transform overrideFocus)
    {
        if (overrideFocus != null)
            return overrideFocus;

        if (teamFocusOverrides.TryGetValue(teamId, out Transform mapped) && mapped != null)
            return mapped;

        TeamSpawnConfig config = GetTeamConfig(teamId);
        if (config != null && config.defaultFocusTarget != null)
            return config.defaultFocusTarget;

        if (defaultFocusTarget != null)
            return defaultFocusTarget;

        if (player != null)
        {
            TeamAgent playerAgent = player.GetComponent<TeamAgent>();
            int playerTeam = playerAgent != null ? playerAgent.TeamId : 0;
            if (teamId != playerTeam)
                return player.transform;
        }

        return null;
    }

    private int ResolvePlayerTeamId()
    {
        if (player == null)
            return 0;

        TeamAgent playerAgent = player.GetComponent<TeamAgent>();
        return playerAgent != null ? playerAgent.TeamId : 0;
    }

    private int GetMaxAutoMembersForTeam(int teamId)
    {
        TeamSpawnConfig config = GetTeamConfig(teamId);
        if (config != null)
            return Mathf.Clamp(config.autoSquadMaxMembers, 2, 10);

        return Mathf.Clamp(defaultAutoSquadMaxMembers, 2, 10);
    }

    private TeamSpawnConfig GetTeamConfig(int teamId)
    {
        for (int i = 0; i < teamConfigs.Count; i++)
        {
            TeamSpawnConfig config = teamConfigs[i];
            if (config != null && config.teamId == teamId)
                return config;
        }

        return null;
    }

    private void TryStartWaveMode()
    {
        if (spawnMode != SpawnMode.SetWaves)
            return;

        StartWaveMode(restart: true);
    }

    private void TryStartupSpawnFromTeamConfigs()
    {
        if (GetComponent<SimpleTeamSpawner>() != null)
            return;

        if (spawnMode != SpawnMode.Manual || !autoSpawnConfiguredTeamsOnStart)
            return;

        if (teamConfigs == null || teamConfigs.Count == 0)
            return;

        int playerTeamId = ResolvePlayerTeamId();
        int spawnedTeams = 0;

        for (int i = 0; i < teamConfigs.Count; i++)
        {
            TeamSpawnConfig config = teamConfigs[i];
            if (config == null)
                continue;

            if (!includePlayerTeamInStartup && config.teamId == playerTeamId)
                continue;

            int shipCount = Mathf.Clamp(startupShipsPerTeam, 1, 10);
            Vector3 anchorPos = GetStartupAnchorPosition(spawnedTeams);
            Transform focus = ResolveFocusTarget(config.teamId, config.defaultFocusTarget);

            if (startupSpawnAsSquads)
            {
                SpawnSquadForTeam(
                    config.teamId,
                    shipCount,
                    anchorPos,
                    config.autoFormationType,
                    config.squadSpacing,
                    config.autoSquadState,
                    config.squadEngageDistance,
                    config.squadAnchorMoveSpeed,
                    focus,
                    prefabOverride: null);
            }
            else
            {
                for (int shipIndex = 0; shipIndex < shipCount; shipIndex++)
                {
                    Vector2 jitter = new Vector2(
                        Random.Range(-Mathf.Abs(startupShipJitter.x), Mathf.Abs(startupShipJitter.x)),
                        Random.Range(-Mathf.Abs(startupShipJitter.y), Mathf.Abs(startupShipJitter.y)));

                    SpawnShipForTeam(
                        config.teamId,
                        anchorPos + (Vector3)jitter,
                        prefabOverride: null,
                        assignAutoSquad: true,
                        focusTargetOverride: focus);
                }
            }

            spawnedTeams++;
        }

        if (spawnedTeams == 0)
        {
            Debug.LogWarning(
                "FleetSpawner: startup spawn is enabled but no eligible teams were spawned. " +
                "Check team configs and includePlayerTeamInStartup.");
        }
    }

    private Vector3 GetStartupAnchorPosition(int teamIndex)
    {
        Vector2 center = player != null ? (Vector2)player.transform.position : Vector2.zero;

        if (teamIndex <= 0)
            return center + Random.insideUnitCircle * Mathf.Max(1f, startupTeamSpacing * 0.3f);

        int ringSize = 6;
        int ring = ((teamIndex - 1) / ringSize) + 1;
        int indexInRing = (teamIndex - 1) % ringSize;
        float angleDeg = (360f / ringSize) * indexInRing;
        float radius = Mathf.Max(1f, startupTeamSpacing) * ring;

        Vector2 offset = Quaternion.Euler(0f, 0f, angleDeg) * Vector2.right * radius;
        return center + offset;
    }

    private IEnumerator RunWaves()
    {
        if (initialWaveSpawnDelay > 0f)
            yield return new WaitForSeconds(initialWaveSpawnDelay);

        if (waves == null || waves.Count == 0)
        {
            waveRoutine = null;
            yield break;
        }

        currentWave = 0;

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            FleetWave wave = waves[waveIndex];
            if (wave == null)
                continue;

            currentWave++;
            OnWaveChanged?.Invoke(currentWave);

            List<FleetWaveEntry> entries = wave.entries;
            if (entries != null)
            {
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    FleetWaveEntry entry = entries[entryIndex];
                    if (entry == null)
                        continue;

                    int requestedCount = Mathf.Max(0, entry.count);
                    if (requestedCount <= 0)
                        continue;

                    if (entry.spawnAsSquad)
                    {
                        int remaining = requestedCount;
                        while (remaining > 0)
                        {
                            int squadSize = PickWaveSquadSize(entry, remaining);
                            Vector3 anchorPos = GetRandomSpawnPosition();

                            SpawnSquadForTeam(
                                entry.teamId,
                                squadSize,
                                anchorPos,
                                entry.formationType,
                                entry.squadSpacing,
                                entry.initialSquadState,
                                entry.squadEngageDistance,
                                entry.squadAnchorMoveSpeed,
                                entry.focusTarget,
                                entry.prefabOverride);

                            remaining -= squadSize;

                            if (wave.spawnSpacing > 0f)
                                yield return new WaitForSeconds(wave.spawnSpacing);
                        }
                    }
                    else
                    {
                        for (int spawnIndex = 0; spawnIndex < requestedCount; spawnIndex++)
                        {
                            SpawnShipForTeam(
                                entry.teamId,
                                GetRandomSpawnPosition(),
                                entry.prefabOverride,
                                assignAutoSquad: true,
                                focusTargetOverride: entry.focusTarget);

                            if (wave.spawnSpacing > 0f)
                                yield return new WaitForSeconds(wave.spawnSpacing);
                        }
                    }
                }
            }

            if (waitForTrackedEnemyTeamsToClear)
            {
                while (ComputeTrackedEnemyCount() > 0)
                    yield return null;
            }

            if (wave.timeToNextWaveAfterClear > 0f)
                yield return new WaitForSeconds(wave.timeToNextWaveAfterClear);
        }

        AllWavesCompleted?.Invoke();
        waveRoutine = null;
    }

    private static int PickWaveSquadSize(FleetWaveEntry entry, int remaining)
    {
        int minSize = Mathf.Clamp(entry.minSquadSize, 2, 10);
        int maxSize = Mathf.Clamp(entry.maxSquadSize, minSize, 10);
        int requested = Random.Range(minSize, maxSize + 1);

        if (remaining >= 2)
            return Mathf.Min(requested, remaining);

        return 1;
    }

    private int ComputeTrackedEnemyCount()
    {
        if (trackedEnemyTeamIds == null || trackedEnemyTeamIds.Count == 0)
            return 0;

        int total = 0;
        for (int i = 0; i < trackedEnemyTeamIds.Count; i++)
            total += GetAliveCountForTeam(trackedEnemyTeamIds[i]);

        return total;
    }

    private void EmitTrackedEnemyCount(bool force = false)
    {
        int next = ComputeTrackedEnemyCount();
        if (!force && next == cachedTrackedEnemyCount)
            return;

        cachedTrackedEnemyCount = next;
        OnAliveEnemiesChanged?.Invoke(cachedTrackedEnemyCount);
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float minWidth = Mathf.Min(fallbackMinSpawnAreaSize.x, fallbackMaxSpawnAreaSize.x);
        float maxWidth = Mathf.Max(fallbackMinSpawnAreaSize.x, fallbackMaxSpawnAreaSize.x);
        float minHeight = Mathf.Min(fallbackMinSpawnAreaSize.y, fallbackMaxSpawnAreaSize.y);
        float maxHeight = Mathf.Max(fallbackMinSpawnAreaSize.y, fallbackMaxSpawnAreaSize.y);

        float spawnAreaWidth = Random.Range(minWidth, maxWidth);
        float spawnAreaHeight = Random.Range(minHeight, maxHeight);

        float randomX = Random.Range(-spawnAreaWidth * 0.5f, spawnAreaWidth * 0.5f);
        float randomY = Random.Range(-spawnAreaHeight * 0.5f, spawnAreaHeight * 0.5f);

        if (player == null)
            return new Vector3(randomX, randomY, 0f);

        return new Vector3(
            randomX + player.transform.position.x,
            randomY + player.transform.position.y,
            player.transform.position.z);
    }

    private GameObject ResolveShipPrefab(int teamId, GameObject prefabOverride)
    {
        if (prefabOverride != null)
            return prefabOverride;

        TeamSpawnConfig config = GetTeamConfig(teamId);
        if (config != null)
        {
            GameObject weighted = PickWeightedPrefab(config.shipPrefabs);
            if (weighted != null)
                return weighted;
        }

        return fallbackShipPrefab;
    }

    private static GameObject PickWeightedPrefab(List<ShipPrefabOption> options)
    {
        if (options == null || options.Count == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < options.Count; i++)
        {
            ShipPrefabOption option = options[i];
            if (option == null || option.prefab == null)
                continue;

            totalWeight += Mathf.Max(0, option.weight);
        }

        if (totalWeight <= 0)
        {
            for (int i = 0; i < options.Count; i++)
            {
                ShipPrefabOption option = options[i];
                if (option != null && option.prefab != null)
                    return option.prefab;
            }

            return null;
        }

        int pick = Random.Range(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < options.Count; i++)
        {
            ShipPrefabOption option = options[i];
            if (option == null || option.prefab == null)
                continue;

            int weight = Mathf.Max(0, option.weight);
            if (weight == 0)
                continue;

            cumulative += weight;
            if (pick < cumulative)
                return option.prefab;
        }

        return null;
    }

    private static EnemySquadRole ResolveRole(GameObject ship, int slotIndex)
    {
        if (ship == null)
            return slotIndex == 0 ? EnemySquadRole.Leader : EnemySquadRole.Wingman;

        if (ship.GetComponent<ShieldGiver>() != null)
            return EnemySquadRole.ShieldSupport;

        if (ship.GetComponent<Kamakazy>() != null)
            return EnemySquadRole.Kamikaze;

        return slotIndex == 0 ? EnemySquadRole.Leader : EnemySquadRole.Wingman;
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

                PooledFleetShip pooled = go.GetComponent<PooledFleetShip>();
                if (pooled == null)
                    pooled = go.AddComponent<PooledFleetShip>();

                pooled.Init(this, pool);
                return go;
            },
            actionOnGet: go =>
            {
                go.SetActive(true);

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

    private void EnsureRoots()
    {
        if (activeShipsRoot == null)
        {
            GameObject go = new GameObject("Fleet_Active");
            activeShipsRoot = go.transform;
        }

        if (pooledRoot == null)
        {
            GameObject go = new GameObject("Fleet_Pooled");
            pooledRoot = go.transform;
        }
    }

    private Vector3 ComputeSquadOffset(int squadIndex)
    {
        if (squadIndex <= 0)
            return Vector3.zero;

        int ringSize = 6;
        int ring = ((squadIndex - 1) / ringSize) + 1;
        int indexInRing = (squadIndex - 1) % ringSize;

        float angleDeg = (360f / ringSize) * indexInRing;
        float radius = Mathf.Max(1f, squadSeparationDistance) * ring;
        Vector2 offset = Quaternion.Euler(0f, 0f, angleDeg) * Vector2.right * radius;
        return offset;
    }
}
