using UnityEngine;

public class EnemySensors : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask playerMask;

    [Header("Wall Check")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckDistance = 0.2f;

    [Header("Ledge Check")]
    [SerializeField] private Transform ledgeCheck;
    [SerializeField] private float ledgeCheckDistance = 0.4f;

    [Header("Player Detect")]
    [SerializeField] private Transform playerDetectOrigin;
    [SerializeField] private float detectRadius = 4f;
    // Blocks line of sight (typically Ground + Wall layers combined into one mask)
    [SerializeField] private LayerMask sightBlockMask;

    public bool WallAhead()
    {
        // points where the enemy faces
        RaycastHit2D hit = Physics2D.Raycast(wallCheck.position, transform.right, wallCheckDistance, groundMask);
        return hit.collider != null;
    }

    public bool NoGroundAhead() 
    {
        // ledgeCheck is moved to the front by Rotate()
        RaycastHit2D hit = Physics2D.Raycast(ledgeCheck.position, Vector2.down, ledgeCheckDistance, groundMask);
        return hit.collider == null;
    }


    public Transform DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(playerDetectOrigin.position, detectRadius, playerMask);
        if (!hit) return null;


        // Is the player in line of sight? (not blocked by walls/ground)
        Transform player = hit.transform;
        Vector2 origin = playerDetectOrigin.position;
        Vector2 target = player.position;
        float dist = Vector2.Distance(origin, target); 


        // Linecast towards the player and if it hits a wall/ground before them then its blocked
        RaycastHit2D sightCheck = Physics2D.Raycast(origin, (target - origin).normalized, dist, sightBlockMask);
        if (sightCheck.collider != null) return null;

        return player;

    }

    private void OnDrawGizmos()
    {
        if (!wallCheck || !ledgeCheck) return;

        // Red ray for wall check
        Gizmos.color = Color.red;
        Gizmos.DrawRay(wallCheck.position, transform.right * wallCheckDistance);

        // Blue ray for ledge check
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(ledgeCheck.position, Vector2.down * ledgeCheckDistance);

        // Yellow wire sphere for detection radius
        if (playerDetectOrigin)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerDetectOrigin.position, detectRadius);
        }
    }
}
