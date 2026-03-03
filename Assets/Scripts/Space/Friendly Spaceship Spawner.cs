using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

public class FriendlySpaceshipSpawner : MonoBehaviour
{
    public static FriendlySpaceshipSpawner Instance { get; private set; }

    public event Action<int> OnAliveFriendliesChanged; // Pass the new alive friendlies count

    [SerializeField] private GameObject[] teamPrefabs; // Array of team prefabs to spawn from

    [SerializeField]
    private int[] teamSpawnWeights; // Corresponding spawn weights for each prefab (must be same length as teamPrefabs)

    private GameObject player;
    private int playerTeamId = 0; // Assuming player is always team 0
    private int aliveFriendlies = 0;
    [SerializeField] private float minSpawnRadius = 15f;
    [SerializeField] private float maxSpawnRadius = 30f;

    [SerializeField] private float
        initialSpawnDelay = 5f; // Time to wait before starting the first spawn, giving player time to get ready

    [Header("Pooling")] [SerializeField] private int defaultPoolCapacity = 30;
    [SerializeField] private int maxPoolSize = 70;


    [Header("Hierarchy Parents ")] [SerializeField]
    private Transform activeTeamatesRoot;

    [SerializeField] private Transform pooledRoot;

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();

    public int AliveFriendlies
    {
        get { return aliveFriendlies; }
    }


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
            Debug.LogError("Player not found in the scene! Make sure the player has the 'Player' tag.");

        }

        // Try to get the player's team ID from their TeamAgent component, if it exists
        var playerTeamAgent = player.GetComponent<TeamAgent>();
        if (playerTeamAgent != null)
        {
            playerTeamId = playerTeamAgent.TeamId;
        }


        // Create parent objects for organization if they are not assigned in the inspector
        if (activeTeamatesRoot == null)
        {
            var go = new GameObject("Teamates_Active");
            activeTeamatesRoot = go.transform;
        }

        if (pooledRoot == null)
        {
            var go = new GameObject("Teamates_Pooled");
            go.SetActive(false);
            pooledRoot = go.transform;
        }

        if (teamPrefabs != null && teamPrefabs.Length > 0)
        {
            foreach (var prefab in teamPrefabs)
            {
                if (prefab != null)
                {
                    Prewarm(prefab, Mathf.Max(0, defaultPoolCapacity)); // Prewarm the pool
                }
            }
        }

        StartCoroutine(RunSpawner());
    }

    private IEnumerator RunSpawner()
    {
        yield return new WaitForSeconds(initialSpawnDelay);
        for (int i = 0; i < teamSpawnWeights.Length; i++)
        {
            if (teamPrefabs[i] != null)
            {
                for (int j = 0; j < teamSpawnWeights[i]; j++)
                {
                    Spawn(teamPrefabs[i]);
                    yield return new WaitForSeconds(0.5f);
                } // Delay between spawns, can be adjusted or randomized as needed
            }
        }
    }

    private void Spawn(GameObject prefab)
    {
        if (prefab == null) return;

        Vector3 spawnPosition = GetRandomSpawnPosiiton();
        var pool = GetPool(prefab);
        var go = pool.Get();

        go.transform.SetParent(activeTeamatesRoot, true);
        go.transform.position = spawnPosition;
        go.transform.rotation = Quaternion.identity;
        
        go.SetActive(true);

        // Assign a team (so enemies can fight each other)
        if (go.TryGetComponent<TeamAgent>(out var agent))
        {
            agent.SetTeam(playerTeamId);
        }

        // Re-init AI every spawn (pool-safe)
        if (go.TryGetComponent<EnemySpaceshipAI>(out var ai))
            ai.ResetForSpawn(player);

        if (go.TryGetComponent<EnemySpaceshipCombatAI>(out var combat))
            combat.ResetForSpawn();


        aliveFriendlies++;
        OnAliveFriendliesChanged?.Invoke(aliveFriendlies);
    }

    private Vector3 GetRandomSpawnPosiiton()
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector3 spawnPosition = player.transform.position + (Vector3)(randomDirection * randomDistance);
        return spawnPosition;
    }

    public void NotifyTeamateGone()
    {
        aliveFriendlies = Mathf.Max(0, aliveFriendlies - 1);
        OnAliveFriendliesChanged?.Invoke(aliveFriendlies);
    }

    /// <summary>
    ///  Prewarms the object pool for a given prefab by instantiating and immediately releasing a specified number of instances. This helps to reduce runtime instantiation overhead when the game starts, ensuring smoother performance during gameplay.
    /// </summary>
    /// <param name="prefab"></param>
    /// <param name="count"></param>
    private void Prewarm(GameObject prefab, int count)
    {
        var pool = GetPool(prefab);
        for (int i = 0; i < count; i++)
        {
            var go = pool.Get();
            pool.Release(go);
        }
    }

    /// <summary>
    ///  Gets or creates an object pool for the given prefab. Each prefab has its own pool to ensure type safety and proper management of pooled objects.
    /// </summary>
    /// <param name="prefab"></param>
    /// <returns></returns>
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

                // Ensure the prefab has a PooledTeamate component for pooling management
                var pe = go.GetComponent<PooledTeamate>();
                if (pe == null) pe = go.AddComponent<PooledTeamate>();
                pe.Init(this, pool);

                return go;
            },
            actionOnGet: go =>
            {
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
}