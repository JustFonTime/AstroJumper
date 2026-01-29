using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSpaceshipUpgradesSO", menuName = "Scriptable Objects/PlayerSpaceshipUpgradesSO")]
public class PlayerSpaceshipUpgradesSO : ScriptableObject
{
    [Header("Movement Upgrades")] public int moveForceStartingLevel = 1;
    public float moveForceUpgradePerLevel = 2f;
    public float moveForceCostPerLevel = 100f;
    public int maxSpeedStartingLevel = 1;
    public float maxSpeedUpgradePerLevel = 4f;
    public float maxSpeedCostPerLevel = 100f;

    [Header("Boost Upgrades")] public int boostForceStartingLevel = 1;
    public float boostForceUpgradePerLevel = 5f;
    public float boostForceCostPerLevel = 100f;

    [Header("Barrel Roll Upgrades")] public int barrelRollDistanceStartingLevel = 1;
    public float barrelRollDistanceUpgradePerLevel = 2f;
    public float barrelRollDistanceCostPerLevel = 100f;
    public int barrelRollSpeedStartingLevel = 1;
    public float barrelRollSpeedUpgradePerLevel = 0.1f;
    public float barrelRollSpeedCostPerLevel = 100f;

    [Header("Combat Upgrades")] public int fireRateStartingLevel = 1;
    public float fireRateUpgradePerLevel = 0.05f;
    public float fireRateCostPerLevel = 100f;

    //---------------------Helpers---------------------------------------
    public float GetUpgradeCost(PlayerUpgradeState.UpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case PlayerUpgradeState.UpgradeType.MoveForce:
                return moveForceCostPerLevel;
            case PlayerUpgradeState.UpgradeType.MaxSpeed:
                return maxSpeedCostPerLevel;

            case PlayerUpgradeState.UpgradeType.BoostForce:
                return boostForceCostPerLevel;
            case PlayerUpgradeState.UpgradeType.BarrelRollDistance:
                return barrelRollDistanceCostPerLevel;
            case PlayerUpgradeState.UpgradeType.BarrelRollSpeed:
                return barrelRollSpeedCostPerLevel;
            case PlayerUpgradeState.UpgradeType.FireRate:
                return fireRateCostPerLevel;
        }

        return -1;
    }
}