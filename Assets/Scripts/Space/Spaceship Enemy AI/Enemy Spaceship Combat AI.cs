using System.Collections;
using UnityEngine;

[RequireComponent(typeof(TeamAgent))]
[RequireComponent(typeof(TargetingComponent))]
public class EnemySpaceshipCombatAI : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private EnemyShipProfileSO shipProfile;
    [SerializeField] private GameObject laserPrefab;

    private TargetingComponent targeting;
    private Coroutine fireCo;
    private bool canFire;

    private void Awake()
    {
        targeting = GetComponent<TargetingComponent>();
    }

    private void OnEnable()
    {
        StartFiringLoop();
    }

    private void OnDisable()
    {
        StopFiringLoop();
        canFire = false;
    }

    // Pool-safe
    public void ResetForSpawn()
    {
        StopFiringLoop();
        canFire = false;
        StartFiringLoop();
    }

    private void StartFiringLoop()
    {
        if (shipProfile == null || laserPrefab == null || firePoint == null) return;
        if (fireCo != null) StopCoroutine(fireCo);
        fireCo = StartCoroutine(FireLoop());
    }

    private void StopFiringLoop()
    {
        if (fireCo != null) StopCoroutine(fireCo);
        fireCo = null;
    }

    private IEnumerator FireLoop()
    {
        // Desync so all enemies don't shoot in the same frame
        yield return new WaitForSeconds(Random.Range(0f, 0.4f));

        while (true)
        {
            float fireRate = Random.Range(shipProfile.minFireRate, shipProfile.maxFireRate);
            yield return new WaitForSeconds(fireRate);

            var t = targeting != null ? targeting.CurrentTarget : null;
            if (t == null) continue;

            float dist = Vector2.Distance(transform.position, t.transform.position);
            if (dist <= shipProfile.combatRange)
            {
                Instantiate(laserPrefab, firePoint.position, firePoint.rotation);
            }
        }
    }
}