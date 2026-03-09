using System.Collections.Generic;
using UnityEngine;

public class EnemySquadController : MonoBehaviour
{
    private static readonly List<EnemySquadController> Active = new List<EnemySquadController>(64);

    [Header("Squad")]
    [SerializeField] private EnemySquadFormationType formationType = EnemySquadFormationType.Vee;
    [SerializeField] private EnemySquadState currentState = EnemySquadState.Engage;
    [SerializeField] private float slotSpacing = 5f;

    [Header("Anchor Motion")]
    [SerializeField] private float anchorMoveSpeed = 14f;
    [SerializeField] private float maxCatchupSpeed = 34f;
    [SerializeField] private float targetLeadTime = 0.45f;
    [SerializeField] private float anchorCatchupBoost = 1.15f;
    [SerializeField] private float engageDistanceFromTarget = 18f;
    [SerializeField] private float regroupDistanceFromTarget = 26f;
    [SerializeField] private float engageOrbitSpeedDeg = 52f;
    [SerializeField] private float regroupOrbitSpeedDeg = 28f;
    [SerializeField] private float orbitLateralOffset = 6f;
    [SerializeField] private float orbitLaneSpacing = 5f;
    [SerializeField] private float squadRepulsionRadius = 26f;
    [SerializeField] private float squadRepulsionStrength = 13f;
    [Header("Anchor Formation Hold")]
    [SerializeField] private bool paceAnchorToFollowers = true;
    [SerializeField] private float anchorSlowWhenLaggingDistance = 2.5f;
    [SerializeField] private float anchorStopWhenLaggingDistance = 7f;
    [Range(0f, 1f)] [SerializeField] private float anchorMinLagSpeedScale = 0.05f;
    [Range(0f, 1f)] [SerializeField] private float anchorLagRecoveryBias = 0.55f;

    [Header("Combat Influence")]
    [Range(0f, 1f)] [SerializeField] private float engageBlendWeight = 0.97f;
    [Range(0f, 1f)] [SerializeField] private float regroupBlendWeight = 1f;
    [Header("Role Slot Lock")]
    [Range(0f, 1f)] [SerializeField] private float leaderEngageBlendScale = 0.72f;
    [Range(0f, 1f)] [SerializeField] private float followerEngageBlendScale = 1f;
    [Range(0f, 1f)] [SerializeField] private float leaderRegroupBlendScale = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float followerRegroupBlendScale = 1f;
    [SerializeField] private float combatSlotSpacingMultiplier = 1.15f;
    private readonly List<EnemySquadMember> members = new List<EnemySquadMember>(8);
    private Transform focusTarget;
    private float orbitAngleDeg;
    private float orbitSign = 1f;
    private float orbitSpeedMultiplier = 1f;
    private float orbitLaneOffset;

    public EnemySquadFormationType FormationType => formationType;
    public EnemySquadState CurrentState => currentState;
    public Transform FocusTarget => focusTarget;
    public int MemberCount => members.Count;

    private void OnEnable()
    {
        if (!Active.Contains(this))
            Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    public void Initialize(
        Transform target,
        EnemySquadFormationType formation,
        float spacing,
        EnemySquadState initialState,
        float engageDistance,
        float moveSpeed)
    {
        focusTarget = target;
        formationType = formation;
        slotSpacing = Mathf.Max(1f, spacing);
        currentState = initialState;
        engageDistanceFromTarget = Mathf.Max(1f, engageDistance);
        anchorMoveSpeed = Mathf.Max(0.1f, moveSpeed);

        orbitAngleDeg = Random.Range(0f, 360f);
        orbitSign = Random.value > 0.5f ? 1f : -1f;
        orbitSpeedMultiplier = 0.8f + (Random.value * 0.45f);

        int laneIndex = Mathf.Abs(GetInstanceID()) % 5;
        orbitLaneOffset = (laneIndex - 2) * orbitLaneSpacing;
    }

    private void FixedUpdate()
    {
        UpdateAnchorPosition();
    }

    public void SetState(EnemySquadState nextState)
    {
        currentState = nextState;
    }

    public void SetFocusTarget(Transform target)
    {
        focusTarget = target;
    }

    public void RegisterMember(EnemySquadMember member, EnemySquadRole role)
    {
        if (member == null)
            return;

        if (!members.Contains(member))
            members.Add(member);

        RefreshMemberSlots(member, role);
    }

    public void UnregisterMember(EnemySquadMember member)
    {
        if (member == null)
            return;

        if (members.Remove(member))
            member.ClearSquad(this);

        if (members.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        RefreshMemberSlots(null, EnemySquadRole.Wingman);
    }

    public Vector3 GetPreviewSlotWorldPosition(int slotIndex)
    {
        Vector2 offset = GetSlotOffset(slotIndex, Mathf.Max(1, slotIndex + 1));
        return transform.position + (Vector3)offset;
    }

    public bool TryGetTravelGoal(EnemySquadMember member, Vector2 myPos, out Vector2 goalPos, out float throttle01)
    {
        goalPos = myPos;
        throttle01 = 0f;

        if (member == null)
            return false;

        switch (currentState)
        {
            case EnemySquadState.FormUp:
            case EnemySquadState.Regroup:
                goalPos = GetWorldSlotPosition(member.SlotIndex);
                throttle01 = 1f;
                return true;

            case EnemySquadState.Retreat:
                if (focusTarget == null)
                    return false;

                Vector2 away = ((Vector2)transform.position - (Vector2)focusTarget.position).normalized;
                if (away.sqrMagnitude < 0.001f)
                    away = Vector2.up;

                goalPos = myPos + away * Mathf.Max(regroupDistanceFromTarget, slotSpacing * 2f);
                throttle01 = 1f;
                return true;
        }

        return false;
    }

    public bool TryGetCombatGoal(
        EnemySquadMember member,
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

        if (member == null)
            return false;

        switch (currentState)
        {
            case EnemySquadState.FormUp:
            case EnemySquadState.Regroup:
                goalPos = GetWorldSlotPosition(member.SlotIndex);
                throttle01 = 1f;
                blendWeight = GetRoleBlendWeight(member, regroupBlendWeight, leaderRegroupBlendScale,
                    followerRegroupBlendScale);
                suppressAttackRuns = true;
                return true;

            case EnemySquadState.Retreat:
            {
                Vector2 away = (myPos - targetPos).normalized;
                if (away.sqrMagnitude < 0.001f)
                    away = Vector2.up;

                goalPos = myPos + away * Mathf.Max(regroupDistanceFromTarget, slotSpacing * 2f);
                throttle01 = 1f;
                blendWeight = 1f;
                suppressAttackRuns = true;
                return true;
            }

            case EnemySquadState.Engage:
                goalPos = GetCombatSlotPosition(member.SlotIndex, targetPos);
                throttle01 = member.Role == EnemySquadRole.ShieldSupport ? 0.9f : 1f;
                blendWeight = GetRoleBlendWeight(member, engageBlendWeight, leaderEngageBlendScale,
                    followerEngageBlendScale);
                suppressAttackRuns = true;
                return true;
        }

        return false;
    }

    private void UpdateAnchorPosition()
    {
        if (focusTarget == null)
            return;

        Vector2 focusPos = focusTarget.position;
        Vector2 focusVel = Vector2.zero;
        var focusRb = focusTarget.GetComponent<Rigidbody2D>();
        if (focusRb != null)
            focusVel = focusRb.linearVelocity;

        Vector2 predictedFocusPos = focusPos + focusVel * targetLeadTime;
        Vector2 currentPos = transform.position;

        float orbitSpeedDeg = currentState == EnemySquadState.Engage ? engageOrbitSpeedDeg : regroupOrbitSpeedDeg;
        float baseDistance = currentState == EnemySquadState.Engage ? engageDistanceFromTarget : regroupDistanceFromTarget;
        float desiredDistance = Mathf.Max(6f, baseDistance + orbitLaneOffset);

        orbitAngleDeg += orbitSpeedDeg * orbitSign * orbitSpeedMultiplier * Time.fixedDeltaTime;

        float orbitRad = orbitAngleDeg * Mathf.Deg2Rad;
        Vector2 radial = new Vector2(Mathf.Cos(orbitRad), Mathf.Sin(orbitRad));
        Vector2 tangent = new Vector2(-radial.y, radial.x) * orbitSign;

        Vector2 desiredPos = predictedFocusPos + radial * desiredDistance + tangent * orbitLateralOffset;
        desiredPos += ComputeSquadRepulsion(currentPos);

        Vector2 toDesired = desiredPos - currentPos;
        float dynamicSpeed = anchorMoveSpeed + focusVel.magnitude * anchorCatchupBoost;
        dynamicSpeed = Mathf.Min(dynamicSpeed, maxCatchupSpeed);
        dynamicSpeed *= ComputeFollowerLagSpeedScale(focusPos);

        if (dynamicSpeed <= 0.001f)
            return;

        Vector2 step = Vector2.ClampMagnitude(toDesired, dynamicSpeed * Time.fixedDeltaTime);
        transform.position = currentPos + step;
    }

    private float ComputeFollowerLagSpeedScale(Vector2 targetPos)
    {
        if (!paceAnchorToFollowers || members.Count <= 1)
            return 1f;

        float maxLag = 0f;
        float lagSum = 0f;
        int followerCount = 0;

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member == null || member.Role == EnemySquadRole.Leader)
                continue;

            Vector2 slotPos = currentState == EnemySquadState.Engage
                ? GetCombatSlotPosition(member.SlotIndex, targetPos)
                : GetWorldSlotPosition(member.SlotIndex);

            float lag = Vector2.Distance(member.transform.position, slotPos);
            maxLag = Mathf.Max(maxLag, lag);
            lagSum += lag;
            followerCount++;
        }

        if (followerCount <= 0)
            return 1f;

        float avgLag = lagSum / followerCount;
        float effectiveLag = Mathf.Lerp(avgLag, maxLag, Mathf.Clamp01(anchorLagRecoveryBias));
        if (effectiveLag <= anchorSlowWhenLaggingDistance)
            return 1f;

        float stopDistance = Mathf.Max(anchorSlowWhenLaggingDistance + 0.01f, anchorStopWhenLaggingDistance);
        float minScale = Mathf.Clamp01(anchorMinLagSpeedScale);
        float t = Mathf.InverseLerp(anchorSlowWhenLaggingDistance, stopDistance, effectiveLag);
        return Mathf.Lerp(1f, minScale, t);
    }

    private Vector2 GetWorldSlotPosition(int slotIndex)
    {
        Vector2 offset = GetSlotOffset(slotIndex, members.Count);
        return (Vector2)transform.position + offset;
    }

    private Vector2 GetCombatSlotPosition(int slotIndex, Vector2 targetPos)
    {
        Vector2 forward = targetPos - (Vector2)transform.position;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector2.up;

        Vector2 tangent = new Vector2(-forward.y, forward.x).normalized * orbitSign;
        Vector2 formationForward = (forward.normalized + tangent * 0.45f).normalized;
        float angleDeg = Mathf.Atan2(formationForward.y, formationForward.x) * Mathf.Rad2Deg - 90f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angleDeg);
        Vector2 localOffset = GetSlotOffset(slotIndex, members.Count) * Mathf.Max(0.1f, combatSlotSpacingMultiplier);
        Vector2 formationCenter = (Vector2)transform.position;

        return formationCenter + (Vector2)(rotation * localOffset);
    }

    private static float GetRoleBlendWeight(
        EnemySquadMember member,
        float baseWeight,
        float leaderScale,
        float followerScale)
    {
        float roleScale = member != null && member.Role == EnemySquadRole.Leader ? leaderScale : followerScale;
        return Mathf.Clamp01(baseWeight * roleScale);
    }

    private void RefreshMemberSlots(EnemySquadMember roleOverrideMember, EnemySquadRole roleOverride)
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member == null)
                continue;

            EnemySquadRole assignedRole = member == roleOverrideMember ? roleOverride : member.Role;
            member.SetSquad(this, assignedRole, i);
        }
    }

    private Vector2 ComputeSquadRepulsion(Vector2 currentPos)
    {
        if (squadRepulsionRadius <= 0f)
            return Vector2.zero;

        Vector2 repel = Vector2.zero;
        int used = 0;

        for (int i = 0; i < Active.Count; i++)
        {
            EnemySquadController other = Active[i];
            if (other == null || other == this)
                continue;
            if (other.focusTarget != focusTarget)
                continue;

            Vector2 delta = currentPos - (Vector2)other.transform.position;
            float distance = delta.magnitude;
            if (distance < 0.001f || distance > squadRepulsionRadius)
                continue;

            float weight = 1f - Mathf.Clamp01(distance / squadRepulsionRadius);
            repel += (delta / distance) * weight;
            used++;
        }

        if (used == 0)
            return Vector2.zero;

        return repel.normalized * squadRepulsionStrength;
    }

    private Vector2 GetSlotOffset(int slotIndex, int totalMembers)
    {
        float spacing = Mathf.Max(1f, slotSpacing);

        switch (formationType)
        {
            case EnemySquadFormationType.Line:
            {
                float centerOffset = (Mathf.Max(1, totalMembers) - 1) * 0.5f;
                return new Vector2((slotIndex - centerOffset) * spacing, 0f);
            }

            case EnemySquadFormationType.Diamond:
            {
                Vector2[] points =
                {
                    new Vector2(0f, 0f),
                    new Vector2(-1f, -1f),
                    new Vector2(1f, -1f),
                    new Vector2(0f, -2f),
                    new Vector2(0f, 1f)
                };
                return points[Mathf.Clamp(slotIndex, 0, points.Length - 1)] * spacing;
            }

            case EnemySquadFormationType.Ring:
            {
                float count = Mathf.Max(1, totalMembers);
                float angle = (Mathf.PI * 2f * slotIndex) / count;
                float radius = Mathf.Max(spacing, spacing * 0.75f * count / Mathf.PI);
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            case EnemySquadFormationType.Escort:
            {
                Vector2[] points =
                {
                    new Vector2(0f, 0f),
                    new Vector2(-1.25f, -0.85f),
                    new Vector2(1.25f, -0.85f),
                    new Vector2(-2f, -1.75f),
                    new Vector2(2f, -1.75f)
                };
                return points[Mathf.Clamp(slotIndex, 0, points.Length - 1)] * spacing;
            }

            case EnemySquadFormationType.Vee:
            default:
            {
                Vector2[] points =
                {
                    new Vector2(0f, 0f),
                    new Vector2(-1f, -1f),
                    new Vector2(1f, -1f),
                    new Vector2(-2f, -2f),
                    new Vector2(2f, -2f)
                };
                return points[Mathf.Clamp(slotIndex, 0, points.Length - 1)] * spacing;
            }
        }
    }
}

