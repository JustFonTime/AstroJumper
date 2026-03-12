using UnityEngine;
using UnityEngine.Pool;

[DisallowMultipleComponent]
public class PooledFleetShip : MonoBehaviour
{
    private IObjectPool<GameObject> pool;
    private FleetSpawner spawner;

    public void Init(FleetSpawner spawnerRef, IObjectPool<GameObject> poolRef)
    {
        spawner = spawnerRef;
        pool = poolRef;
    }

    public void Despawn()
    {
        int teamId = 0;
        TeamAgent teamAgent = GetComponent<TeamAgent>();
        if (teamAgent != null)
            teamId = teamAgent.TeamId;

        spawner?.NotifyShipGone(teamId);

        if (pool != null)
            pool.Release(gameObject);
        else
            Destroy(gameObject);
    }
}
