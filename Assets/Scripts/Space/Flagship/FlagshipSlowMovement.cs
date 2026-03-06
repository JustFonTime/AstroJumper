using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FlagshipSlowMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float turnSpeedDegPerSec = 10f;
    [SerializeField] private float roamRadius = 20f;
    [SerializeField] private float arriveDistance = 2f;
    [SerializeField] private float minRetargetDelay = 1.5f;
    [SerializeField] private float maxRetargetDelay = 4f;

    private Rigidbody2D rb;
    private Vector2 anchorPosition;
    private Vector2 currentTarget;
    private float retargetTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anchorPosition = rb != null ? rb.position : (Vector2)transform.position;
        PickNewTarget();
    }

    private void OnEnable()
    {
        if (rb != null)
            anchorPosition = rb.position;

        PickNewTarget();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        rb.angularVelocity = 0f;

        retargetTimer -= Time.fixedDeltaTime;
        Vector2 toTarget = currentTarget - rb.position;

        if (toTarget.magnitude <= arriveDistance || retargetTimer <= 0f)
        {
            PickNewTarget();
            toTarget = currentTarget - rb.position;
        }

        if (toTarget.sqrMagnitude <= 0.01f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desiredDir = toTarget.normalized;
        float desiredAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg - 90f;
        float newAngle = Mathf.MoveTowardsAngle(rb.rotation, desiredAngle, turnSpeedDegPerSec * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
        rb.linearVelocity = (Vector2)transform.up * moveSpeed;
    }

    private void PickNewTarget()
    {
        currentTarget = anchorPosition + Random.insideUnitCircle * roamRadius;
        retargetTimer = Random.Range(minRetargetDelay, maxRetargetDelay);
    }
}


