using UnityEngine;

public class PlayerUpgradeState : MonoBehaviour
{
    public enum UpgradeType
    {
        MoveForce,
        MaxSpeed,
        BoostForce,
        BarrelRollDistance,
        BarrelRollSpeed,
        FireRate
    }

    [SerializeField] private PlayerSpaceshipUpgradesSO upgradesSO;


    public PlayerSpaceshipUpgradesSO Defs => upgradesSO;


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
        }

        return 0f;
    }
}