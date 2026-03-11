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

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private bool drawAvoidanceDebug = false;
    [SerializeField] private Color goalLineColor = Color.yellow;
    [SerializeField] private Color targetLineColor = Color.green;
    [SerializeField] private Color avoidanceLineColor = new Color(1f, 0.25f, 0.9f, 0.9f);

    private Vector2 dbgGoal;
    private Vector2 dbgDesiredDir;
    private Vector2 dbgAvoidance;
    private float dbgThrottle;
    private int dbgAvoidanceCount;
    private float localAvoidancePressure01;
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
        dbgThrottle = 0f;
        dbgAvoidanceCount = 0;
        localAvoidancePressure01 = 0f;
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
            dbgAvoidanceCount = 0;
            localAvoidancePressure01 = 0f;
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
            dbgAvoidanceCount = 0;
            localAvoidancePressure01 = 0f;
            ApplyFlightAssist(false);
            ClampSpeed();
            return;
        }

        Vector2 desiredDir = toGoal / dist;
        ApplyLocalAvoidance(ref desiredDir);
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

        if (reduceThrottleDuringAvoidance && localAvoidancePressure01 > 0.001f)
        {
            float response = Mathf.Clamp01(avoidanceThrottleResponse);
            float pressure = Mathf.Clamp01(localAvoidancePressure01 * response);
            float minScale = Mathf.Clamp(avoidanceThrottleMinScale, 0.1f, 1f);
            float throttleScale = Mathf.Lerp(1f, minScale, pressure);
            throttle01 *= throttleScale;
        }

        SteerToward(desiredDir);
        ApplyForwardThrust(throttle01);
        ApplyFlightAssist(throttle01 > 0.05f);
        ClampSpeed();

        if (focusTarget != null)
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

#if UNITY_EDITOR
        Handles.Label(transform.position + Vector3.up * 1.2f, $"{dbgState} thr={dbgThrottle:0.00} avoid={dbgAvoidanceCount} p={localAvoidancePressure01:0.00}");
#endif
    }
}
