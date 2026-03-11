using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TeamAgent))]
public class FlagshipNoFlyZone : MonoBehaviour
{
    private static readonly List<FlagshipNoFlyZone> active = new List<FlagshipNoFlyZone>(16);
    public static IReadOnlyList<FlagshipNoFlyZone> Active => active;

    [Header("Zone")]
    [SerializeField] private float radius = 130f;
    [SerializeField] private float buffer = 25f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;
    [SerializeField] private bool drawOnlyWhenSelected = false;
    [SerializeField] private Color coreColor = new Color(1f, 0.15f, 0.15f, 0.95f);
    [SerializeField] private Color bufferColor = new Color(1f, 0.15f, 0.15f, 0.45f);

    private TeamAgent teamAgent;

    public int TeamId => teamAgent != null ? teamAgent.TeamId : -1;
    public Vector2 Center => transform.position;
    public float Radius => Mathf.Max(1f, radius);
    public float Buffer => Mathf.Max(0f, buffer);
    public float EffectiveRadius => Radius + Buffer;

    private void Awake()
    {
        EnsureTeamAgent();
    }

    private void OnEnable()
    {
        EnsureTeamAgent();
        if (!active.Contains(this))
            active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    private void OnValidate()
    {
        radius = Mathf.Max(1f, radius);
        buffer = Mathf.Max(0f, buffer);
    }

    public void Configure(float newRadius, float newBuffer, bool debugEnabled)
    {
        radius = Mathf.Max(1f, newRadius);
        buffer = Mathf.Max(0f, newBuffer);
        drawDebug = debugEnabled;
    }

    private void EnsureTeamAgent()
    {
        if (teamAgent == null)
            teamAgent = GetComponent<TeamAgent>();
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug || drawOnlyWhenSelected)
            return;

        DrawZoneGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        DrawZoneGizmos();
    }

    private void DrawZoneGizmos()
    {
        Vector3 center = transform.position;
        float core = Radius;
        float outer = EffectiveRadius;

        Gizmos.color = coreColor;
        Gizmos.DrawWireSphere(center, core);

        if (outer > core + 0.01f)
        {
            Gizmos.color = bufferColor;
            Gizmos.DrawWireSphere(center, outer);
        }

        Gizmos.color = coreColor;
        Gizmos.DrawLine(center + Vector3.left * core, center + Vector3.right * core);
        Gizmos.DrawLine(center + Vector3.up * core, center + Vector3.down * core);
    }
}

