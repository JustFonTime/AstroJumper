using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(FleetSpawner))]
[AddComponentMenu("Space/Spawning/Simple Team Spawner")]
public class SimpleTeamSpawner : MonoBehaviour
{
    private const int PlayerTeamId = 0;
    private const int EnemyTeamId = 1;

    private enum SquadObjectiveRole
    {
        Defend,
        Attack,
        Roam
    }

    private sealed class SquadRuntime
    {
        public EnemySquadController Squad;
        public int TeamId;
        public SquadObjectiveRole Role;
        public Transform Proxy;
        public float OrbitAngleDeg;
        public float OrbitSpeedDeg;
        public float OrbitDirection;
        public float RoamT;
        public float RoamSpeed;
        public float RoamPhase;
        public float RoamDirection;
    }

    [Header("Battle")]
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private bool clearExistingBattleOnSpawn = true;
    [SerializeField] private bool updateObjectiveProxies = true;
    [SerializeField] private bool enableSpawnHotkey = true;
    [SerializeField] private KeyCode spawnBattleKey = KeyCode.B;

    [Header("Prefabs")]
    [SerializeField] private GameObject flagshipPrefab;
    [SerializeField] private GameObject playerTeamFlagshipPrefab;
    [SerializeField] private GameObject enemyTeamFlagshipPrefab;
    [SerializeField] private GameObject sharedShipPrefab;
    [SerializeField] private GameObject playerTeamShipPrefab;
    [SerializeField] private GameObject enemyTeamShipPrefab;

    [Header("Fleet Counts")]
    [SerializeField] [Range(1, 12)] private int squadsPerTeam = 4;
    [SerializeField] [Range(1, 10)] private int shipsPerSquad = 5;

    [Header("Formation")]
    [SerializeField] private EnemySquadFormationType formationType = EnemySquadFormationType.Vee;
    [SerializeField] private EnemySquadState squadState = EnemySquadState.Engage;
    [SerializeField] private float squadSpacing = 5f;
    [SerializeField] private float squadEngageDistance = 18f;
    [SerializeField] private float squadAnchorMoveSpeed = 14f;

    [Header("Flagship Spawn")]
    [SerializeField] private Transform playerFlagshipSpawnPoint;
    [SerializeField] private Transform enemyFlagshipSpawnPoint;
    [SerializeField] private bool useArenaBoundaryIfAvailable = true;
    [SerializeField] [Range(0.2f, 0.95f)] private float boundarySpawnRadiusRatio = 0.82f;
    [SerializeField] private float fallbackFlagshipSeparation = 1400f;
    [SerializeField] private float fleetSpawnRadiusAroundFlagship = 120f;
    [SerializeField] private Vector2 squadSpawnJitter = new Vector2(20f, 20f);
    [SerializeField] private bool enableFlagshipSlowMovement = true;

    [Header("Role Zones")]
    [SerializeField] [Range(0f, 1f)] private float defendRatio = 0.34f;
    [SerializeField] [Range(0f, 1f)] private float attackRatio = 0.43f;
    [SerializeField] private float defendOrbitRadius = 180f;
    [SerializeField] private float attackOrbitRadius = 220f;
    [SerializeField] private float objectiveOrbitSpeedDegPerSec = 14f;
    [SerializeField] private float roamCorridorHalfWidth = 140f;
    [SerializeField] private float roamProgressSpeed = 0.18f;

    [Header("Reinforcements")]
    [SerializeField] private bool hookReinforcementRequests = true;
    [SerializeField] private bool disableFleetSpawnerAutoFulfill = true;
    [SerializeField] private float reinforcementSpawnRadius = 75f;
    [SerializeField] [Range(1, 10)] private int maxReinforcementsPerRequest = 5;

    [Header("Debug")]
    [SerializeField] private bool drawSpawnRadiusDebug = false;
    [SerializeField] private bool drawSpawnRadiusOnlyWhenSelected = false;
    [SerializeField] private Color fleetSpawnRadiusDebugColor = new Color(0.2f, 0.55f, 1f, 0.95f);
    [SerializeField] private Color reinforcementSpawnRadiusDebugColor = new Color(0.2f, 0.55f, 1f, 0.5f);

    private FleetSpawner fleetSpawner;
    private Transform playerFlagshipTransform;
    private Transform enemyFlagshipTransform;

    private readonly List<SquadRuntime> runtimes = new List<SquadRuntime>(64);
    private readonly Dictionary<EnemySquadController, SquadRuntime> runtimeBySquad =
        new Dictionary<EnemySquadController, SquadRuntime>(64);
    private readonly List<Transform> objectiveProxies = new List<Transform>(64);

    private void Awake()
    {
        fleetSpawner = GetComponent<FleetSpawner>();
        if (fleetSpawner == null)
            fleetSpawner = FleetSpawner.Instance;
    }

    private void OnEnable()
    {
        if (hookReinforcementRequests && fleetSpawner != null)
            fleetSpawner.OnReinforcementRequested += OnFleetReinforcementRequested;
    }

    private void OnDisable()
    {
        if (fleetSpawner != null)
            fleetSpawner.OnReinforcementRequested -= OnFleetReinforcementRequested;
    }

    private void Start()
    {

        if (fleetSpawner != null)
        {
            fleetSpawner.SetTrackedEnemyTeams(new List<int> { EnemyTeamId });
            if (disableFleetSpawnerAutoFulfill)
                fleetSpawner.SetAutoFulfillReinforcementRequests(false);
        }

        if (autoSpawnOnStart)
            SpawnBattle();
    }

    private void Update()
    {
        if (updateObjectiveProxies)
            UpdateObjectiveProxies(Time.deltaTime);

        if (enableSpawnHotkey && Input.GetKeyDown(spawnBattleKey))
            SpawnBattle();
    }

    [ContextMenu("Spawn Battle")]
    public void SpawnBattle()
    {
        if (fleetSpawner == null)
        {
            Debug.LogError("SimpleTeamSpawner: FleetSpawner reference is missing.");
            return;
        }

        if (clearExistingBattleOnSpawn)
            ClearExistingBattleObjects();
        else
            ClearRuntimeData();

        if (!SpawnFlagships())
            return;

        SpawnFleetForTeam(PlayerTeamId);
        SpawnFleetForTeam(EnemyTeamId);
    }

    public void SpawnExtraSquadForTeamIndex(int teamIndex)
    {
        int teamId = teamIndex == 0 ? PlayerTeamId : EnemyTeamId;
        SpawnSingleSquadWithRole(teamId, SquadObjectiveRole.Attack, runtimes.Count);
    }

    public void SpawnAllConfiguredTeams()
    {
        SpawnBattle();
    }

    private bool SpawnFlagships()
    {
        GameObject playerFlagshipPrefab = ResolveFlagshipPrefabForTeam(PlayerTeamId);
        GameObject enemyFlagshipPrefab = ResolveFlagshipPrefabForTeam(EnemyTeamId);
        if (playerFlagshipPrefab == null || enemyFlagshipPrefab == null)
        {
            Debug.LogError("SimpleTeamSpawner: flagship prefab is missing for one or both teams. Assign a shared flagshipPrefab or a team-specific override.");
            return false;
        }

        ResolveFlagshipPositions(out Vector3 playerSpawnPos, out Vector3 enemySpawnPos);
        Vector2 axis = (enemySpawnPos - playerSpawnPos);
        Vector2 axisDir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector2.right;

        GameObject playerObj = Instantiate(
            playerFlagshipPrefab,
            playerSpawnPos,
            Quaternion.FromToRotation(Vector3.up, axisDir),
            transform);

        GameObject enemyObj = Instantiate(
            enemyFlagshipPrefab,
            enemySpawnPos,
            Quaternion.FromToRotation(Vector3.up, -axisDir),
            transform);

        playerFlagshipTransform = playerObj != null ? playerObj.transform : null;
        enemyFlagshipTransform = enemyObj != null ? enemyObj.transform : null;

        ConfigureFlagship(playerObj, PlayerTeamId);
        ConfigureFlagship(enemyObj, EnemyTeamId);


        return playerFlagshipTransform != null && enemyFlagshipTransform != null;
    }
    private void ResolveFlagshipPositions(out Vector3 playerPos, out Vector3 enemyPos)
    {
        float separation = Mathf.Max(300f, fallbackFlagshipSeparation);

        if (playerFlagshipSpawnPoint != null && enemyFlagshipSpawnPoint != null)
        {
            playerPos = playerFlagshipSpawnPoint.position;
            enemyPos = enemyFlagshipSpawnPoint.position;
            if (Vector2.Distance(playerPos, enemyPos) < 50f)
                enemyPos = playerPos + Vector3.right * separation;
            return;
        }

        if (useArenaBoundaryIfAvailable)
        {
            SpaceArenaBoundaryController[] boundaries = FindObjectsOfType<SpaceArenaBoundaryController>(true);
            SpaceArenaBoundaryController boundary = boundaries != null && boundaries.Length > 0 ? boundaries[0] : null;
            if (boundary != null)
            {
                float radius = Mathf.Max(100f, boundary.SafeRadius * Mathf.Clamp(boundarySpawnRadiusRatio, 0.2f, 0.95f));
                Vector2 center = boundary.CenterPosition;
                playerPos = center + Vector2.left * radius;
                enemyPos = center + Vector2.right * radius;
                return;
            }
        }

        if (playerFlagshipSpawnPoint != null)
        {
            playerPos = playerFlagshipSpawnPoint.position;
            enemyPos = playerPos + Vector3.right * separation;
            return;
        }

        if (enemyFlagshipSpawnPoint != null)
        {
            enemyPos = enemyFlagshipSpawnPoint.position;
            playerPos = enemyPos + Vector3.left * separation;
            return;
        }

        Vector3 centerPos = transform.position;
        playerPos = centerPos + Vector3.left * (separation * 0.5f);
        enemyPos = centerPos + Vector3.right * (separation * 0.5f);
    }

    private FlagshipController ConfigureFlagship(GameObject flagshipObject, int teamId)
    {
        if (flagshipObject == null)
            return null;

        TeamAgent agent = flagshipObject.GetComponent<TeamAgent>();
        if (agent != null)
            agent.SetTeam(teamId);

        FlagshipSlowMovement slowMovement = flagshipObject.GetComponent<FlagshipSlowMovement>();
        bool allowFlagshipMovement = enableFlagshipSlowMovement && slowMovement != null;
        if (slowMovement != null)
            slowMovement.enabled = allowFlagshipMovement;

        EnemySpaceshipAI shipAi = flagshipObject.GetComponent<EnemySpaceshipAI>();
        if (shipAi != null)
            shipAi.enabled = false;

        Rigidbody2D rb = flagshipObject.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            RigidbodyConstraints2D freezeTransformConstraints =
                RigidbodyConstraints2D.FreezePositionX |
                RigidbodyConstraints2D.FreezePositionY |
                RigidbodyConstraints2D.FreezeRotation;

            if (allowFlagshipMovement)
                rb.constraints &= ~freezeTransformConstraints;
            else
                rb.constraints |= freezeTransformConstraints;
        }

        FlagshipController controller = flagshipObject.GetComponent<FlagshipController>();
        if (controller != null)
        {
            FlagshipShieldNode[] nodes = flagshipObject.GetComponentsInChildren<FlagshipShieldNode>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null)
                    nodes[i].Bind(controller);
            }
        }

        return controller;
    }

    private void SpawnFleetForTeam(int teamId)
    {
        int count = Mathf.Max(1, squadsPerTeam);
        for (int i = 0; i < count; i++)
        {
            SquadObjectiveRole role = ResolveRoleByIndex(i, count);
            SpawnSingleSquadWithRole(teamId, role, i);
        }
    }

    private void SpawnSingleSquadWithRole(int teamId, SquadObjectiveRole role, int squadIndex)
    {
        if (fleetSpawner == null)
            return;

        Transform flagship = GetOwnFlagshipTransform(teamId);
        if (flagship == null)
            return;

        Vector2 basePos = flagship.position;
        Vector2 offset = Random.insideUnitCircle * Mathf.Max(2f, fleetSpawnRadiusAroundFlagship);
        Vector2 spawnPos = basePos + offset + RandomJitter();

        Transform proxy = CreateObjectiveProxy(teamId, role, squadIndex, spawnPos);

        EnemySquadController squad = fleetSpawner.SpawnSquadForTeam(
            teamId,
            Mathf.Clamp(shipsPerSquad, 1, 10),
            spawnPos,
            formationType,
            Mathf.Max(1f, squadSpacing),
            squadState,
            Mathf.Max(1f, squadEngageDistance),
            Mathf.Max(0.1f, squadAnchorMoveSpeed),
            proxy,
            ResolveShipPrefabForTeam(teamId));

        if (squad == null)
        {
            if (proxy != null)
                Destroy(proxy.gameObject);
            return;
        }

        RegisterRuntime(squad, teamId, role, proxy);
    }

    private void RegisterRuntime(EnemySquadController squad, int teamId, SquadObjectiveRole role, Transform proxy)
    {
        if (squad == null || proxy == null)
            return;

        SquadRuntime runtime = new SquadRuntime
        {
            Squad = squad,
            TeamId = teamId,
            Role = role,
            Proxy = proxy,
            OrbitAngleDeg = Random.Range(0f, 360f),
            OrbitSpeedDeg = Mathf.Max(2f, objectiveOrbitSpeedDegPerSec) * Random.Range(0.8f, 1.2f),
            OrbitDirection = Random.value < 0.5f ? -1f : 1f,
            RoamT = Random.value,
            RoamSpeed = Mathf.Max(0.02f, roamProgressSpeed) * Random.Range(0.8f, 1.2f),
            RoamPhase = Random.Range(0f, Mathf.PI * 2f),
            RoamDirection = Random.value < 0.5f ? -1f : 1f
        };

        runtimes.Add(runtime);
        runtimeBySquad[squad] = runtime;
    }

    private Transform CreateObjectiveProxy(int teamId, SquadObjectiveRole role, int index, Vector2 initialPosition)
    {
        GameObject go = new GameObject($"SquadProxy_T{teamId}_{role}_{index + 1}");
        go.transform.SetParent(transform, true);
        go.transform.position = initialPosition;
        objectiveProxies.Add(go.transform);
        return go.transform;
    }

    private void UpdateObjectiveProxies(float deltaTime)
    {
        for (int i = runtimes.Count - 1; i >= 0; i--)
        {
            SquadRuntime runtime = runtimes[i];
            if (runtime == null || runtime.Squad == null || runtime.Proxy == null || !runtime.Squad.isActiveAndEnabled)
            {
                RemoveRuntimeAt(i);
                continue;
            }

            Vector2 nextPos = ComputeRolePosition(runtime, deltaTime);
            runtime.Proxy.position = nextPos;

            if (runtime.Squad.FocusTarget != runtime.Proxy)
                runtime.Squad.SetFocusTarget(runtime.Proxy);

            if (runtime.Squad.CurrentState != EnemySquadState.Engage)
                runtime.Squad.SetState(EnemySquadState.Engage);
        }
    }

    private Vector2 ComputeRolePosition(SquadRuntime runtime, float deltaTime)
    {
        Transform ownFlagship = GetOwnFlagshipTransform(runtime.TeamId);
        Transform enemyFlagship = GetEnemyFlagshipTransform(runtime.TeamId);
        if (ownFlagship == null || enemyFlagship == null)
            return runtime.Proxy.position;

        Vector2 ownPos = ownFlagship.position;
        Vector2 enemyPos = enemyFlagship.position;

        if (runtime.Role == SquadObjectiveRole.Defend)
        {
            runtime.OrbitAngleDeg += runtime.OrbitSpeedDeg * runtime.OrbitDirection * deltaTime;
            Vector2 dir = DirectionFromDegrees(runtime.OrbitAngleDeg);
            return ownPos + dir * Mathf.Max(20f, defendOrbitRadius);
        }

        if (runtime.Role == SquadObjectiveRole.Attack)
        {
            runtime.OrbitAngleDeg += runtime.OrbitSpeedDeg * runtime.OrbitDirection * deltaTime;
            Vector2 dir = DirectionFromDegrees(runtime.OrbitAngleDeg);
            return enemyPos + dir * Mathf.Max(20f, attackOrbitRadius);
        }

        runtime.RoamT += runtime.RoamSpeed * runtime.RoamDirection * deltaTime;
        if (runtime.RoamT >= 1f)
        {
            runtime.RoamT = 1f;
            runtime.RoamDirection = -1f;
        }
        else if (runtime.RoamT <= 0f)
        {
            runtime.RoamT = 0f;
            runtime.RoamDirection = 1f;
        }

        Vector2 line = enemyPos - ownPos;
        Vector2 forward = line.sqrMagnitude > 0.0001f ? line.normalized : Vector2.right;
        Vector2 right = new Vector2(forward.y, -forward.x);

        float t = Mathf.Lerp(0.2f, 0.8f, runtime.RoamT);
        float wave = Mathf.Sin((runtime.RoamT * Mathf.PI * 2f) + runtime.RoamPhase);
        float lateral = wave * Mathf.Max(5f, roamCorridorHalfWidth);

        return Vector2.Lerp(ownPos, enemyPos, t) + right * lateral;
    }

    private static Vector2 DirectionFromDegrees(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private SquadObjectiveRole ResolveRoleByIndex(int squadIndex, int totalSquads)
    {
        float normalized = (squadIndex + 0.5f) / Mathf.Max(1, totalSquads);
        float defendCut = Mathf.Clamp01(defendRatio);
        float attackCut = Mathf.Clamp01(defendRatio + attackRatio);

        if (normalized <= defendCut)
            return SquadObjectiveRole.Defend;

        if (normalized <= attackCut)
            return SquadObjectiveRole.Attack;

        return SquadObjectiveRole.Roam;
    }

    private GameObject ResolveShipPrefabForTeam(int teamId)
    {
        if (teamId == PlayerTeamId)
            return playerTeamShipPrefab != null ? playerTeamShipPrefab : sharedShipPrefab;

        if (teamId == EnemyTeamId)
            return enemyTeamShipPrefab != null ? enemyTeamShipPrefab : sharedShipPrefab;

        return sharedShipPrefab;
    }


    private GameObject ResolveFlagshipPrefabForTeam(int teamId)
    {
        if (teamId == PlayerTeamId)
            return playerTeamFlagshipPrefab != null ? playerTeamFlagshipPrefab : flagshipPrefab;

        if (teamId == EnemyTeamId)
            return enemyTeamFlagshipPrefab != null ? enemyTeamFlagshipPrefab : flagshipPrefab;

        return flagshipPrefab;
    }

    private Transform GetOwnFlagshipTransform(int teamId)
    {
        return teamId == PlayerTeamId ? playerFlagshipTransform : enemyFlagshipTransform;
    }

    private Transform GetEnemyFlagshipTransform(int teamId)
    {
        return teamId == PlayerTeamId ? enemyFlagshipTransform : playerFlagshipTransform;
    }

    private void OnFleetReinforcementRequested(ReinforcementRequest request)
    {
        if (!hookReinforcementRequests || fleetSpawner == null || !request.IsValid)
            return;

        EnemySquadController squad = request.Squad;
        if (squad == null || !squad.isActiveAndEnabled)
            return;

        if (!runtimeBySquad.TryGetValue(squad, out SquadRuntime runtime) || runtime == null)
        {
            Transform proxy = CreateObjectiveProxy(request.TeamId, SquadObjectiveRole.Attack, runtimes.Count, squad.transform.position);
            RegisterRuntime(squad, request.TeamId, SquadObjectiveRole.Attack, proxy);
            if (squad.FocusTarget != proxy)
                squad.SetFocusTarget(proxy);
            runtimeBySquad.TryGetValue(squad, out runtime);
        }

        Transform ownFlagship = GetOwnFlagshipTransform(request.TeamId);
        if (ownFlagship == null)
            return;

        int toSpawn = Mathf.Clamp(request.MissingCount, 1, maxReinforcementsPerRequest);
        GameObject prefab = ResolveShipPrefabForTeam(request.TeamId);

        for (int i = 0; i < toSpawn; i++)
        {
            if (squad == null || !squad.isActiveAndEnabled)
                break;

            Vector2 spawnPos = (Vector2)ownFlagship.position + Random.insideUnitCircle * Mathf.Max(1f, reinforcementSpawnRadius);
            GameObject ship = fleetSpawner.SpawnShipForTeam(
                request.TeamId,
                spawnPos,
                prefab,
                assignAutoSquad: false,
                focusTargetOverride: runtime != null ? runtime.Proxy : null);

            if (ship == null)
                continue;

            EnemySquadMember member = ship.GetComponent<EnemySquadMember>();
            if (member == null)
                member = ship.AddComponent<EnemySquadMember>();

            squad.RegisterMember(member, EnemySquadRole.Wingman);
        }

        fleetSpawner.RemovePendingReinforcementRequest(squad);
    }

    private Vector2 RandomJitter()
    {
        float x = Random.Range(-Mathf.Abs(squadSpawnJitter.x), Mathf.Abs(squadSpawnJitter.x));
        float y = Random.Range(-Mathf.Abs(squadSpawnJitter.y), Mathf.Abs(squadSpawnJitter.y));
        return new Vector2(x, y);
    }

    private void ClearExistingBattleObjects()
    {
        ClearRuntimeData();

        HashSet<GameObject> toDestroy = new HashSet<GameObject>();
        IReadOnlyList<TeamAgent> agents = TeamRegistry.Agents;

        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            if (agent.CompareTag("Player"))
                continue;

            if (agent.TeamId == PlayerTeamId || agent.TeamId == EnemyTeamId)
                toDestroy.Add(agent.gameObject);
        }

        foreach (GameObject go in toDestroy)
        {
            if (go != null)
                Destroy(go);
        }

        IReadOnlyList<EnemySquadController> squads = EnemySquadController.Active;
        for (int i = squads.Count - 1; i >= 0; i--)
        {
            EnemySquadController squad = squads[i];
            if (squad == null)
                continue;

            int teamId = squad.TeamId;
            if (teamId == PlayerTeamId || teamId == EnemyTeamId)
                Destroy(squad.gameObject);
        }

        playerFlagshipTransform = null;
        enemyFlagshipTransform = null;
    }

    private void ClearRuntimeData()
    {
        for (int i = 0; i < objectiveProxies.Count; i++)
        {
            Transform proxy = objectiveProxies[i];
            if (proxy != null)
                Destroy(proxy.gameObject);
        }

        objectiveProxies.Clear();
        runtimes.Clear();
        runtimeBySquad.Clear();
    }

    private void RemoveRuntimeAt(int index)
    {
        if (index < 0 || index >= runtimes.Count)
            return;

        SquadRuntime runtime = runtimes[index];
        if (runtime != null)
        {
            if (runtime.Squad != null)
                runtimeBySquad.Remove(runtime.Squad);

            if (runtime.Proxy != null)
            {
                objectiveProxies.Remove(runtime.Proxy);
                Destroy(runtime.Proxy.gameObject);
            }
        }

        runtimes.RemoveAt(index);
    }

    private void OnDrawGizmos()
    {
        if (!drawSpawnRadiusDebug || drawSpawnRadiusOnlyWhenSelected)
            return;

        DrawSpawnRadiusDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSpawnRadiusDebug)
            return;

        DrawSpawnRadiusDebugGizmos();
    }

    private void DrawSpawnRadiusDebugGizmos()
    {
        DrawSpawnRings(playerFlagshipTransform != null ? playerFlagshipTransform : playerFlagshipSpawnPoint);
        DrawSpawnRings(enemyFlagshipTransform != null ? enemyFlagshipTransform : enemyFlagshipSpawnPoint);
    }

    private void DrawSpawnRings(Transform anchor)
    {
        if (anchor == null)
            return;

        float fleetRadius = Mathf.Max(0f, fleetSpawnRadiusAroundFlagship);
        if (fleetRadius > 0.01f)
        {
            Gizmos.color = fleetSpawnRadiusDebugColor;
            Gizmos.DrawWireSphere(anchor.position, fleetRadius);
        }

        float reinforceRadius = Mathf.Max(0f, reinforcementSpawnRadius);
        if (reinforceRadius > 0.01f)
        {
            Gizmos.color = reinforcementSpawnRadiusDebugColor;
            Gizmos.DrawWireSphere(anchor.position, reinforceRadius);
        }
    }
}





