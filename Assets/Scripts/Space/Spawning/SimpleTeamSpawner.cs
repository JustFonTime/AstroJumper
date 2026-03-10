using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(FleetSpawner))]
[AddComponentMenu("Space/Spawning/Simple Team Spawner")]
public class SimpleTeamSpawner : MonoBehaviour
{
    [Header("Battle Setup")]
    [SerializeField] private int teamAId = 1;
    [SerializeField] private int teamBId = 2;
    [SerializeField] [Range(1, 10)] private int squadsPerFleet = 2;
    [SerializeField] [Range(1, 10)] private int shipsPerSquad = 5;
    [SerializeField] private GameObject sharedShipPrefab;
    [SerializeField] private GameObject teamAShipPrefab;
    [SerializeField] private GameObject teamBShipPrefab;

    [Header("Formation")]
    [SerializeField] private EnemySquadFormationType formationType = EnemySquadFormationType.Vee;
    [SerializeField] private EnemySquadState squadState = EnemySquadState.Engage;
    [SerializeField] private float squadSpacing = 5f;
    [SerializeField] private float squadEngageDistance = 18f;
    [SerializeField] private float squadAnchorMoveSpeed = 14f;
    [SerializeField] private float squadClusterSpacing = 16f;
    [SerializeField] private Vector2 squadJitter = new Vector2(4f, 4f);

    [Header("Spawn Points")]
    [SerializeField] private Transform teamASpawnPoint;
    [SerializeField] private Transform teamBSpawnPoint;
    [SerializeField] private float defaultFleetSeparation = 220f;

    [Header("Runtime")]
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private bool disableOtherSpawnersOnStart = true;
    [SerializeField] private bool updateFleetFocusFromLiveCenters = true;
    [SerializeField] private bool enableSpawnHotkey = true;
    [SerializeField] private KeyCode spawnBattleKey = KeyCode.B;
    [SerializeField] private bool setTrackedEnemyTeamsOnFleetSpawner = true;

    private FleetSpawner fleetSpawner;
    private Transform teamAFocusProxy;
    private Transform teamBFocusProxy;

    private void Awake()
    {
        fleetSpawner = GetComponent<FleetSpawner>();
        if (fleetSpawner == null)
            fleetSpawner = FleetSpawner.Instance;
    }

    private void Start()
    {
        if (disableOtherSpawnersOnStart)
            DisableOtherSpawners();

        EnsureFocusProxies();

        if (setTrackedEnemyTeamsOnFleetSpawner && fleetSpawner != null)
            fleetSpawner.SetTrackedEnemyTeams(new List<int> { teamAId, teamBId });

        if (autoSpawnOnStart)
            SpawnBattle();
    }

    private void Update()
    {
        if (updateFleetFocusFromLiveCenters)
            UpdateFocusTargetsFromLiveTeams();

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

        if (teamAId == teamBId)
        {
            Debug.LogError("SimpleTeamSpawner: teamAId and teamBId must be different.");
            return;
        }

        EnsureFocusProxies();

        Vector3 teamABase = ResolveSpawnBase(isTeamA: true);
        Vector3 teamBBase = ResolveSpawnBase(isTeamA: false);

        SpawnFleet(teamAId, teamABase, teamBFocusProxy, ResolveTeamPrefab(isTeamA: true));
        SpawnFleet(teamBId, teamBBase, teamAFocusProxy, ResolveTeamPrefab(isTeamA: false));

        UpdateFocusTargetsFromLiveTeams();
    }

    public void SpawnExtraSquadForTeamIndex(int teamIndex)
    {
        if (fleetSpawner == null)
            return;

        EnsureFocusProxies();

        if (teamIndex == 0)
        {
            SpawnSingleSquad(teamAId, ResolveSpawnBase(isTeamA: true), teamBFocusProxy, ResolveTeamPrefab(isTeamA: true), 0);
            return;
        }

        if (teamIndex == 1)
            SpawnSingleSquad(teamBId, ResolveSpawnBase(isTeamA: false), teamAFocusProxy, ResolveTeamPrefab(isTeamA: false), 0);
    }

    public void SpawnAllConfiguredTeams()
    {
        SpawnBattle();
    }

    private void SpawnFleet(int teamId, Vector3 basePosition, Transform focusTarget, GameObject prefab)
    {
        int count = Mathf.Max(1, squadsPerFleet);
        for (int squadIndex = 0; squadIndex < count; squadIndex++)
            SpawnSingleSquad(teamId, basePosition, focusTarget, prefab, squadIndex);
    }

    private void SpawnSingleSquad(
        int teamId,
        Vector3 basePosition,
        Transform focusTarget,
        GameObject prefab,
        int squadIndex)
    {
        Vector3 spawnPos = basePosition + ComputeSquadOffset(squadIndex) + (Vector3)RandomJitter();
        fleetSpawner.SpawnSquadForTeam(
            teamId,
            Mathf.Clamp(shipsPerSquad, 1, 10),
            spawnPos,
            formationType,
            Mathf.Max(1f, squadSpacing),
            squadState,
            Mathf.Max(1f, squadEngageDistance),
            Mathf.Max(0.1f, squadAnchorMoveSpeed),
            focusTarget,
            prefab);
    }

    private Vector3 ResolveSpawnBase(bool isTeamA)
    {
        Transform spawn = isTeamA ? teamASpawnPoint : teamBSpawnPoint;
        if (spawn != null)
            return spawn.position;

        float halfSeparation = Mathf.Max(10f, defaultFleetSeparation) * 0.5f;
        return isTeamA ? Vector3.left * halfSeparation : Vector3.right * halfSeparation;
    }

    private GameObject ResolveTeamPrefab(bool isTeamA)
    {
        GameObject specific = isTeamA ? teamAShipPrefab : teamBShipPrefab;
        return specific != null ? specific : sharedShipPrefab;
    }

    private Vector2 RandomJitter()
    {
        float x = Random.Range(-Mathf.Abs(squadJitter.x), Mathf.Abs(squadJitter.x));
        float y = Random.Range(-Mathf.Abs(squadJitter.y), Mathf.Abs(squadJitter.y));
        return new Vector2(x, y);
    }

    private Vector3 ComputeSquadOffset(int squadIndex)
    {
        if (squadIndex <= 0)
            return Vector3.zero;

        int ringSize = 6;
        int ring = ((squadIndex - 1) / ringSize) + 1;
        int indexInRing = (squadIndex - 1) % ringSize;
        float angleDeg = (360f / ringSize) * indexInRing;
        float radius = Mathf.Max(1f, squadClusterSpacing) * ring;
        Vector2 offset = Quaternion.Euler(0f, 0f, angleDeg) * Vector2.right * radius;
        return offset;
    }

    private void EnsureFocusProxies()
    {
        if (teamAFocusProxy == null)
            teamAFocusProxy = CreateFocusProxy("TeamA_FocusProxy");

        if (teamBFocusProxy == null)
            teamBFocusProxy = CreateFocusProxy("TeamB_FocusProxy");

        if (teamAFocusProxy != null)
            teamAFocusProxy.position = ResolveSpawnBase(isTeamA: true);

        if (teamBFocusProxy != null)
            teamBFocusProxy.position = ResolveSpawnBase(isTeamA: false);
    }

    private Transform CreateFocusProxy(string focusName)
    {
        GameObject go = new GameObject(focusName);
        go.transform.SetParent(transform, true);
        return go.transform;
    }

    private void UpdateFocusTargetsFromLiveTeams()
    {
        if (teamAFocusProxy == null || teamBFocusProxy == null)
            return;

        if (TryGetTeamCenter(teamAId, out Vector3 teamACenter))
            teamAFocusProxy.position = teamACenter;

        if (TryGetTeamCenter(teamBId, out Vector3 teamBCenter))
            teamBFocusProxy.position = teamBCenter;
    }

    private static bool TryGetTeamCenter(int teamId, out Vector3 center)
    {
        center = Vector3.zero;
        int count = 0;

        IReadOnlyList<TeamAgent> agents = TeamRegistry.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled || agent.TeamId != teamId)
                continue;

            center += agent.transform.position;
            count++;
        }

        if (count <= 0)
            return false;

        center /= count;
        return true;
    }

    private void DisableOtherSpawners()
    {
#pragma warning disable CS0618
        EnemySpaceshipSpawner[] enemySpawners = FindObjectsOfType<EnemySpaceshipSpawner>(true);
        for (int i = 0; i < enemySpawners.Length; i++)
        {
            EnemySpaceshipSpawner spawner = enemySpawners[i];
            if (spawner != null)
                spawner.enabled = false;
        }

        FriendlySpaceshipSpawner[] friendlySpawners = FindObjectsOfType<FriendlySpaceshipSpawner>(true);
        for (int i = 0; i < friendlySpawners.Length; i++)
        {
            FriendlySpaceshipSpawner spawner = friendlySpawners[i];
            if (spawner != null)
                spawner.enabled = false;
        }
#pragma warning restore CS0618
    }
}
