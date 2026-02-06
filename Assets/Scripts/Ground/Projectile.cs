using UnityEngine;

public class Projectile : MonoBehaviour
{
    HitBox hitBoxInfo;
    private float speed = 1f;
    private int direction = 1; // 1 for right, -1 for left

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HitBox hitBoxInfo = GetComponent<HitBox>();
    }

    // Update is called once per frame
    void Update()
    {
        
        transform.Translate(Vector3.right * Time.deltaTime * 1f * direction); 
    }

    public void SetDirection(int dir)
    {
        direction = dir;
    }
}
