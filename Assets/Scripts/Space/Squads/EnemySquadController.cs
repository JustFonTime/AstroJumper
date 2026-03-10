using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemySquadController : MonoBehaviour
{
    private static readonly List<EnemySquadController> ActiveSquads = new List<EnemySquadController>(64);

    [Header("Formation")]
    [SerializeField] private EnemySquadFormationType formationType = EnemySquadFormationType.Vee;
    [SerializeField] private float slotSpacing = 5f;
    [SerializeField] [Range(2, 10)] private int maxFormationMembers = 10;

    [Header("Vee")]
    [SerializeField] private float veeWidthMultiplier = 1.5f;
    [SerializeField] private float veeDepthMultiplier = 0.85f;

    [Header("Line")]
    [SerializeField] private float lineWidthMultiplier = 1.5f;
    [SerializeField] private float lineDepthMultiplier = 0f;

    [Header("Diamond")]
    [SerializeField] private float diamondWidthMultiplier = 1.45f;
    [SerializeField] private float diamondDepthMultiplier = 1.2f;

    [Header("Ring")]
    [SerializeField] private float ringWidthMultiplier = 1f;
    [SerializeField] private float ringDepthMultiplier = 1f;

    [Header("Escort")]
    [SerializeField] private float escortWidthMultiplier = 1.5f;
    [SerializeField] private float escortDepthMultiplier = 1f;

    [Header("Follower Anchoring")]
    [SerializeField] private bool velocityAlignedSlots = true;
    [SerializeField] private float velocityForwardMinSpeed = 1.5f;
    [SerializeField] private float followerSlotLeadTime = 0.2f;

    [Header("Follower Recovery")]
    [SerializeField] private float slotRecoveryDistance = 18f;
    [SerializeField] private float recoveryHoldDistance = 7f;
    [SerializeField] private float recoveryLateralClamp = 1.5f;
    [SerializeField] private float followerAvoidanceRadius = 2.5f;
    [SerializeField] private float followerAvoidanceStrength = 1.4f;

    [Header("Leader Chase")]
    [SerializeField] private float leaderDesiredDistanceFromFocus = 18f;
    [SerializeField] private float leaderArriveDistance = 1.25f;
    [SerializeField] private float leaderFullThrottleDistance = 8f;
    [SerializeField] private float leaderTargetPredictionTime = 0.2f;

    [Header("Friendly Follow")]
    [SerializeField] private float friendlyFollowTrailingDistance = 18f;
    [SerializeField] private float friendlyFollowLateralSpacing = 9f;
    [SerializeField] private bool friendlyFollowUseVelocityHeading = true;
    [SerializeField] private float friendlyFollowMinHeadingSpeed = 1f;

    [Header("Leader Coordination")]
    [SerializeField] private bool useSquadLaneOffsets = true;
    [SerializeField] private float squadLaneSpacing = 10f;
    [SerializeField] private float squadRepulsionRadius = 16f;
    [SerializeField] private float squadRepulsionStrength = 5f;
    [SerializeField] private bool paceLeaderToFollowers = true;
    [SerializeField] private float leaderSlowStartLag = 6f;
    [SerializeField] private float leaderSlowStopLag = 18f;
    [SerializeField] [Range(0.05f, 1f)] private float leaderMinThrottleScale = 0.35f;

    [Header("Follower Catchup")]
    [SerializeField] private float followerArriveDistance = 1.35f;
    [SerializeField] private float followerFullThrottleDistance = 7f;
    [SerializeField] private float followerArrivePaddingFromSpacing = 0.15f;

    [Header("Reinforcement Requests")]
    [SerializeField] private int desiredMemberCount = 5;
    [SerializeField] private float requestDelaySeconds = 2f;
    [SerializeField] private float requestCooldownSeconds = 8f;
    [SerializeField] private bool enableReinforcementRequests = true;

    [Header("State")]
    [SerializeField] private EnemySquadState currentState = EnemySquadState.Engage;

    private static readonly Vector2[] DiamondPattern =
    {
        new Vector2(-1f, -1f),
        new Vector2(1f, -1f),
        new Vector2(0f, -2f),
        new Vector2(-2f, -2f),
        new Vector2(2f, -2f),
        new Vector2(-1f, -3f),
        new Vector2(1f, -3f),
        new Vector2(0f, -4f),
        new Vector2(0f, -5f)
    };

    private static readonly Vector2[] EscortPattern =
    {
        new Vector2(-1.5f, -0.75f),
        new Vector2(1.5f, -0.75f),
        new Vector2(-3f, -1.5f),
        new Vector2(3f, -1.5f),
        new Vector2(-1f, -2.25f),
        new Vector2(1f, -2.25f),
        new Vector2(-4.5f, -2.25f),
        new Vector2(4.5f, -2.25f),
        new Vector2(0f, -3f)
    };

    private readonly List<EnemySquadMember> members = new List<EnemySquadMember>(12);
    private readonly Dictionary<EnemySquadMember, EnemySquadRole> preferredRoles =
        new Dictionary<EnemySquadMember, EnemySquadRole>(12);

    private Transform focusTarget;
    private float underStrengthTimer;
    private float nextAllowedRequestTime;

    public static IReadOnlyList<EnemySquadController> Active => ActiveSquads;

    public event Action<ReinforcementRequest> ReinforcementRequested;

    public EnemySquadFormationType FormationType => formationType;
    public EnemySquadState CurrentState => currentState;
    public Transform FocusTarget => focusTarget;
    public int TeamId => GetSquadTeamId();
    public float SlotSpacing => slotSpacing;
    public int DesiredMemberCount => Mathf.Clamp(desiredMemberCount, 1, MaxMembers);
    public bool IsUnderStrength => MemberCount < DesiredMemberCount;
    public int MissingMemberCount => Mathf.Max(0, DesiredMemberCount - MemberCount);
    public int MaxMembers => Mathf.Clamp(maxFormationMembers, 2, 10);
    public float SecondsUntilNextReinforcementRequest => Mathf.Max(0f, nextAllowedRequestTime - Time.time);
    public EnemySquadMember LeaderMember => GetLeaderMember();

    public int MemberCount
    {
        get
        {
            CleanupMembers();
            return members.Count;
        }
    }

    private void OnEnable()
    {
        if (!ActiveSquads.Contains(this))
            ActiveSquads.Add(this);
    }

    private void OnDisable()
    {
        ActiveSquads.Remove(this);

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member != null)
                member.ClearSquad(this);
        }

        members.Clear();
        preferredRoles.Clear();
        ResetReinforcementState();
    }

    private void Update()
    {
        TickReinforcementRequests();
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
        leaderDesiredDistanceFromFocus = Mathf.Max(1f, engageDistance);
        ResetReinforcementState();
    }

    private void LateUpdate()
    {
        Transform leader = GetLeaderTransform();
        if (leader == null)
            return;

        transform.position = leader.position;
        transform.up = leader.up;
    }

    public void SetState(EnemySquadState nextState)
    {
        currentState = nextState;
    }

    public void SetFormation(EnemySquadFormationType nextFormation)
    {
        formationType = nextFormation;
        RefreshMemberSlots();
    }

    public void SetSlotSpacing(float spacing)
    {
        slotSpacing = Mathf.Max(1f, spacing);
    }

    public void AdjustSlotSpacing(float delta)
    {
        SetSlotSpacing(slotSpacing + delta);
    }

    public void SetFocusTarget(Transform target)
    {
        focusTarget = target;
    }

    public void ConfigureReinforcementPolicy(int desiredCount, float delaySeconds, float cooldownSeconds, bool enabled)
    {
        desiredMemberCount = Mathf.Clamp(desiredCount, 1, MaxMembers);
        requestDelaySeconds = Mathf.Max(0f, delaySeconds);
        requestCooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        enableReinforcementRequests = enabled;
        ResetReinforcementState();
    }

    public void RegisterMember(EnemySquadMember member, EnemySquadRole role)
    {
        if (member == null)
            return;

        if (!members.Contains(member))
        {
            if (members.Count >= MaxMembers)
                return;

            members.Add(member);
        }

        preferredRoles[member] = role;
        RefreshMemberSlots();
    }

    public void UnregisterMember(EnemySquadMember member)
    {
        if (member == null)
            return;

        preferredRoles.Remove(member);

        if (members.Remove(member))
            member.ClearSquad(this);

        CleanupMembers();

        if (members.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        RefreshMemberSlots();
    }

    public Vector3 GetPreviewSlotWorldPosition(int slotIndex)
    {
        int previewCount = Mathf.Clamp(Mathf.Max(MemberCount, slotIndex + 1), 1, MaxMembers);
        Vector2 localOffset = GetSlotOffset(slotIndex, previewCount);
        return transform.position + (Vector3)(transform.rotation * localOffset);
    }

    public bool TryGetTravelGoal(EnemySquadMember member, Vector2 myPos, out Vector2 goalPos, out float throttle01)
    {
        goalPos = myPos;
        throttle01 = 0f;

        if (member == null)
            return false;

        CleanupMembers();

        bool isLeader = member.Role == EnemySquadRole.Leader || member.SlotIndex <= 0;
        Transform leader = GetLeaderTransform();

        if (isLeader || leader == null || leader == member.transform)
            return TryGetLeaderGoal(leader, myPos, out goalPos, out throttle01);

        Vector2 slotGoal = GetFollowerSlotWorldPosition(leader, member.SlotIndex);
        float slotDistance = Vector2.Distance(myPos, slotGoal);

        if (slotDistance > Mathf.Max(1f, slotRecoveryDistance))
        {
            goalPos = GetFollowerRecoveryWorldPosition(leader, member.SlotIndex);
            goalPos += ComputeFollowerAvoidanceOffset(member, myPos);
            throttle01 = 1f;
            return true;
        }

        goalPos = slotGoal + ComputeFollowerAvoidanceOffset(member, myPos);

        float dist = Vector2.Distance(myPos, goalPos);
        float dynamicArriveDistance = followerArriveDistance + Mathf.Max(0f, slotSpacing * followerArrivePaddingFromSpacing);
        float dynamicFullThrottleDistance = Mathf.Max(
            dynamicArriveDistance + 0.5f,
            followerFullThrottleDistance + Mathf.Max(0f, slotSpacing * 0.25f));

        throttle01 = ComputeThrottleFromDistance(dist, dynamicArriveDistance, dynamicFullThrottleDistance);

        if (slotDistance > recoveryHoldDistance)
            throttle01 = Mathf.Max(throttle01, 0.65f);

        return true;
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
        bool hasGoal = TryGetTravelGoal(member, myPos, out goalPos, out throttle01);
        blendWeight = hasGoal ? 1f : 0f;
        suppressAttackRuns = true;
        return hasGoal;
    }

    private bool TryGetLeaderGoal(Transform leader, Vector2 leaderPos, out Vector2 goalPos, out float throttle01)
    {
        goalPos = leaderPos;
        throttle01 = 0f;

        if (focusTarget == null)
            return false;

        if (TryGetFriendlyFollowGoal(leaderPos, out Vector2 friendlyGoal))
        {
            goalPos = friendlyGoal + ComputeSquadRepulsionOffset(leaderPos);

            float friendlyDist = Vector2.Distance(leaderPos, goalPos);
            throttle01 = ComputeThrottleFromDistance(friendlyDist, leaderArriveDistance, leaderFullThrottleDistance);
            throttle01 *= ComputeLeaderPacingScale(leader);
            throttle01 = Mathf.Clamp01(throttle01);
            return true;
        }

        Vector2 focusPos = GetPredictedFocusPosition();
        Vector2 toFocus = focusPos - leaderPos;
        float focusDistance = toFocus.magnitude;

        if (focusDistance < 0.001f)
            return true;

        float desired = Mathf.Max(1f, leaderDesiredDistanceFromFocus);
        Vector2 dir = toFocus / focusDistance;
        Vector2 right = new Vector2(dir.y, -dir.x);

        Vector2 laneOffset = ComputeSquadLaneOffset(right);
        Vector2 repulsionOffset = ComputeSquadRepulsionOffset(leaderPos);
        goalPos = focusPos - dir * desired + laneOffset + repulsionOffset;

        float distToGoal = Vector2.Distance(leaderPos, goalPos);
        throttle01 = ComputeThrottleFromDistance(distToGoal, leaderArriveDistance, leaderFullThrottleDistance);

        if (focusDistance < desired - leaderArriveDistance)
            throttle01 = 1f;

        throttle01 *= ComputeLeaderPacingScale(leader);
        throttle01 = Mathf.Clamp01(throttle01);

        return true;
    }

    private Vector2 GetPredictedFocusPosition()
    {
        if (focusTarget == null)
            return Vector2.zero;

        Vector2 pos = focusTarget.position;
        if (leaderTargetPredictionTime <= 0f)
            return pos;

        Rigidbody2D focusRb = focusTarget.GetComponent<Rigidbody2D>();
        if (focusRb == null)
            return pos;

        return pos + focusRb.linearVelocity * leaderTargetPredictionTime;
    }

    private bool TryGetFriendlyFollowGoal(Vector2 leaderPos, out Vector2 goalPos)
    {
        goalPos = leaderPos;

        if (focusTarget == null)
            return false;

        int squadTeamId = GetSquadTeamId();
        TeamAgent focusTeamAgent = focusTarget.GetComponentInParent<TeamAgent>();
        if (focusTeamAgent == null || focusTeamAgent.TeamId != squadTeamId)
            return false;

        Vector2 focusPos = GetPredictedFocusPosition();
        Vector2 forward = focusTarget.up;

        Rigidbody2D focusRb = focusTarget.GetComponent<Rigidbody2D>();
        if (friendlyFollowUseVelocityHeading && focusRb != null)
        {
            float minSpeed = Mathf.Max(0f, friendlyFollowMinHeadingSpeed);
            if (focusRb.linearVelocity.sqrMagnitude >= minSpeed * minSpeed)
                forward = focusRb.linearVelocity.normalized;
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector2.up;

        Vector2 right = new Vector2(forward.y, -forward.x);
        float trail = Mathf.Max(0f, friendlyFollowTrailingDistance);
        float lateral = ComputeFriendlyFollowLateralOffset();
        goalPos = focusPos - (forward * trail) + (right * lateral);
        return true;
    }

    private float ComputeFriendlyFollowLateralOffset()
    {
        float spacing = Mathf.Max(0f, friendlyFollowLateralSpacing);
        if (spacing <= 0f || focusTarget == null)
            return 0f;

        int myTeamId = GetSquadTeamId();
        int myInstanceId = GetInstanceID();
        int rank = 0;
        int total = 0;

        for (int i = 0; i < ActiveSquads.Count; i++)
        {
            EnemySquadController squad = ActiveSquads[i];
            if (!IsComparableSquad(squad, myTeamId))
                continue;

            total++;
            if (squad.GetInstanceID() < myInstanceId)
                rank++;
        }

        if (total <= 1)
            return 0f;

        float centeredIndex = rank - ((total - 1) * 0.5f);
        return centeredIndex * spacing;
    }
    private Vector2 GetFollowerSlotWorldPosition(Transform leader, int slotIndex)
    {
        Vector2 localOffset = GetSlotOffset(slotIndex, Mathf.Clamp(MemberCount, 1, MaxMembers));

        Vector2 anchorPos = leader.position;
        Rigidbody2D leaderRb = leader.GetComponent<Rigidbody2D>();
        if (leaderRb != null && followerSlotLeadTime > 0f)
            anchorPos += leaderRb.linearVelocity * followerSlotLeadTime;

        GetFormationBasis(leader, leaderRb, out Vector2 forward, out Vector2 right);
        return anchorPos + right * localOffset.x + forward * localOffset.y;
    }

    private Vector2 GetFollowerRecoveryWorldPosition(Transform leader, int slotIndex)
    {
        Vector2 anchorPos = leader.position;
        Rigidbody2D leaderRb = leader.GetComponent<Rigidbody2D>();
        if (leaderRb != null && followerSlotLeadTime > 0f)
            anchorPos += leaderRb.linearVelocity * followerSlotLeadTime;

        GetFormationBasis(leader, leaderRb, out Vector2 forward, out Vector2 right);

        Vector2 slotOffset = GetSlotOffset(slotIndex, Mathf.Clamp(MemberCount, 1, MaxMembers));
        float maxLateral = Mathf.Max(0.1f, slotSpacing * recoveryLateralClamp);
        float lateral = Mathf.Clamp(slotOffset.x, -maxLateral, maxLateral);
        float backDistance = Mathf.Max(slotSpacing * 0.8f, Mathf.Abs(slotOffset.y) * 0.35f + slotSpacing * 0.5f);

        return anchorPos + right * lateral - forward * backDistance;
    }

    private Vector2 ComputeFollowerAvoidanceOffset(EnemySquadMember member, Vector2 myPos)
    {
        if (followerAvoidanceRadius <= 0f || followerAvoidanceStrength <= 0f)
            return Vector2.zero;

        Vector2 repel = Vector2.zero;

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember other = members[i];
            if (other == null || other == member)
                continue;

            Vector2 delta = myPos - (Vector2)other.transform.position;
            float dist = delta.magnitude;
            if (dist < 0.0001f || dist > followerAvoidanceRadius)
                continue;

            float weight = 1f - (dist / followerAvoidanceRadius);
            repel += (delta / dist) * weight;
        }

        if (repel.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        return repel * followerAvoidanceStrength;
    }

    private void GetFormationBasis(Transform leader, Rigidbody2D leaderRb, out Vector2 forward, out Vector2 right)
    {
        forward = leader != null ? (Vector2)leader.up : Vector2.up;

        if (velocityAlignedSlots && leaderRb != null)
        {
            float minSpeedSq = Mathf.Max(0f, velocityForwardMinSpeed) * Mathf.Max(0f, velocityForwardMinSpeed);
            if (leaderRb.linearVelocity.sqrMagnitude >= minSpeedSq)
                forward = leaderRb.linearVelocity.normalized;
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector2.up;

        right = new Vector2(forward.y, -forward.x);
    }

    private Vector2 ComputeSquadLaneOffset(Vector2 right)
    {
        if (!useSquadLaneOffsets || focusTarget == null)
            return Vector2.zero;

        int myTeamId = GetSquadTeamId();
        int myInstanceId = GetInstanceID();

        int total = 0;
        int rank = 0;

        for (int i = 0; i < ActiveSquads.Count; i++)
        {
            EnemySquadController squad = ActiveSquads[i];
            if (!IsComparableSquad(squad, myTeamId))
                continue;

            total++;
            if (squad.GetInstanceID() < myInstanceId)
                rank++;
        }

        if (total <= 1)
            return Vector2.zero;

        float centeredIndex = rank - ((total - 1) * 0.5f);
        float laneOffset = centeredIndex * Mathf.Max(0f, squadLaneSpacing);
        return right * laneOffset;
    }

    private Vector2 ComputeSquadRepulsionOffset(Vector2 leaderPos)
    {
        if (squadRepulsionRadius <= 0f || squadRepulsionStrength <= 0f)
            return Vector2.zero;

        int myTeamId = GetSquadTeamId();
        Vector2 repel = Vector2.zero;

        for (int i = 0; i < ActiveSquads.Count; i++)
        {
            EnemySquadController squad = ActiveSquads[i];
            if (squad == null || squad == this)
                continue;
            if (!IsComparableSquad(squad, myTeamId))
                continue;

            Vector2 otherPos = squad.transform.position;
            Vector2 delta = leaderPos - otherPos;
            float dist = delta.magnitude;
            if (dist < 0.0001f || dist > squadRepulsionRadius)
                continue;

            float weight = 1f - (dist / squadRepulsionRadius);
            repel += (delta / dist) * weight;
        }

        if (repel.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        return repel * squadRepulsionStrength;
    }

    private bool IsComparableSquad(EnemySquadController squad, int myTeamId)
    {
        if (squad == null || !squad.isActiveAndEnabled || squad == this)
            return false;

        if (squad.focusTarget != focusTarget)
            return false;

        if (squad.MemberCount <= 0)
            return false;

        return squad.GetSquadTeamId() == myTeamId;
    }

    private float ComputeLeaderPacingScale(Transform leader)
    {
        if (!paceLeaderToFollowers || leader == null)
            return 1f;

        float maxLag = 0f;

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member == null || member.Role == EnemySquadRole.Leader)
                continue;

            Vector2 expected = GetFollowerSlotWorldPosition(leader, member.SlotIndex);
            float lag = Vector2.Distance(member.transform.position, expected);
            if (lag > maxLag)
                maxLag = lag;
        }

        float slowStart = Mathf.Max(0.1f, leaderSlowStartLag);
        float slowStop = Mathf.Max(slowStart + 0.1f, leaderSlowStopLag);

        if (maxLag <= slowStart)
            return 1f;

        if (maxLag >= slowStop)
            return leaderMinThrottleScale;

        float t = Mathf.InverseLerp(slowStart, slowStop, maxLag);
        return Mathf.Lerp(1f, leaderMinThrottleScale, t);
    }

    private Vector2 GetSlotOffset(int slotIndex, int totalMembers)
    {
        float spacing = Mathf.Max(0.1f, slotSpacing);
        int clampedMembers = Mathf.Clamp(totalMembers, 1, MaxMembers);
        int followerIndex = Mathf.Max(0, slotIndex - 1);
        int followerCount = Mathf.Max(1, clampedMembers - 1);

        switch (formationType)
        {
            case EnemySquadFormationType.Line:
            {
                if (slotIndex <= 0)
                    return Vector2.zero;

                int row = (slotIndex + 1) / 2;
                bool left = (slotIndex % 2) == 1;
                float x = (left ? -row : row) * spacing * Mathf.Max(0.1f, lineWidthMultiplier);
                float y = -(row - 1) * spacing * Mathf.Max(0f, lineDepthMultiplier);
                return new Vector2(x, y);
            }

            case EnemySquadFormationType.Diamond:
            {
                if (slotIndex <= 0)
                    return Vector2.zero;

                Vector2 normalized = GetPatternOffset(followerIndex, DiamondPattern);
                return new Vector2(
                    normalized.x * spacing * Mathf.Max(0.1f, diamondWidthMultiplier),
                    normalized.y * spacing * Mathf.Max(0.1f, diamondDepthMultiplier));
            }

            case EnemySquadFormationType.Ring:
            {
                if (slotIndex <= 0)
                    return Vector2.zero;

                float angle = ((Mathf.PI * 2f * followerIndex) / followerCount) - (Mathf.PI * 0.5f);
                float baseRadius = spacing * Mathf.Max(1f, followerCount * 0.36f);
                float radiusX = baseRadius * Mathf.Max(0.1f, ringWidthMultiplier);
                float radiusY = baseRadius * Mathf.Max(0.1f, ringDepthMultiplier);
                return new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            }

            case EnemySquadFormationType.Escort:
            {
                if (slotIndex <= 0)
                    return Vector2.zero;

                Vector2 normalized = GetPatternOffset(followerIndex, EscortPattern);
                return new Vector2(
                    normalized.x * spacing * Mathf.Max(0.1f, escortWidthMultiplier),
                    normalized.y * spacing * Mathf.Max(0.1f, escortDepthMultiplier));
            }

            case EnemySquadFormationType.Vee:
            default:
            {
                if (slotIndex <= 0)
                    return Vector2.zero;

                int row = (slotIndex + 1) / 2;
                bool left = (slotIndex % 2) == 1;
                float x = (left ? -row : row) * spacing * Mathf.Max(0.1f, veeWidthMultiplier);
                float y = -row * spacing * Mathf.Max(0.1f, veeDepthMultiplier);
                return new Vector2(x, y);
            }
        }
    }

    private static Vector2 GetPatternOffset(int followerIndex, Vector2[] pattern)
    {
        if (pattern == null || pattern.Length == 0)
            return Vector2.zero;

        if (followerIndex < pattern.Length)
            return pattern[followerIndex];

        Vector2 tail = pattern[pattern.Length - 1];
        int extra = followerIndex - pattern.Length + 1;
        float side = (extra % 2 == 0) ? 1f : -1f;
        return tail + new Vector2(side * (0.5f + (0.5f * extra)), -extra);
    }

    private static float ComputeThrottleFromDistance(float dist, float arriveDistance, float fullThrottleDistance)
    {
        float arrive = Mathf.Max(0.01f, arriveDistance);
        float full = Mathf.Max(arrive + 0.01f, fullThrottleDistance);

        if (dist <= arrive)
            return 0f;

        if (dist >= full)
            return 1f;

        return Mathf.Clamp01(Mathf.InverseLerp(arrive, full, dist));
    }

    private EnemySquadMember GetLeaderMember()
    {
        CleanupMembers();

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member != null && member.Role == EnemySquadRole.Leader)
                return member;
        }

        return members.Count > 0 ? members[0] : null;
    }

    private Transform GetLeaderTransform()
    {
        EnemySquadMember leader = GetLeaderMember();
        return leader != null ? leader.transform : null;
    }

    private void RefreshMemberSlots()
    {
        CleanupMembers();
        if (members.Count == 0)
            return;

        if (members.Count > MaxMembers)
            members.RemoveRange(MaxMembers, members.Count - MaxMembers);

        int leaderIndex = FindLeaderIndex();
        if (leaderIndex > 0)
        {
            EnemySquadMember leader = members[leaderIndex];
            members.RemoveAt(leaderIndex);
            members.Insert(0, leader);
        }

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member == null)
                continue;

            EnemySquadRole preferredRole = EnemySquadRole.Wingman;
            if (preferredRoles.TryGetValue(member, out EnemySquadRole storedRole))
                preferredRole = storedRole;

            EnemySquadRole assignedRole = i == 0
                ? EnemySquadRole.Leader
                : (preferredRole == EnemySquadRole.Leader ? EnemySquadRole.Wingman : preferredRole);

            member.SetSquad(this, assignedRole, i);
        }
    }

    private int FindLeaderIndex()
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member == null)
                continue;

            if (preferredRoles.TryGetValue(member, out EnemySquadRole role) && role == EnemySquadRole.Leader)
                return i;

            if (member.Role == EnemySquadRole.Leader)
                return i;
        }

        return 0;
    }

    private void CleanupMembers()
    {
        for (int i = members.Count - 1; i >= 0; i--)
        {
            EnemySquadMember member = members[i];
            if (member != null)
                continue;

            members.RemoveAt(i);
        }
    }

    private void TickReinforcementRequests()
    {
        CleanupMembers();

        if (!enableReinforcementRequests)
        {
            ResetReinforcementState();
            return;
        }

        int currentCount = members.Count;
        int desiredCount = DesiredMemberCount;
        if (currentCount >= desiredCount)
        {
            ResetReinforcementState();
            return;
        }

        underStrengthTimer += Time.deltaTime;
        if (underStrengthTimer < Mathf.Max(0f, requestDelaySeconds))
            return;

        if (Time.time < nextAllowedRequestTime)
            return;

        EmitReinforcementRequest(desiredCount, currentCount);
        underStrengthTimer = 0f;
        nextAllowedRequestTime = Time.time + Mathf.Max(0f, requestCooldownSeconds);
    }

    private void EmitReinforcementRequest(int desiredCount, int currentCount)
    {
        Transform leader = GetLeaderTransform();
        Vector2 rallyPoint = leader != null ? (Vector2)leader.position : (Vector2)transform.position;

        ReinforcementRequest request = new ReinforcementRequest(
            this,
            GetSquadTeamId(),
            desiredCount,
            currentCount,
            focusTarget,
            rallyPoint,
            Time.time);

        if (request.IsValid)
            ReinforcementRequested?.Invoke(request);
    }

    private int GetSquadTeamId()
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member == null)
                continue;

            TeamAgent teamAgent = member.GetComponent<TeamAgent>();
            if (teamAgent != null)
                return teamAgent.TeamId;
        }

        return 0;
    }

    private void ResetReinforcementState()
    {
        underStrengthTimer = 0f;
        nextAllowedRequestTime = 0f;
    }
}
