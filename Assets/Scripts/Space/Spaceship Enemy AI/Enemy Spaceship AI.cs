using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
public class EnemySpaceshipAI : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private EnemyShipProfileSO shipProfile;

    private Rigidbody2D rb;

    [SerializeField] private bool isMovingTowardsPlayer = false;
    [SerializeField] private bool isBarrellRolling = false;
    [SerializeField] private bool strafeRight = true;
    [SerializeField] private float currentSpeedMultiplier = 1f;

    // cached input/aim
    private Vector2 moveInput;
    private float targetAngle;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (shipProfile == null)
        {
            Debug.LogError("No ship profile assigned");
            enabled = false;
            return;
        }

        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        if (shipProfile.useRandomBarrelRoll)
            StartCoroutine(RandomBarrellRoll());

        if (shipProfile.useRandomStrafeDirection)
            StartCoroutine(RandomSwitchStraffeDirection());

        strafeRight = shipProfile.startStrafeRight;
    }

    private void FixedUpdate()
    {
        UpdateSpeedMultiplyier();
        if (isBarrellRolling)
            return;


        StayInRangeOfPlayer();
        StraffeAroundPlayer();
        RotateToPlayer();
        ClampSpeed();
    }

    private void ClampSpeed()
    {
        float max = shipProfile.maxSpeed * (shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f);
        if (rb.linearVelocity.magnitude > max)
            rb.linearVelocity = rb.linearVelocity.normalized *
                                (shipProfile.maxSpeed * (shipProfile.useRandomSpeed ? currentSpeedMultiplier : 1f));
        ;
    }

    private void RotateToPlayer()
    {
        Vector2 direction = (player.transform.position - transform.position).normalized;
        targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + shipProfile.rotationOffset;

        float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, shipProfile.rotationLerp * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
    }

    private void MoveTowardsPlayer()
    {
        Vector2 direction = (player.transform.position - transform.position).normalized;
        float mVoveForce = this.shipProfile.moveForce;

        if (shipProfile.useRandomSpeed)
            mVoveForce *= currentSpeedMultiplier;


        rb.AddForce(direction * mVoveForce, ForceMode2D.Force);
    }

    private void MoveAwayFromPlayer()
    {
        Vector2 direction = (transform.position - player.transform.position).normalized;


        float mVoveForce = this.shipProfile.moveForce;

        if (shipProfile.useRandomSpeed)
        {
            mVoveForce *= currentSpeedMultiplier;
        }

        rb.AddForce(direction * mVoveForce, ForceMode2D.Force);
    }

    private void StayInRangeOfPlayer()
    {
        float distance = Vector2.Distance(transform.position, player.transform.position);
        if (shipProfile.minDistanceFromPlayer <= distance && distance <= shipProfile.maxDistanceFromPlayer)
        {
        }
        else
        {
        }

        if (distance < shipProfile.minDistanceFromPlayer)
        {
            isMovingTowardsPlayer = false;
        }
        else if (distance > shipProfile.maxDistanceFromPlayer)
        {
            isMovingTowardsPlayer = true;
        }


        if (isMovingTowardsPlayer)
        {
            MoveTowardsPlayer();
        }
        else
        {
            MoveAwayFromPlayer();
        }
    }

    private void StraffeAroundPlayer()
    {
        Vector2 toPlayer = (player.transform.position - transform.position).normalized;

        // perpendicular direction
        Vector2 strafeDirection = new Vector2(-toPlayer.y, toPlayer.x);


        if (!strafeRight)
            strafeDirection = -strafeDirection;

        float strafeForceModified = shipProfile.strafeForce;
        if (shipProfile.useRandomBarrelRoll)
            strafeForceModified *= currentSpeedMultiplier;

        rb.AddForce(strafeDirection * strafeForceModified, ForceMode2D.Force);
    }


    private IEnumerator RandomSwitchStraffeDirection()
    {
        while (shipProfile.useRandomStrafeDirection)
        {
            strafeRight = !strafeRight;
            float waitTime = UnityEngine.Random.Range(shipProfile.minStrafeInterval, shipProfile.maxStrafeInterval);
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
        while (shipProfile.useRandomBarrelRoll)
        {
            float waitTime = UnityEngine.Random.Range(shipProfile.barrelRollMinTime, shipProfile.barrelRollMaxTime);
            yield return new WaitForSeconds(waitTime);
            StartCoroutine(BarrellRolly());
        }
    }

    private IEnumerator BarrellRolly()
    {
        isBarrellRolling = true;
        //use random direction for roll
        Vector2 rollDir = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)).normalized;
        // math stuff
        float desiredDeltaV = shipProfile.barrelRollDistance / Mathf.Max(0.01f, shipProfile.barrelRollDuration);
        float impulse = rb.mass * desiredDeltaV;

        // add dash force
        rb.AddForce(rollDir * impulse, ForceMode2D.Impulse);

        float elapsed = 0f;
        float spinPerSecond = shipProfile.barrelRollSpinDegrees / Mathf.Max(0.01f, shipProfile.barrelRollDuration);

        while (elapsed < shipProfile.barrelRollDuration)
        {
            // spin the ship
            float newAngle = rb.rotation + spinPerSecond * Time.fixedDeltaTime;
            rb.MoveRotation(newAngle);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isBarrellRolling = false;
    }
}