using UnityEngine;
using UnityEngine.Pool;

public class PooledEnemy : MonoBehaviour
{
    private IObjectPool<GameObject> pool;
    private EnemySpaceshipSpawner spawner;

    public void Init(EnemySpaceshipSpawner spawnerRef, IObjectPool<GameObject> poolRef)
    {
        spawner = spawnerRef;
        pool = poolRef;
    }

    public void Despawn()
    {
        // tell spawner for wave counting
        spawner?.NotifyEnemyGone();

        if (pool != null)
            pool.Release(gameObject);
        else
            Destroy(gameObject);
    }
}