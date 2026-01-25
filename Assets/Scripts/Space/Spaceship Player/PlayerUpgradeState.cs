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

    public int MoveForceLevel { get; private set; }
    public int MaxSpeedLevel { get; private set; }
    public int BoostForceLevel { get; private set; }
    public int BarrelRollDistanceLevel { get; private set; }
    public int BarrelRollSpeedLevel { get; private set; }
    public int FireRateLevel { get; private set; }

    public PlayerSpaceshipUpgradesSO Defs => upgradesSO;

    private void Awake()
    {
        ResetToStartLevels();
    }

    public void ResetToStartLevels()
    {
        MoveForceLevel = Mathf.Max(1, upgradesSO.moveForceStartingLevel);
        MaxSpeedLevel = Mathf.Max(1, upgradesSO.maxSpeedStartingLevel);
        BoostForceLevel = Mathf.Max(1, upgradesSO.boostForceStartingLevel);
        BarrelRollDistanceLevel = Mathf.Max(1, upgradesSO.barrelRollDistanceStartingLevel);
        BarrelRollSpeedLevel = Mathf.Max(1, upgradesSO.barrelRollSpeedStartingLevel);
        FireRateLevel = Mathf.Max(1, upgradesSO.fireRateStartingLevel);

        print(MoveForceLevel);
    }

    public void AddMoveForce(int amount = 1) => MoveForceLevel += amount;

    public float GetUpgradeBoost(UpgradeType upgradeType)
    {
        switch (upgradeType)
        {
            case UpgradeType.MoveForce:
                return MoveForceLevel * upgradesSO.moveForceUpgradePerLevel;
            case UpgradeType.MaxSpeed:
                return MaxSpeedLevel * upgradesSO.maxSpeedUpgradePerLevel;
            case UpgradeType.BoostForce:
                return BoostForceLevel * upgradesSO.boostForceUpgradePerLevel;
            case UpgradeType.BarrelRollDistance:
                return BarrelRollDistanceLevel * upgradesSO.barrelRollDistanceUpgradePerLevel;
            case UpgradeType.BarrelRollSpeed:
                return BarrelRollSpeedLevel * upgradesSO.barrelRollSpeedUpgradePerLevel;
            case UpgradeType.FireRate:
                return FireRateLevel * upgradesSO.fireRateUpgradePerLevel;
        }

        return 0f;
    }
}