#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class SpaceshipMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Camera cam;

    [SerializeField] private PlayerUpgradeState upgradeState;

    [Header("Forward Thrusters (No Reverse / No Strafe)")]
    [SerializeField] private float forwardThrust = 12f;
    [SerializeField] private float maxSpeed = 12f;

    [Header("Brake (S key)")]
    [Tooltip("Extra damping force when braking (S). Higher = stops faster.")]
    [SerializeField] private float brakeStrength = 6f;

    [Header("Flight Assist")]
    [Tooltip("Removes sideways drift (higher = snappier)")]
    [SerializeField] private float alignStrength = 6f;

    [Tooltip("Damping when not thrusting (higher = stops coasting faster)")]
    [SerializeField] private float coastDampStrength = 1.25f;

    [Header("Turning (A/D)")]
    [Tooltip("Degrees/sec when you have NO turning authority (usually 0).")]
    [SerializeField] private float minTurnDegPerSec = 0f;

    [Tooltip("Degrees/sec when you have FULL turning authority.")]
    [SerializeField] private float maxTurnDegPerSec = 240f;

    [Tooltip("How much turning authority you get based on forward speed (0..1).")]
    [SerializeField] private AnimationCurve turnAuthorityBySpeed =
        new AnimationCurve(
            new Keyframe(0f, 0f),     // stopped -> no turn
            new Keyframe(0.25f, 0.25f),
            new Keyframe(1f, 1f)      // at max speed -> full turn
        );

    [Tooltip("Optional: make ship resist turning unless moving forward.")]
    [SerializeField] private bool requireForwardSpeedToTurn = true;


    [Header("Barrel Roll (Dodge)")]
    [SerializeField] private float barrellRollDistance = 10f;
    [SerializeField] private float barrellRollDuration = 0.5f;
    [SerializeField] private float barrellRollSpinDegrees = 360f;
    [SerializeField] private float barrellRollCooldown = 2f;

    private bool canBarrellRoll = true;
    private bool isBarrellRolling = false;

    [Header("Boost")]
    [SerializeField] private float boostForce = 20f;
    [SerializeField] private float maxBoost = 100f;
    [SerializeField] private float currentBoost = 100f;

    public float CurrentBoost => currentBoost;
    public float MaxBoost => maxBoost;

    [SerializeField] private float boostConsumptionRate = 30f; // per second
    [SerializeField] private float rechargeDelay = 1f;
    [SerializeField] private float rechargeRate = 25f; // per second

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset actionsAsset;
    [SerializeField] private string actionMapName = "Player";

    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string boostActionName = "Boost";
    [SerializeField] private string barrelRollActionName = "BarrelRoll";

    private InputAction moveAction;
    private InputAction boostAction;
    private InputAction barrelRollAction;


    private bool isBoosting = false;
    private bool isRecharging = false;

    // Input
    private float throttle01;   // W only (0..1)
    private float brake01;      // S only (0..1)
    private float turnInput;    // A/D (-1..1)

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawDebug = false;
    [SerializeField] private bool drawOnlyWhenSelected = true;

    [SerializeField] private float velocityGizmoScale = 0.25f;
    [SerializeField] private float forceGizmoScale = 0.05f;

    [SerializeField] private float maxVelocityGizmoLength = 8f;
    [SerializeField] private float maxForceGizmoLength = 6f;

    // cached gizmo forces (last FixedUpdate)
    private Vector2 dbgAlignForce;
    private Vector2 dbgDampForce;
    private Vector2 dbgThrustForce;
    private float dbgTurnRate;
    private float dbgForwardSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;

        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        currentBoost = maxBoost;

        var map = actionsAsset.FindActionMap(actionMapName, true);

        moveAction = map.FindAction(moveActionName, true);
        boostAction = map.FindAction(boostActionName, true);
        barrelRollAction = map.FindAction(barrelRollActionName, true);
    }

    private void Update()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();

        float v = move.y;
        throttle01 = Mathf.Clamp01(v);
        brake01 = Mathf.Clamp01(-v);

        turnInput = move.x;

        isBoosting = boostAction.IsPressed() && currentBoost > 0f;

        if (!isBoosting && !isRecharging && currentBoost < maxBoost)
        {
            isRecharging = true;
            StartCoroutine(RechargeBoost());
        }


    }

    private void OnBarrelRoll(InputAction.CallbackContext ctx)
    {
        if (canBarrellRoll && !isBarrellRolling)
            StartCoroutine(BarrellRolly());
    }

    private void FixedUpdate()
    {
        if (!rb) return;

        if (!isBarrellRolling)
            rb.angularVelocity = 0f;

        if (isBarrellRolling) return;

        ApplyForwardThrust();
        ApplyBoost();
        ApplyFlightAssistAndBrake();
        ApplyTurn();
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
            return;
        }

        Vector2 forward = transform.up;

        // remove sideways drift
        float forwardSpeed = Vector2.Dot(v, forward);
        Vector2 desiredVel = forward * forwardSpeed;

        Vector2 alignForce = (desiredVel - v) * alignStrength;
        rb.AddForce(alignForce, ForceMode2D.Force);
        dbgAlignForce = alignForce;

        // damping
        bool noThrottle = throttle01 < 0.001f;
        float damp = noThrottle ? coastDampStrength : 0f;

        if (brake01 > 0.001f)
            damp += brakeStrength * brake01;

        Vector2 dampForce = Vector2.zero;
        if (damp > 0f)
        {
            dampForce = -v * damp;
            rb.AddForce(dampForce, ForceMode2D.Force);
        }

        dbgDampForce = dampForce;
    }

    private void ApplyTurn()
    {
        if (Mathf.Abs(turnInput) < 0.001f) { dbgTurnRate = 0f; return; }

        // turning authity depends on  speed
        float forwardSpeed = Mathf.Max(0f, Vector2.Dot(rb.linearVelocity, transform.up));
        dbgForwardSpeed = forwardSpeed;

        float speed01 = Mathf.Clamp01(forwardSpeed / Mathf.Max(0.01f, maxSpeed));
        float authority = turnAuthorityBySpeed != null ? turnAuthorityBySpeed.Evaluate(speed01) : speed01;

        if (requireForwardSpeedToTurn && authority <= 0.001f)
        {
            dbgTurnRate = 0f;
            return;
        }

        float turnRate = Mathf.Lerp(minTurnDegPerSec, maxTurnDegPerSec, authority);
        dbgTurnRate = turnRate;

        float maxStep = turnRate * Time.fixedDeltaTime;

        // Unity 2D rotation
        float step = -turnInput * maxStep;

        rb.MoveRotation(rb.rotation + step);
    }

    private void ClampSpeed()
    {
        float maxSpeedWithUpgrades = maxSpeed + GetUpgrade(PlayerUpgradeState.UpgradeType.MaxSpeed);

        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude > maxSpeedWithUpgrades * maxSpeedWithUpgrades)
            rb.linearVelocity = v.normalized * maxSpeedWithUpgrades;
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

        // dodge sideways (based on turn input; default right)
        Vector2 right = transform.right;
        float h = moveAction.ReadValue<Vector2>().x;
        Vector2 rollDir = right * (h < 0f ? -1f : 1f);

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
        DrawArrow(pos, ClampVec3(lateralV * velocityGizmoScale, maxVelocityGizmoLength), Color.magenta); // drift

        DrawArrow(pos, ClampVec3(dbgThrustForce * forceGizmoScale, maxForceGizmoLength), Color.white); // thrust
        DrawArrow(pos, ClampVec3(dbgAlignForce * forceGizmoScale, maxForceGizmoLength), new Color(1f, 0.6f, 0f)); // align
        DrawArrow(pos, ClampVec3(dbgDampForce * forceGizmoScale, maxForceGizmoLength), Color.red); // brake/coast

        DrawArrow(pos, (Vector3)(forward * 2f), Color.yellow); // facing

#if UNITY_EDITOR
        Handles.Label(pos + Vector3.up * 1.2f,
            $"v={v.magnitude:0.0} fwd={dbgForwardSpeed:0.0} turnRate={dbgTurnRate:0.0} thr={throttle01:0.0} brk={brake01:0.0}");
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

    private void OnEnable()
    {
        moveAction.Enable();
        boostAction.Enable();
        barrelRollAction.Enable();

        barrelRollAction.performed += OnBarrelRoll;
    }

    private void OnDisable()
    {
        barrelRollAction.performed -= OnBarrelRoll;

        moveAction.Disable();
        boostAction.Disable();
        barrelRollAction.Disable();
    }
}


