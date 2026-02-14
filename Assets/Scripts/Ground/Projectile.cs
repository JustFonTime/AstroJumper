using UnityEngine;

public class Projectile : MonoBehaviour
{
    HitBox hitBoxInfo;
    private float speed = 1f;
    private int direction = 1; // 1 for right, -1 for left
    private float yValue = 0f;

    private Vector3 desiredTransform;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        desiredTransform = new Vector3(transform.position.x + speed * direction * Time.deltaTime, transform.position.y, transform.position.z); 
        transform.position = new Vector3(transform.position.x, yValue, transform.position.z);
    }

    void LateUpdate()
    {
        transform.position = desiredTransform;
    }

    public void SetDirection(int dir)
    {
        direction = dir;
    }

    public void SetYValue(float y)
    {
        yValue = y;
    }
}
