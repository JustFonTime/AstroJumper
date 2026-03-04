using System.Collections.Generic;
using UnityEngine;


// Manages a pool of EnemyProjectile instances for efficient reuse.
// different from PlayerProjectilePool since there was a lot of difficulty 
// with combining the one that the player uses and the enemy uses. 


public class EnemyProjectilePool : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private int initialPoolSize = 8;

    private readonly Queue<GameObject> available = new Queue<GameObject>();

    private void Awake()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError($"[EnemyProjectilePool] on {name}: projectilePrefab not assigned!", this);
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
            available.Enqueue(CreateBullet());
    }

    // Spawns a bullet from the pool at the given position, travelling in the given velocity
    public void Fire(Vector2 spawnPosition, Vector2 velocity)
    {
        GameObject bullet = available.Count > 0
            ? available.Dequeue()
            : CreateBullet(); // grow if empty

        bullet.transform.position = spawnPosition;
        bullet.transform.rotation = Quaternion.identity;
        bullet.SetActive(true);

        EnemyProjectile proj = bullet.GetComponent<EnemyProjectile>();
        if (proj != null)
            proj.Fire(velocity, this);
        else
            Debug.LogError("[EnemyProjectilePool] Prefab is missing EnemyProjectile component!", this);
    }

    // Called by EnemyProjectile when it hits something or expires to return itself to the pool
    public void Return(GameObject bullet)
    {
        bullet.SetActive(false);
        available.Enqueue(bullet);
    }

    private GameObject CreateBullet()
    {
        GameObject bullet = Instantiate(projectilePrefab);
        bullet.SetActive(false);
        return bullet;
    }
}