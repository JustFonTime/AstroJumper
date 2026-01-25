using System;
using System.Collections;
using UnityEngine;

public class EnemySpaceshipCombatAI : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private EnemyShipProfileSO shipProfile;
    [SerializeField] private EnemySpaceshipAI shipAI;
    [SerializeField] private GameObject laserPrefab;
    private GameObject player;
    [SerializeField] public bool canFire = false;

    private void Start()
    {
        if (shipProfile == null)
        {
            Debug.LogError("No ship profile assigned");
            enabled = false;
            return;
        }

        player = GameObject.FindGameObjectWithTag("Player");
        StartCoroutine(FireLaserCooldown());
    }

    private void Update()
    {
        if (player == null)
            return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);
        if (distanceToPlayer <= shipProfile.combatRange && canFire)
        {
            FireLaser();
        }
    }

    private void FireLaser()
    {
        Instantiate(laserPrefab, firePoint.position, firePoint.rotation);
        StartCoroutine(FireLaserCooldown());
    }

    IEnumerator FireLaserCooldown()
    {
        canFire = false;
        float fireRate = UnityEngine.Random.Range(shipProfile.minFireRate, shipProfile.maxFireRate);
        yield return new WaitForSeconds(fireRate);
        canFire = true;
    }
}