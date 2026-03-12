using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TeamAgent))]
[RequireComponent(typeof(TargetingComponent))]
public class EnemySpaceshipAI : MonoBehaviour
{
    private Rigidbody2D rb;
    private TargetingComponent targeting;
    private TeamAgent team;
    private EnemySquadMember squadMember;
    private readonly Collider2D[] localAvoidanceHits = new Collider2D[32];
    private readonly RaycastHit2D[] lineOfSightHits = new RaycastHit2D[16];
    private readonly Vector2[] dbgLineOfSightDirections = new Vector2[9];
    private readonly float[] dbgLineOfSightClearance = new float[9];

    [Header("Refs")]
    [SerializeField] private GameObject player;
    [SerializeField] private EnemyShipProfileSO shipProfile;

    [Header("Fallback Chase")]
    [SerializeField] private float fallbackChaseDistance = 18f;
    [SerializeField] private float fallbackArriveDistance = 1.25f;
    [SerializeField] private float fallbackFullThrottleDistance = 8f;
    [SerializeField] private float minimumThrottleWhenMoving = 0.2f;

    [Header("Local Avoidance")]
    [SerializeField] private bool useLocalAvoidance = false;
    [SerializeField] private bool avoidFriendlyShips = true;
    [SerializeField] private bool avoidHostileShips = true;
    [SerializeField] private float localAvoidanceRadius = 10f;
    [SerializeField] private float localAvoidanceStrength = 0.95f;
    [SerializeField] private float localAvoidanceLookAheadTime = 0.55f;
    [SerializeField] private float localAvoidanceClosingSpeed = 0.6f;
    [SerializeField] private float localAvoidanceHardSeparationDistance = 4f;
    [SerializeField] [Range(0f, 1f)] private float localAvoidanceMaxBlend = 0.8f;
    [SerializeField] private LayerMask localAvoidanceMask = ~0;
    [SerializeField] private bool reduceThrottleDuringAvoidance = false;
    [SerializeField] [Range(0.1f, 1f)] private float avoidanceThrottleMinScale = 0.65f;
    [SerializeField] [Range(0f, 1f)] private float avoidanceThrottleResponse = 0.85f;

    [Header("Line Of Sight Steering")]
    [SerializeField] private bool useLineOfSightSteering = true;
    [SerializeField] [Range(3, 9)] private int lineOfSightProbeCount = 5;
    [SerializeField] [Range(15f, 170f)] private float lineOfSightProbeArcDegrees = 80f;
    [SerializeField] private float lineOfSightProbeRange = 55f;
    [SerializeField] private float lineOfSightProbeRadius = 1.4f;
    [SerializeField] private float lineOfSightSteeringStrength = 1.2f;
    [SerializeField] [Range(0.5f, 0.99f)] private float lineOfSightCenterBlockThreshold = 0.88f;
    [SerializeField] private float lineOfSightSideLockSeconds = 0.65f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Flagship No-Fly")]
    [SerializeField] private bool useFlagshipNoFlyZones = true;
    [SerializeField] private bool avoidFriendlyFlagshipZones = true;
    [SerializeField] private bool avoidHostileFlagshipZones = true;
    [SerializeField] private float flagshipNoFlyExtraPadding = 12f;
    [SerializeField] private float flagshipNoFlyInfluenceDistance = 45f;
    [SerializeField] private float flagshipNoFlySteeringStrength = 1.35f;
    [SerializeField] [Range(0f, 1f)] private float flagshipNoFlyMaxBlend = 0.9f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private bool drawAvoidanceDebug = false;
    [SerializeField] private bool drawLineOfSightDebug = false;
    [SerializeField] private Color goalLineColor = Color.yellow;
    [SerializeField] private Color targetLineColor = Color.green;
    [SerializeField] private Color avoidanceLineColor = new Color(1f, 0.25f, 0.9f, 0.9f);
    [SerializeField] private Color lineOfSightBlockedColor = new Color(1f, 0.25f, 0.2f, 0.95f);
    [SerializeField] private Color lineOfSightClearColor = new Color(0.2f, 1f, 0.6f, 0.95f);
    [SerializeField] private Color lineOfSightSteerColor = new Color(0.2f, 0.95f, 1f, 0.95f);
    [SerializeField] private bool drawFlagshipNoFlySteeringDebug = false;
    [SerializeField] private Color flagshipNoFlySteerColor = new Color(1f, 0.2f, 0.2f, 0.95f);

    private Vector2 dbgGoal;
    private Vector2 dbgDesiredDir;
    private Vector2 dbgAvoidance;
    private Vector2 dbgLineOfSightSteer;
    private float dbgThrottle;
    private int dbgAvoidanceCount;
    private int dbgLineOfSightBlockedCount;
    private int dbgLineOfSightProbeCount;
    private float localAvoidancePressure01;
    private float lineOfSightPressure01;
    private Vector2 dbgFlagshipNoFlySteer;
    private int dbgFlagshipNoFlyZoneCount;
    private float flagshipNoFlyPressure01;
    private int lineOfSightLockedSideSign;
    private float lineOfSightLockUntilTime;
    private string dbgState = "spawn";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        targeting = GetComponent<TargetingComponent>();
        team = GetComponent<TeamAgent>();
        squadMember = GetComponent<EnemySquadMember>();

        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    private void Start()
    {
        ResetForSpawn(player != null ? player : GameObject.FindGameObjectWithTag("Player"));
    }

    public void ResetForSpawn(GameObject playerTarget)
    {
        if (shipProfile == null)
        {
            Debug.LogError($"{name} has no shipProfile assigned.");
            enabled = false;
            return;
        }

        enabled = true;

        if (player == null)
            player = playerTarget != null ? playerTarget : GameObject.FindGameObjectWithTag("Player");

        if (squadMember == null)
            squadMember = GetComponent<EnemySquadMember>();

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        targeting?.RetargetNow();

        dbgGoal = rb.position;
        dbgDesiredDir = transform.up;
        dbgAvoidance = Vector2.zero;
        dbgLineOfSightSteer = Vector2.zero;
        dbgFlagshipNoFlySteer = Vector2.zero;
        dbgThrottle = 0f;
        dbgAvoidanceCount = 0;
        dbgLineOfSightBlockedCount = 0;
        dbgLineOfSightProbeCount = 0;
        dbgFlagshipNoFlyZoneCount = 0;
        localAvoidancePressure01 = 0f;
        lineOfSightPressure01 = 0f;
        flagshipNoFlyPressure01 = 0f;
        lineOfSightLockedSideSign = 0;
        lineOfSightLockUntilTime = 0f;
        dbgState = "spawn";
    }

    private void FixedUpdate()
    {
        if (!enabled || shipProfile == null)
            return;

        rb.angularVelocity = 0f;

        bool hasGoal = TryGetMovementGoal(out Vector2 goalPos, out float throttle01, out Transform focusTarget);
        dbgGoal = goalPos;
        dbgThrottle = throttle01;

        if (!hasGoal)
        {
            dbgState = "idle";
            dbgAvoidance = Vector2.zero;
            dbgLineOfSightSteer = Vector2.zero;
            dbgFlagshipNoFlySteer = Vector2.zero;
            dbgAvoidanceCount = 0;
            dbgLineOfSightBlockedCount = 0;
            dbgLineOfSightProbeCount = 0;
            dbgFlagshipNoFlyZoneCount = 0;
            localAvoidancePressure01 = 0f;
            lineOfSightPressure01 = 0f;
            flagshipNoFlyPressure01 = 0f;
            ApplyFlightAssist(false);
            ClampSpeed();
            return;
        }

        Vector2 toGoal = goalPos - rb.position;
        float dist = toGoal.magnitude;

        if (dist <= 0.001f)
        {
            dbgState = "hold";
            dbgAvoidance = Vector2.zero;
            dbgLineOfSightSteer = Vector2.zero;
            dbgFlagshipNoFlySteer = Vector2.zero;
            dbgAvoidanceCount = 0;
            dbgLineOfSightBlockedCount = 0;
            dbgLineOfSightProbeCount = 0;
            dbgFlagshipNoFlyZoneCount = 0;
            localAvoidancePressure01 = 0f;
            lineOfSightPressure01 = 0f;
            flagshipNoFlyPressure01 = 0f;
            ApplyFlightAssist(false);
            ClampSpeed();
            return;
        }

        Vector2 desiredDir = toGoal / dist;
        ApplyFlagshipNoFlyZoneSteering(ref desiredDir);
        ApplyLocalAvoidance(ref desiredDir);
        ApplyLineOfSightSteering(ref desiredDir);
        dbgDesiredDir = desiredDir;

        float arriveDistance = shipProfile.arriveDistance > 0f ? shipProfile.arriveDistance : fallbackArriveDistance;
        if (dist <= arriveDistance)
        {
            throttle01 = 0f;
            dbgState = "arrive";
        }
        else if (throttle01 <= 0f)
        {
            throttle01 = Mathf.Max(minimumThrottleWhenMoving,
                ComputeThrottleFromDistance(dist, arriveDistance,
                    shipProfile.fullThrottleDistance > 0f ? shipProfile.fullThrottleDistance : fallbackFullThrottleDistance));
            dbgState = "move-fallback";
        }

        if (squadMember != null && squadMember.Squad != null)
            dbgState = squadMember.Role == EnemySquadRole.Leader ? "leader-slot" : "follower-slot";

        float totalAvoidancePressure = Mathf.Max(flagshipNoFlyPressure01, Mathf.Max(localAvoidancePressure01, lineOfSightPressure01));
        if (reduceThrottleDuringAvoidance && totalAvoidancePressure > 0.001f)
        {
            float response = Mathf.Clamp01(avoidanceThrottleResponse);
            float pressure = Mathf.Clamp01(totalAvoidancePressure * response);
            float minScale = Mathf.Clamp(avoidanceThrottleMinScale, 0.1f, 1f);
            float throttleScale = Mathf.Lerp(1f, minScale, pressure);
            throttle01 *= throttleScale;
        }

        SteerToward(desiredDir);
        ApplyForwardThrust(throttle01);
        ApplyFlightAssist(throttle01 > 0.05f);
        ClampSpeed();

        if (drawDebug && focusTarget != null)
            Debug.DrawLine(transform.position, focusTarget.position, targetLineColor, 0f, false);
    }

    private bool TryGetMovementGoal(out Vector2 goalPos, out float throttle01, out Transform focusTarget)
    {
        goalPos = rb.position;
        throttle01 = 0f;
        focusTarget = null;

        if (squadMember != null && squadMember.Squad != null)
        {
            focusTarget = squadMember.Squad.FocusTarget;
            if (squadMember.TryGetTravelGoal(rb.position, out goalPos, out throttle01))
                return true;
        }

        focusTarget = GetFallbackFocusTarget();
        if (focusTarget == null)
            return false;

        Vector2 focusPos = focusTarget.position;
        Vector2 toFocus = focusPos - rb.position;
        float dist = toFocus.magnitude;
        if (dist <= 0.001f)
            return false;

        float desiredDistance = shipProfile.focusDistance > 0f ? shipProfile.focusDistance : fallbackChaseDistance;
        Vector2 dir = toFocus / dist;
        goalPos = focusPos - dir * desiredDistance;

        float distanceError = Mathf.Abs(dist - desiredDistance);
        float arrive = shipProfile.arriveDistance > 0f ? shipProfile.arriveDistance : fallbackArriveDistance;
        float full = shipProfile.fullThrottleDistance > 0f ? shipProfile.fullThrottleDistance : fallbackFullThrottleDistance;
        throttle01 = ComputeThrottleFromDistance(distanceError, arrive, full);

        if (dist < desiredDistance - arrive)
            throttle01 = 1f;

        return true;
    }

    private Transform GetFallbackFocusTarget()
    {
        TeamAgent target = targeting != null ? targeting.CurrentTarget : null;
        if (target != null)
            return target.transform;

        targeting?.RetargetNow();
        target = targeting != null ? targeting.CurrentTarget : null;
        if (target != null)
            return target.transform;

        return player != null ? player.transform : null;
    }

    private void ApplyFlagshipNoFlyZoneSteering(ref Vector2 desiredDir)
    {
        dbgFlagshipNoFlySteer = Vector2.zero;
        dbgFlagshipNoFlyZoneCount = 0;
        flagshipNoFlyPressure01 = 0f;

        if (!useFlagshipNoFlyZones)
            return;

        IReadOnlyList<FlagshipNoFlyZone> zones = FlagshipNoFlyZone.Active;
        if (zones == null || zones.Count <= 0)
            return;

        Vector2 myPos = rb.position;
        float influenceDistance = Mathf.Max(1f, flagshipNoFlyInfluenceDistance);
        float extraPadding = Mathf.Max(0f, flagshipNoFlyExtraPadding);

        Vector2 aggregate = Vector2.zero;
        float strongestPressure = 0f;

        for (int i = 0; i < zones.Count; i++)
        {
            FlagshipNoFlyZone zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            if (!ShouldAvoidFlagshipNoFlyZone(zone.TeamId))
                continue;

            Vector2 toShip = myPos - zone.Center;
            float dist = toShip.magnitude;
            float radius = Mathf.Max(1f, zone.EffectiveRadius + extraPadding);
            float influenceRadius = radius + influenceDistance;

            if (dist > influenceRadius)
                continue;

            dbgFlagshipNoFlyZoneCount++;

            Vector2 away = dist > 0.01f ? toShip / dist : (Vector2)transform.up;
            float inside01 = dist < radius ? 1f - Mathf.Clamp01(dist / radius) : 0f;
            float near01 = 1f - Mathf.Clamp01((dist - radius) / influenceDistance);
            float inward01 = Mathf.Clamp01(Vector2.Dot(desiredDir, -away));

            float pressure = Mathf.Max(inside01 * 1.1f, near01 * (0.18f + inward01 * 0.82f));
            if (pressure <= 0.0001f)
                continue;

            Vector2 tangent = new Vector2(-away.y, away.x) * ComputeFlagshipNoFlyOrbitSign(zone);
            float tangentWeight = Mathf.Lerp(0.35f, 0.95f, inward01);
            if (inside01 > 0.01f)
                tangentWeight = Mathf.Max(tangentWeight, 0.7f);

            Vector2 zoneSteer = (away + tangent * tangentWeight).normalized;
            aggregate += zoneSteer * pressure;
            strongestPressure = Mathf.Max(strongestPressure, pressure);
        }

        if (aggregate.sqrMagnitude <= 0.0001f)
            return;

        Vector2 steerDir = (desiredDir + aggregate.normalized * Mathf.Max(0f, flagshipNoFlySteeringStrength)).normalized;
        float blend = Mathf.Clamp01(Mathf.Max(strongestPressure, aggregate.magnitude * 0.22f));
        blend = Mathf.Min(Mathf.Clamp01(flagshipNoFlyMaxBlend), blend);

        desiredDir = Vector2.Lerp(desiredDir, steerDir, blend).normalized;
        flagshipNoFlyPressure01 = Mathf.Clamp01(Mathf.Max(strongestPressure, blend));
        dbgFlagshipNoFlySteer = aggregate.normalized * (blend * influenceDistance * 0.5f);
    }

    private bool ShouldAvoidFlagshipNoFlyZone(int zoneTeamId)
    {
        if (zoneTeamId < 0 || team == null)
            return true;

        bool sameTeam = zoneTeamId == team.TeamId;
        if (sameTeam)
            return avoidFriendlyFlagshipZones;

        bool hostile = TeamRegistry.IsHostile(team.TeamId, zoneTeamId);
        if (hostile)
            return avoidHostileFlagshipZones;

        return avoidFriendlyFlagshipZones || avoidHostileFlagshipZones;
    }

    private int ComputeFlagshipNoFlyOrbitSign(FlagshipNoFlyZone zone)
    {
        int hash = GetInstanceID();
        if (zone != null)
            hash ^= zone.GetInstanceID() * 83492791;
        if (team != null)
            hash ^= team.TeamId * 19349663;
        if (squadMember != null)
            hash ^= (squadMember.SlotIndex + 41) * 73856093;

        return (hash & 1) == 0 ? 1 : -1;
    }
    private void ApplyLocalAvoidance(ref Vector2 desiredDir)
    {
        dbgAvoidance = Vector2.zero;
        dbgAvoidanceCount = 0;
        localAvoidancePressure01 = 0f;

        if (!useLocalAvoidance || localAvoidanceRadius <= 0.01f || localAvoidanceStrength <= 0f)
            return;

        Vector2 myPos = rb.position;
        Vector2 myVel = rb.linearVelocity;
        float radius = Mathf.Max(0.5f, localAvoidanceRadius);

        int hitCount = Physics2D.OverlapCircleNonAlloc(myPos, radius, localAvoidanceHits, localAvoidanceMask);
        if (hitCount <= 0)
            return;

        Vector2 repel = Vector2.zero;
        float strongestWeight = 0f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = localAvoidanceHits[i];
            if (hit == null)
                continue;

            Rigidbody2D otherRb = hit.attachedRigidbody;
            if (otherRb == rb)
                continue;

            TeamAgent otherTeam = hit.GetComponentInParent<TeamAgent>();
            if (!ShouldAvoidTeam(otherTeam))
                continue;

            Vector2 otherPos = otherRb != null ? otherRb.position : (Vector2)hit.bounds.center;
            Vector2 delta = myPos - otherPos;
            float dist = delta.magnitude;

            if (dist < 0.001f || dist > radius)
                continue;

            Vector2 otherVel = otherRb != null ? otherRb.linearVelocity : Vector2.zero;
            Vector2 toOther = -delta / dist;
            float closing = Vector2.Dot(myVel - otherVel, toOther);

            if (closing < localAvoidanceClosingSpeed && dist > localAvoidanceHardSeparationDistance)
                continue;

            float lookAhead = Mathf.Max(0f, localAvoidanceLookAheadTime);
            Vector2 futureDelta = (myPos + myVel * lookAhead) - (otherPos + otherVel * lookAhead);
            float futureDist = futureDelta.magnitude;
            Vector2 pushDir = futureDist > 0.01f ? futureDelta / futureDist : delta / dist;

            float distWeight = 1f - (dist / radius);
            float futureWeight = 1f - Mathf.Clamp01(futureDist / radius);

            float hardDistance = Mathf.Max(0.01f, localAvoidanceHardSeparationDistance);
            float hardWeight = dist < hardDistance ? 1f - Mathf.Clamp01(dist / hardDistance) : 0f;

            float closingWeight = 0f;
            if (closing > localAvoidanceClosingSpeed)
            {
                float denom = Mathf.Max(0.01f, localAvoidanceClosingSpeed * 2f);
                closingWeight = Mathf.Clamp01((closing - localAvoidanceClosingSpeed) / denom);
            }

            float weight = Mathf.Max(distWeight, futureWeight * 0.85f) + (hardWeight * 1.35f) + (closingWeight * 0.65f);
            if (weight <= 0.0001f)
                continue;

            repel += pushDir * weight;
            strongestWeight = Mathf.Max(strongestWeight, weight);
            dbgAvoidanceCount++;
        }

        if (repel.sqrMagnitude <= 0.0001f)
            return;

        Vector2 avoidDir = repel.normalized;
        Vector2 steered = (desiredDir + avoidDir * localAvoidanceStrength).normalized;

        float blend = Mathf.Clamp01(Mathf.Max(strongestWeight, repel.magnitude * 0.2f));
        blend = Mathf.Min(Mathf.Clamp01(localAvoidanceMaxBlend), blend);

        desiredDir = Vector2.Lerp(desiredDir, steered, blend).normalized;
        localAvoidancePressure01 = Mathf.Clamp01(Mathf.Max(strongestWeight, blend));
        dbgAvoidance = avoidDir * (blend * radius * 0.35f);
    }

    private void ApplyLineOfSightSteering(ref Vector2 desiredDir)
    {
        dbgLineOfSightSteer = Vector2.zero;
        dbgLineOfSightBlockedCount = 0;
        dbgLineOfSightProbeCount = 0;
        lineOfSightPressure01 = 0f;

        if (!useLineOfSightSteering || lineOfSightProbeRange <= 0.5f)
        {
            if (Time.time >= lineOfSightLockUntilTime)
                lineOfSightLockedSideSign = 0;
            return;
        }

        int probeCount = Mathf.Clamp(lineOfSightProbeCount, 3, 9);
        if ((probeCount & 1) == 0)
            probeCount = Mathf.Min(9, probeCount + 1);

        int centerIndex = probeCount / 2;
        float arc = Mathf.Max(10f, lineOfSightProbeArcDegrees);
        float step = probeCount > 1 ? arc / (probeCount - 1f) : 0f;
        float startAngle = -arc * 0.5f;
        float probeRange = Mathf.Max(1f, lineOfSightProbeRange);
        float probeRadius = Mathf.Max(0f, lineOfSightProbeRadius);

        Vector2 forward = desiredDir.sqrMagnitude > 0.0001f ? desiredDir.normalized : (Vector2)transform.up;
        Vector2 origin = rb.position;

        float leftClearanceSum = 0f;
        float rightClearanceSum = 0f;
        int leftCount = 0;
        int rightCount = 0;
        float centerClearance = 1f;

        for (int i = 0; i < probeCount; i++)
        {
            float angle = startAngle + (step * i);
            Vector2 probeDir = RotateVector(forward, angle);
            float clearance = SampleLineOfSightClearance(origin, probeDir, probeRange, probeRadius);

            dbgLineOfSightDirections[i] = probeDir;
            dbgLineOfSightClearance[i] = clearance;
            dbgLineOfSightProbeCount++;
            if (clearance < 0.999f)
                dbgLineOfSightBlockedCount++;

            if (i == centerIndex)
            {
                centerClearance = clearance;
            }
            else if (i > centerIndex)
            {
                leftClearanceSum += clearance;
                leftCount++;
            }
            else
            {
                rightClearanceSum += clearance;
                rightCount++;
            }
        }

        float leftClearance = leftCount > 0 ? leftClearanceSum / leftCount : 1f;
        float rightClearance = rightCount > 0 ? rightClearanceSum / rightCount : 1f;
        float centerThreshold = Mathf.Clamp01(lineOfSightCenterBlockThreshold);
        bool centerBlocked = centerClearance < centerThreshold;

        if (!centerBlocked && dbgLineOfSightBlockedCount <= 0)
        {
            if (Time.time >= lineOfSightLockUntilTime)
                lineOfSightLockedSideSign = 0;
            return;
        }

        int sideSign;
        bool usingLockedSide = Time.time < lineOfSightLockUntilTime && lineOfSightLockedSideSign != 0;
        if (usingLockedSide)
        {
            sideSign = lineOfSightLockedSideSign;
        }
        else
        {
            float sideDelta = leftClearance - rightClearance;
            if (Mathf.Abs(sideDelta) <= 0.06f)
                sideSign = ComputeStableSideBias();
            else
                sideSign = sideDelta > 0f ? 1 : -1;

            lineOfSightLockedSideSign = sideSign;
        }

        if (centerBlocked)
            lineOfSightLockUntilTime = Time.time + Mathf.Max(0.05f, lineOfSightSideLockSeconds);
        else if (Time.time >= lineOfSightLockUntilTime)
            lineOfSightLockedSideSign = 0;

        float sideClearance = sideSign > 0 ? leftClearance : rightClearance;
        float centerPressure = 1f - centerClearance;
        float sidePressure = Mathf.Clamp01(0.55f - sideClearance) * 0.6f;
        float blockedPressure = Mathf.Clamp01(dbgLineOfSightBlockedCount / (float)probeCount) * 0.55f;
        float pressure = Mathf.Clamp01(Mathf.Max(centerPressure, Mathf.Max(sidePressure, blockedPressure)));
        if (pressure <= 0.001f)
            return;

        Vector2 sideDir = new Vector2(-forward.y, forward.x) * sideSign;
        Vector2 steerDir = (desiredDir + sideDir * (Mathf.Max(0f, lineOfSightSteeringStrength) * pressure)).normalized;
        float blend = Mathf.Clamp01(pressure);

        desiredDir = Vector2.Lerp(desiredDir, steerDir, blend).normalized;
        lineOfSightPressure01 = pressure;
        dbgLineOfSightSteer = sideDir * (pressure * probeRange * 0.3f);
    }

    private float SampleLineOfSightClearance(Vector2 origin, Vector2 direction, float maxDistance, float probeRadius)
    {
        int hitCount = probeRadius > 0.01f
            ? Physics2D.CircleCastNonAlloc(origin, probeRadius, direction, lineOfSightHits, maxDistance, lineOfSightMask)
            : Physics2D.RaycastNonAlloc(origin, direction, lineOfSightHits, maxDistance, lineOfSightMask);

        if (hitCount <= 0)
            return 1f;

        float nearest = maxDistance;
        bool blocked = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = lineOfSightHits[i];
            Collider2D hitCollider = hit.collider;
            if (!IsLineOfSightObstacle(hitCollider))
                continue;

            float dist = hit.distance;
            if (dist <= 0.0001f)
            {
                Vector2 closest = hitCollider.ClosestPoint(origin);
                dist = Vector2.Distance(origin, closest);
            }

            nearest = Mathf.Min(nearest, dist);
            blocked = true;
        }

        if (!blocked)
            return 1f;

        return Mathf.Clamp01(nearest / Mathf.Max(0.01f, maxDistance));
    }

    private bool IsLineOfSightObstacle(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return false;

        Rigidbody2D otherRb = hitCollider.attachedRigidbody;
        if (otherRb == rb)
            return false;

        Transform myRoot = transform.root;
        Transform otherRoot = otherRb != null ? otherRb.transform.root : hitCollider.transform.root;
        if (otherRoot == myRoot)
            return false;

        TeamAgent otherTeam = hitCollider.GetComponentInParent<TeamAgent>();
        return ShouldAvoidTeam(otherTeam);
    }

    private int ComputeStableSideBias()
    {
        int hash = GetInstanceID();
        if (team != null)
            hash ^= team.TeamId * 73856093;
        if (squadMember != null)
            hash ^= (squadMember.SlotIndex + 31) * 19349663;

        return (hash & 1) == 0 ? 1 : -1;
    }

    private static Vector2 RotateVector(Vector2 source, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            source.x * cos - source.y * sin,
            source.x * sin + source.y * cos);
    }

    private bool ShouldAvoidTeam(TeamAgent otherTeam)
    {
        if (otherTeam == null || team == null)
            return true;

        bool sameTeam = otherTeam.TeamId == team.TeamId;
        if (sameTeam)
            return avoidFriendlyShips;

        bool hostile = TeamRegistry.IsHostile(team.TeamId, otherTeam.TeamId);
        if (hostile)
            return avoidHostileShips;

        return avoidFriendlyShips || avoidHostileShips;
    }

    private void SteerToward(Vector2 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.0001f)
            return;

        float desiredAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg + shipProfile.rotationOffset;
        float delta = Mathf.DeltaAngle(rb.rotation, desiredAngle);

        float forwardSpeed = Mathf.Max(0f, Vector2.Dot(rb.linearVelocity, transform.up));
        float speed01 = Mathf.Clamp01(forwardSpeed / Mathf.Max(0.01f, shipProfile.maxSpeed));
        float authority = shipProfile.turnAuthorityBySpeed != null
            ? shipProfile.turnAuthorityBySpeed.Evaluate(speed01)
            : speed01;

        float turnRate = Mathf.Lerp(shipProfile.minTurnDegPerSec, shipProfile.maxTurnDegPerSec, authority);
        float maxStep = turnRate * Time.fixedDeltaTime;
        float newAngle = rb.rotation + Mathf.Clamp(delta, -maxStep, maxStep);
        rb.MoveRotation(newAngle);
    }

    private void ApplyForwardThrust(float throttle01)
    {
        if (throttle01 <= 0.001f)
            return;

        float thrust = shipProfile.forwardThrust;
        rb.AddForce((Vector2)transform.up * (throttle01 * thrust), ForceMode2D.Force);
    }

    private void ApplyFlightAssist(bool isThrusting)
    {
        Vector2 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.0001f)
            return;

        Vector2 forward = transform.up;
        float forwardSpeed = Vector2.Dot(velocity, forward);
        Vector2 desiredVelocity = forward * forwardSpeed;
        Vector2 alignForce = (desiredVelocity - velocity) * shipProfile.alignStrength;
        rb.AddForce(alignForce, ForceMode2D.Force);

        if (!isThrusting && shipProfile.dampStrength > 0f)
            rb.AddForce(-velocity * shipProfile.dampStrength, ForceMode2D.Force);
    }

    private void ClampSpeed()
    {
        float max = Mathf.Max(0.01f, shipProfile.maxSpeed);
        Vector2 velocity = rb.linearVelocity;

        if (velocity.sqrMagnitude > max * max)
            rb.linearVelocity = velocity.normalized * max;
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

    private void OnDrawGizmos()
    {
        if (!drawDebug || drawOnlyWhenSelected)
            return;

        DrawDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
            return;

        DrawDebugGizmos();
    }

    private void DrawDebugGizmos()
    {
        if (!Application.isPlaying)
            return;

        Gizmos.color = goalLineColor;
        Gizmos.DrawLine(transform.position, dbgGoal);
        Gizmos.DrawSphere(dbgGoal, 0.3f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(dbgDesiredDir * 2f));

        if (drawAvoidanceDebug && dbgAvoidance.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = avoidanceLineColor;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)dbgAvoidance);
            Gizmos.DrawSphere(transform.position + (Vector3)dbgAvoidance, 0.18f);
        }

        if (drawLineOfSightDebug && dbgLineOfSightProbeCount > 0)
        {
            Vector3 start = transform.position;
            float probeRange = Mathf.Max(1f, lineOfSightProbeRange);

            for (int i = 0; i < dbgLineOfSightProbeCount; i++)
            {
                float clearance = Mathf.Clamp01(dbgLineOfSightClearance[i]);
                Vector2 probeDir = dbgLineOfSightDirections[i];
                Vector3 end = start + (Vector3)(probeDir * probeRange);

                Gizmos.color = Color.Lerp(lineOfSightBlockedColor, lineOfSightClearColor, clearance);
                Gizmos.DrawLine(start, end);

                Vector3 marker = start + (Vector3)(probeDir * (probeRange * clearance));
                Gizmos.DrawSphere(marker, 0.09f);
            }

            if (dbgLineOfSightSteer.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = lineOfSightSteerColor;
                Gizmos.DrawLine(start, start + (Vector3)dbgLineOfSightSteer);
                Gizmos.DrawSphere(start + (Vector3)dbgLineOfSightSteer, 0.16f);
            }
        }

        if (drawFlagshipNoFlySteeringDebug && dbgFlagshipNoFlySteer.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = flagshipNoFlySteerColor;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)dbgFlagshipNoFlySteer);
            Gizmos.DrawSphere(transform.position + (Vector3)dbgFlagshipNoFlySteer, 0.2f);
        }

#if UNITY_EDITOR
        Handles.Label(
            transform.position + Vector3.up * 1.2f,
            $"{dbgState} thr={dbgThrottle:0.00} avoid={dbgAvoidanceCount} los={dbgLineOfSightBlockedCount}/{dbgLineOfSightProbeCount} nf={dbgFlagshipNoFlyZoneCount} p={Mathf.Max(flagshipNoFlyPressure01, Mathf.Max(localAvoidancePressure01, lineOfSightPressure01)):0.00}");
#endif
    }
}











