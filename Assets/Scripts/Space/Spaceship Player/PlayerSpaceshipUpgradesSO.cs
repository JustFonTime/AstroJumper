using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSpaceshipUpgradesSO", menuName = "Scriptable Objects/PlayerSpaceshipUpgradesSO")]
public class PlayerSpaceshipUpgradesSO : ScriptableObject
{
    [Header("Movement Upgrades")] public int moveForceStartingLevel = 1;
    public float moveForceUpgradePerLevel = 2f;
    public int maxSpeedStartingLevel = 1;
    public float maxSpeedUpgradePerLevel = 4f;

    [Header("Boost Upgrades")] public int boostForceStartingLevel = 1;
    public float boostForceUpgradePerLevel = 5f;

    [Header("Barrel Roll Upgrades")] public int barrelRollDistanceStartingLevel = 1;
    public float barrelRollDistanceUpgradePerLevel = 2f;
    public int barrelRollSpeedStartingLevel = 1;
    public float barrelRollSpeedUpgradePerLevel = 0.1f;

    [Header("Combat Upgrades")] public int fireRateStartingLevel = 1;
    public float fireRateUpgradePerLevel = 0.05f;
}