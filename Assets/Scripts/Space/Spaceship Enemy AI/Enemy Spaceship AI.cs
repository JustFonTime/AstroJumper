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
    private enum DogfightPhase
    {
        Approach,
        Orbit,
        AttackRun,
        PeelOff,
        Retreat
    }

    private Rigidbody2D rb;
    private TeamAgent self;
    private TargetingComponent targeting;
    private ShipOrderController orders;
    private SpaceshipHealthComponent health;

    [Header("Refs (fallback only)")] [SerializeField]
    private GameObject player;

    [SerializeField] private EnemyShipProfileSO shipProfile;

    [Header("Runtime")] [SerializeField] private bool isBarrellRolling = false;
    [SerializeField] private float currentSpeedMultiplier = 1f;

    private Coroutine barrelCo;
    private DogfightPhase phase = DogfightPhase.Approach;
    private float phaseTimer;
    private float attackRunCooldownTimer;
    private Vector2 phaseDirection;

    [Header("Arena / Return (no navmesh needed)")]
    [SerializeField] private float returnToCenterRadius = 25f;
    [SerializeField] private float hardReturnRadius = 80f;
    [SerializeField] private float returnForceMultiplier = 1.25f;

    [Header("Dogfight Movement")]
    [SerializeField] private float preferredRange = 0f;
    [Range(0.1f, 1.25f)] [SerializeField] private float preferredRangeFromCombatRange = 0.75f;
    [SerializeField] private float rangeTolerance = 10f;
    [SerializeField] private bool enableOrbit = true;
    [SerializeField] private float orbitAngularSpeedDeg = 180f;
    [SerializeField] private float orbitTangentOffset = 12f;
    [Range(0f, 1f)] [SerializeField] private float orbitThrottle = 0.85f;
    [SerializeField] private bool randomizeDogfightSeedOnSpawn = true;

    private float orbitSign = 1f;
    private float slotBaseAngleRad = 0f;
    private float orbitPhaseRad = 0f;

    [Header("Separation")]
    [SerializeField] private bool enableSeparation = true;
    [SerializeField] private float separationRadius = 10f;
    [SerializeField] private float separationStrength = 5f;
    [SerializeField] private LayerMask separationMask = ~0;
    [SerializeField] private bool separationIgnoreTriggers = true;

    private readonly Collider2D[] separationHits = new Collider2D[24];
    private readonly Collider2D[] forwardAvoidanceHits = new Collider2D[24];

    [Header("Debug")] [SerializeField] private bool drawDebug = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private bool drawTargetLine = true;
    [SerializeField] private Color hasTargetColor = Color.green;
    [SerializeField] private Color noTargetColor = Color.red;
    [SerializeField] private float velocityGizmoScale = 0.25f;
    [SerializeField] private float forceGizmoScale = 0.05f;
    [SerializeField] private float maxVelocityGizmoLength = 8f;
    [SerializeField] private float maxForceGizmoLength = 6f;

    private Vector2 dbgDesiredDir;
    private Vector2 dbgThrustForce;
    private Vector2 dbgAlignForce;
    private Vector2 dbgDampForce;
    private Vector2 dbgSeparation;
    private Vector2 dbgAvoidance;
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
        health = GetComponent<SpaceshipHealthComponent>();

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

        isBarrellRolling = false;
        currentSpeedMultiplier = 1f;
        phase = DogfightPhase.Approach;
        phaseTimer = 0f;
        attackRunCooldownTimer = Random.Range(shipProfile.minAttackRunCooldown, shipProfile.maxAttackRunCooldown);
        phaseDirection = Vector2.zero;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (barrelCo != null) StopCoroutine(barrelCo);
        barrelCo = null;

        if (targeting != null)
            targeting.RetargetNow();

        if (shipProfile.useRandomBarrelRoll)
            barrelCo = StartCoroutine(RandomBarrellRoll());

        if (randomizeDogfightSeedOnSpawn)
        {
            orbitSign = Random.value > 0.5f ? 1f : -1f;
            slotBaseAngleRad = Random.Range(0f, Mathf.PI * 2f);
            orbitPhaseRad = Random.Range(0f, Mathf.PI * 2f);
        }
        else
        {
            float t = Mathf.Abs(GetInstanceID() * 0.6180339f);
            t = t - Mathf.Floor(t);
            orbitSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
            slotBaseAngleRad = t * Mathf.PI * 2f;
            orbitPhaseRad = (1f - t) * Mathf.PI * 2f;
        }

        dbgDesiredDir = Vector2.zero;
        dbgThrustForce = Vector2.zero;
        dbgAlignForce = Vector2.zero;
        dbgDampForce = Vector2.zero;
        dbgSeparation = Vector2.zero;
        dbgAvoidance = Vector2.zero;
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

        if (!isBarrellRolling)
            rb.angularVelocity = 0f;

        if (isBarrellRolling) return;

        UpdateSpeedMultiplier();
        if (attackRunCooldownTimer > 0f)
            attackRunCooldownTimer -= Time.fixedDeltaTime;

        Transform targetTf = GetTargetTransform();
        bool hasTarget = targetTf != null;

        Vector2 desiredDir;
        float throttle01 = 0f;
        bool hardReturn = false;

        if (hasTarget)
        {
            desiredDir = ComputeDogfightDesiredDir(targetTf, out throttle01);
            dbgState = phase.ToString();
        }
        else
        {
            phase = DogfightPhase.Approach;
            desiredDir = ComputeIdleDesiredDir(out throttle01, out hardReturn);
        }

        dbgSeparation = Vector2.zero;
        if (enableSeparation)
        {
            Vector2 sep = ComputeSeparation();
            dbgSeparation = sep;
            Vector2 blended = desiredDir + sep * separationStrength;
            if (blended.sqrMagnitude > 0.0001f)
                desiredDir = blended.normalized;
        }

        dbgAvoidance = Vector2.zero;
        if (shipProfile.useForwardAvoidance)
        {
            Vector2 avoidance = ComputeForwardAvoidance();
            dbgAvoidance = avoidance;
            Vector2 blended = desiredDir + avoidance * shipProfile.forwardAvoidanceStrength;
            if (blended.sqrMagnitude > 0.0001f)
                desiredDir = blended.normalized;
        }

        dbgDesiredDir = desiredDir;

        SteerToward(desiredDir);
        ApplyForwardThrust(throttle01, hardReturn);
        ApplyFlightAssist(throttle01 > 0.05f);
        ClampSpeed();

        if (drawTargetLine)
        {
            if (hasTarget)
                Debug.DrawLine(transform.position, targetTf.position, hasTargetColor, 0f, false);
            else
                Debug.DrawLine(transform.position, transform.position + (Vector3)desiredDir * 3f, noTargetColor, 0f, false);
        }
    }

    private Vector2 ComputeIdleDesiredDir(out float throttle01, out bool hardReturn)
    {
        Vector2 desiredPos = GetArenaCenter();
        throttle01 = 0f;
        hardReturn = false;

        bool orderProvidedGoal = false;
        if (orders != null && orders.TryGetMovementGoal(rb.position, out var goalPos, out var orderThrottle))
        {
            desiredPos = goalPos;
            throttle01 = Mathf.Clamp01(orderThrottle);
            orderProvidedGoal = true;
            dbgState = "order-move";
        }

        Vector2 toDesired = desiredPos - rb.position;
        float dist = toDesired.magnitude;
        if (toDesired.sqrMagnitude < 0.0001f)
        {
            dbgState = orderProvidedGoal ? "order-arrived" : "idle";
            return Vector2.zero;
        }

        Vector2 desiredDir = toDesired / Mathf.Max(0.0001f, dist);
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

        return desiredDir;
    }

    private Vector2 ComputeDogfightDesiredDir(Transform targetTf, out float throttle01)
    {
        Vector2 targetPos = targetTf.position;
        Vector2 toTarget = targetPos - rb.position;
        float dist = toTarget.magnitude;
        float desiredRange = GetPreferredRange();
        float tooFar = desiredRange + rangeTolerance;
        float tooClose = Mathf.Max(0.05f, desiredRange - rangeTolerance);

        if (dist < 0.0001f)
        {
            throttle01 = 1f;
            return Random.insideUnitCircle.normalized;
        }

        if (ShouldRetreat(targetPos))
            StartRetreat(targetPos);

        switch (phase)
        {
            case DogfightPhase.Retreat:
                phaseTimer -= Time.fixedDeltaTime;
                if (phaseTimer <= 0f || !ShouldKeepRetreating())
                    phase = DogfightPhase.Approach;
                else
                {
                    throttle01 = 1f;
                    return phaseDirection;
                }
                break;

            case DogfightPhase.AttackRun:
                phaseTimer -= Time.fixedDeltaTime;
                if (phaseTimer <= 0f)
                    StartPeelOff(targetPos);
                else
                {
                    throttle01 = 1f;
                    return ComputeAttackRunDirection(targetPos);
                }
                break;

            case DogfightPhase.PeelOff:
                phaseTimer -= Time.fixedDeltaTime;
                if (phaseTimer <= 0f)
                    phase = DogfightPhase.Approach;
                else
                {
                    throttle01 = 1f;
                    return phaseDirection;
                }
                break;
        }

        bool canAttackRun = shipProfile.useAttackRuns && attackRunCooldownTimer <= 0f && dist <= desiredRange * shipProfile.attackRunStartDistanceMultiplier;
        if (canAttackRun && dist >= tooClose * 0.8f)
        {
            StartAttackRun(targetPos);
            throttle01 = 1f;
            return ComputeAttackRunDirection(targetPos);
        }

        if (dist > tooFar)
        {
            phase = DogfightPhase.Approach;
            Vector2 dirToTarget = toTarget / dist;
            Vector2 ringPoint = targetPos - dirToTarget * desiredRange;
            throttle01 = 1f;
            return (ringPoint - rb.position).normalized;
        }

        if (enableOrbit)
        {
            phase = DogfightPhase.Orbit;
            float orbitAngleRad = slotBaseAngleRad + orbitPhaseRad + orbitSign * Mathf.Deg2Rad * (orbitAngularSpeedDeg * Time.time);
            Vector2 radial = DirFromAngleRad(orbitAngleRad);
            Vector2 tangent = Perp(radial) * orbitSign;
            Vector2 orbitPoint = targetPos + radial * desiredRange + tangent * orbitTangentOffset;

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

        phase = DogfightPhase.Approach;
        {
            Vector2 dirToTarget = toTarget / dist;
            Vector2 ringPoint = targetPos - dirToTarget * desiredRange;
            float err = Mathf.Abs(dist - desiredRange);
            throttle01 = err > rangeTolerance ? 1f : 0.6f;
            return (ringPoint - rb.position).normalized;
        }
    }

    private bool ShouldRetreat(Vector2 targetPos)
    {
        if (!shipProfile.retreatWhenLow || health == null) return false;
        if (orders != null && orders.CurrentOrder != ShipOrderController.OrderKind.Skirmish) return false;
        if (phase == DogfightPhase.Retreat) return true;

        float shieldRatio = health.MaxShield <= 0 ? 0f : (float)health.Shield / Mathf.Max(1f, health.MaxShield);
        float hullRatio = health.MaxHealth <= 0 ? 0f : (float)health.Health / Mathf.Max(1f, health.MaxHealth);
        return shieldRatio <= shipProfile.retreatShieldRatioThreshold || hullRatio <= shipProfile.retreatHullRatioThreshold;
    }

    private bool ShouldKeepRetreating()
    {
        if (!shipProfile.retreatWhenLow || health == null) return false;
        float shieldRatio = health.MaxShield <= 0 ? 0f : (float)health.Shield / Mathf.Max(1f, health.MaxShield);
        float hullRatio = health.MaxHealth <= 0 ? 0f : (float)health.Health / Mathf.Max(1f, health.MaxHealth);
        return shieldRatio <= shipProfile.retreatShieldRatioThreshold || hullRatio <= shipProfile.retreatHullRatioThreshold;
    }

    private void StartRetreat(Vector2 targetPos)
    {
        phase = DogfightPhase.Retreat;
        phaseTimer = shipProfile.retreatDuration;
        Vector2 away = ((Vector2)rb.position - targetPos).normalized;
        Vector2 side = Perp(away) * orbitSign * shipProfile.retreatSideOffset;
        phaseDirection = (away + side).normalized;
    }

    private void StartAttackRun(Vector2 targetPos)
    {
        phase = DogfightPhase.AttackRun;
        phaseTimer = Random.Range(shipProfile.minAttackRunDuration, shipProfile.maxAttackRunDuration);
        attackRunCooldownTimer = Random.Range(shipProfile.minAttackRunCooldown, shipProfile.maxAttackRunCooldown);
        orbitSign = Random.value > 0.5f ? 1f : -1f;
    }

    private void StartPeelOff(Vector2 targetPos)
    {
        phase = DogfightPhase.PeelOff;
        phaseTimer = Random.Range(shipProfile.minPeelOffDuration, shipProfile.maxPeelOffDuration);
        Vector2 away = ((Vector2)rb.position - targetPos).normalized;
        Vector2 side = Perp(away) * orbitSign * shipProfile.peelOffDistance;
        phaseDirection = (away + side).normalized;
    }

    private Vector2 ComputeAttackRunDirection(Vector2 targetPos)
    {
        Vector2 toTarget = (targetPos - rb.position).normalized;
        Vector2 strafe = Perp(toTarget) * orbitSign * shipProfile.attackRunStrafeOffset;
        Vector2 aimPoint = targetPos + (Vector2)strafe + toTarget * shipProfile.attackRunOvershootDistance;
        return (aimPoint - rb.position).normalized;
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

            TeamAgent otherTeam = col.GetComponentInParent<TeamAgent>();
            if (otherTeam == null || otherTeam == self) continue;

            Rigidbody2D otherRb = col.attachedRigidbody;
            if (otherRb == null) continue;

            Vector2 delta = rb.position - otherRb.position;
            float d2 = delta.sqrMagnitude;
            if (d2 < 0.0001f) continue;

            float d = Mathf.Sqrt(d2);
            float w = Mathf.Clamp01(1f - (d / Mathf.Max(0.01f, separationRadius)));
            repel += (delta / d) * w;
            used++;
        }

        if (used == 0) return Vector2.zero;
        Vector2 outDir = repel / used;
        return outDir.sqrMagnitude > 0.0001f ? outDir.normalized : Vector2.zero;
    }

    private Vector2 ComputeForwardAvoidance()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            rb.position + (Vector2)transform.up * (shipProfile.forwardAvoidanceDistance * 0.5f),
            shipProfile.forwardAvoidanceRadius,
            forwardAvoidanceHits,
            separationMask);

        if (count <= 0) return Vector2.zero;

        Vector2 avoid = Vector2.zero;
        int used = 0;
        Vector2 forward = transform.up;

        for (int i = 0; i < count; i++)
        {
            var col = forwardAvoidanceHits[i];
            if (col == null) continue;
            if (separationIgnoreTriggers && col.isTrigger) continue;

            TeamAgent other = col.GetComponentInParent<TeamAgent>();
            if (other == null || other == self) continue;

            Vector2 toOther = (Vector2)other.transform.position - rb.position;
            float distance = toOther.magnitude;
            if (distance < 0.001f) continue;

            Vector2 dirToOther = toOther / distance;
            float aheadness = Vector2.Dot(forward, dirToOther);
            if (aheadness < shipProfile.forwardAvoidanceDotThreshold) continue;

            float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, shipProfile.forwardAvoidanceDistance));
            avoid += ((Vector2)rb.position - (Vector2)other.transform.position).normalized * aheadness * closeness;
            used++;
        }

        if (used == 0) return Vector2.zero;
        return (avoid / used).normalized;
    }

    private static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);
    private static Vector2 DirFromAngleRad(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

    private Transform GetTargetTransform()
    {
        if (targeting == null)
            return player != null ? player.transform : null;

        var t = targeting.CurrentTarget;
        if (t != null) return t.transform;

        targeting.RetargetNow();
        t = targeting.CurrentTarget;
        if (t != null) return t.transform;

        return player != null ? player.transform : null;
    }

    private Vector2 GetArenaCenter()
    {
        return player != null ? (Vector2)player.transform.position : Vector2.zero;
    }

    private void SteerToward(Vector2 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.0001f) return;

        float desiredAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg + shipProfile.rotationOffset;
        float delta = Mathf.DeltaAngle(rb.rotation, desiredAngle);
        dbgDeltaAngle = delta;

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
        Vector2 forward = transform.up;
        float fwdSpeed = Vector2.Dot(v, forward);
        Vector2 desiredVel = forward * fwdSpeed;

        Vector2 alignForce = (desiredVel - v) * (shipProfile.alignStrength * speedMul);
        rb.AddForce(alignForce, ForceMode2D.Force);
        dbgAlignForce = alignForce;

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

        DrawArrow(pos, ClampVec3(v * velocityGizmoScale, maxVelocityGizmoLength), Color.cyan);
        DrawArrow(pos, ClampVec3(dbgDesiredDir * 2.5f, maxVelocityGizmoLength), Color.yellow);
        DrawArrow(pos, ClampVec3(dbgSeparation * 2.0f, maxVelocityGizmoLength), Color.magenta);
        DrawArrow(pos, ClampVec3(dbgAvoidance * 2.0f, maxVelocityGizmoLength), Color.blue);
        DrawArrow(pos, ClampVec3(dbgThrustForce * forceGizmoScale, maxForceGizmoLength), Color.white);
        DrawArrow(pos, ClampVec3(dbgAlignForce * forceGizmoScale, maxForceGizmoLength), new Color(1f, 0.6f, 0f));
        DrawArrow(pos, ClampVec3(dbgDampForce * forceGizmoScale, maxForceGizmoLength), Color.red);
        DrawArrow(pos, (Vector3)(transform.up * 2f), Color.green);

#if UNITY_EDITOR
        Handles.Label(pos + Vector3.up * 1.2f, $"{dbgState} v={v.magnitude:0.0}");
#endif
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
