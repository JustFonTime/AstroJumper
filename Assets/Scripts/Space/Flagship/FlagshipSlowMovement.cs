using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FlagshipSlowMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float turnSpeedDegPerSec = 10f;
    [SerializeField] private bool moveTowardWaypoint = true;
    [SerializeField] private Transform waypoint;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (!moveTowardWaypoint || waypoint == null || rb == null) return;

        Vector2 toTarget = (Vector2)waypoint.position - rb.position;
        if (toTarget.sqrMagnitude < 0.01f)
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
}
