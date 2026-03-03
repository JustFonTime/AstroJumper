using System.Collections;
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
    private TeamAgent self;
    private TargetingComponent targeting;
    private ShipOrderController orders;

    [Header("Refs (fallback only)")] [SerializeField]
    private GameObject player; // fallback if targeting has nobody

    [SerializeField] private EnemyShipProfileSO shipProfile;


    [Header("Runtime")] [SerializeField] private bool isBarrellRolling = false;
    [SerializeField] private float currentSpeedMultiplier = 1f;

    private Coroutine barrelCo;

    [Header("Arena / Return (no navmesh needed)")]
    [Tooltip("If no target, and farther than this from center, will thrust toward center. 0 disables return.")]
    [SerializeField]
    private float returnToCenterRadius = 25f;

    [Tooltip("If *very* far from center, thrust harder to get back.")] [SerializeField]
    private float hardReturnRadius = 80f;

    [SerializeField] private float returnForceMultiplier = 1.25f;

    // ============================================================
    // DOGFIGHT MOVEMENT
    // ============================================================
    [Header("Dogfight Movement (New)")]
    [Tooltip("If 0, will derive from shipProfile.combatRange * preferredRangeFromCombatRange.")]
    [SerializeField]
    private float preferredRange = 0f;

    [Tooltip("If preferredRange=0, use combatRange * this value.")] [Range(0.1f, 1.25f)] [SerializeField]
    private float preferredRangeFromCombatRange = 0.75f;

    [Tooltip("How much wiggle room around preferredRange counts as 'in range'.")] [SerializeField]
    private float rangeTolerance = 10f;

    [Tooltip("When in range, AI orbits instead of ramming straight in.")] [SerializeField]
    private bool enableOrbit = true;

    [Tooltip("How fast the orbit point moves around the target (deg/sec).")] [SerializeField]
    private float orbitAngularSpeedDeg = 180f;

    [Tooltip("Extra tangential offset (distance) to help create nice arcs while orbiting.")] [SerializeField]
    private float orbitTangentOffset = 12f;

    [Tooltip("Throttle while in-range orbiting (0..1). Keeps it moving instead of freezing).")]
    [Range(0f, 1f)]
    [SerializeField]
    private float orbitThrottle = 0.85f;

    [Tooltip("If true, each spawn randomizes its orbit direction/slot angle for variety.")] [SerializeField]
    private bool randomizeDogfightSeedOnSpawn = true;

    // internal “style” seeds (per ship instance/spawn)
    private float orbitSign = 1f; // +1 or -1
    private float slotBaseAngleRad = 0f; // where this attacker “likes” to be around the target
    private float orbitPhaseRad = 0f; // extra phase so not all ships sync

    // ============================================================
    // SEPARATION / AVOID CLUMPING 
    // ============================================================
    [Header("Separation (New)")] [SerializeField]
    private bool enableSeparation = true;

    [Tooltip("How far to look for nearby ships to avoid.")] [SerializeField]
    private float separationRadius = 10f;

    [Tooltip("How strongly we push away from nearby ships.")] [SerializeField]
    private float separationStrength = 5f;

    [Tooltip(
        "Optional: limit hits to certain layers. Default Everything is usually fine because we filter by TeamAgent.")]
    [SerializeField]
    private LayerMask separationMask = ~0;

    [Tooltip("Ignore trigger colliders for separation.")] [SerializeField]
    private bool separationIgnoreTriggers = true;

    private readonly Collider2D[] separationHits = new Collider2D[24];

    // -----------------------
    // Debug Gizmos / Lines
    // -----------------------
    [Header("Debug")] [SerializeField] private bool drawDebug = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;

    [SerializeField] private bool drawTargetLine = true;
    [SerializeField] private Color hasTargetColor = Color.green;
    [SerializeField] private Color noTargetColor = Color.red;

    [SerializeField] private float velocityGizmoScale = 0.25f;
    [SerializeField] private float forceGizmoScale = 0.05f;
    [SerializeField] private float maxVelocityGizmoLength = 8f;
    [SerializeField] private float maxForceGizmoLength = 6f;

    // cached debug
    private Vector2 dbgDesiredDir;
    private Vector2 dbgThrustForce;
    private Vector2 dbgAlignForce;
    private Vector2 dbgDampForce;
    private Vector2 dbgSeparation;

    private float dbgForwardSpeed;
    private float dbgTurnRate;
    private float dbgDeltaAngle;
    private string dbgState = "spawn";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        self = GetComponent<TeamAgent>();
        targeting = GetComponent<TargetingComponent>();
        orders = GetComponent<ShipOrderController>();

        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    private void Start()
    {
        ResetForSpawn(player != null ? player : GameObject.FindGameObjectWithTag("Player"));
    }

    // Pool-safe: call every time reused
    public void ResetForSpawn(GameObject playerTarget)
    {
        if (shipProfile == null)
        {
            Debug.LogError($"{name} has no shipProfile assigned.");
            enabled = false;
            return;
        }

        enabled = true;

        // fallback only
        if (player == null)
            player = playerTarget != null ? playerTarget : GameObject.FindGameObjectWithTag("Player");

        isBarrellRolling = false;
        currentSpeedMultiplier = 1f;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (barrelCo != null) StopCoroutine(barrelCo);
        barrelCo = null;

        if (targeting != null)
            targeting.RetargetNow();

        if (shipProfile.useRandomBarrelRoll)
            barrelCo = StartCoroutine(RandomBarrellRoll());

        // dogfight seeds
        if (randomizeDogfightSeedOnSpawn)
        {
            orbitSign = Random.value > 0.5f ? 1f : -1f;
            slotBaseAngleRad = Random.Range(0f, Mathf.PI * 2f);
            orbitPhaseRad = Random.Range(0f, Mathf.PI * 2f);
        }
        else
        {
            // stable-ish distribution using instance id (good if you want consistent patterns)
            float t = Mathf.Abs(GetInstanceID() * 0.6180339f);
            t = t - Mathf.Floor(t);
            orbitSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
            slotBaseAngleRad = t * Mathf.PI * 2f;
            orbitPhaseRad = (1f - t) * Mathf.PI * 2f;
        }

        // reset debug
        dbgDesiredDir = Vector2.zero;
        dbgThrustForce = Vector2.zero;
        dbgAlignForce = Vector2.zero;
        dbgDampForce = Vector2.zero;
        dbgSeparation = Vector2.zero;
        dbgForwardSpeed = 0f;
        dbgTurnRate = 0f;
        dbgDeltaAngle = 0f;
        dbgState = "spawn";
    }

    private void OnDisable()
    {
        if (barrelCo != null) StopCoroutine(barrelCo);
        barrelCo = null;
    }

    private void FixedUpdate()
    {
        if (!enabled || shipProfile == null) return;
        if (isBarrellRolling) return;

        UpdateSpeedMultiplier();

        Transform targetTf = GetTargetTransform();
        bool hasTarget = targetTf != null;

        Vector2 desiredDir;
        float throttle01 = 0f;
        bool hardReturn = false;

        if (hasTarget)
        {
            desiredDir = ComputeDogfightDesiredDir(targetTf, out throttle01);
            dbgState = throttle01 >= 0.9f ? "dogfight-commit" : "dogfight-orbit";
        }
        else
        {
            // if no target, check if we have an order-provided goal; if not, default to arena center

            Vector2 desiredPos = GetArenaCenter();

            bool orderProvidedGoal = false;
            if (orders != null && orders.TryGetMovementGoal(rb.position, out var goalPos, out var orderThrottle))
            {
                desiredPos = goalPos;
                throttle01 = Mathf.Clamp01(orderThrottle);
                hardReturn = false;
                orderProvidedGoal = true;
                dbgState = "order-move";
            }

            Vector2 toDesired = desiredPos - rb.position;
            float dist = toDesired.magnitude;

            if (toDesired.sqrMagnitude < 0.0001f)
            {
                dbgDesiredDir = Vector2.zero;
                dbgThrustForce = Vector2.zero;
                dbgAlignForce = Vector2.zero;
                dbgDampForce = Vector2.zero;
                dbgSeparation = Vector2.zero;
                dbgState = orderProvidedGoal ? "order-arrived" : "idle";
                return;
            }

            desiredDir = toDesired / Mathf.Max(0.0001f, dist);

            // If we DIDN'T get an order goal, then use your original return-to-center throttle rules.
            if (!orderProvidedGoal)
            {
                if (returnToCenterRadius <= 0f)
                {
                    throttle01 = 0f;
                    dbgState = "idle";
                }
                else
                {
                    throttle01 = dist > returnToCenterRadius ? 1f : 0f;
                    hardReturn = dist > hardReturnRadius;
                    dbgState = throttle01 > 0f ? "return" : "idle";
                }
            }
        }

        // Separation steering (repel from nearby ships)
        dbgSeparation = Vector2.zero;
        if (enableSeparation)
        {
            Vector2 sep = ComputeSeparation();
            dbgSeparation = sep;

            // Blend: separation is more important when close to others
            Vector2 blended = desiredDir + sep * separationStrength;
            if (blended.sqrMagnitude > 0.0001f)
                desiredDir = blended.normalized;
        }

        dbgDesiredDir = desiredDir;

        // Turn to face movement direction (thrusters)
        SteerToward(desiredDir);

        // Forward thrust
        ApplyForwardThrust(throttle01, hardReturn);

        // Flight assist (same concept as player)
        ApplyFlightAssist(throttle01 > 0.05f);

        // Clamp
        ClampSpeed();

        // debug line
        if (drawTargetLine)
        {
            if (hasTarget)
                Debug.DrawLine(transform.position, targetTf.position, hasTargetColor, 0f, false);
            else
                Debug.DrawLine(transform.position, transform.position + (Vector3)desiredDir * 3f, noTargetColor, 0f,
                    false);
        }
    }

    private Vector2 ComputeDogfightDesiredDir(Transform targetTf, out float throttle01)
    {
        Vector2 targetPos = targetTf.position;
        Vector2 toTarget = targetPos - rb.position;
        float dist = toTarget.magnitude;

        if (dist < 0.0001f)
        {
            // If literally overlapping, pick a random direction
            throttle01 = 1f;
            return Random.insideUnitCircle.normalized;
        }

        float desiredRange = GetPreferredRange();

        // Range bands
        float tooFar = desiredRange + rangeTolerance;
        float tooClose = Mathf.Max(0.05f, desiredRange - rangeTolerance);

        // If too far: approach the ring point on the line to target (prevents overshoot + ramming)
        if (dist > tooFar)
        {
            Vector2 dirToTarget = toTarget / dist;
            Vector2 ringPoint = targetPos - dirToTarget * desiredRange;
            throttle01 = 1f;
            return (ringPoint - rb.position).normalized;
        }

        // If in/near range: orbit around target
        if (enableOrbit)
        {
            float orbitAngleRad = slotBaseAngleRad + orbitPhaseRad +
                                  orbitSign * Mathf.Deg2Rad * (orbitAngularSpeedDeg * Time.time);

            Vector2 radial = DirFromAngleRad(orbitAngleRad); // where we want to be around the target
            Vector2 tangent = Perp(radial) * orbitSign;

            // Base orbit point on the ring
            Vector2 orbitPoint = targetPos + radial * desiredRange;

            // Add tangent offset so it moves in arcs and doesn't "park"
            orbitPoint += tangent * orbitTangentOffset;

            // If too close, bias outward a bit harder (prevents nose-to-nose)
            if (dist < tooClose)
            {
                Vector2 away = (rb.position - targetPos).normalized;
                orbitPoint = targetPos + away * desiredRange + Perp(away) * orbitSign * orbitTangentOffset * 1.2f;
                throttle01 = 1f;
                return (orbitPoint - rb.position).normalized;
            }

            throttle01 = orbitThrottle;
            return (orbitPoint - rb.position).normalized;
        }

        // Orbit disabled but still don't ram: hold roughly at range by aiming for a ring point
        {
            Vector2 dirToTarget = toTarget / dist;
            Vector2 ringPoint = targetPos - dirToTarget * desiredRange;

            // throttle small-ish if already close to ring
            float err = Mathf.Abs(dist - desiredRange);
            throttle01 = err > rangeTolerance ? 1f : 0.6f;

            return (ringPoint - rb.position).normalized;
        }
    }

    private float GetPreferredRange()
    {
        if (preferredRange > 0f) return preferredRange;
        if (shipProfile == null) return 10f;
        return Mathf.Max(1f, shipProfile.combatRange * preferredRangeFromCombatRange);
    }

    private Vector2 ComputeSeparation()
    {
        int count = Physics2D.OverlapCircleNonAlloc(rb.position, separationRadius, separationHits, separationMask);

        if (count <= 0) return Vector2.zero;

        Vector2 repel = Vector2.zero;
        int used = 0;

        for (int i = 0; i < count; i++)
        {
            var col = separationHits[i];
            if (col == null) continue;

            if (separationIgnoreTriggers && col.isTrigger) continue;

            // Get the other ship by TeamAgent (filters bullets/props)
            TeamAgent otherTeam = col.GetComponentInParent<TeamAgent>();
            if (otherTeam == null) continue;
            if (otherTeam == self) continue;

            Rigidbody2D otherRb = col.attachedRigidbody;
            if (otherRb == null) continue;

            Vector2 delta = rb.position - otherRb.position;
            float d2 = delta.sqrMagnitude;
            if (d2 < 0.0001f) continue;

            // Stronger when closer: 1/d (ish)
            float d = Mathf.Sqrt(d2);
            float w = Mathf.Clamp01(1f - (d / Mathf.Max(0.01f, separationRadius)));
            repel += (delta / d) * w;

            used++;
        }

        if (used == 0) return Vector2.zero;

        Vector2 outDir = repel / used;
        return outDir.sqrMagnitude > 0.0001f ? outDir.normalized : Vector2.zero;
    }

    private static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);

    private static Vector2 DirFromAngleRad(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

    private Transform GetTargetTransform()
    {
        if (targeting == null)
            return player != null ? player.transform : null;

        var t = targeting.CurrentTarget;
        if (t != null) return t.transform;

        // quick reacquire
        targeting.RetargetNow();
        t = targeting.CurrentTarget;
        if (t != null) return t.transform;

        // fallback
        return player != null ? player.transform : null;
    }

    private Vector2 GetArenaCenter()
    {
        // if player exists, use them as “center” of the action; otherwise world origin
        return player != null ? (Vector2)player.transform.position : Vector2.zero;
    }

    // TURNING: same style as your player turning
    private void SteerToward(Vector2 desiredDir)
    {
        float desiredAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg + shipProfile.rotationOffset;

        float delta = Mathf.DeltaAngle(rb.rotation, desiredAngle);
        dbgDeltaAngle = delta;

        // forward-speed-based authority
        float forwardSpeed = Mathf.Max(0f, Vector2.Dot(rb.linearVelocity, transform.up));
        dbgForwardSpeed = forwardSpeed;

        float speed01 = Mathf.Clamp01(forwardSpeed / Mathf.Max(0.01f, shipProfile.maxSpeed));

        float authority = shipProfile.turnAuthorityBySpeed != null
            ? shipProfile.turnAuthorityBySpeed.Evaluate(speed01)
            : speed01;

        float turnRate = Mathf.Lerp(shipProfile.minTurnDegPerSec, shipProfile.maxTurnDegPerSec, authority);
        dbgTurnRate = turnRate;

        float maxStep = turnRate * Time.fixedDeltaTime;
        float newAngle = rb.rotation + Mathf.Clamp(delta, -maxStep, maxStep);
        rb.MoveRotation(newAngle);
    }

    private void ApplyForwardThrust(float throttle01, bool hardReturn)
    {
        dbgThrustForce = Vector2.zero;
        if (throttle01 <= 0.001f) return;

        float speedMul = shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f;

        float thrust = shipProfile.forwardThrust * speedMul;
        if (hardReturn) thrust *= returnForceMultiplier;

        Vector2 f = (Vector2)transform.up * (throttle01 * thrust);
        rb.AddForce(f, ForceMode2D.Force);

        dbgThrustForce = f;
    }

    private void ApplyFlightAssist(bool isThrusting)
    {
        Vector2 v = rb.linearVelocity;

        if (v.sqrMagnitude < 0.0001f)
        {
            dbgAlignForce = Vector2.zero;
            dbgDampForce = Vector2.zero;
            return;
        }

        float speedMul = shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f;

        // Align: kill sideways drift (keep forward component)
        Vector2 forward = transform.up;
        float fwdSpeed = Vector2.Dot(v, forward);
        Vector2 desiredVel = forward * fwdSpeed;

        Vector2 alignForce = (desiredVel - v) * (shipProfile.alignStrength * speedMul);
        rb.AddForce(alignForce, ForceMode2D.Force);
        dbgAlignForce = alignForce;

        // Damping when not thrusting (coast)
        Vector2 dampForce = Vector2.zero;
        if (!isThrusting && shipProfile.dampStrength > 0f)
        {
            dampForce = -v * (shipProfile.dampStrength * speedMul);
            rb.AddForce(dampForce, ForceMode2D.Force);
        }

        dbgDampForce = dampForce;
    }

    private void ClampSpeed()
    {
        float speedMul = shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f;
        float max = shipProfile.maxSpeed * speedMul;

        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude > max * max)
            rb.linearVelocity = v.normalized * max;
    }

    private void UpdateSpeedMultiplier()
    {
        if (!shipProfile.useRandomSpeed)
        {
            currentSpeedMultiplier = 1f;
            return;
        }

        float t = (Mathf.Sin(Time.time * Mathf.PI * 2f * shipProfile.oscillationHz) + 1f) * 0.5f;
        currentSpeedMultiplier = Mathf.Lerp(shipProfile.minSpeedMultiplier, shipProfile.maxSpeedMultiplier, t);
    }

    private IEnumerator RandomBarrellRoll()
    {
        // desync so not all ships roll at same time right after spawn
        yield return new WaitForSeconds(Random.Range(0f, 1.5f));

        while (shipProfile.useRandomBarrelRoll)
        {
            float wait = Random.Range(shipProfile.barrelRollMinTime, shipProfile.barrelRollMaxTime);
            yield return new WaitForSeconds(wait);

            if (!isBarrellRolling)
                yield return StartCoroutine(BarrellRolly());
        }
    }

    private IEnumerator BarrellRolly()
    {
        isBarrellRolling = true;

        // Dodge sideways relative to ship
        Vector2 rollDir = (Vector2)transform.right * (Random.value > 0.5f ? 1f : -1f);

        float duration = Mathf.Max(0.01f, shipProfile.barrelRollDuration);
        float desiredDeltaV = shipProfile.barrelRollDistance / duration;
        float impulse = rb.mass * desiredDeltaV;

        rb.AddForce(rollDir * impulse, ForceMode2D.Impulse);

        float elapsed = 0f;
        float spinPerSecond = shipProfile.barrelRollSpinDegrees / duration;

        while (elapsed < duration)
        {
            float newAngle = rb.rotation + spinPerSecond * Time.fixedDeltaTime;
            rb.MoveRotation(newAngle);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isBarrellRolling = false;
    }

    // -----------------------
    // Gizmos / Debug Drawing
    // -----------------------
    private void OnDrawGizmos()
    {
        if (!drawDebug) return;
        if (drawOnlyWhenSelected) return;
        DrawDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;
        DrawDebugGizmos();
    }

    private void DrawDebugGizmos()
    {
        if (!Application.isPlaying) return;

        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!rb) return;

        Vector3 pos = transform.position;
        Vector2 v = rb.linearVelocity;

        DrawArrow(pos, ClampVec3(v * velocityGizmoScale, maxVelocityGizmoLength), Color.cyan); // velocity
        DrawArrow(pos, ClampVec3(dbgDesiredDir * 2.5f, maxVelocityGizmoLength), Color.yellow); // desired dir
        DrawArrow(pos, ClampVec3(dbgSeparation * 2.0f, maxVelocityGizmoLength), Color.magenta); // separation
        DrawArrow(pos, ClampVec3(dbgThrustForce * forceGizmoScale, maxForceGizmoLength), Color.white); // thrust
        DrawArrow(pos, ClampVec3(dbgAlignForce * forceGizmoScale, maxForceGizmoLength),
            new Color(1f, 0.6f, 0f)); // align
        DrawArrow(pos, ClampVec3(dbgDampForce * forceGizmoScale, maxForceGizmoLength), Color.red); // damp
        DrawArrow(pos, (Vector3)(transform.up * 2f), Color.green); // facing
    }

    private static Vector3 ClampVec3(Vector2 v, float maxLen)
    {
        float mag = v.magnitude;
        if (mag <= maxLen) return v;
        return (Vector3)(v * (maxLen / Mathf.Max(0.0001f, mag)));
    }

    private static void DrawArrow(Vector3 pos, Vector3 vec, Color color)
    {
        if (vec.sqrMagnitude < 0.000001f) return;

        Gizmos.color = color;
        Gizmos.DrawLine(pos, pos + vec);

        Vector3 dir = vec.normalized;
        float headLen = Mathf.Clamp(vec.magnitude * 0.25f, 0.15f, 0.6f);
        float headAngle = 25f;

        Vector3 right = Quaternion.Euler(0f, 0f, headAngle) * (-dir);
        Vector3 left = Quaternion.Euler(0f, 0f, -headAngle) * (-dir);

        Gizmos.DrawLine(pos + vec, pos + vec + right * headLen);
        Gizmos.DrawLine(pos + vec, pos + vec + left * headLen);
    }
}