using UnityEngine;


//This needs to be renamed, its really just getting the level and ettings for what each level means and defiening the upgrades we have
//More like a PlayerUpgradeStatsInfo
public class PlayerUpgradeState : MonoBehaviour
{
    public enum UpgradeType
    {
        MoveForce,
        MaxSpeed,
        BoostForce,
        BarrelRollDistance,
        BarrelRollSpeed,
        FireRate,
        MaxHealth,
        MaxShields,
    }

    [SerializeField] private PlayerSpaceshipUpgradesSO upgradesSO;


    public PlayerSpaceshipUpgradesSO Defs => upgradesSO;

    /// <summary>
    /// Uses the upgrade type to get the players level form save manager and the player upgrade so to find the correct boost amount
    /// </summary>
    /// <param name="upgradeType"></param>
    /// <returns></returns>
    public float GetUpgradeBoost(UpgradeType upgradeType)
    {
        var u = SaveManager.instance.CurrentSaveData.spaceshipUpgradeData;
        switch (upgradeType)
        {
            case UpgradeType.MoveForce:
                return u.moveForceLevel * upgradesSO.moveForceUpgradePerLevel;
            case UpgradeType.MaxSpeed:
                return u.maxSpeedLevel * upgradesSO.maxSpeedUpgradePerLevel;
            case UpgradeType.BoostForce:
                return u.boostForceLevel * upgradesSO.boostForceUpgradePerLevel;
            case UpgradeType.BarrelRollDistance:
                return u.barrelRollDistanceLevel * upgradesSO.barrelRollDistanceUpgradePerLevel;
            case UpgradeType.BarrelRollSpeed:
                return u.barrelRollSpeedLevel * upgradesSO.barrelRollSpeedUpgradePerLevel;
            case UpgradeType.FireRate:
                return u.fireRateLevel * upgradesSO.fireRateUpgradePerLevel;
            case UpgradeType.MaxHealth:
                return u.maxHealthLevel * upgradesSO.maxHealthUpgradePerLevel;
            case UpgradeType.MaxShields:
                return u.maxShieldsLevel * upgradesSO.maxShieldsPerLevel;
        }

        return 0f;
    }
}