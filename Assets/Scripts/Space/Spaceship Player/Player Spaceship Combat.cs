using System;
using UnityEngine;
using System.Collections;


public class PlayerSpaceshipCombat : MonoBehaviour
{
    [SerializeField] private PlayerUpgradeState upgrades;
    [SerializeField] private SpaceAttackInfo[] attacks;

    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            if (attacks != null && attacks.Length > 0)
                TryAttack(attacks[0]);
        }

        if (Input.GetMouseButton(1))
        {
            if (attacks != null && attacks.Length > 1)
            {
                TryAttack(attacks[1]);
            }
        }
    }
    
 
    
    void TryAttack(SpaceAttackInfo attack)
    {
        if (attack == null) return;
        if (!attack.canFire) return;
        if (attack.projectile == null || attack.firePoint == null) return;
        Instantiate(attack.projectile, attack.firePoint.position, attack.firePoint.rotation);
        StartCoroutine(AttackCooldown(attack));
    }

    IEnumerator AttackCooldown(SpaceAttackInfo attack)
    {
        attack.canFire = false;

        float upgradedRate = attack.fireRate;
        if (upgrades != null)
            upgradedRate -= upgrades.GetUpgradeBoost(PlayerUpgradeState.UpgradeType.FireRate);

        upgradedRate = Mathf.Max(0.02f, upgradedRate); // safety clamp

        yield return new WaitForSeconds(upgradedRate);
        attack.canFire = true;
    }
}