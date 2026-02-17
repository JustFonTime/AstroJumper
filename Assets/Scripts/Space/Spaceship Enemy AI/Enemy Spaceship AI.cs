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
    [Header("Refs (fallback only)")]
    [SerializeField] private GameObject player; // optional fallback if no team target
    [SerializeField] private EnemyShipProfileSO shipProfile;

    private Rigidbody2D rb;
    private TeamAgent self;
    private TargetingComponent targeting;

    [Header("Runtime")]
    [SerializeField] private bool isBarrellRolling = false;
    [SerializeField] private float currentSpeedMultiplier = 1f;

    // coroutines (pool safe)
    private Coroutine barrelCo;

    [Header("Arena / Return (no navmesh needed)")]
    [Tooltip("When no target, if farther than this from center, will thrust toward center. If 0, will not return and just idle in place.")]
    [SerializeField] private float returnToCenterRadius = 25f;

    [Tooltip("Harder pull when supper far away")]
    [SerializeField] private float hardReturnRadius = 80f;

    [SerializeField] private float returnForceMultiplier = 1.25f;

    // -----------------------
    // Debug Gizmos / Lines
    // -----------------------
    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;
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
    private float dbgForwardSpeed;
    private float dbgTurnRate;
    private float dbgDeltaAngle;
    private string dbgState = "spawn";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        self = GetComponent<TeamAgent>();
        targeting = GetComponent<TargetingComponent>();

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

        // reset debug
        dbgDesiredDir = Vector2.zero;
        dbgThrustForce = Vector2.zero;
        dbgAlignForce = Vector2.zero;
        dbgDampForce = Vector2.zero;
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

        // Decide where we want to go:
        // - if we have a target: chase it
        // - if not: return toward center (player position if exists, else Vector2.zero)
        Vector2 desiredPos;
        bool hasTarget = (targetTf != null);

        if (hasTarget)
        {
            desiredPos = targetTf.position;
            dbgState = "chase";
        }
        else
        {
            desiredPos = GetArenaCenter();
            dbgState = "return/idle";
        }

        Vector2 toDesired = desiredPos - rb.position;
        if (toDesired.sqrMagnitude < 0.0001f)
        {
            // nothing to do
            dbgDesiredDir = Vector2.zero;
            dbgThrustForce = Vector2.zero;
            dbgAlignForce = Vector2.zero;
            dbgDampForce = Vector2.zero;
            return;
        }

        dbgDesiredDir = toDesired.normalized;

        //  Turn using speed-based turn authority
        SteerToward(dbgDesiredDir);

        // Forward-only thrust
        bool shouldThrust = hasTarget;

        if (!hasTarget)
        {
            float distToCenter = toDesired.magnitude;
            shouldThrust = distToCenter > returnToCenterRadius;
        }

        ApplyForwardThrust(shouldThrust, hasTarget);

        //  flight assist (kills sideways drift a bit)
        ApplyFlightAssist(shouldThrust);

      
        ClampSpeed();

        
        if (drawTargetLine)
        {
            if (hasTarget)
                Debug.DrawLine(transform.position, targetTf.position, hasTargetColor, 0f, false);
            else
                Debug.DrawLine(transform.position, transform.position + (Vector3)dbgDesiredDir * 3f, noTargetColor, 0f, false);
        }
    }

    private Transform GetTargetTransform()
    {
        if (targeting == null) return player != null ? player.transform : null;

        var t = targeting.CurrentTarget;
        if (t != null) return t.transform;

        // try quick reacquire
        targeting.RetargetNow();
        t = targeting.CurrentTarget;
        if (t != null) return t.transform;

        // fallback
        return player != null ? player.transform : null;
    }

    private Vector2 GetArenaCenter()
    {
        // if player exists, use their position as center of arena. otherwise use world zero.
        return player != null ? (Vector2)player.transform.position : Vector2.zero;
    }

    private void SteerToward(Vector2 desiredDir)
    {
        // desired facing angle (respect sprite offset)
        float desiredAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg + shipProfile.rotationOffset;

        float delta = Mathf.DeltaAngle(rb.rotation, desiredAngle);
        dbgDeltaAngle = delta;

        //turning authority scales with forward speed (cant turn as well when stopped or moving backwards)
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

    private void ApplyForwardThrust(bool shouldThrust, bool hasTarget)
    {
        dbgThrustForce = Vector2.zero;

        if (!shouldThrust) return;

        float speedMul = shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f;
        float thrust = shipProfile.forwardThrust * speedMul;

        //return to center when no target and past certain radius (optional safety if somehow drifted very far)
        if (!hasTarget)
        {
            float dist = Vector2.Distance(rb.position, GetArenaCenter());
            if (dist > hardReturnRadius)
                thrust *= returnForceMultiplier;
        }

        Vector2 f = (Vector2)transform.up * thrust;
        rb.AddForce(f, ForceMode2D.Force);
        dbgThrustForce = f;
    }

    private void ApplyFlightAssist(bool shouldThrust)
    {
        Vector2 v = rb.linearVelocity;

        if (v.sqrMagnitude < 0.0001f)
        {
            dbgAlignForce = Vector2.zero;
            dbgDampForce = Vector2.zero;
            return;
        }

        float speedMul = shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f;

        // nullify sideways velocity (keep forward velocity intact)
        Vector2 forward = transform.up;
        float fwdSpeed = Vector2.Dot(v, forward);
        Vector2 desiredVel = forward * fwdSpeed;

        Vector2 alignForce = (desiredVel - v) * (shipProfile.alignStrength * speedMul);
        rb.AddForce(alignForce, ForceMode2D.Force);
        dbgAlignForce = alignForce;

        //damp when not thrusting
        Vector2 dampForce = Vector2.zero;
        if (!shouldThrust && shipProfile.dampStrength > 0f)
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
        // random desync so not all ships roll at same time on spawn
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

        DrawArrow(pos, ClampVec3(v * velocityGizmoScale, maxVelocityGizmoLength), Color.cyan);               // velocity
        DrawArrow(pos, ClampVec3(dbgDesiredDir * 2.5f, maxVelocityGizmoLength), Color.yellow);              // desired dir
        DrawArrow(pos, ClampVec3(dbgThrustForce * forceGizmoScale, maxForceGizmoLength), Color.white);      // thrust force
        DrawArrow(pos, ClampVec3(dbgAlignForce * forceGizmoScale, maxForceGizmoLength), new Color(1f,0.6f,0f)); // align
        DrawArrow(pos, ClampVec3(dbgDampForce * forceGizmoScale, maxForceGizmoLength), Color.red);          // damp

        DrawArrow(pos, (Vector3)(transform.up * 2f), Color.green); // facing

#if UNITY_EDITOR
        Handles.Label(pos + Vector3.up * 1.1f,
            $"{dbgState} | v={v.magnitude:0.0} | fwd={dbgForwardSpeed:0.0} | turn={dbgTurnRate:0.0} | dAng={dbgDeltaAngle:0.0} | team={self.TeamId}");
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
