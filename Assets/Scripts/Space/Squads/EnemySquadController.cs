using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [SerializeField] private float followerAvoidanceRadius = 8f;
    [SerializeField] private float followerAvoidanceStrength = 3.8f;

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

    [Header("Hostile Engagement Area")]
    [SerializeField] private bool useHostileEngagementArea = false;
    [SerializeField] private bool treatTeamlessFocusAsHostile = true;
    [SerializeField] private bool preferNearestHostileAnchor = false;
    [SerializeField] private float engagementGoalRefreshInterval = 0.9f;
    [SerializeField] private float engagementGoalHysteresisDistance = 6f;
    [SerializeField] private float engagementRadiusMin = 18f;
    [SerializeField] private float engagementRadiusMax = 80f;
    [SerializeField] private float engagementRadiusPadding = 10f;
    [SerializeField] private float engagementSectorArcDegrees = 140f;
    [SerializeField] private float engagementAnchorPredictionTime = 0.25f;
    [SerializeField] private float engagementTangentLeadDistance = 12f;
    [SerializeField] [Range(0.25f, 0.98f)] private float engagementInnerKeepoutRatio = 0.82f;
    [SerializeField] private float engagementKeepoutStrength = 2f;
    [SerializeField] private float enemyClusterSampleRadius = 120f;
    [SerializeField] private int minimumClusterAgentsForCentroid = 4;

    [Header("Waypoint Pathing")]
    [SerializeField] private bool useWaypointEngagementPaths = false;
    [SerializeField] private float waypointAnchorRadius = 360f;
    [SerializeField] [Range(3, 8)] private int waypointNodeCount = 4;
    [SerializeField] private float waypointSegmentSpacing = 45f;
    [SerializeField] private float waypointArrivalDistance = 8f;
    [SerializeField] private float waypointAnchorDriftDistance = 90f;
    [SerializeField] private float waypointRepathMinInterval = 0.25f;
    [SerializeField] private float waypointAngularJitterDegrees = 12f;
    [SerializeField] private float waypointRadialJitter = 10f;
    [SerializeField] [Range(10f, 175f)] private float waypointMaxTurnDegrees = 75f;
    [SerializeField] [Range(0.2f, 1.0f)] private float waypointPathInnerRadiusRatio = 0.45f;
    [SerializeField] [Range(0.25f, 1.2f)] private float waypointPathOuterRadiusRatio = 0.78f;
    [SerializeField] [Range(1f, 2f)] private float waypointApproachStartRadiusMultiplier = 1.15f;
    [SerializeField] [Range(0.2f, 1f)] private float waypointApproachStandoffRadiusRatio = 0.68f;

    [Header("Waypoint Blocker Sensing")]
    [SerializeField] private float forwardBlockerRange = 50f;
    [SerializeField] [Range(10f, 120f)] private float forwardBlockerConeAngle = 50f;
    [SerializeField] private float forwardBlockerPersistSeconds = 1.75f;
    [SerializeField] [Range(1, 8)] private int forwardBlockerThreshold = 1;
    [SerializeField] private LayerMask forwardBlockerMask = ~0;

    [Header("Leader Corridor")]
    [SerializeField] private bool useLeaderCorridorPath = false;
    [SerializeField] private float corridorRepathInterval = 0.9f;
    [SerializeField] private float corridorWaypointArriveDistance = 4f;
    [SerializeField] private float corridorArcDistance = 18f;
    [SerializeField] private float corridorLookaheadDistance = 14f;
    [SerializeField] private float corridorBlockProbeRadius = 8f;
    [SerializeField] private int corridorBlockerThreshold = 3;
    [SerializeField] private float corridorBlockedRepathCooldown = 0.35f;
    [SerializeField] private LayerMask corridorBlockMask = ~0;

    [Header("Leader Collision Avoidance")]
    [SerializeField] private bool usePredictiveLeaderCollisionAvoidance = false;
    [SerializeField] private bool avoidFriendlyCollisionTargets = true;
    [SerializeField] private float collisionAvoidanceLookAheadTime = 1.2f;
    [SerializeField] private float collisionAvoidanceProbeRadius = 26f;
    [SerializeField] private float collisionAvoidanceMinClosingSpeed = 0.75f;
    [SerializeField] private float collisionAvoidanceStrength = 16f;
    [SerializeField] private float collisionAvoidanceMaxOffset = 24f;
    [SerializeField] private float collisionAvoidanceAssumedSpeed = 10f;
    [SerializeField] private float collisionAvoidanceHardSeparationDistance = 8f;

    [Header("Leader Coordination")]
    [SerializeField] private bool useSquadLaneOffsets = false;
    [SerializeField] private float squadLaneSpacing = 10f;
    [SerializeField] private float squadRepulsionRadius = 26f;
    [SerializeField] private float squadRepulsionStrength = 8f;
    [SerializeField] private bool paceLeaderToFollowers = true;
    [SerializeField] private float leaderSlowStartLag = 6f;
    [SerializeField] private float leaderSlowStopLag = 18f;
    [SerializeField] [Range(0.05f, 1f)] private float leaderMinThrottleScale = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float hostileMinCruiseThrottle = 0.35f;

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

    [Header("Debug Navigation")]
    [SerializeField] private bool drawNavigationDebug = false;
    [SerializeField] private bool drawNavigationOnlyWhenSelected = true;
    [SerializeField] private bool drawEngagementAreaDebug = false;
    [SerializeField] private bool drawCorridorDebug = false;
    [SerializeField] private bool drawClusterVelocityDebug = false;
    [SerializeField] private bool drawNavigationLabels = false;
    [SerializeField] private bool drawLeaderGoalDebug = false;
    [SerializeField] private bool drawAvoidanceDebug = false;
    [SerializeField] private bool drawLegacyCorridorDebug = false;
    [SerializeField] private float navigationDebugMarkerSize = 1f;
    [SerializeField] private float navigationDebugLabelHeight = 2.5f;
    [SerializeField] private Color navigationGoalColor = new Color(1f, 0.92f, 0.16f, 0.95f);
    [SerializeField] private Color navigationEngagementColor = new Color(1f, 0.43f, 0.05f, 0.8f);
    [SerializeField] private Color navigationCorridorColor = new Color(0.2f, 0.85f, 1f, 0.95f);
    [SerializeField] private Color navigationClusterVelocityColor = new Color(0.45f, 1f, 0.45f, 0.95f);
    [SerializeField] private Color navigationAvoidanceColor = new Color(1f, 0.2f, 0.9f, 0.95f);
    [SerializeField] private Color navigationAvoidanceProbeColor = new Color(1f, 0.2f, 0.9f, 0.25f);

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
    private bool hasCachedHostileEngagementGoal;
    private Vector2 cachedHostileEngagementGoal;
    private float nextHostileEngagementGoalRefreshTime;
    private readonly Vector2[] leaderCorridorWaypoints = new Vector2[3];
    private int leaderCorridorWaypointCount;
    private int leaderCorridorWaypointIndex;
    private float nextLeaderCorridorRepathTime;
    private float nextLeaderBlockedRepathTime;
    private int leaderCorridorSideSign = 1;
    private int lastResolvedHostileTeamId = int.MinValue;
    private readonly Collider2D[] corridorBlockHits = new Collider2D[32];
    private readonly Collider2D[] forwardBlockerHits = new Collider2D[64];
    private SquadFlightPath currentFlightPath;
    private float forwardBlockerTimer;
    private float nextWaypointRepathAllowedTime;
    private int cachedPathHostileTeamId = int.MinValue;
    private int flightPathBuildSequence;
    private FlightPathRepathReason lastPathRepathReason = FlightPathRepathReason.None;

    private Vector2 debugLastLeaderPosition;
    private bool debugHasLeaderGoal;
    private Vector2 debugLeaderGoal;
    private float debugLeaderThrottle;
    private string debugLeaderMode = "idle";
    private bool debugHasEngagementData;
    private Vector2 debugEngagementCenter;
    private float debugEngagementRadius;
    private Vector2 debugEngagementPoint;
    private Vector2 debugClusterVelocity;

    private Vector2 debugCollisionAvoidanceOffset;
    private float debugCollisionAvoidanceProbeRadius;
    private int debugCollisionThreatCount;
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
    public int CurrentPathNodeCount => currentFlightPath != null ? currentFlightPath.NodeCount : 0;
    public int CurrentPathNodeIndex => currentFlightPath != null ? currentFlightPath.CurrentIndex : -1;
    public FlightPathRepathReason LastRepathReason => lastPathRepathReason;
    public bool IsFollowingWaypointPath =>
        currentState == EnemySquadState.Engage &&
        useWaypointEngagementPaths &&
        currentFlightPath != null &&
        currentFlightPath.HasCurrentNode;

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
        ResetLeaderNavigationState();

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
        ResetLeaderNavigationState();
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
        ResetLeaderNavigationState();
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
        if (currentState == nextState)
            return;

        currentState = nextState;
        if (currentState != EnemySquadState.Engage)
            ClearCurrentFlightPath(FlightPathRepathReason.Invalid);
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
        if (focusTarget == target)
            return;

        focusTarget = target;
        ClearCurrentFlightPath(FlightPathRepathReason.TargetChanged);
        ResetLeaderNavigationState();
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
        debugLastLeaderPosition = leaderPos;

        if (focusTarget == null)
        {
            debugHasEngagementData = false;
            ClearDebugLeaderGoal(leaderPos, "no-focus");
            return false;
        }

        if (TryGetFriendlyFollowGoal(leaderPos, out Vector2 friendlyGoal))
        {
            goalPos = friendlyGoal + ComputeSquadRepulsionOffset(leaderPos);
            goalPos += ComputeLeaderCollisionAvoidanceOffset(leader, leaderPos, goalPos);

            float friendlyDist = Vector2.Distance(leaderPos, goalPos);
            throttle01 = ComputeThrottleFromDistance(friendlyDist, leaderArriveDistance, leaderFullThrottleDistance);
            throttle01 *= ComputeLeaderPacingScale(leader);
            throttle01 = Mathf.Clamp01(throttle01);

            debugHasEngagementData = false;
            SetDebugLeaderGoal(leaderPos, goalPos, throttle01, "friendly-follow");
            return true;
        }

        if (currentState == EnemySquadState.Engage &&
            useWaypointEngagementPaths &&
            TryGetWaypointEngagementGoal(leader, leaderPos, out Vector2 waypointGoal, out FlightPathRepathReason repathReason))
        {
            goalPos = waypointGoal;
            goalPos += ComputeSquadRepulsionOffset(leaderPos);
            goalPos += ComputeLeaderCollisionAvoidanceOffset(leader, leaderPos, goalPos);
            goalPos = ApplyHostileDistanceBias(leaderPos, goalPos);

            float distToGoal = Vector2.Distance(leaderPos, goalPos);
            throttle01 = ComputeThrottleFromDistance(distToGoal, leaderArriveDistance, leaderFullThrottleDistance);
            throttle01 *= ComputeLeaderPacingScale(leader);
            throttle01 = Mathf.Max(throttle01, Mathf.Clamp01(hostileMinCruiseThrottle));
            throttle01 = Mathf.Clamp01(throttle01);

            SetDebugLeaderGoal(leaderPos, goalPos, throttle01, $"hostile-waypath[{repathReason}]");
            return true;
        }

        if (TryGetHostileEngagementGoal(leaderPos, out Vector2 hostileEngagementGoal))
        {
            if (useLeaderCorridorPath)
            {
                UpdateLeaderCorridorState(leaderPos, hostileEngagementGoal);
                goalPos = GetCurrentLeaderCorridorGoal(hostileEngagementGoal);
            }
            else
            {
                goalPos = hostileEngagementGoal;
            }

            goalPos += ComputeSquadRepulsionOffset(leaderPos);
            goalPos += ComputeLeaderCollisionAvoidanceOffset(leader, leaderPos, goalPos);
            goalPos = ApplyHostileDistanceBias(leaderPos, goalPos);

            float distToGoal = Vector2.Distance(leaderPos, goalPos);
            throttle01 = ComputeThrottleFromDistance(distToGoal, leaderArriveDistance, leaderFullThrottleDistance);
            throttle01 *= ComputeLeaderPacingScale(leader);
            throttle01 = Mathf.Max(throttle01, Mathf.Clamp01(hostileMinCruiseThrottle));
            throttle01 = Mathf.Clamp01(throttle01);

            SetDebugLeaderGoal(
                leaderPos,
                goalPos,
                throttle01,
                useLeaderCorridorPath ? "hostile-corridor" : "hostile-engage");
            return true;
        }

        Vector2 focusPos = GetPredictedFocusPosition();
        Vector2 toFocus = focusPos - leaderPos;
        float focusDistance = toFocus.magnitude;

        if (focusDistance < 0.001f)
        {
            debugHasEngagementData = false;
            SetDebugLeaderGoal(leaderPos, leaderPos, 0f, "hold");
            return true;
        }

        float desired = Mathf.Max(1f, leaderDesiredDistanceFromFocus);
        Vector2 dir = toFocus / focusDistance;
        Vector2 right = new Vector2(dir.y, -dir.x);

        Vector2 laneOffset = ComputeSquadLaneOffset(right);
        Vector2 repulsionOffset = ComputeSquadRepulsionOffset(leaderPos);
        goalPos = focusPos - dir * desired + laneOffset + repulsionOffset;
        goalPos += ComputeLeaderCollisionAvoidanceOffset(leader, leaderPos, goalPos);

        float distFallback = Vector2.Distance(leaderPos, goalPos);
        throttle01 = ComputeThrottleFromDistance(distFallback, leaderArriveDistance, leaderFullThrottleDistance);

        if (focusDistance < desired - leaderArriveDistance)
            throttle01 = 1f;

        throttle01 *= ComputeLeaderPacingScale(leader);
        throttle01 = Mathf.Clamp01(throttle01);

        debugHasEngagementData = false;
        SetDebugLeaderGoal(leaderPos, goalPos, throttle01, "fallback-chase");
        return true;
    }

    private bool TryGetHostileEngagementGoal(Vector2 leaderPos, out Vector2 goalPos)
    {
        goalPos = leaderPos;

        if (!useHostileEngagementArea || !IsHostileOrUnknownFocusTarget())
        {
            debugHasEngagementData = false;
            return false;
        }

        bool shouldRefresh = !hasCachedHostileEngagementGoal || Time.time >= nextHostileEngagementGoalRefreshTime;
        if (shouldRefresh)
        {
            Vector2 candidate = ComputeHostileEngagementPoint(leaderPos);
            if (hasCachedHostileEngagementGoal &&
                Vector2.Distance(candidate, cachedHostileEngagementGoal) <= Mathf.Max(0f, engagementGoalHysteresisDistance))
            {
                candidate = cachedHostileEngagementGoal;
            }

            cachedHostileEngagementGoal = candidate;
            hasCachedHostileEngagementGoal = true;
            nextHostileEngagementGoalRefreshTime = Time.time + Mathf.Max(0.05f, engagementGoalRefreshInterval);
        }

        goalPos = hasCachedHostileEngagementGoal ? cachedHostileEngagementGoal : ComputeHostileEngagementPoint(leaderPos);
        return true;
    }

    private bool TryGetWaypointEngagementGoal(
        Transform leader,
        Vector2 leaderPos,
        out Vector2 goalPos,
        out FlightPathRepathReason repathReason)
    {
        goalPos = leaderPos;
        repathReason = FlightPathRepathReason.None;

        if (leader == null)
        {
            ClearCurrentFlightPath(FlightPathRepathReason.Invalid);
            debugHasEngagementData = false;
            return false;
        }

        bool hasHostileContext = IsHostileOrUnknownFocusTarget() || HasAnyHostileAgents(GetSquadTeamId());
        if (!hasHostileContext)
        {
            ClearCurrentFlightPath(FlightPathRepathReason.Invalid);
            debugHasEngagementData = false;
            return false;
        }
        ResolveWaypointAnchor(out Vector2 anchorCenter, out Vector2 anchorVelocity, out float anchorExtent);
        anchorCenter += anchorVelocity * Mathf.Max(0f, engagementAnchorPredictionTime);

        float configuredRadius = Mathf.Max(25f, waypointAnchorRadius);
        float anchorRadius = Mathf.Max(configuredRadius, anchorExtent + Mathf.Max(0f, engagementRadiusPadding * 0.75f));

        int hostileTeamId = lastResolvedHostileTeamId;
        if (TryResolveHostileTeamId(anchorCenter, out int resolvedHostileTeamId))
        {
            hostileTeamId = resolvedHostileTeamId;
            lastResolvedHostileTeamId = resolvedHostileTeamId;
        }

        float innerRatio = Mathf.Clamp(waypointPathInnerRadiusRatio, 0.2f, 0.95f);
        float outerRatio = Mathf.Clamp(waypointPathOuterRadiusRatio, innerRatio + 0.05f, 1.2f);
        float pathMinRadius = Mathf.Max(20f, anchorRadius * innerRatio);
        float pathMaxRadius = Mathf.Max(pathMinRadius + 1f, anchorRadius * outerRatio);

        float distanceToAnchor = Vector2.Distance(leaderPos, anchorCenter);
        float approachStartRadius = Mathf.Max(pathMaxRadius, anchorRadius * Mathf.Max(1f, waypointApproachStartRadiusMultiplier));
        if (distanceToAnchor > approachStartRadius)
        {
            ClearCurrentFlightPath(FlightPathRepathReason.None);

            Vector2 toAnchor = anchorCenter - leaderPos;
            if (toAnchor.sqrMagnitude < 0.0001f)
                toAnchor = leader != null ? (Vector2)leader.up : Vector2.up;
            if (toAnchor.sqrMagnitude < 0.0001f)
                toAnchor = Vector2.up;

            Vector2 approachDir = toAnchor.normalized;
            float desiredApproachRadius = anchorRadius * Mathf.Clamp(waypointApproachStandoffRadiusRatio, 0.2f, 1f);
            float standoff = Mathf.Clamp(
                Mathf.Max(leaderDesiredDistanceFromFocus, desiredApproachRadius),
                pathMinRadius,
                pathMaxRadius);
            goalPos = anchorCenter - (approachDir * standoff);

            debugHasEngagementData = true;
            debugEngagementCenter = anchorCenter;
            debugEngagementRadius = anchorRadius;
            debugEngagementPoint = goalPos;
            debugClusterVelocity = anchorVelocity;

            repathReason = FlightPathRepathReason.None;
            return true;
        }


        AdvanceCurrentWaypointIfReached(leaderPos);

        FlightPathRepathReason requestedReason =
            EvaluateWaypointRepathReason(leader, leaderPos, anchorCenter, hostileTeamId);
        bool needsPath = currentFlightPath == null || !currentFlightPath.HasCurrentNode;

        if (requestedReason != FlightPathRepathReason.None || needsPath)
        {
            if (!needsPath &&
                requestedReason != FlightPathRepathReason.Completed &&
                requestedReason != FlightPathRepathReason.TargetChanged &&
                requestedReason != FlightPathRepathReason.Invalid &&
                Time.time < nextWaypointRepathAllowedTime)
            {
                requestedReason = FlightPathRepathReason.None;
            }

            if (requestedReason == FlightPathRepathReason.None && needsPath)
                requestedReason = FlightPathRepathReason.Invalid;

            if (requestedReason != FlightPathRepathReason.None)
            {
                BuildWaypointFlightPath(leaderPos, anchorCenter, anchorRadius, hostileTeamId, requestedReason);
                repathReason = requestedReason;
            }
        }

        if (currentFlightPath == null || !currentFlightPath.HasCurrentNode)
        {
            debugHasEngagementData = true;
            debugEngagementCenter = anchorCenter;
            debugEngagementRadius = anchorRadius;
            debugEngagementPoint = leaderPos;
            debugClusterVelocity = anchorVelocity;
            return false;
        }

        goalPos = currentFlightPath.CurrentNode;

        debugHasEngagementData = true;
        debugEngagementCenter = anchorCenter;
        debugEngagementRadius = anchorRadius;
        debugEngagementPoint = goalPos;
        debugClusterVelocity = anchorVelocity;

        repathReason = repathReason != FlightPathRepathReason.None ? repathReason : lastPathRepathReason;
        return true;
    }

    private void ResolveWaypointAnchor(out Vector2 center, out Vector2 velocity, out float extent)
    {
        center = GetPredictedFocusPosition();
        velocity = Vector2.zero;
        extent = 0f;

        if (preferNearestHostileAnchor &&
            TryResolveNearestHostileAnchor(GetSquadTeamId(), transform.position, out Vector2 nearestCenter, out Vector2 nearestVelocity, out float nearestExtent, out int nearestHostileTeamId))
        {
            center = nearestCenter;
            velocity = nearestVelocity;
            extent = nearestExtent;
            lastResolvedHostileTeamId = nearestHostileTeamId;
            return;
        }

        if (TryResolveHostileCluster(out Vector2 clusterCenter, out Vector2 clusterVelocity, out float clusterExtent))
        {
            center = clusterCenter;
            velocity = clusterVelocity;
            extent = clusterExtent;
            return;
        }

        if (focusTarget != null)
        {
            Rigidbody2D focusRb = focusTarget.GetComponent<Rigidbody2D>();
            if (focusRb != null)
                velocity = focusRb.linearVelocity;
        }
    }

    private bool TryResolveNearestHostileAnchor(
        int squadTeamId,
        Vector2 seed,
        out Vector2 center,
        out Vector2 averageVelocity,
        out float extent,
        out int hostileTeamId)
    {
        center = seed;
        averageVelocity = Vector2.zero;
        extent = 0f;
        hostileTeamId = int.MinValue;

        TeamAgent nearest = null;
        float nearestDistanceSq = float.PositiveInfinity;

        IReadOnlyList<TeamAgent> agents = TeamRegistry.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            if (!TeamRegistry.IsHostile(squadTeamId, agent.TeamId))
                continue;

            float distanceSq = ((Vector2)agent.transform.position - seed).sqrMagnitude;
            if (distanceSq >= nearestDistanceSq)
                continue;

            nearestDistanceSq = distanceSq;
            nearest = agent;
        }

        if (nearest == null)
            return false;

        hostileTeamId = nearest.TeamId;

        Vector2 summedPos = Vector2.zero;
        Vector2 summedVel = Vector2.zero;
        int sampledCount = 0;

        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled || agent.TeamId != hostileTeamId)
                continue;

            sampledCount++;
            summedPos += (Vector2)agent.transform.position;

            Rigidbody2D rb = agent.GetComponent<Rigidbody2D>();
            if (rb != null)
                summedVel += rb.linearVelocity;
        }

        if (sampledCount <= 0)
            return false;

        center = summedPos / sampledCount;
        averageVelocity = summedVel / sampledCount;

        float maxDistance = 0f;
        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled || agent.TeamId != hostileTeamId)
                continue;

            float distance = Vector2.Distance(center, agent.transform.position);
            if (distance > maxDistance)
                maxDistance = distance;
        }

        extent = maxDistance;
        return true;
    }

    private void AdvanceCurrentWaypointIfReached(Vector2 leaderPos)
    {
        if (currentFlightPath == null || !currentFlightPath.HasCurrentNode)
            return;

        float arriveDistance = Mathf.Max(0.25f, waypointArrivalDistance);
        while (currentFlightPath.HasCurrentNode &&
               Vector2.Distance(leaderPos, currentFlightPath.CurrentNode) <= arriveDistance)
        {
            currentFlightPath.CurrentIndex++;
        }

        if (currentFlightPath != null && currentFlightPath.CurrentIndex >= currentFlightPath.NodeCount)
            lastPathRepathReason = FlightPathRepathReason.Completed;
    }

    private FlightPathRepathReason EvaluateWaypointRepathReason(
        Transform leader,
        Vector2 leaderPos,
        Vector2 anchorCenter,
        int hostileTeamId)
    {
        if (currentFlightPath == null)
            return FlightPathRepathReason.Invalid;

        if (!currentFlightPath.HasCurrentNode || currentFlightPath.NodeCount <= 0)
            return FlightPathRepathReason.Completed;

        if (cachedPathHostileTeamId != int.MinValue &&
            hostileTeamId != int.MinValue &&
            cachedPathHostileTeamId != hostileTeamId)
        {
            return FlightPathRepathReason.TargetChanged;
        }

        float driftDistance = Vector2.Distance(anchorCenter, currentFlightPath.AnchorCenter);
        if (driftDistance > Mathf.Max(1f, waypointAnchorDriftDistance))
            return FlightPathRepathReason.AnchorDrift;

        if (IsWaypointForwardBlocked(leader, leaderPos))
            return FlightPathRepathReason.Blocked;

        return FlightPathRepathReason.None;
    }

    private bool IsWaypointForwardBlocked(Transform leader, Vector2 leaderPos)
    {
        if (currentFlightPath == null || !currentFlightPath.HasCurrentNode)
        {
            forwardBlockerTimer = 0f;
            return false;
        }

        float range = Mathf.Max(0.5f, forwardBlockerRange);
        float coneHalf = Mathf.Clamp(forwardBlockerConeAngle, 5f, 170f) * 0.5f;
        Vector2 forward = currentFlightPath.CurrentNode - leaderPos;

        if (forward.sqrMagnitude < 0.0001f)
            forward = leader != null ? (Vector2)leader.up : Vector2.up;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector2.up;

        forward.Normalize();

        int hitCount = Physics2D.OverlapCircleNonAlloc(leaderPos, range, forwardBlockerHits, forwardBlockerMask);
        int blockers = 0;
        int threshold = Mathf.Max(1, forwardBlockerThreshold);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = forwardBlockerHits[i];
            if (hit == null)
                continue;

            Transform hitTransform = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform;
            if (hitTransform == null)
                continue;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            TeamAgent agent = hit.GetComponentInParent<TeamAgent>();
            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            EnemySquadMember hitMember = agent.GetComponent<EnemySquadMember>();
            if (hitMember != null && hitMember.Squad == this)
                continue;

            Vector2 toHit = (Vector2)agent.transform.position - leaderPos;
            float dist = toHit.magnitude;
            if (dist > range || dist < 0.001f)
                continue;

            Vector2 dirToHit = toHit / dist;
            float angle = Vector2.Angle(forward, dirToHit);
            if (angle > coneHalf)
                continue;

            blockers++;
            if (blockers >= threshold)
                break;
        }

        bool blockedNow = blockers >= threshold;
        if (blockedNow)
            forwardBlockerTimer += Time.deltaTime;
        else
            forwardBlockerTimer = 0f;

        return blockedNow && forwardBlockerTimer >= Mathf.Max(0f, forwardBlockerPersistSeconds);
    }

    private void BuildWaypointFlightPath(
        Vector2 leaderPos,
        Vector2 anchorCenter,
        float anchorRadius,
        int hostileTeamId,
        FlightPathRepathReason reason)
    {
        int nodeCount = Mathf.Clamp(waypointNodeCount, 3, 8);
        float sideSign = ResolveBaselineEngagementSide(GetSquadTeamId());
        if (Mathf.Abs(sideSign) < 0.01f)
            sideSign = ((GetInstanceID() & 1) == 0) ? 1f : -1f;

        int seed = GetInstanceID();
        seed ^= GetSquadTeamId() * 73856093;
        seed ^= hostileTeamId * 19349663;
        seed ^= flightPathBuildSequence * 83492791;

        Vector2[] nodes = BuildWaypointNodes(leaderPos, anchorCenter, anchorRadius, sideSign, nodeCount, seed);

        currentFlightPath = new SquadFlightPath(
            nodes,
            0,
            Time.time,
            anchorCenter,
            anchorRadius,
            sideSign,
            reason);

        cachedPathHostileTeamId = hostileTeamId;
        flightPathBuildSequence++;
        lastPathRepathReason = reason;
        nextWaypointRepathAllowedTime = Time.time + Mathf.Max(0.01f, waypointRepathMinInterval);
        forwardBlockerTimer = 0f;
    }

    private Vector2[] BuildWaypointNodes(
        Vector2 leaderPos,
        Vector2 anchorCenter,
        float anchorRadius,
        float sideSign,
        int nodeCount,
        int seed)
    {
        Vector2[] nodes = new Vector2[Mathf.Max(0, nodeCount)];
        if (nodes.Length == 0)
            return nodes;

        Vector2 radial = leaderPos - anchorCenter;
        if (radial.sqrMagnitude < 0.0001f)
            radial = transform.up;
        if (radial.sqrMagnitude < 0.0001f)
            radial = Vector2.up;

        float startAngle = Mathf.Atan2(radial.y, radial.x);
        float spacing = Mathf.Max(5f, waypointSegmentSpacing);
        float arcStep = spacing / Mathf.Max(30f, anchorRadius);
        arcStep = Mathf.Clamp(arcStep, 0.10f, 0.70f);

        float innerRatio = Mathf.Clamp(waypointPathInnerRadiusRatio, 0.2f, 0.95f);
        float outerRatio = Mathf.Clamp(waypointPathOuterRadiusRatio, innerRatio + 0.05f, 1.2f);
        float minPathRadius = Mathf.Max(20f, anchorRadius * innerRatio);
        float maxPathRadius = Mathf.Max(minPathRadius + 1f, anchorRadius * outerRatio);
        float currentRadius = Vector2.Distance(leaderPos, anchorCenter);
        float baseBandT = Mathf.InverseLerp(minPathRadius, maxPathRadius, currentRadius);
        float buildJitter = (Deterministic01(seed + 5) - 0.5f) * 0.35f;
        baseBandT = Mathf.Clamp01(baseBandT + buildJitter);
        float baseRadius = Mathf.Lerp(minPathRadius, maxPathRadius, baseBandT);

        float maxTurn = Mathf.Clamp(waypointMaxTurnDegrees, 10f, 175f);

        Vector2 previousPoint = leaderPos;
        Vector2 previousDir = radial.normalized;

        for (int i = 0; i < nodes.Length; i++)
        {
            float angularJitter01 = Deterministic01(seed + (i * 17) + 3);
            float radialJitter01 = Deterministic01(seed + (i * 17) + 11);

            float jitterDegrees = (angularJitter01 - 0.5f) * 2f * Mathf.Max(0f, waypointAngularJitterDegrees);
            float angle = startAngle + (arcStep * (i + 1) * Mathf.Sign(sideSign)) + (jitterDegrees * Mathf.Deg2Rad);

            float radiusJitter = (radialJitter01 - 0.5f) * 2f * Mathf.Max(0f, waypointRadialJitter);
            float radius = Mathf.Clamp(baseRadius + radiusJitter, minPathRadius, maxPathRadius);

            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 candidate = anchorCenter + dir * radius;

            candidate = ClampWaypointTurn(previousPoint, previousDir, candidate, spacing, maxTurn);

            nodes[i] = candidate;

            Vector2 toNode = candidate - previousPoint;
            if (toNode.sqrMagnitude > 0.0001f)
                previousDir = toNode.normalized;

            previousPoint = candidate;
        }

        return nodes;
    }

    private Vector2 ClampWaypointTurn(
        Vector2 origin,
        Vector2 previousDirection,
        Vector2 candidate,
        float segmentSpacing,
        float maxTurnDegrees)
    {
        Vector2 toCandidate = candidate - origin;
        float dist = toCandidate.magnitude;
        if (dist < 0.001f)
            return origin + previousDirection.normalized * Mathf.Max(5f, segmentSpacing);

        Vector2 desiredDir = toCandidate / dist;
        Vector2 prevDir = previousDirection.sqrMagnitude > 0.0001f ? previousDirection.normalized : desiredDir;

        float turnAngle = Vector2.Angle(prevDir, desiredDir);
        if (turnAngle <= maxTurnDegrees)
            return candidate;

        float cross = (prevDir.x * desiredDir.y) - (prevDir.y * desiredDir.x);
        float sign = Mathf.Abs(cross) < 0.001f ? 1f : Mathf.Sign(cross);
        Vector2 limitedDir = RotateDirection(prevDir, maxTurnDegrees * sign).normalized;
        return origin + limitedDir * Mathf.Max(5f, segmentSpacing);
    }

    private static float Deterministic01(int seed)
    {
        unchecked
        {
            int n = seed;
            n = (n << 13) ^ n;
            int nn = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
            return nn / 2147483647f;
        }
    }

    private void ClearCurrentFlightPath(FlightPathRepathReason reason)
    {
        currentFlightPath = null;
        cachedPathHostileTeamId = int.MinValue;
        forwardBlockerTimer = 0f;

        if (reason != FlightPathRepathReason.None)
            lastPathRepathReason = reason;
    }
    private Vector2 ComputeHostileEngagementPoint(Vector2 leaderPos)
    {
        Vector2 center = GetPredictedFocusPosition();
        Vector2 averageVelocity = Vector2.zero;
        float clusterExtent = 0f;

        if (TryResolveHostileCluster(out Vector2 clusterCenter, out Vector2 clusterVelocity, out float extent))
        {
            center = clusterCenter;
            averageVelocity = clusterVelocity;
            clusterExtent = extent;
        }

        center += averageVelocity * Mathf.Max(0f, engagementAnchorPredictionTime);

        float radius = ComputeEngagementRadius(clusterExtent);
        Vector2 radial = leaderPos - center;
        if (radial.sqrMagnitude < 0.0001f)
            radial = transform.up;

        if (radial.sqrMagnitude < 0.0001f)
            radial = Vector2.up;

        float sectorOffsetDegrees = ComputeEngagementSectorOffsetDegrees();
        Vector2 direction = RotateDirection(radial.normalized, sectorOffsetDegrees);
        float sideSign = Mathf.Abs(sectorOffsetDegrees) > 1f
            ? Mathf.Sign(sectorOffsetDegrees)
            : ResolveBaselineEngagementSide(GetSquadTeamId());
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        float tangentLead = Mathf.Max(0f, engagementTangentLeadDistance);
        Vector2 point = center + (direction * radius) + (tangent * tangentLead * sideSign);

        debugHasEngagementData = true;
        debugEngagementCenter = center;
        debugEngagementRadius = radius;
        debugEngagementPoint = point;
        debugClusterVelocity = averageVelocity;

        return point;
    }

    private Vector2 ApplyHostileDistanceBias(Vector2 leaderPos, Vector2 goalPos)
    {
        if (!debugHasEngagementData)
            return goalPos;

        if (IsFollowingWaypointPath)
            return goalPos;

        float desiredRadius = Mathf.Max(1f, debugEngagementRadius);
        float keepoutRatio = Mathf.Clamp(engagementInnerKeepoutRatio, 0.25f, 0.98f);
        float innerKeepoutRadius = desiredRadius * keepoutRatio;

        Vector2 fromCenter = leaderPos - debugEngagementCenter;
        float distToCenter = fromCenter.magnitude;
        if (distToCenter >= innerKeepoutRadius)
            return goalPos;

        Vector2 outward = distToCenter > 0.001f ? fromCenter / distToCenter : (goalPos - debugEngagementCenter);
        if (outward.sqrMagnitude < 0.0001f)
            outward = Vector2.up;
        else
            outward.Normalize();

        float pushDistance = (innerKeepoutRadius - distToCenter) * Mathf.Max(0f, engagementKeepoutStrength);
        return goalPos + outward * pushDistance;
    }

    private bool TryResolveHostileCluster(out Vector2 center, out Vector2 averageVelocity, out float extent)
    {
        center = Vector2.zero;
        averageVelocity = Vector2.zero;
        extent = 0f;

        if (focusTarget == null)
            return false;

        Vector2 seed = GetPredictedFocusPosition();
        if (!TryResolveHostileTeamId(seed, out int hostileTeamId))
        {
            center = seed;
            Rigidbody2D focusRbFallback = focusTarget.GetComponent<Rigidbody2D>();
            averageVelocity = focusRbFallback != null ? focusRbFallback.linearVelocity : Vector2.zero;
            extent = 0f;
            return true;
        }

        lastResolvedHostileTeamId = hostileTeamId;

        float sampleRadius = Mathf.Max(1f, enemyClusterSampleRadius);
        float sampleRadiusSq = sampleRadius * sampleRadius;

        Vector2 summedPos = Vector2.zero;
        Vector2 summedVel = Vector2.zero;
        int sampledCount = 0;
        float maxDistFromSeed = 0f;

        IReadOnlyList<TeamAgent> agents = TeamRegistry.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled || agent.TeamId != hostileTeamId)
                continue;

            Vector2 pos = agent.transform.position;
            Vector2 delta = pos - seed;
            float d2 = delta.sqrMagnitude;
            if (d2 > sampleRadiusSq)
                continue;

            sampledCount++;
            summedPos += pos;

            Rigidbody2D rb = agent.GetComponent<Rigidbody2D>();
            if (rb != null)
                summedVel += rb.linearVelocity;

            float dist = Mathf.Sqrt(d2);
            if (dist > maxDistFromSeed)
                maxDistFromSeed = dist;
        }

        if (sampledCount <= 0)
        {
            center = seed;
            Rigidbody2D focusRbFallback = focusTarget.GetComponent<Rigidbody2D>();
            averageVelocity = focusRbFallback != null ? focusRbFallback.linearVelocity : Vector2.zero;
            extent = 0f;
            return true;
        }

        int minForCentroid = Mathf.Max(1, minimumClusterAgentsForCentroid);
        center = sampledCount >= minForCentroid ? (summedPos / sampledCount) : seed;
        averageVelocity = summedVel / sampledCount;

        if (averageVelocity.sqrMagnitude < 0.0001f)
        {
            Rigidbody2D focusRbFallback = focusTarget.GetComponent<Rigidbody2D>();
            if (focusRbFallback != null)
                averageVelocity = focusRbFallback.linearVelocity;
        }

        extent = maxDistFromSeed;
        return true;
    }

    private bool TryResolveHostileTeamId(Vector2 seed, out int hostileTeamId)
    {
        hostileTeamId = int.MinValue;

        TeamAgent focusTeamAgent = focusTarget != null ? focusTarget.GetComponentInParent<TeamAgent>() : null;
        if (focusTeamAgent != null)
        {
            hostileTeamId = focusTeamAgent.TeamId;
            return true;
        }

        int squadTeamId = GetSquadTeamId();
        float bestDistanceSq = float.PositiveInfinity;

        IReadOnlyList<TeamAgent> agents = TeamRegistry.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            if (!TeamRegistry.IsHostile(squadTeamId, agent.TeamId))
                continue;

            float distanceSq = ((Vector2)agent.transform.position - seed).sqrMagnitude;
            if (distanceSq >= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            hostileTeamId = agent.TeamId;
        }

        return hostileTeamId != int.MinValue;
    }

    private float ComputeEngagementRadius(float clusterExtent)
    {
        float minRadius = Mathf.Max(1f, engagementRadiusMin);
        float maxRadius = Mathf.Max(minRadius + 0.5f, engagementRadiusMax);
        float desired = Mathf.Max(leaderDesiredDistanceFromFocus, clusterExtent + Mathf.Max(0f, engagementRadiusPadding));
        return Mathf.Clamp(desired, minRadius, maxRadius);
    }

    private float ComputeEngagementSectorOffsetDegrees()
    {
        float arc = Mathf.Max(0f, engagementSectorArcDegrees);
        if (arc <= 0f)
            return 0f;

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
        {
            float baselineSide = ResolveBaselineEngagementSide(myTeamId);
            return (arc * 0.5f) * baselineSide;
        }

        float step = arc / Mathf.Max(1, total - 1);
        float centered = rank - ((total - 1) * 0.5f);
        return centered * step;
    }

    private float ResolveBaselineEngagementSide(int myTeamId)
    {
        if (lastResolvedHostileTeamId != int.MinValue && lastResolvedHostileTeamId != myTeamId)
            return ComputeTeamPairSideSign(myTeamId, lastResolvedHostileTeamId);

        TeamAgent focusTeam = focusTarget != null ? focusTarget.GetComponentInParent<TeamAgent>() : null;
        if (focusTeam != null && focusTeam.TeamId != myTeamId)
            return ComputeTeamPairSideSign(myTeamId, focusTeam.TeamId);

        return leaderCorridorSideSign >= 0 ? 1f : -1f;
    }

    private static float ComputeTeamPairSideSign(int teamA, int teamB)
    {
        int minTeam = Mathf.Min(teamA, teamB);
        int maxTeam = Mathf.Max(teamA, teamB);
        int hash = (minTeam * 73856093) ^ (maxTeam * 19349663);

        // Stable random side for this pair, but opposite sign per team so they split around the target.
        float pairSeedSign = (hash & 1) == 0 ? 1f : -1f;
        return teamA < teamB ? pairSeedSign : -pairSeedSign;
    }

    private static bool HasAnyHostileAgents(int squadTeamId)
    {
        IReadOnlyList<TeamAgent> agents = TeamRegistry.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            if (TeamRegistry.IsHostile(squadTeamId, agent.TeamId))
                return true;
        }

        return false;
    }

    private bool IsHostileOrUnknownFocusTarget()
    {
        if (focusTarget == null)
            return false;

        TeamAgent focusTeamAgent = focusTarget.GetComponentInParent<TeamAgent>();
        if (focusTeamAgent == null)
            return treatTeamlessFocusAsHostile;

        int squadTeamId = GetSquadTeamId();
        return TeamRegistry.IsHostile(squadTeamId, focusTeamAgent.TeamId);
    }

    private void UpdateLeaderCorridorState(Vector2 leaderPos, Vector2 engagementGoal)
    {
        bool blockedAhead = IsLeaderCorridorBlockedAhead(leaderPos);
        bool shouldRebuild = ShouldRebuildLeaderCorridor(engagementGoal);

        if (blockedAhead && Time.time >= nextLeaderBlockedRepathTime)
        {
            leaderCorridorSideSign *= -1;
            nextLeaderBlockedRepathTime = Time.time + Mathf.Max(0.05f, corridorBlockedRepathCooldown);
            shouldRebuild = true;
        }

        if (shouldRebuild)
        {
            BuildLeaderCorridor(leaderPos, engagementGoal);
            nextLeaderCorridorRepathTime = Time.time + Mathf.Max(0.1f, corridorRepathInterval);
        }

        if (!HasValidLeaderCorridor())
            return;

        float arriveDistance = Mathf.Max(0.25f, corridorWaypointArriveDistance);
        while (leaderCorridorWaypointIndex < leaderCorridorWaypointCount &&
               Vector2.Distance(leaderPos, leaderCorridorWaypoints[leaderCorridorWaypointIndex]) <= arriveDistance)
        {
            leaderCorridorWaypointIndex++;
        }
    }

    private Vector2 GetCurrentLeaderCorridorGoal(Vector2 engagementGoal)
    {
        if (!HasValidLeaderCorridor())
            return engagementGoal;

        if (leaderCorridorWaypointIndex >= leaderCorridorWaypointCount)
            return engagementGoal;

        return leaderCorridorWaypoints[leaderCorridorWaypointIndex];
    }

    private bool HasValidLeaderCorridor()
    {
        return leaderCorridorWaypointCount > 0;
    }

    private bool ShouldRebuildLeaderCorridor(Vector2 engagementGoal)
    {
        if (!HasValidLeaderCorridor())
            return true;

        if (Time.time >= nextLeaderCorridorRepathTime)
            return true;

        Vector2 last = leaderCorridorWaypoints[Mathf.Max(0, leaderCorridorWaypointCount - 1)];
        float driftThreshold = Mathf.Max(1f, engagementGoalHysteresisDistance * 0.75f);
        return Vector2.Distance(last, engagementGoal) > driftThreshold;
    }

    private void BuildLeaderCorridor(Vector2 leaderPos, Vector2 engagementGoal)
    {
        leaderCorridorWaypointCount = 0;
        leaderCorridorWaypointIndex = 0;

        Vector2 toGoal = engagementGoal - leaderPos;
        float dist = toGoal.magnitude;
        float arriveDistance = Mathf.Max(0.25f, corridorWaypointArriveDistance);

        if (dist <= Mathf.Max(arriveDistance * 1.5f, 2f))
        {
            leaderCorridorWaypoints[0] = engagementGoal;
            leaderCorridorWaypointCount = 1;
            return;
        }

        Vector2 forward = toGoal / dist;
        Vector2 right = new Vector2(forward.y, -forward.x);

        float sectorOffset = ComputeEngagementSectorOffsetDegrees();
        float sideSign = Mathf.Abs(sectorOffset) > 1f ? Mathf.Sign(sectorOffset) : leaderCorridorSideSign;

        float arcDistanceClamped = Mathf.Min(Mathf.Max(0f, corridorArcDistance), dist * 0.45f);
        float midForward = Mathf.Clamp(dist * 0.55f, 3f, dist - 1f);
        Vector2 mid = leaderPos + (forward * midForward) + (right * arcDistanceClamped * sideSign);

        if (arcDistanceClamped > 0.01f && leaderCorridorWaypointCount < leaderCorridorWaypoints.Length)
        {
            leaderCorridorWaypoints[leaderCorridorWaypointCount] = mid;
            leaderCorridorWaypointCount++;
        }

        if (dist > 30f && leaderCorridorWaypointCount < leaderCorridorWaypoints.Length - 1)
        {
            float lateForward = Mathf.Min(dist - arriveDistance, dist * 0.85f);
            Vector2 late = leaderPos + (forward * lateForward) + (right * arcDistanceClamped * 0.35f * sideSign);
            leaderCorridorWaypoints[leaderCorridorWaypointCount] = late;
            leaderCorridorWaypointCount++;
        }

        if (leaderCorridorWaypointCount < leaderCorridorWaypoints.Length)
        {
            leaderCorridorWaypoints[leaderCorridorWaypointCount] = engagementGoal;
            leaderCorridorWaypointCount++;
        }
        else
        {
            leaderCorridorWaypoints[leaderCorridorWaypoints.Length - 1] = engagementGoal;
        }
    }

    private bool IsLeaderCorridorBlockedAhead(Vector2 leaderPos)
    {
        if (!HasValidLeaderCorridor() || leaderCorridorWaypointIndex >= leaderCorridorWaypointCount)
            return false;

        Vector2 target = leaderCorridorWaypoints[leaderCorridorWaypointIndex];
        Vector2 toTarget = target - leaderPos;
        float dist = toTarget.magnitude;
        if (dist <= Mathf.Max(0.25f, corridorWaypointArriveDistance))
            return false;

        float lookahead = Mathf.Max(0.5f, corridorLookaheadDistance);
        Vector2 probePos = leaderPos + (toTarget / dist) * Mathf.Min(dist, lookahead);

        float probeRadius = Mathf.Max(0.5f, corridorBlockProbeRadius);
        int hitCount = Physics2D.OverlapCircleNonAlloc(probePos, probeRadius, corridorBlockHits, corridorBlockMask);
        if (hitCount <= 0)
            return false;

        int blockers = 0;
        int requiredBlockers = Mathf.Max(1, corridorBlockerThreshold);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = corridorBlockHits[i];
            if (hit == null)
                continue;

            Transform hitTransform = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform;
            if (hitTransform == null)
                continue;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            TeamAgent agent = hit.GetComponentInParent<TeamAgent>();
            if (agent == null)
                continue;

            EnemySquadMember hitMember = agent.GetComponent<EnemySquadMember>();
            if (hitMember != null && hitMember.Squad == this)
                continue;

            blockers++;
            if (blockers >= requiredBlockers)
                return true;
        }

        return false;
    }

    private Vector2 ComputeLeaderCollisionAvoidanceOffset(Transform leader, Vector2 leaderPos, Vector2 desiredGoal)
    {
        debugCollisionAvoidanceOffset = Vector2.zero;
        debugCollisionAvoidanceProbeRadius = Mathf.Max(0f, collisionAvoidanceProbeRadius);
        debugCollisionThreatCount = 0;

        if (!usePredictiveLeaderCollisionAvoidance || leader == null)
            return Vector2.zero;

        float probeRadius = Mathf.Max(1f, collisionAvoidanceProbeRadius);
        float lookAhead = Mathf.Max(0f, collisionAvoidanceLookAheadTime);
        float minClosingSpeed = Mathf.Max(0f, collisionAvoidanceMinClosingSpeed);
        float hardSeparationDistance = Mathf.Max(0f, collisionAvoidanceHardSeparationDistance);

        Rigidbody2D leaderRb = leader.GetComponent<Rigidbody2D>();
        Vector2 selfVelocity = leaderRb != null ? leaderRb.linearVelocity : Vector2.zero;

        Vector2 forward = desiredGoal - leaderPos;
        if (forward.sqrMagnitude < 0.0001f)
            forward = leader.up;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector2.up;
        forward.Normalize();

        float assumedSpeed = Mathf.Max(0f, collisionAvoidanceAssumedSpeed);
        Vector2 desiredVelocity = forward * Mathf.Max(selfVelocity.magnitude, assumedSpeed);
        Vector2 projectedSelfVelocity = Vector2.Lerp(selfVelocity, desiredVelocity, 0.65f);
        Vector2 futureSelf = leaderPos + projectedSelfVelocity * lookAhead;

        Vector2 right = new Vector2(forward.y, -forward.x);

        int myTeamId = GetSquadTeamId();
        int myLeaderId = leader.GetInstanceID();
        float probeRadiusSq = probeRadius * probeRadius;

        Vector2 aggregate = Vector2.zero;

        IReadOnlyList<TeamAgent> agents = TeamRegistry.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            TeamAgent agent = agents[i];
            if (agent == null || !agent.isActiveAndEnabled)
                continue;

            if (agent.transform == leader)
                continue;

            EnemySquadMember otherMember = agent.GetComponent<EnemySquadMember>();
            if (otherMember != null && otherMember.Squad == this)
                continue;

            bool hostile = TeamRegistry.IsHostile(myTeamId, agent.TeamId);
            if (!hostile && !avoidFriendlyCollisionTargets)
                continue;

            Rigidbody2D otherRb = agent.GetComponent<Rigidbody2D>();
            Vector2 otherVelocity = otherRb != null ? otherRb.linearVelocity : Vector2.zero;
            Vector2 futureOther = (Vector2)agent.transform.position + otherVelocity * lookAhead;

            Vector2 delta = futureSelf - futureOther;
            float distSq = delta.sqrMagnitude;
            if (distSq > probeRadiusSq)
                continue;

            float dist = Mathf.Sqrt(distSq);
            if (dist < 0.0001f)
            {
                float pairSign = myLeaderId < agent.transform.GetInstanceID() ? 1f : -1f;
                delta = right * pairSign;
                dist = 1f;
            }

            Vector2 away = delta / dist;
            Vector2 relativeVelocity = projectedSelfVelocity - otherVelocity;
            float closingSpeed = -Vector2.Dot(relativeVelocity, away);

            bool hardThreat = hardSeparationDistance > 0f && dist <= hardSeparationDistance;
            if (!hardThreat && closingSpeed < minClosingSpeed && dist > probeRadius * 0.9f)
                continue;

            float proximity01 = 1f - Mathf.Clamp01(dist / probeRadius);
            float closing01 = minClosingSpeed <= 0.01f
                ? 1f
                : Mathf.Clamp01(closingSpeed / Mathf.Max(0.1f, minClosingSpeed * 2.25f));
            float urgency = hardThreat
                ? 1f
                : Mathf.Clamp01((proximity01 * 0.75f) + (closing01 * 0.7f));
            if (urgency <= 0.001f)
                continue;

            Vector2 toOther = -delta;
            float sideFromOther = Mathf.Sign(Vector2.Dot(toOther, right));
            if (Mathf.Abs(sideFromOther) < 0.01f)
                sideFromOther = myLeaderId < agent.transform.GetInstanceID() ? 1f : -1f;

            Vector2 lateralAway = right * -sideFromOther;
            aggregate += (away * 0.7f + lateralAway * 1.1f) * urgency;
            debugCollisionThreatCount++;
        }

        if (debugCollisionThreatCount <= 0)
            return Vector2.zero;

        Vector2 offset = aggregate * Mathf.Max(0f, collisionAvoidanceStrength);
        float maxOffset = Mathf.Max(0f, collisionAvoidanceMaxOffset);
        if (maxOffset > 0.001f && offset.magnitude > maxOffset)
            offset = offset.normalized * maxOffset;

        debugCollisionAvoidanceOffset = offset;
        debugCollisionAvoidanceProbeRadius = probeRadius;
        return offset;
    }

    private static Vector2 RotateDirection(Vector2 direction, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    private void SetDebugLeaderGoal(Vector2 leaderPos, Vector2 goalPos, float throttle01, string mode)
    {
        debugLastLeaderPosition = leaderPos;
        debugHasLeaderGoal = true;
        debugLeaderGoal = goalPos;
        debugLeaderThrottle = throttle01;
        debugLeaderMode = mode;
    }

    private void ClearDebugLeaderGoal(Vector2 leaderPos, string mode)
    {
        debugLastLeaderPosition = leaderPos;
        debugHasLeaderGoal = false;
        debugLeaderGoal = leaderPos;
        debugLeaderThrottle = 0f;
        debugLeaderMode = mode;
    }

    private void OnDrawGizmos()
    {
        if (!drawNavigationDebug || drawNavigationOnlyWhenSelected)
            return;

        DrawNavigationDebug();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawNavigationDebug)
            return;

        DrawNavigationDebug();
    }

    private void DrawNavigationDebug()
    {
        if (!Application.isPlaying)
            return;

        Vector3 leaderWorld = LeaderMember != null ? LeaderMember.transform.position : (Vector3)debugLastLeaderPosition;
        float markerSize = Mathf.Max(0.05f, navigationDebugMarkerSize);

        if (drawLeaderGoalDebug && debugHasLeaderGoal)
        {
            Gizmos.color = navigationGoalColor;
            Gizmos.DrawLine(leaderWorld, debugLeaderGoal);
            Gizmos.DrawSphere(debugLeaderGoal, markerSize * 0.3f);
        }

        if (drawEngagementAreaDebug && debugHasEngagementData)
        {
            Gizmos.color = navigationEngagementColor;
            Gizmos.DrawWireSphere(debugEngagementCenter, Mathf.Max(0.1f, debugEngagementRadius));
            Gizmos.DrawLine(debugEngagementCenter, debugEngagementPoint);
            Gizmos.DrawSphere(debugEngagementPoint, markerSize * 0.28f);
        }

        if (drawCorridorDebug && currentFlightPath != null && currentFlightPath.NodeCount > 0)
        {
            Vector3 segmentStart = leaderWorld;
            for (int i = 0; i < currentFlightPath.NodeCount; i++)
            {
                Vector3 waypoint = currentFlightPath.Nodes[i];
                bool passed = i < currentFlightPath.CurrentIndex;
                bool current = i == currentFlightPath.CurrentIndex;

                Color color = navigationCorridorColor;
                if (passed)
                    color.a *= 0.35f;

                Gizmos.color = color;
                Gizmos.DrawLine(segmentStart, waypoint);
                Gizmos.DrawCube(waypoint, Vector3.one * (current ? markerSize * 0.34f : markerSize * 0.24f));
                segmentStart = waypoint;
            }
        }

        if (drawCorridorDebug && drawLegacyCorridorDebug && leaderCorridorWaypointCount > 0)
        {
            Vector3 segmentStart = leaderWorld;
            for (int i = 0; i < leaderCorridorWaypointCount; i++)
            {
                Vector3 waypoint = leaderCorridorWaypoints[i];
                bool passed = i < leaderCorridorWaypointIndex;
                Color color = navigationCorridorColor;
                color.a = passed ? color.a * 0.2f : color.a * 0.55f;

                Gizmos.color = color;
                Gizmos.DrawLine(segmentStart, waypoint);
                Gizmos.DrawCube(waypoint, Vector3.one * markerSize * 0.18f);
                segmentStart = waypoint;
            }
        }

        if (drawClusterVelocityDebug && debugHasEngagementData && debugClusterVelocity.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = navigationClusterVelocityColor;
            Gizmos.DrawLine(debugEngagementCenter, debugEngagementCenter + (debugClusterVelocity * 0.35f));
        }

        if (drawAvoidanceDebug && debugCollisionAvoidanceProbeRadius > 0.01f)
        {
            Gizmos.color = navigationAvoidanceProbeColor;
            Gizmos.DrawWireSphere(leaderWorld, debugCollisionAvoidanceProbeRadius);
        }

        if (drawAvoidanceDebug && debugCollisionAvoidanceOffset.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = navigationAvoidanceColor;
            Gizmos.DrawLine(leaderWorld, leaderWorld + (Vector3)debugCollisionAvoidanceOffset);
            Gizmos.DrawSphere(leaderWorld + (Vector3)debugCollisionAvoidanceOffset, markerSize * 0.24f);
        }
#if UNITY_EDITOR
        if (drawNavigationLabels)
        {
            int currentPathIndex = CurrentPathNodeIndex >= 0 ? CurrentPathNodeIndex + 1 : 0;
            string label =
                $"Team {GetSquadTeamId()}  {debugLeaderMode}\n" +
                $"Throttle {debugLeaderThrottle:0.00}  Path {currentPathIndex}/{CurrentPathNodeCount}  Repath {lastPathRepathReason}  Block {forwardBlockerTimer:0.00}s  CA {debugCollisionThreatCount}";
            Handles.Label(leaderWorld + (Vector3.up * navigationDebugLabelHeight), label);
        }
#endif
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

    private void ResetLeaderNavigationState()
    {
        hasCachedHostileEngagementGoal = false;
        cachedHostileEngagementGoal = Vector2.zero;
        nextHostileEngagementGoalRefreshTime = 0f;

        leaderCorridorWaypointCount = 0;
        leaderCorridorWaypointIndex = 0;
        nextLeaderCorridorRepathTime = 0f;
        nextLeaderBlockedRepathTime = 0f;
        leaderCorridorSideSign = (GetInstanceID() & 1) == 0 ? 1 : -1;
        lastResolvedHostileTeamId = int.MinValue;

        currentFlightPath = null;
        forwardBlockerTimer = 0f;
        nextWaypointRepathAllowedTime = 0f;
        cachedPathHostileTeamId = int.MinValue;
        flightPathBuildSequence = 0;
        lastPathRepathReason = FlightPathRepathReason.None;

        debugLastLeaderPosition = transform.position;
        debugHasLeaderGoal = false;
        debugLeaderGoal = transform.position;
        debugLeaderThrottle = 0f;
        debugLeaderMode = "reset";
        debugHasEngagementData = false;
        debugEngagementCenter = transform.position;
        debugEngagementRadius = 0f;
        debugEngagementPoint = transform.position;
        debugClusterVelocity = Vector2.zero;
        debugCollisionAvoidanceOffset = Vector2.zero;
        debugCollisionAvoidanceProbeRadius = Mathf.Max(0f, collisionAvoidanceProbeRadius);
        debugCollisionThreatCount = 0;
    }

    private void ResetReinforcementState()
    {
        underStrengthTimer = 0f;
        nextAllowedRequestTime = 0f;
    }
}






