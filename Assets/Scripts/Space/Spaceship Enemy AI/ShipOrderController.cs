using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TeamAgent))]
[RequireComponent(typeof(TargetingComponent))]
public class ShipOrderController : MonoBehaviour
{
    public enum OrderKind
    {
        Skirmish, // fight freely using TargetingComponent
        Escort, // stay near leader; engage threats near leader
        Protect, // orbit anchor; engage threats near anchor
        FocusFire, // force a specific target
        Regroup, // return to leader; minimal chasing
        Retreat // pull away / disengage
    }

    public static readonly List<ShipOrderController> Active = new List<ShipOrderController>(256);

    [Header("Order")] [SerializeField] private OrderKind defaultOrder = OrderKind.Skirmish;
    [SerializeField] private bool squadControllable = true;

    [Header("Retargeting (Orders)")] [SerializeField]
    private float orderThinkInterval = 0.20f;

    [Header("Escort")] [SerializeField] private float escortRadius = 12f;
    [SerializeField] private float assistRadius = 35f;
    [SerializeField] private float escortLeash = 45f;

    [Header("Protect")] [SerializeField] private float protectOrbitRadius = 16f;
    [SerializeField] private float defenseRadius = 40f;
    [SerializeField] private float protectLeash = 55f;
    [SerializeField] private float protectOrbitAngularSpeedDeg = 45f;

    [Header("Regroup")] [SerializeField] private float regroupRadius = 10f;

    [Header("Retreat")] [SerializeField] private float retreatDistance = 25f;

    [Header("Threat Scan")] [SerializeField]
    private LayerMask shipMask = ~0;

    [SerializeField] private float threatPickInnerBias = 0.75f; // closer-to-anchor bias

    private readonly Collider2D[] scanHits = new Collider2D[32];

    private TeamAgent self;
    private TargetingComponent targeting;

    private OrderKind currentOrder;
    private Transform anchor; // leader/flagship/player/etc.
    private TeamAgent forcedTarget; // for FocusFire (or for Protect/Escort threat)

    // per-ship slot angle so formations look natural
    private float slotAngleRad;
    private float orbitSign;

    private float thinkTimer;

    public bool SquadControllable => squadControllable;
    public int TeamId => self != null ? self.TeamId : 0;

    public OrderKind CurrentOrder => currentOrder;
    public Transform Anchor => anchor;

    private void Awake()
    {
        self = GetComponent<TeamAgent>();
        targeting = GetComponent<TargetingComponent>();

        // stable-ish “spread around” values per instance
        float t = Mathf.Abs(GetInstanceID() * 0.6180339f);
        t = t - Mathf.Floor(t);
        slotAngleRad = t * Mathf.PI * 2f;
        orbitSign = ((GetInstanceID() & 1) == 0) ? 1f : -1f;
    }

    private void OnEnable()
    {
        if (!Active.Contains(this))
            Active.Add(this);

        ApplyOrder(defaultOrder, null, null);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void FixedUpdate()
    {
        thinkTimer -= Time.fixedDeltaTime;
        if (thinkTimer > 0f) return;
        thinkTimer = orderThinkInterval;

        TickOrderLogic();
    }

    // -------------------------
    // Order API (call these!)
    // -------------------------
    public void IssueSkirmish()
    {
        ApplyOrder(OrderKind.Skirmish, null, null);
    }

    public void IssueEscort(Transform leader)
    {
        ApplyOrder(OrderKind.Escort, leader, null);
    }

    public void IssueProtect(Transform anchorTransform)
    {
        ApplyOrder(OrderKind.Protect, anchorTransform, null);
    }

    public void IssueFocusFire(TeamAgent target)
    {
        ApplyOrder(OrderKind.FocusFire, null, target);
    }

    public void IssueRegroup(Transform leader)
    {
        ApplyOrder(OrderKind.Regroup, leader, null);
    }

    public void IssueRetreatFrom(Vector2 threatPos)
    {
        // anchor becomes a temporary “escape point direction” holder (we compute goal dynamically)
        anchor = null;
        forcedTarget = null;
        currentOrder = OrderKind.Retreat;

        // stop auto targeting
        targeting.SetAutoTargetingEnabled(false);
        targeting.SetExternalTarget(null);

        // store a pseudo-angle away from threat in slotAngleRad
        Vector2 away = ((Vector2)transform.position - threatPos);
        if (away.sqrMagnitude < 0.001f) away = Random.insideUnitCircle;
        slotAngleRad = Mathf.Atan2(away.y, away.x);
    }

    private void ApplyOrder(OrderKind kind, Transform newAnchor, TeamAgent newForcedTarget)
    {
        currentOrder = kind;
        anchor = newAnchor;
        forcedTarget = newForcedTarget;

        if (kind == OrderKind.Skirmish)
        {
            targeting.SetAutoTargetingEnabled(true);
            targeting.SetExternalTarget(null);
            return;
        }

        // Most “command” states should suppress random targeting
        targeting.SetAutoTargetingEnabled(false);

        // FocusFire sets a forced target directly
        if (kind == OrderKind.FocusFire)
        {
            if (forcedTarget != null)
                targeting.SetExternalTarget(forcedTarget, true);
            else
                targeting.SetExternalTarget(null);

            return;
        }

        // Escort/Protect/Regroup/Retreat start with no forced combat target
        targeting.SetExternalTarget(null);
    }

    // -------------------------
    // Order thinking (who to fight?)
    // -------------------------
    private void TickOrderLogic()
    {
        switch (currentOrder)
        {
            case OrderKind.Skirmish:
                // TargetingComponent handles everything
                return;

            case OrderKind.FocusFire:
                // keep forced target if valid; otherwise clear
                if (forcedTarget == null || !forcedTarget.isActiveAndEnabled ||
                    !TeamRegistry.IsHostile(self.TeamId, forcedTarget.TeamId))
                {
                    forcedTarget = null;
                    targeting.SetExternalTarget(null);
                }
                else
                {
                    targeting.SetExternalTarget(forcedTarget, true);
                }

                return;

            case OrderKind.Escort:
                TickEscort();
                return;

            case OrderKind.Protect:
                TickProtect();
                return;

            case OrderKind.Regroup:
                // no fighting unless something is basically on top of leader
                TickRegroup();
                return;

            case OrderKind.Retreat:
                targeting.SetExternalTarget(null);
                return;
        }
    }

    private void TickEscort()
    {
        if (anchor == null)
        {
            // if no anchor, downgrade
            IssueSkirmish();
            return;
        }

        TeamAgent threat = FindBestThreatNearPoint(anchor.position, assistRadius);

        // leash: don't chase threats too far from leader
        if (threat != null)
        {
            float d = Vector2.Distance(threat.transform.position, anchor.position);
            if (d > assistRadius + escortLeash)
                threat = null;
        }

        targeting.SetExternalTarget(threat, true);
    }

    private void TickProtect()
    {
        if (anchor == null)
        {
            IssueSkirmish();
            return;
        }

        TeamAgent threat = FindBestThreatNearPoint(anchor.position, defenseRadius);

        // leash: abandon chase if threat too far from anchor
        if (threat != null)
        {
            float d = Vector2.Distance(threat.transform.position, anchor.position);
            if (d > defenseRadius + protectLeash)
                threat = null;
        }

        targeting.SetExternalTarget(threat, true);
    }

    private void TickRegroup()
    {
        if (anchor == null)
        {
            IssueSkirmish();
            return;
        }

        // Only engage if hostile is very close to leader
        TeamAgent threat = FindBestThreatNearPoint(anchor.position, regroupRadius * 1.25f);
        targeting.SetExternalTarget(threat, true);
    }

    private TeamAgent FindBestThreatNearPoint(Vector2 point, float radius)
    {
        int count = Physics2D.OverlapCircleNonAlloc(point, radius, scanHits, shipMask);
        if (count <= 0) return null;

        TeamAgent best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            var col = scanHits[i];
            if (col == null) continue;

            TeamAgent other = col.GetComponentInParent<TeamAgent>();
            if (other == null) continue;
            if (other == self) continue;
            if (!other.isActiveAndEnabled) continue;
            if (!TeamRegistry.IsHostile(self.TeamId, other.TeamId)) continue;

            float d = Vector2.Distance(other.transform.position, point);

            // Slight bias toward threats closer to the protected point
            float score = d * threatPickInnerBias + Vector2.Distance(other.transform.position, transform.position) *
                (1f - threatPickInnerBias);

            if (score < bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        return best;
    }

    // -------------------------
    // Movement helper (used by your movement AI)
    // -------------------------
    public bool TryGetMovementGoal(Vector2 myPos, out Vector2 goalPos, out float throttle01)
    {
        goalPos = myPos;
        throttle01 = 0f;

        switch (currentOrder)
        {
            case OrderKind.Escort:
                if (anchor == null) return false;
                goalPos = (Vector2)anchor.position + DirFromAngle(slotAngleRad) * escortRadius;
                throttle01 = 1f;
                return true;

            case OrderKind.Regroup:
                if (anchor == null) return false;
                goalPos = (Vector2)anchor.position + DirFromAngle(slotAngleRad) * regroupRadius;
                throttle01 = 1f;
                return true;

            case OrderKind.Protect:
                if (anchor == null) return false;

                // orbit anchor even when not fighting
                float ang = slotAngleRad + orbitSign * Mathf.Deg2Rad * (protectOrbitAngularSpeedDeg * Time.time);
                goalPos = (Vector2)anchor.position + DirFromAngle(ang) * protectOrbitRadius;
                throttle01 = 0.85f;
                return true;

            case OrderKind.Retreat:
                Vector2 dir = DirFromAngle(slotAngleRad);
                goalPos = myPos + dir.normalized * retreatDistance;
                throttle01 = 1f;
                return true;
        }

        return false;
    }

    private static Vector2 DirFromAngle(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
}