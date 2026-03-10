using System.Collections;
using UnityEngine;

[RequireComponent(typeof(TeamAgent))]
[RequireComponent(typeof(TargetingComponent))]
public class EnemySpaceshipCombatAI : MonoBehaviour
{
    private TargetingComponent targeting;
    private TeamAgent team;

    private Coroutine fireCo;
    private float spawnRadius = 1f;
    private readonly RaycastHit2D[] lineOfFireHits = new RaycastHit2D[24];

    [Header("Refs")] [SerializeField] private Transform firePoint;
    [SerializeField] private EnemyShipProfileSO shipProfile;
    [SerializeField] private GameObject laserPrefab;

    [Header("Fire Safety")]
    [SerializeField] private bool avoidFriendlyFire = true;
    [SerializeField] private bool retargetIfUnsafe = true;
    [SerializeField] private LayerMask lineOfFireMask = ~0;
    [SerializeField] private float fireSafetyPadding = 0.5f;
    [SerializeField] private float baseSafetyConeAngle = 3f;
    [SerializeField] private bool drawSafetyRays = false;
    [SerializeField] private Color safeRayColor = new Color(0.2f, 1f, 0.2f, 0.75f);
    [SerializeField] private Color blockedRayColor = new Color(1f, 0.7f, 0.15f, 0.95f);

    [Header("Debug")] [SerializeField] private Color firePointLineColor = Color.red;

    public Vector3 AimDirectionWorld { get; private set; }

    private void Awake()
    {
        targeting = GetComponent<TargetingComponent>();
        team = GetComponent<TeamAgent>();

        if (firePoint != null)
        {
            spawnRadius = firePoint.localPosition.magnitude;
            if (spawnRadius < 0.001f) spawnRadius = 1f;
        }
    }

    private void OnEnable()
    {
        ResetForSpawn();
    }

    private void OnDisable()
    {
        if (fireCo != null) StopCoroutine(fireCo);
        fireCo = null;
    }

    public void ResetForSpawn()
    {
        if (shipProfile == null || laserPrefab == null || firePoint == null)
            return;

        if (fireCo != null) StopCoroutine(fireCo);
        fireCo = StartCoroutine(FireLoop());
    }

    private void FixedUpdate()
    {
        UpdateAimDirection();
        RotateSpawnPointAroundCenter();
        DrawDebug();
    }

    private void UpdateAimDirection()
    {
        if (!targeting.CurrentTarget) targeting.RetargetNow();

        if (!targeting.CurrentTarget)
        {
            AimDirectionWorld = transform.up;
            return;
        }

        Vector3 targetPos = targeting.CurrentTarget.transform.position;
        Vector3 fromPos = firePoint != null ? firePoint.position : transform.position;
        AimDirectionWorld = (targetPos - fromPos).normalized;
    }

    private void RotateSpawnPointAroundCenter()
    {
        if (firePoint == null) return;

        Vector2 aimDir = AimDirectionWorld;
        if (aimDir.sqrMagnitude < 0.0001f)
            aimDir = transform.up;

        Vector2 localAimDir = transform.InverseTransformDirection(aimDir).normalized;
        firePoint.localPosition = (Vector3)(localAimDir * spawnRadius);
        firePoint.up = aimDir;
    }

    private IEnumerator FireLoop()
    {
        yield return new WaitForSeconds(Random.Range(0f, 0.4f));

        while (true)
        {
            if (shipProfile == null)
                yield break;

            float fireRate = Random.Range(shipProfile.minFireRate, shipProfile.maxFireRate);
            yield return new WaitForSeconds(fireRate);

            TeamAgent target = targeting != null ? targeting.CurrentTarget : null;
            if (target == null)
            {
                targeting?.RetargetNow();
                continue;
            }

            if (!TryGetShotBaseDirection(target, out Vector2 baseDirection, out float targetDistance))
                continue;

            if (avoidFriendlyFire && !HasSafeFireLane(baseDirection, targetDistance, target))
            {
                if (retargetIfUnsafe && targeting != null)
                {
                    targeting.RetargetNow();
                    target = targeting.CurrentTarget;

                    if (target == null)
                        continue;

                    if (!TryGetShotBaseDirection(target, out baseDirection, out targetDistance))
                        continue;

                    if (!HasSafeFireLane(baseDirection, targetDistance, target))
                        continue;
                }
                else
                {
                    continue;
                }
            }

            Vector3 shotDirection = ApplySpread(baseDirection);
            Quaternion shotRotation = Quaternion.FromToRotation(Vector3.up, shotDirection);
            GameObject proj = Instantiate(laserPrefab, firePoint.position, shotRotation);

            if (proj.TryGetComponent<SpaceshipLaser>(out var laser))
                laser.teamId = team != null ? team.TeamId : 0;
        }
    }

    private bool TryGetShotBaseDirection(TeamAgent target, out Vector2 direction, out float targetDistance)
    {
        direction = transform.up;
        targetDistance = 0f;

        if (target == null || firePoint == null)
            return false;

        Vector2 origin = firePoint.position;
        Vector2 toTarget = (Vector2)target.transform.position - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
            return false;

        if (shipProfile != null && distance > shipProfile.combatRange)
            return false;

        direction = toTarget / distance;
        targetDistance = distance;
        return true;
    }

    private bool HasSafeFireLane(Vector2 baseDirection, float targetDistance, TeamAgent target)
    {
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        float maxDistance = targetDistance + Mathf.Max(0f, fireSafetyPadding);

        float spreadAngle = 0f;
        if (shipProfile != null && shipProfile.useWeaponSpread)
            spreadAngle = Mathf.Max(Mathf.Abs(shipProfile.minSpreadAngle), Mathf.Abs(shipProfile.maxSpreadAngle));

        float safetyCone = Mathf.Max(0f, baseSafetyConeAngle) + spreadAngle;

        if (!IsDirectionSafe(origin, baseDirection, maxDistance, target))
            return false;

        if (safetyCone <= 0.01f)
            return true;

        Vector2 leftEdge = Rotate(baseDirection, safetyCone);
        Vector2 rightEdge = Rotate(baseDirection, -safetyCone);
        if (!IsDirectionSafe(origin, leftEdge, maxDistance, target))
            return false;
        if (!IsDirectionSafe(origin, rightEdge, maxDistance, target))
            return false;

        if (safetyCone > 4f)
        {
            Vector2 leftMid = Rotate(baseDirection, safetyCone * 0.5f);
            Vector2 rightMid = Rotate(baseDirection, -safetyCone * 0.5f);
            if (!IsDirectionSafe(origin, leftMid, maxDistance, target))
                return false;
            if (!IsDirectionSafe(origin, rightMid, maxDistance, target))
                return false;
        }

        return true;
    }

    private bool IsDirectionSafe(Vector2 origin, Vector2 direction, float maxDistance, TeamAgent target)
    {
        int hitCount = Physics2D.RaycastNonAlloc(origin, direction, lineOfFireHits, maxDistance, lineOfFireMask);

        bool blockedByFriendly = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = lineOfFireHits[i];
            if (hit.collider == null)
                continue;

            TeamAgent hitTeam = hit.collider.GetComponentInParent<TeamAgent>();
            if (hitTeam == null)
                continue;
            if (hitTeam == team)
                continue;

            if (team != null && hitTeam.TeamId == team.TeamId)
            {
                blockedByFriendly = true;
                break;
            }

            if (target != null && hitTeam == target)
                break;

            if (team == null || TeamRegistry.IsHostile(team.TeamId, hitTeam.TeamId))
                break;
        }

        if (drawSafetyRays)
        {
            Color c = blockedByFriendly ? blockedRayColor : safeRayColor;
            Debug.DrawRay(origin, direction * maxDistance, c, 0.1f, false);
        }

        return !blockedByFriendly;
    }

    private static Vector2 Rotate(Vector2 vector, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }

    private Vector3 ApplySpread(Vector3 baseDirection)
    {
        if (shipProfile == null || !shipProfile.useWeaponSpread)
            return baseDirection;

        float angle = Random.Range(shipProfile.minSpreadAngle, shipProfile.maxSpreadAngle);
        return Quaternion.Euler(0f, 0f, angle) * baseDirection;
    }

    private void DrawDebug()
    {
        if (firePoint != null)
            Debug.DrawLine(transform.position, firePoint.position, firePointLineColor, 0f, false);
    }
}

