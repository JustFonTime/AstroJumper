using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpaceshipLaser : MonoBehaviour
{
    private Rigidbody2D rigidBody;

    [Header("Movement")]
    [SerializeField] private float speed = 100f;
    [SerializeField] private float maxSpeed = 100f;

    [Header("Lifetime")]
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private bool destroyOnHit = true;

    [Header("Combat")]
    [SerializeField] private int damage = 10;
    [SerializeField] public int teamId = -1; // must be set by the spawner

    [Header("VFX")]
    [Tooltip("Particle prefab spawned when we successfully damage something.")]
    [SerializeField] private ParticleSystem hitVfxPrefab;

    [Tooltip("Extra seconds added to the particle's duration before destroying it.")]
    [SerializeField] private float vfxDestroyPadding = 0.25f;

    //prevent double triggers
    private bool hasHitSomething = false;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHitSomething) return;

        // dont desotry if on same team
        if (teamId != -1)
        {
            var otherTeamAgent = other.GetComponentInParent<TeamAgent>();
            if (otherTeamAgent != null && otherTeamAgent.TeamId == teamId)
            {
                return; // friendly -> ignore
            }
        }

        //find smth damageable
        var damageable = other.GetComponentInParent<ISpaceDamagable>();
        if (damageable == null)
        {
            // If you WANT lasers to die on walls/asteroids/etc that are not damageable,
            // you can destroy here. Otherwise, just ignore.
            // if (destroyOnHit) Destroy(gameObject);
            return;
        }

        // not on our team and can be damaged -> hit it!
        damageable.TakeDamage(damage);

        // spawn vfx
        SpawnHitVfx();

        // destroy self if we are supposed to
        if (destroyOnHit)
        {
            hasHitSomething = true;
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        MoveWithPhysics();
        ClampSpeed();
    }

    private void ClampSpeed()
    {
        if (rigidBody.linearVelocity.magnitude > maxSpeed)
        {
            rigidBody.linearVelocity = rigidBody.linearVelocity.normalized * maxSpeed;
        }
    }

    private void MoveWithPhysics()
    {
        rigidBody.AddForce(transform.up * speed);
    }

    private void SpawnHitVfx()
    {
        if (hitVfxPrefab == null) return;

        //Spawn at laser position, not rotated (since most hit effects are just explosion bursts that look fine without rotation)
        ParticleSystem vfx = Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);

        // If you want it to align with the bullet direction:
        // ParticleSystem vfx = Instantiate(hitVfxPrefab, transform.position, transform.rotation);

        // Set lifetime
        float lifetime = GetParticleSystemTotalLifetime(vfx);
        Destroy(vfx.gameObject, lifetime + vfxDestroyPadding);
    }

    private static float GetParticleSystemTotalLifetime(ParticleSystem ps)
    {
        // Total time = duration + startLifetime (max) + max child lifetimes
        var main = ps.main;

        float duration = main.duration;

        // startLifetime can be constant or range
        float startLifetimeMax = main.startLifetime.mode switch
        {
            ParticleSystemCurveMode.TwoConstants => main.startLifetime.constantMax,
            ParticleSystemCurveMode.Constant => main.startLifetime.constant,
            _ => main.startLifetime.constantMax // decent fallback
        };

        // Include child particle systems too (common for hit effects)
        float childMax = 0f;
        var children = ps.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == ps) continue;
            var cm = children[i].main;

            float d = cm.duration;
            float sl = cm.startLifetime.mode switch
            {
                ParticleSystemCurveMode.TwoConstants => cm.startLifetime.constantMax,
                ParticleSystemCurveMode.Constant => cm.startLifetime.constant,
                _ => cm.startLifetime.constantMax
            };

            childMax = Mathf.Max(childMax, d + sl);
        }

        return Mathf.Max(duration + startLifetimeMax, childMax);
    }
}