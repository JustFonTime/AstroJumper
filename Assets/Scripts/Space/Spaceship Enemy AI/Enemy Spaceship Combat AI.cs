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
    [Header("Refs")] [SerializeField] private Transform firePoint;
    [SerializeField] private EnemyShipProfileSO shipProfile;
    [SerializeField] private GameObject laserPrefab;

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

            var t = targeting != null ? targeting.CurrentTarget : null;
            if (t == null) continue;

            float dist = Vector2.Distance(transform.position, t.transform.position);
            if (dist > shipProfile.combatRange) continue;

            UpdateAimDirection();

            Vector3 shotDirection = ApplySpread(AimDirectionWorld);
            Quaternion shotRotation = Quaternion.FromToRotation(Vector3.up, shotDirection);
            GameObject proj = Instantiate(laserPrefab, firePoint.position, shotRotation);

            if (proj.TryGetComponent<SpaceshipLaser>(out var laser))
                laser.teamId = team != null ? team.TeamId : 0;
        }
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
