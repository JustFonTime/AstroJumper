using System.Collections;
using UnityEngine;

[RequireComponent(typeof(TeamAgent))]
[RequireComponent(typeof(TargetingComponent))]
public class EnemySpaceshipCombatAI : MonoBehaviour
{
    private TargetingComponent targeting; //get the target from here
    private TeamAgent team; //get our team from here 

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
            // keep the prefab distacne
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

    // Pool-safe
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

        // Aim from the firePoint position, not the ship center, so it lines up better with the projectiles
        Vector3 fromPos = firePoint != null ? firePoint.position : transform.position;

        AimDirectionWorld = (targetPos - fromPos).normalized;
    }

    private void RotateSpawnPointAroundCenter()
    {
        if (firePoint == null) return;

        Vector2 aimDir = AimDirectionWorld;
        if (aimDir.sqrMagnitude < 0.0001f)
            aimDir = transform.up;

        // Put the spawn point on a circle around the ship, toward aim direction
        Vector2 localAimDir = transform.InverseTransformDirection(aimDir).normalized;
        // Use LOCAL position so it stays attached to the ship nicely

        firePoint.localPosition = (Vector3)(localAimDir * spawnRadius);

        // Rotate the spawn point to face the aim direction, so projectiles shoot toward the mouse even if the ship is turning
        firePoint.up = aimDir;
    }


    private IEnumerator FireLoop()
    {
        // desync so not all enemies fire same frame
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

            // Ensure firePoint is aimed properly right before firing
            UpdateAimDirection();


            GameObject proj = Instantiate(laserPrefab, firePoint.position, firePoint.rotation);

            // Team tag the projectile if it supports it
            if (proj.TryGetComponent<SpaceshipLaser>(out var laser))
                laser.teamId = team != null ? team.TeamId : 0;
        }
    }


    private void DrawDebug()
    {
        if (firePoint != null)
        {
            Debug.DrawLine(transform.position, firePoint.position, firePointLineColor, 0f, false);
        }
    }
}