using System;
using System.Collections.Generic;
using UnityEngine;
using System;
using Random = UnityEngine.Random;


[DisallowMultipleComponent]
[RequireComponent(typeof(FleetSpawner))]
[AddComponentMenu("Space/Spawning/Simple Team Spawner")]
public class SimpleTeamSpawner : MonoBehaviour
{
    [Serializable]
    public class SimpleTeamEntry
    {
        public string label = "Team";
        public int teamId = 1;
        [Range(1, 10)] public int squadSize = 5;
        [Min(1)] public int initialSquads = 1;
        public GameObject shipPrefab;
        public Transform spawnPoint;
        public Transform focusTarget;
    }

    [Header("Teams")]
    [SerializeField] private List<SimpleTeamEntry> teams = new List<SimpleTeamEntry>();
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private bool disableLegacySpawnersOnStart = true;

    [Header("Shared Squad Settings")]
    [SerializeField] private EnemySquadFormationType formationType = EnemySquadFormationType.Vee;
    [SerializeField] private EnemySquadState squadState = EnemySquadState.Engage;
    [SerializeField] private float squadSpacing = 5f;
    [SerializeField] private float squadEngageDistance = 18f;
    [SerializeField] private float squadAnchorMoveSpeed = 14f;
    [SerializeField] private float squadClusterSpacing = 14f;

    [Header("Fallback Spawn Placement")]
    [SerializeField] private SpaceArenaBoundaryController arenaBoundary;
    [SerializeField] [Range(0.05f, 2f)] private float boundaryRadiusMultiplier = 0.85f;
    [SerializeField] private float defaultTeamSeparation = 5000f;
    [SerializeField] private Vector2 localJitter = new Vector2(8f, 8f);

    [Header("Hotkeys (Spawn One Extra Squad)")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private KeyCode spawnTeam1Key = KeyCode.U;
    [SerializeField] private KeyCode spawnTeam2Key = KeyCode.I;
    [SerializeField] private KeyCode spawnTeam3Key = KeyCode.O;

    private FleetSpawner fleetSpawner;
    private GameObject player;
    private int playerTeamId;

    private void Awake()
    {
        fleetSpawner = GetComponent<FleetSpawner>();
        if (fleetSpawner == null)
            fleetSpawner = FleetSpawner.Instance;

        ResolveRuntimeRefs();
    }

    private void Start()
    {
        ResolveRuntimeRefs();

        if (disableLegacySpawnersOnStart)
            DisableLegacySpawners();

        ApplyTrackedEnemyTeamsToFleet();

        if (autoSpawnOnStart)
            SpawnAllConfiguredTeams();
    }

    private void Update()
    {
        if (!enableHotkeys)
            return;

        if (Input.GetKeyDown(spawnTeam1Key))
            SpawnExtraSquadForTeamIndex(0);

        if (Input.GetKeyDown(spawnTeam2Key))
            SpawnExtraSquadForTeamIndex(1);

        if (Input.GetKeyDown(spawnTeam3Key))
            SpawnExtraSquadForTeamIndex(2);
    }

    [ContextMenu("Spawn All Configured Teams")]
    public void SpawnAllConfiguredTeams()
    {
        if (fleetSpawner == null)
        {
            Debug.LogError("SimpleTeamSpawner: FleetSpawner reference is missing.");
            return;
        }

        for (int i = 0; i < teams.Count; i++)
        {
            SimpleTeamEntry entry = teams[i];
            if (entry == null)
                continue;

            SpawnTeamByIndex(i, Mathf.Max(1, entry.initialSquads));
        }
    }

    public void SpawnExtraSquadForTeamIndex(int teamIndex)
    {
        SpawnTeamByIndex(teamIndex, 1);
    }

    private void SpawnTeamByIndex(int teamIndex, int squadsToSpawn)
    {
        if (fleetSpawner == null)
            return;

        if (teamIndex < 0 || teamIndex >= teams.Count)
            return;

        SimpleTeamEntry entry = teams[teamIndex];
        if (entry == null)
            return;

        int squadCount = Mathf.Max(1, squadsToSpawn);
        int squadSize = Mathf.Clamp(entry.squadSize, 1, 10);

        for (int squadIndex = 0; squadIndex < squadCount; squadIndex++)
        {
            Vector3 basePos = ResolveBasePositionForTeamIndex(teamIndex);
            Vector3 clusterOffset = ComputeSquadOffset(squadIndex);
            Vector3 spawnPos = basePos + clusterOffset + (Vector3)RandomLocalJitter();

            fleetSpawner.SpawnSquadForTeam(
                entry.teamId,
                squadSize,
                spawnPos,
                formationType,
                squadSpacing,
                squadState,
                squadEngageDistance,
                squadAnchorMoveSpeed,
                entry.focusTarget,
                entry.shipPrefab);
        }
    }

    private Vector3 ResolveBasePositionForTeamIndex(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= teams.Count)
            return Vector3.zero;

        SimpleTeamEntry entry = teams[teamIndex];
        if (entry != null && entry.spawnPoint != null)
            return entry.spawnPoint.position;

        if (arenaBoundary == null)
            arenaBoundary = FindObjectOfType<SpaceArenaBoundaryController>();

        if (arenaBoundary != null && arenaBoundary.SafeRadius > 0.01f)
        {
            int totalTeams = Mathf.Max(1, teams.Count);
            float angleDeg = (360f / totalTeams) * teamIndex;
            float radius = Mathf.Max(1f, arenaBoundary.SafeRadius * Mathf.Max(0.05f, boundaryRadiusMultiplier));
            Vector2 offset = Quaternion.Euler(0f, 0f, angleDeg) * Vector2.right * radius;
            return (Vector3)arenaBoundary.CenterPosition + (Vector3)offset;
        }

        Vector3 center = player != null ? player.transform.position : Vector3.zero;
        float separation = Mathf.Max(100f, defaultTeamSeparation);
        return center + new Vector3(separation * teamIndex, 0f, 0f);
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

    private Vector2 RandomLocalJitter()
    {
        float x = Random.Range(-Mathf.Abs(localJitter.x), Mathf.Abs(localJitter.x));
        float y = Random.Range(-Mathf.Abs(localJitter.y), Mathf.Abs(localJitter.y));
        return new Vector2(x, y);
    }

    private void ResolveRuntimeRefs()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            playerTeamId = 0;
            return;
        }

        TeamAgent playerTeamAgent = player.GetComponent<TeamAgent>();
        playerTeamId = playerTeamAgent != null ? playerTeamAgent.TeamId : 0;
    }

    private void ApplyTrackedEnemyTeamsToFleet()
    {
        if (fleetSpawner == null)
            return;

        HashSet<int> trackedEnemyTeams = new HashSet<int>();
        for (int i = 0; i < teams.Count; i++)
        {
            SimpleTeamEntry entry = teams[i];
            if (entry == null)
                continue;

            if (entry.teamId == playerTeamId)
                continue;

            trackedEnemyTeams.Add(entry.teamId);
        }

        fleetSpawner.SetTrackedEnemyTeams(new List<int>(trackedEnemyTeams));
    }

    private void DisableLegacySpawners()
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
