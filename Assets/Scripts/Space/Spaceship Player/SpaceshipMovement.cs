#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpaceshipMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Camera cam;

    [SerializeField] private PlayerUpgradeState upgradeState;

    [Header("Forward Thrusters (No Reverse / No Strafe)")] [SerializeField]
    private float forwardThrust = 12f;

    [SerializeField] private float maxSpeed = 12f;

    [Header("Brake (S key)")] [Tooltip("Extra damping force when braking (S). Higher = stops faster.")] [SerializeField]
    private float brakeStrength = 6f;

    [Tooltip("If true, cancels backwards drift (so ship never moves backwards for long).")] [SerializeField]
    private bool preventBackwardVelocity = true;

    [Tooltip("How aggressively we kill backward velocity when preventBackwardVelocity is on.")] [SerializeField]
    private float backwardKillStrength = 18f;

    [Header("Flight Assist")]
    [Tooltip("How strongly we remove sideways drift (higher = snappier, lower = drifty).")]
    [SerializeField]
    private float alignStrength = 6f;

    [Tooltip("Base damping when you give no input (helps stop endless drifting).")] [SerializeField]
    private float coastDampStrength = 1.25f;

    [Header("Rotation (Mouse Aim)")] [SerializeField]
    private float rotationOffset = -90f;

    [SerializeField] private float rotationLerp = 12f;

    [Header("Barrel Roll (Dodge)")] [SerializeField]
    private float barrellRollDistance = 10f;

    [SerializeField] private float barrellRollDuration = 0.5f;
    [SerializeField] private float barrellRollSpinDegrees = 360f;
    [SerializeField] private KeyCode barrellRollKey = KeyCode.Space;
    [SerializeField] private float barrellRollCooldown = 2f;

    private bool canBarrellRoll = true;
    private bool isBarrellRolling = false;

    [Header("Boost")] [SerializeField] private float boostForce = 20f;
    [SerializeField] private float maxBoost = 100f;
    [SerializeField] private float currentBoost = 100f;

    [SerializeField] private KeyCode boostKey = KeyCode.LeftShift;
    [SerializeField] private float boostConsumptionRate = 30f; // per second
    [SerializeField] private float rechargeDelay = 1f;
    [SerializeField] private float rechargeRate = 25f; // per second

    private bool isBoosting = false;
    private bool isRecharging = false;

    // Input
    private float throttle01; // 0..1 (W)
    private float brake01; // 0..1 (S)
    private float targetAngle;

    [Header("Debug Gizmos")] [SerializeField]
    private bool drawDebug = true;

    [SerializeField] private bool drawOnlyWhenSelected = true;

    [SerializeField] private float velocityGizmoScale = 0.25f;
    [SerializeField] private float forceGizmoScale = 0.05f;

    [SerializeField] private float maxVelocityGizmoLength = 8f;
    [SerializeField] private float maxForceGizmoLength = 6f;

    // cached gizmo forces (last FixedUpdate)
    private Vector2 dbgAlignForce;
    private Vector2 dbgDampForce;
    private Vector2 dbgThrustForce;
    private Vector2 dbgBackwardKillForce;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    private void Update()
    {
        // Axis-based (works for keyboard + gamepad)
        // Vertical: W = +1, S = -1
        float v = Input.GetAxisRaw("Vertical");
        throttle01 = Mathf.Clamp01(v);
        brake01 = Mathf.Clamp01(-v);

        UpdateTargetAngleToMouse();

        if (Input.GetKeyDown(barrellRollKey) && canBarrellRoll && !isBarrellRolling)
            StartCoroutine(BarrellRolly());

        isBoosting = Input.GetKey(boostKey) && currentBoost > 0f;

        if (!isBoosting && !isRecharging && currentBoost < maxBoost)
        {
            isRecharging = true;
            StartCoroutine(RechargeBoost());
        }
    }

    private void FixedUpdate()
    {
        if (isBarrellRolling) return;

        ApplyForwardThrust();
        ApplyBoost();
        ApplyFlightAssistAndBrake();
        RotateShipPhysics();
        ClampSpeed();
    }

    private void ApplyForwardThrust()
    {
        dbgThrustForce = Vector2.zero;

        if (throttle01 <= 0.001f) return;

        float moveUpgrade = GetUpgrade(PlayerUpgradeState.UpgradeType.MoveForce);
        float thrust = forwardThrust + moveUpgrade;

        Vector2 forward = transform.up;
        Vector2 f = forward * (throttle01 * thrust);
        rb.AddForce(f, ForceMode2D.Force);

        dbgThrustForce = f;
    }

    private void ApplyFlightAssistAndBrake()
    {
        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude < 0.0001f)
        {
            dbgAlignForce = Vector2.zero;
            dbgDampForce = Vector2.zero;
            dbgBackwardKillForce = Vector2.zero;
            return;
        }

        Vector2 forward = transform.up;

        // --- 1) Align: kill sideways drift (keeps it modern-arcade)
        float forwardSpeed = Vector2.Dot(v, forward);
        Vector2 desiredVel = forward * forwardSpeed; // remove lateral component
        Vector2 alignForce = (desiredVel - v) * alignStrength;
        rb.AddForce(alignForce, ForceMode2D.Force);
        dbgAlignForce = alignForce;

        // --- 2) Damping: coast + brake
        // Coasting damping when not thrusting
        bool noThrottle = throttle01 < 0.001f;
        float damp = noThrottle ? coastDampStrength : 0f;

        // Braking adds more damping
        if (brake01 > 0.001f)
            damp += brakeStrength * brake01;

        Vector2 dampForce = Vector2.zero;
        if (damp > 0f)
        {
            dampForce = -v * damp;
            rb.AddForce(dampForce, ForceMode2D.Force);
        }

        dbgDampForce = dampForce;

        // --- 3) Prevent backward motion (optional)
        dbgBackwardKillForce = Vector2.zero;
        if (preventBackwardVelocity && forwardSpeed < 0f)
        {
            // push forward proportional to how fast we’re moving backwards
            Vector2 kill = forward * (-forwardSpeed * backwardKillStrength);
            rb.AddForce(kill, ForceMode2D.Force);
            dbgBackwardKillForce = kill;
        }
    }

    private void RotateShipPhysics()
    {
        float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, rotationLerp * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
    }

    private void ClampSpeed()
    {
        float maxSpeedWithUpgrades = maxSpeed + GetUpgrade(PlayerUpgradeState.UpgradeType.MaxSpeed);

        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude > maxSpeedWithUpgrades * maxSpeedWithUpgrades)
            rb.linearVelocity = v.normalized * maxSpeedWithUpgrades;
    }

    private void UpdateTargetAngleToMouse()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        Vector3 mouse = Input.mousePosition;

        float zDist = Mathf.Abs(transform.position.z - cam.transform.position.z);
        mouse.z = zDist;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouse);
        Vector2 toMouse = (Vector2)(mouseWorld - transform.position);

        if (toMouse.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        targetAngle = angle + rotationOffset;
    }

    private void ApplyBoost()
    {
        if (!isBoosting) return;

        float boostUpgrade = GetUpgrade(PlayerUpgradeState.UpgradeType.BoostForce);

        Vector2 forward = transform.up;
        rb.AddForce(forward * (boostForce + boostUpgrade), ForceMode2D.Force);

        currentBoost -= boostConsumptionRate * Time.fixedDeltaTime;
        currentBoost = Mathf.Max(0f, currentBoost);

        isRecharging = false;
    }

    private IEnumerator RechargeBoost()
    {
        yield return new WaitForSeconds(rechargeDelay);

        while (!isBoosting && currentBoost < maxBoost)
        {
            currentBoost += rechargeRate * Time.deltaTime;
            currentBoost = Mathf.Min(currentBoost, maxBoost);
            yield return null;
        }

        isRecharging = false;
    }

    private IEnumerator BarrellRolly()
    {
        float dist = barrellRollDistance + GetUpgrade(PlayerUpgradeState.UpgradeType.BarrelRollDistance);
        float dur = barrellRollDuration - GetUpgrade(PlayerUpgradeState.UpgradeType.BarrelRollSpeed);
        dur = Mathf.Max(0.05f, dur);

        isBarrellRolling = true;
        canBarrellRoll = false;

        // With forward-only ships, barrel roll is a sideways dodge (feels great + readable)
        Vector2 right = transform.right;
        Vector2 rollDir = right * (Input.GetAxisRaw("Horizontal") < 0f ? -1f : 1f);

        float desiredDeltaV = dist / dur;
        float impulse = rb.mass * desiredDeltaV;

        rb.AddForce(rollDir * impulse, ForceMode2D.Impulse);

        float elapsed = 0f;
        float spinPerSecond = barrellRollSpinDegrees / dur;

        while (elapsed < dur)
        {
            float newAngle = rb.rotation + spinPerSecond * Time.fixedDeltaTime;
            rb.MoveRotation(newAngle);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isBarrellRolling = false;

        yield return new WaitForSeconds(barrellRollCooldown);
        canBarrellRoll = true;
    }

    private float GetUpgrade(PlayerUpgradeState.UpgradeType type)
    {
        if (!upgradeState) return 0f;
        return upgradeState.GetUpgradeBoost(type);
    }

    // -----------------------
    // Gizmos / Debug Drawing
    // -----------------------
    private void OnDrawGizmos()
    {
        if (!drawDebug) return;
        if (drawOnlyWhenSelected) return;
        DrawMovementDebug();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;
        DrawMovementDebug();
    }

    private void DrawMovementDebug()
    {
        if (!Application.isPlaying) return;

        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!rb) return;

        Vector3 pos = transform.position;

        Vector2 v = rb.linearVelocity;
        Vector2 forward = transform.up;

        float forwardSpeed = Vector2.Dot(v, forward);
        Vector2 forwardV = forward * forwardSpeed;
        Vector2 lateralV = v - forwardV;

        DrawArrow(pos, ClampVec3(v * velocityGizmoScale, maxVelocityGizmoLength), Color.cyan); // velocity
        DrawArrow(pos, ClampVec3(forwardV * velocityGizmoScale, maxVelocityGizmoLength), Color.green); // forward vel
        DrawArrow(pos, ClampVec3(lateralV * velocityGizmoScale, maxVelocityGizmoLength),
            Color.magenta); // lateral drift

        DrawArrow(pos, ClampVec3(dbgThrustForce * forceGizmoScale, maxForceGizmoLength), Color.white); // thrust
        DrawArrow(pos, ClampVec3(dbgAlignForce * forceGizmoScale, maxForceGizmoLength),
            new Color(1f, 0.6f, 0f)); // align
        DrawArrow(pos, ClampVec3(dbgDampForce * forceGizmoScale, maxForceGizmoLength), Color.red); // damping/brake
        DrawArrow(pos, ClampVec3(dbgBackwardKillForce * forceGizmoScale, maxForceGizmoLength),
            Color.blue); // backward kill

        DrawArrow(pos, (Vector3)(forward * 2f), Color.yellow); // facing

#if UNITY_EDITOR
        Handles.Label(pos + Vector3.up * 1.2f,
            $"v={v.magnitude:0.0}  fwd={forwardSpeed:0.0}  lat={lateralV.magnitude:0.0}  thr={throttle01:0.0}  brk={brake01:0.0}");
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

        Vector3 r = Quaternion.Euler(0f, 0f, headAngle) * (-dir);
        Vector3 l = Quaternion.Euler(0f, 0f, -headAngle) * (-dir);

        Gizmos.DrawLine(pos + vec, pos + vec + r * headLen);
        Gizmos.DrawLine(pos + vec, pos + vec + l * headLen);
    }
}