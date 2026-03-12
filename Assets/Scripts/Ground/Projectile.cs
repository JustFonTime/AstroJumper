using UnityEngine;

public class Projectile : MonoBehaviour
{
    HitBox hitBoxInfo;
    private float speed = 1f;
    private int direction = 1; // 1 for right, -1 for left
    private float yValue = 0f;
    private Vector3 desiredTransform;
    private bool isDead = false; // prevent LateUpdate from moving after death

    [SerializeField] private LayerMask wallLayers;

    void OnEnable()
    {
        isDead = false;
        desiredTransform = transform.position; // initialize to current position
    }

    void Update()
    {
        if (isDead) return;

        float moveDistance = speed * Time.deltaTime;
        Vector2 rayOrigin = new Vector2(transform.position.x, transform.position.y);
        Vector2 rayDir = direction == 1 ? Vector2.right : Vector2.left;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDir, moveDistance + 0.5f, wallLayers);

        if (hit.collider != null)
        {
            Debug.Log("Projectile raycast hit: " + hit.collider.name);
            isDead = true;
            HitBox hitBox = GetComponentInChildren<HitBox>();
            if (hitBox != null)
                hitBox.ForceDestroy();
            return;
        }

        desiredTransform = new Vector3(transform.position.x + speed * direction * Time.deltaTime, transform.position.y, transform.position.z);
        transform.position = new Vector3(transform.position.x, yValue, transform.position.z);
    }

    void LateUpdate()
    {
        if (isDead) return;
        transform.position = desiredTransform;
    }

    public void SetWallLayers(LayerMask layers)
    {
        wallLayers = layers;
    }

    public void SetDirection(int dir) { direction = dir; }
    public void SetYValue(float y) { yValue = y; }
    public void SetSpeed(float newSpeed) { speed = newSpeed; }
}
