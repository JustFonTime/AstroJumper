using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpaceArenaBoundaryController : MonoBehaviour
{
    [Header("Center")]
    [SerializeField] private Transform center;
    [SerializeField] private string centerTag = "Player";
    [SerializeField] private bool autoFindCenterOnEnable = true;

    [Header("Boundary")]
    [SerializeField] private float safeRadius = 350f;
    [SerializeField] private float graceSecondsOutside = 10f;
    [SerializeField] private bool includePlayer = false;

    [Header("Damage")]
    [SerializeField] private int lethalDamage = 1000000;
    [SerializeField] private float checkInterval = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRadius = false;
    [SerializeField] private Color debugRadiusColor = new Color(0.2f, 0.95f, 0.95f, 0.9f);

    private readonly Dictionary<SpaceshipHealthComponent, float> outsideTimers = new Dictionary<SpaceshipHealthComponent, float>(256);
    private readonly List<SpaceshipHealthComponent> staleHealthBuffer = new List<SpaceshipHealthComponent>(64);
    private float checkTimer;

    public float SafeRadius => Mathf.Max(0f, safeRadius);
    public Vector2 CenterPosition => center != null ? (Vector2)center.position : (Vector2)transform.position;

    private void OnEnable()
    {
        ResolveCenter(force: false);
        outsideTimers.Clear();
        checkTimer = Random.Range(0f, Mathf.Max(0.05f, checkInterval));
    }

    private void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f)
            return;

        checkTimer = Mathf.Max(0.05f, checkInterval);
        TickBoundary();
    }

    public void SetCenter(Transform newCenter)
    {
        center = newCenter;
    }

    private void ResolveCenter(bool force = false)
    {
        if (!force && center != null)
            return;
        if (!force && !autoFindCenterOnEnable)
            return;

        if (string.IsNullOrWhiteSpace(centerTag))
            return;

        GameObject found = GameObject.FindGameObjectWithTag(centerTag);
        if (found != null)
            center = found.transform;
    }

    private void TickBoundary()
    {
        if (safeRadius <= 0.01f)
            return;

        ResolveCenter();
        Vector2 centerPos = center != null ? (Vector2)center.position : (Vector2)transform.position;
        float safeRadiusSq = safeRadius * safeRadius;
        float dt = Mathf.Max(0.05f, checkInterval);

        IReadOnlyList<TeamAgent> agents = TeamRegistry.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            if (!includePlayer && agent.CompareTag("Player"))
                continue;

            SpaceshipHealthComponent health = agent.GetComponent<SpaceshipHealthComponent>();
            if (health == null || !health.isActiveAndEnabled)
                continue;

            float d2 = ((Vector2)agent.transform.position - centerPos).sqrMagnitude;
            if (d2 <= safeRadiusSq)
            {
                outsideTimers.Remove(health);
                continue;
            }

            float timer = 0f;
            outsideTimers.TryGetValue(health, out timer);
            timer += dt;

            if (timer >= graceSecondsOutside)
            {
                health.TakeDamage(lethalDamage);
                outsideTimers.Remove(health);
                continue;
            }

            outsideTimers[health] = timer;
        }

        CleanupStaleEntries();
    }

    private void CleanupStaleEntries()
    {
        staleHealthBuffer.Clear();

        foreach (KeyValuePair<SpaceshipHealthComponent, float> pair in outsideTimers)
        {
            SpaceshipHealthComponent health = pair.Key;
            if (health == null || !health.isActiveAndEnabled)
                staleHealthBuffer.Add(health);
        }

        for (int i = 0; i < staleHealthBuffer.Count; i++)
            outsideTimers.Remove(staleHealthBuffer[i]);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugRadius || safeRadius <= 0.01f)
            return;

        Gizmos.color = debugRadiusColor;
        Vector3 centerPos = center != null ? center.position : transform.position;
        Gizmos.DrawWireSphere(centerPos, safeRadius);
    }
}


