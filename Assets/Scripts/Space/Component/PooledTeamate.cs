using UnityEngine;
using UnityEngine.Pool;

public class PooledTeamate : MonoBehaviour
{
    private IObjectPool<GameObject> pool;
    private FriendlySpaceshipSpawner spawner;

    public void Init(FriendlySpaceshipSpawner spawnerRef, IObjectPool<GameObject> poolRef)
    {
        spawner = spawnerRef;
        pool = poolRef;
    }

    public void Despawn()
    {
        // tell spawner for wave counting
        spawner?.NotifyTeamateGone();

        if (pool != null)
            pool.Release(gameObject);
        else
            Destroy(gameObject);
    }
}