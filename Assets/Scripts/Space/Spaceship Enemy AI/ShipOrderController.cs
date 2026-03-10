using System;
using System.Collections.Generic;
using UnityEngine;

[Obsolete("ShipOrderController is deprecated. Use SquadController and reinforcement request hooks instead.", false)]
[DisallowMultipleComponent]
[RequireComponent(typeof(TeamAgent))]
[RequireComponent(typeof(TargetingComponent))]
public class ShipOrderController : MonoBehaviour
{
    public enum OrderKind
    {
        Skirmish,
        Escort,
        Protect,
        FocusFire,
        Regroup,
        Retreat
    }

    public static readonly List<ShipOrderController> Active = new List<ShipOrderController>(256);

    [Header("Legacy")]
    [SerializeField] private bool legacyModeEnabled = false;

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

    [SerializeField] private float threatPickInnerBias = 0.75f;

    private readonly Collider2D[] scanHits = new Collider2D[32];

    private TeamAgent self;
    private TargetingComponent targeting;
    private EnemySquadMember squadMember;

    private OrderKind currentOrder;
    private Transform anchor;
    private TeamAgent forcedTarget;

    private float slotAngleRad;
    private float orbitSign;
    private float thinkTimer;

    public bool LegacyModeEnabled => legacyModeEnabled;
    public bool SquadControllable => legacyModeEnabled && squadControllable;
    public int TeamId => self != null ? self.TeamId : 0;
    public OrderKind CurrentOrder => currentOrder;
    public Transform Anchor => anchor;
    public EnemySquadMember SquadMember => squadMember;

    private void Awake()
    {
        self = GetComponent<TeamAgent>();
        targeting = GetComponent<TargetingComponent>();
        squadMember = GetComponent<EnemySquadMember>();

        float t = Mathf.Abs(GetInstanceID() * 0.6180339f);
        t = t - Mathf.Floor(t);
        slotAngleRad = t * Mathf.PI * 2f;
        orbitSign = ((GetInstanceID() & 1) == 0) ? 1f : -1f;
    }

    private void OnEnable()
    {
        if (!legacyModeEnabled)
        {
            Active.Remove(this);
            return;
        }

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
        if (!legacyModeEnabled)
            return;

        thinkTimer -= Time.fixedDeltaTime;
        if (thinkTimer > 0f) return;
        thinkTimer = orderThinkInterval;

        TickOrderLogic();
    }

    public void SetSquadMember(EnemySquadMember member)
    {
        squadMember = member;
    }

    public void IssueSkirmish()
    {
        if (!legacyModeEnabled)
            return;

        ApplyOrder(OrderKind.Skirmish, null, null);
    }

    public void IssueEscort(Transform leader)
    {
        if (!legacyModeEnabled)
            return;

        ApplyOrder(OrderKind.Escort, leader, null);
    }

    public void IssueProtect(Transform anchorTransform)
    {
        if (!legacyModeEnabled)
            return;

        ApplyOrder(OrderKind.Protect, anchorTransform, null);
    }

    public void IssueFocusFire(TeamAgent target)
    {
        if (!legacyModeEnabled)
            return;

        ApplyOrder(OrderKind.FocusFire, null, target);
    }

    public void IssueRegroup(Transform leader)
    {
        if (!legacyModeEnabled)
            return;

        ApplyOrder(OrderKind.Regroup, leader, null);
    }

    public void IssueRetreatFrom(Vector2 threatPos)
    {
        if (!legacyModeEnabled)
            return;

        anchor = null;
        forcedTarget = null;
        currentOrder = OrderKind.Retreat;

        targeting.SetAutoTargetingEnabled(false);
        targeting.SetExternalTarget(null);

        Vector2 away = ((Vector2)transform.position - threatPos);
        if (away.sqrMagnitude < 0.001f) away = UnityEngine.Random.insideUnitCircle;
        slotAngleRad = Mathf.Atan2(away.y, away.x);
    }

    private void ApplyOrder(OrderKind kind, Transform newAnchor, TeamAgent newForcedTarget)
    {
        if (!legacyModeEnabled)
            return;

        currentOrder = kind;
        anchor = newAnchor;
        forcedTarget = newForcedTarget;

        if (kind == OrderKind.Skirmish)
        {
            targeting.SetAutoTargetingEnabled(true);
            targeting.SetExternalTarget(null);
            return;
        }

        targeting.SetAutoTargetingEnabled(false);

        if (kind == OrderKind.FocusFire)
        {
            if (forcedTarget != null)
                targeting.SetExternalTarget(forcedTarget, true);
            else
                targeting.SetExternalTarget(null);

            return;
        }

        targeting.SetExternalTarget(null);
    }

    private void TickOrderLogic()
    {
        switch (currentOrder)
        {
            case OrderKind.Skirmish:
                return;

            case OrderKind.FocusFire:
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
            IssueSkirmish();
            return;
        }

        TeamAgent threat = FindBestThreatNearPoint(anchor.position, assistRadius);
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

    public bool TryGetMovementGoal(Vector2 myPos, out Vector2 goalPos, out float throttle01)
    {
        goalPos = myPos;
        throttle01 = 0f;

        if (!legacyModeEnabled)
            return false;

        if (squadMember != null && squadMember.TryGetTravelGoal(myPos, out goalPos, out throttle01))
            return true;

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

    public bool TryGetCombatGoal(
        Vector2 myPos,
        Vector2 targetPos,
        out Vector2 goalPos,
        out float throttle01,
        out float blendWeight,
        out bool suppressAttackRuns)
    {
        goalPos = myPos;
        throttle01 = 0f;
        blendWeight = 0f;
        suppressAttackRuns = false;

        if (!legacyModeEnabled)
            return false;

        return squadMember != null &&
               squadMember.TryGetCombatGoal(myPos, targetPos, out goalPos, out throttle01, out blendWeight,
                   out suppressAttackRuns);
    }

    private static Vector2 DirFromAngle(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
}

