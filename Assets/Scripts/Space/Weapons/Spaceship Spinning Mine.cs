using UnityEngine;

public class SpaceshipSpinningMine : MonoBehaviour
{
    private Rigidbody2D rigidBody;

    [SerializeField] private float speed = 5;
    [SerializeField] private float maxSpeed = 20;
    [SerializeField] private float lifeTime = 10f;
    [SerializeField] private int damage = 10;
    private bool hasExploded = false;
    private Vector3 moveDirection;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        hasExploded = false;
        moveDirection = transform.up;
        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        MoveWithPhysics();
        ClampSpeed();
    }

    private void MoveWithPhysics()
    {
        rigidBody.AddForce(moveDirection * speed);
    }

    private void ClampSpeed()
    {
        if (rigidBody.linearVelocity.magnitude > maxSpeed)
        {
            rigidBody.linearVelocity = rigidBody.linearVelocity.normalized * maxSpeed;
        }
    }

    public void OnAnimationComplete()
    {
        print("Mine Exploding");
    }
}