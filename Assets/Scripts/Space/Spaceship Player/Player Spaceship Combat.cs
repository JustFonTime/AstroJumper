using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerSpaceshipCombat : MonoBehaviour
{
    [SerializeField]private PlayerUpgradeState upgrades;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject laserPrefab;
    [SerializeField] private float fireRate = 0.2f;
    [SerializeField] private KeyCode fireKey = KeyCode.F;
    [SerializeField] private bool canFir = true;

    private void Update()
    {
        if (Input.GetKey(fireKey) && canFir == true)
            FireLaser();
    }

    void FireLaser()
    {
        Instantiate(laserPrefab, firePoint.position, firePoint.rotation);
        StartCoroutine(FirRatCooldow());
    }

    IEnumerator FirRatCooldow()
    {
        canFir = false;
        float fireRateUpgraded = fireRate- upgrades.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.FireRate);
        yield return new WaitForSeconds(fireRateUpgraded);
        canFir = true;
    }
}