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

    private void Awake()
    {
        targeting = GetComponent<TargetingComponent>();
    }

    private void OnEnable()
    {
        ResetForSpawn();
    }

    private void OnDisable()
    {
        Debug.Log("Stopping fire loop for " + gameObject.name);
        if (fireCo != null) StopCoroutine(fireCo);
        fireCo = null;
    }

    // Pool-safe
    public void ResetForSpawn()
    {
        if (fireCo != null) return;
        StartFiringLoop();
    }

    private void StartFiringLoop()
    {
        if (shipProfile == null || laserPrefab == null || firePoint == null) return;
        if (fireCo != null) return; //already started
        Debug.Log("Starting fire loop for " + gameObject.name);
        fireCo = StartCoroutine(FireLoop());
    }


    private IEnumerator FireLoop()
    {
        Debug.Log("Entered fire loop for " + gameObject.name);
        // Desync so all enemies don't shoot in the same frame
        yield return new WaitForSeconds(Random.Range(0f, 0.4f));

        while (true)
        {
            float fireRate = Random.Range(shipProfile.minFireRate, shipProfile.maxFireRate);
            yield return new WaitForSeconds(fireRate);


            var t = targeting != null ? targeting.CurrentTarget : null;

            Debug.Log("Current target for " + gameObject.name + ": " + (t != null ? t.gameObject.name : "None"));
            if (t == null) continue;

            float dist = Vector2.Distance(transform.position, t.transform.position);
            if (dist <= shipProfile.combatRange)
            {
                var x = Instantiate(laserPrefab, firePoint.position, firePoint.rotation);
                if (x.TryGetComponent<SpaceshipLaser>(out var laser))
                {
                    laser.teamId = GetComponent<TeamAgent>().TeamId;
                }
            }
        }
    }
}