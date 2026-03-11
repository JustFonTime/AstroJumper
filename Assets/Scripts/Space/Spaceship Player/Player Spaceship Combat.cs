using System.Collections;
using UnityEngine;

public class PlayerSpaceshipCombat : MonoBehaviour
{
    [Header("Refs")] [SerializeField] private PlayerUpgradeState upgrades;
    [SerializeField] private SpaceAttackInfo[] attacks;
    [SerializeField] private Camera cam;

    [Tooltip("Default spawn point that gets rotated around the ship to sit between ship + aim direction.")]
    [SerializeField]
    private Transform projectileSpawn;

    [Header("Debug")] [SerializeField] private bool drawDebug = false;
    [SerializeField] private float debugLineLen = 3f;

    public Vector3 AimDirectionWorld { get; private set; }

    private float spawnRadius = 1f;

    private void Awake()
    {
        if (!cam) cam = Camera.main;

        if (projectileSpawn != null)
        {
            spawnRadius = projectileSpawn.localPosition.magnitude;
            if (spawnRadius < 0.001f) spawnRadius = 1f;
        }
    }

    private void Update()
    {
        UpdateAimDirection();
        RotateSpawnPointAroundCenter();

        if (Input.GetMouseButton(0))
        {
            if (attacks != null && attacks.Length > 0)
                TryAttack(attacks[0]);
        }

        if (Input.GetMouseButton(1))
        {
            if (attacks != null && attacks.Length > 1)
                TryAttack(attacks[1]);
        }

        if (drawDebug)
        {
            Debug.DrawLine(transform.position, transform.position + AimDirectionWorld * debugLineLen, Color.yellow);
            if (projectileSpawn != null)
                Debug.DrawLine(transform.position, projectileSpawn.position, Color.cyan);
        }
    }

    private void UpdateAimDirection()
    {
        if (!cam) cam = Camera.main;

        if (!cam)
        {
            AimDirectionWorld = transform.up;
            return;
        }

        Vector3 mouse = Input.mousePosition;
        float zDist = Mathf.Abs(transform.position.z - cam.transform.position.z);
        mouse.z = zDist;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouse);
        Vector2 toMouse = (Vector2)(mouseWorld - transform.position);

        AimDirectionWorld = (toMouse.sqrMagnitude < 0.0001f)
            ? (Vector3)transform.up
            : (Vector3)toMouse.normalized;
    }

    private void RotateSpawnPointAroundCenter()
    {
        if (projectileSpawn == null) return;

        Vector2 aimDir = AimDirectionWorld;
        if (aimDir.sqrMagnitude < 0.0001f)
            aimDir = transform.up;

        Vector2 localAimDir = transform.InverseTransformDirection(aimDir).normalized;
        projectileSpawn.localPosition = (Vector3)(localAimDir * spawnRadius);
        projectileSpawn.up = aimDir;
    }

    private void TryAttack(SpaceAttackInfo attack)
    {
        if (attack == null) return;
        if (!attack.canFire) return;
        if (attack.projectile == null) return;

        Transform firePoint = attack.firePoint != null ? attack.firePoint : projectileSpawn;
        if (firePoint == null) return;

        Vector3 shotDirection = ApplySpread(AimDirectionWorld, attack.useSpread, attack.minSpreadAngle, attack.maxSpreadAngle);
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, shotDirection);
        Instantiate(attack.projectile, firePoint.position, rot);

        StartCoroutine(AttackCooldown(attack));
    }

    private IEnumerator AttackCooldown(SpaceAttackInfo attack)
    {
        attack.canFire = false;

        float upgradedRate = attack.fireRate;
        if (upgrades != null)
            upgradedRate -= upgrades.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.FireRate);

        upgradedRate = Mathf.Max(0.02f, upgradedRate);

        yield return new WaitForSeconds(upgradedRate);
        attack.canFire = true;
    }

    private static Vector3 ApplySpread(Vector3 baseDirection, bool useSpread, float minSpreadAngle, float maxSpreadAngle)
    {
        if (!useSpread)
            return baseDirection;

        float angle = Random.Range(minSpreadAngle, maxSpreadAngle);
        return Quaternion.Euler(0f, 0f, angle) * baseDirection;
    }
}

