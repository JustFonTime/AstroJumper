using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemySpaceshipAI : MonoBehaviour
{
    [Header("Refs")] [SerializeField] private GameObject player;
    [SerializeField] private EnemyShipProfileSO shipProfile;

    private Rigidbody2D rb;

    [Header("Runtime")] [SerializeField] private bool isBarrellRolling = false;
    [SerializeField] private bool strafeRight = true;
    [SerializeField] private float currentSpeedMultiplier = 1f;

    private float targetAngle;

    // coroutines (so we can stop/restart when pooling)
    private Coroutine barrelCo;
    private Coroutine strafeCo;

    [Header("Separation")] [SerializeField]
    private float separationRadius = 2.5f;

    [SerializeField] private float separationForce = 35f;
    [SerializeField] private LayerMask enemyMask;

    private readonly Collider2D[] sepHits = new Collider2D[16];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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

        player = playerTarget != null ? playerTarget : GameObject.FindGameObjectWithTag("Player");


        isBarrellRolling = false;
        currentSpeedMultiplier = 1f;


        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;


        if (barrelCo != null) StopCoroutine(barrelCo);
        if (strafeCo != null) StopCoroutine(strafeCo);
        barrelCo = null;
        strafeCo = null;



        strafeRight = shipProfile.startStrafeRight ^ (Random.value > 0.5f);


        if (shipProfile.useRandomStrafeDirection)
            strafeCo = StartCoroutine(RandomSwitchStraffeDirection());

        if (shipProfile.useRandomBarrelRoll)
            barrelCo = StartCoroutine(RandomBarrellRoll());
    }

    private void OnDisable()
    {
        if (barrelCo != null) StopCoroutine(barrelCo);
        if (strafeCo != null) StopCoroutine(strafeCo);
        barrelCo = null;
        strafeCo = null;
    }

    private void FixedUpdate()
    {
        if (!enabled || player == null) return;

        UpdateSpeedMultiplyier();
        if (isBarrellRolling) return;

        StayInRangeOfPlayer();
        StraffeAroundPlayer();
        ApplySeparation();
        RotateToPlayer();
        ClampSpeed();
    }

    private void ClampSpeed()
    {
        float max = shipProfile.maxSpeed * (shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f);

        if (rb.linearVelocity.sqrMagnitude > max * max)
            rb.linearVelocity = rb.linearVelocity.normalized * max;
    }

    private void RotateToPlayer()
    {
        Vector2 direction = ((Vector2)player.transform.position - rb.position).normalized;
        targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + shipProfile.rotationOffset;

        float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, shipProfile.rotationLerp * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
    }

    private void MoveTowardsPlayer()
    {
        Vector2 direction = ((Vector2)player.transform.position - rb.position).normalized;
        float force = shipProfile.moveForce * (shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f);
        rb.AddForce(direction * force, ForceMode2D.Force);
    }

    private void MoveAwayFromPlayer()
    {
        Vector2 direction = (rb.position - (Vector2)player.transform.position).normalized;
        float force = shipProfile.moveForce * (shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f);
        rb.AddForce(direction * force, ForceMode2D.Force);
    }


    private void StayInRangeOfPlayer()
    {
        float distance = Vector2.Distance(rb.position, player.transform.position);

        if (distance < shipProfile.minDistanceFromPlayer)
        {
            MoveAwayFromPlayer();
            return;
        }

        if (distance > shipProfile.maxDistanceFromPlayer)
        {
            MoveTowardsPlayer();
            return;
        }

        float target = (shipProfile.minDistanceFromPlayer + shipProfile.maxDistanceFromPlayer) * 0.5f;
        float error = distance - target;

        Vector2 toPlayer = ((Vector2)player.transform.position - rb.position).normalized;

        float k = shipProfile.moveForce * 0.25f;
        rb.AddForce(toPlayer * (error * k), ForceMode2D.Force);
    }

    private void StraffeAroundPlayer()
    {
        Vector2 toPlayer = ((Vector2)player.transform.position - rb.position).normalized;

        // perpendicular direction
        Vector2 strafeDir = new Vector2(-toPlayer.y, toPlayer.x);
        if (!strafeRight) strafeDir = -strafeDir;

        float force = shipProfile.strafeForce;

        if (shipProfile.useRandomSpeed)
            force *= currentSpeedMultiplier;

        rb.AddForce(strafeDir * force, ForceMode2D.Force);
    }

    private void ApplySeparation()
    {
        int count = Physics2D.OverlapCircleNonAlloc(rb.position, separationRadius, sepHits, enemyMask);
        Vector2 repel = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            var col = sepHits[i];
            if (!col) continue;

            var otherRb = col.attachedRigidbody;
            if (!otherRb || otherRb == rb) continue;

            Vector2 away = rb.position - (Vector2)col.transform.position;
            float d = away.magnitude;
            if (d < 0.001f) continue;

            repel += away / (d * d); // stronger when closer
        }

        if (repel.sqrMagnitude > 0.0001f)
            rb.AddForce(repel.normalized * separationForce, ForceMode2D.Force);
    }

    private IEnumerator RandomSwitchStraffeDirection()
    {
        // desync enemies so they don't flip at the same time
        yield return new WaitForSeconds(Random.Range(0f, 1.5f));

        while (shipProfile.useRandomStrafeDirection)
        {
            strafeRight = !strafeRight;
            float waitTime = Random.Range(shipProfile.minStrafeInterval, shipProfile.maxStrafeInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void UpdateSpeedMultiplyier()
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
        // desync so not all roll together
        yield return new WaitForSeconds(Random.Range(0f, 1.5f));

        while (shipProfile.useRandomBarrelRoll)
        {
            float waitTime = Random.Range(shipProfile.barrelRollMinTime, shipProfile.barrelRollMaxTime);
            yield return new WaitForSeconds(waitTime);

            // avoid stacking rolls
            if (!isBarrellRolling)
                yield return StartCoroutine(BarrellRolly());
        }
    }

    private IEnumerator BarrellRolly()
    {
        isBarrellRolling = true;

        Vector2 rollDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;

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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, separationRadius);
    }
#endif
}