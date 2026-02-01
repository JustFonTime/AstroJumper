using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpaceshipMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Camera cam;

    [SerializeField] private PlayerUpgradeState upgradeState;

    [Header("Movement")] [SerializeField] private float moveForce = 10f;
    [SerializeField] private float maxSpeed = 12f;

    [Header("Rotation")] [SerializeField] private float rotationOffset = -90f;
    [SerializeField] private float rotationLerp = 12f;

    [Header("Barrel Roll")] [SerializeField]
    private float barrellRollDistance = 10f;

    [SerializeField] private float barrellRollDuration = 0.5f;
    [SerializeField] private float barrellRollSpinDegrees = 360f;
    [SerializeField] private KeyCode barrellRollKey = KeyCode.Space;
    [SerializeField] private float barrellRollCooldown = 2f;

    private bool canBarrellRoll = true;
    private bool isBarrellRolling = false;

    [Header("Boost")] [SerializeField] private float boostForce = 20f;
    [SerializeField] private float maxBoost = 100f;

    public float MaxBoost
    {
        get { return maxBoost; }
    }

    [SerializeField] private float currentBoost;

    public float CurrentBoost
    {
        get { return currentBoost; }
    }

    [SerializeField] private KeyCode boostKey = KeyCode.LeftShift;
    [SerializeField] private float boostConsumptionRate = 3;
    [SerializeField] private float boostCooldown = 2f;
    [SerializeField] private bool isBoosting = false;

    [SerializeField] private bool isRecharging = false;
    [SerializeField] private float rechargeDelay = 1f;

    [SerializeField] private float rechargeRate = 15f;

    // cached input/aim
    private Vector2 moveInput;
    private float targetAngle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cam = Camera.main;
    }

    private void Update()
    {
        // Movement input
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        //Set aim target to mouse
        UpdateTargetAngleToMouse();

        //start barrel roll
        if (Input.GetKeyDown(barrellRollKey) && canBarrellRoll && !isBarrellRolling)
        {
            StartCoroutine(BarrellRolly());
        }

        if (!isBoosting && !isRecharging && currentBoost < maxBoost)
        {
            isRecharging = true;
            StartCoroutine(RechargeBoost());
        }
    }


    private IEnumerator RechargeBoost()
    {
        isRecharging = true;
        yield return new WaitForSeconds(rechargeDelay);

        while (currentBoost < maxBoost)
        {
            currentBoost += rechargeRate * Time.deltaTime;
            currentBoost = Mathf.Min(currentBoost, maxBoost);
            yield return null;
        }

        isRecharging = false;
    }

    private void FixedUpdate()
    {
        if (isBarrellRolling) return;

        MoveSpaceshipPhysics();
        TryBoost();
        RotateShipPhysics();
        ClampSpeed();
    }

    private void MoveSpaceshipPhysics()
    {
        if (moveInput.sqrMagnitude < 0.0001f) return;

        Vector2 dir = moveInput.normalized;
        float moveForceWithUpgrades =
            moveForce + upgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.MoveForce);
        rb.AddForce(dir * moveForceWithUpgrades, ForceMode2D.Force);
    }

    private void RotateShipPhysics()
    {
        // rotate the spacehip towards target angle
        float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, rotationLerp * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
    }

    private void ClampSpeed()
    {
        float maxSpeedWithUpgrades = maxSpeed + upgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.MaxSpeed);
        if (rb.linearVelocity.sqrMagnitude > maxSpeedWithUpgrades * maxSpeedWithUpgrades)

            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeedWithUpgrades;
    }

    private void UpdateTargetAngleToMouse()
    {
        if (cam == null) return;

        Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = (Vector2)(mousePos - transform.position);

        if (toMouse.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        targetAngle = angle + rotationOffset;
    }

    private IEnumerator BarrellRolly()
    {
        float barrellRollDistanceWithUpgrades = barrellRollDistance +
                                                upgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType
                                                    .BarrelRollDistance);
        float barrellRollDurationWithUpgrades = barrellRollDuration -
                                                upgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType
                                                    .BarrelRollSpeed);
        isBarrellRolling = true;
        canBarrellRoll = false;

        //try to get roll direction from movement input, otherwise use facing direction
        Vector2 rollDir = moveInput.sqrMagnitude > 0.0001f ? moveInput.normalized : (Vector2)transform.up;

        // math stuff
        float desiredDeltaV = barrellRollDistanceWithUpgrades / Mathf.Max(0.01f, barrellRollDurationWithUpgrades);
        float impulse = rb.mass * desiredDeltaV;

        // add dash force
        rb.AddForce(rollDir * impulse, ForceMode2D.Impulse);

        float elapsed = 0f;
        float spinPerSecond = barrellRollSpinDegrees / Mathf.Max(0.01f, barrellRollDurationWithUpgrades);

        while (elapsed < barrellRollDurationWithUpgrades)
        {
            // spin the ship
            float newAngle = rb.rotation + spinPerSecond * Time.fixedDeltaTime;
            rb.MoveRotation(newAngle);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isBarrellRolling = false;

        // cooldown
        yield return new WaitForSeconds(barrellRollCooldown);
        canBarrellRoll = true;
    }

    private void TryBoost()
    {
        if (Input.GetKey(boostKey) && currentBoost > 0)
        {
            isBoosting = true;
            Vector2 dir = moveInput.normalized;
            float boostForceWithUpgrades =
                boostForce + upgradeState.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.BoostForce);
            rb.AddForce(dir * boostForceWithUpgrades, ForceMode2D.Force);
            currentBoost -= boostConsumptionRate;
            isRecharging = false;
        }
        else
        {
            isBoosting = false;
        }
    }
}