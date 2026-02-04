using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class SpaceshipLaser : MonoBehaviour
{
    private Rigidbody2D rigidBody;
    [SerializeField] private float speed = 100f;
    [SerializeField] private float maxSpeed = 100f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private int damage = 10;
    [SerializeField] private bool destroyOnHit = true;

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
        // if we hit a child collider, look up the hierarchy
        var damageable = other.GetComponentInParent<ISpaceDamagable>();
        if (damageable == null) return;
        
        damageable.TakeDamage(damage);

        if (destroyOnHit)
            Destroy(gameObject);
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
}