using UnityEngine;
using System;

[System.Serializable]
public class SaveData
{
    public float version = 0.67f;

    public int newMoney; //Important

    //------Upgrades (Player Spaceship current levels)-------
    public SpaceshipUpgradeData spaceshipUpgradeData = new SpaceshipUpgradeData();

    [Serializable]
    public class SpaceshipUpgradeData
    {
        //Needs a matching one for everyu player spachip upgrade
        public int moveForceLevel;
        public int maxSpeedLevel;
        public int boostForceLevel;
        public int barrelRollDistanceLevel;
        public int barrelRollSpeedLevel;
        public int fireRateLevel;
        public int maxShieldsLevel;
        public int maxHealthLevel;
    }

    public void EnsureInitialized(DefualtGameSaveSO defaults)
    {
        if (spaceshipUpgradeData == null)
        {
            spaceshipUpgradeData = CreateDefaultUpgradeData(defaults != null ? defaults.playerSpaceshipUpgradesSO : null);
        }
    }

    public static SaveData CreateDefualtSaveData(DefualtGameSaveSO defaults)
    {
        var d = new SaveData();
        //Init everything to the defualt value
        d.newMoney = (defaults != null) ? defaults.startingNewMoney : 0;
        d.spaceshipUpgradeData = CreateDefaultUpgradeData(defaults != null ? defaults.playerSpaceshipUpgradesSO : null);

        return d;
    }

    private static SpaceshipUpgradeData CreateDefaultUpgradeData(PlayerSpaceshipUpgradesSO upgradeDefaults)
    {
        var upgradeData = new SpaceshipUpgradeData();

        if (upgradeDefaults != null)
        {
            upgradeData.moveForceLevel = upgradeDefaults.moveForceStartingLevel;
            upgradeData.maxSpeedLevel = upgradeDefaults.maxSpeedStartingLevel;
            upgradeData.boostForceLevel = upgradeDefaults.boostForceStartingLevel;
            upgradeData.barrelRollDistanceLevel = upgradeDefaults.barrelRollDistanceStartingLevel;
            upgradeData.barrelRollSpeedLevel = upgradeDefaults.barrelRollSpeedStartingLevel;
            upgradeData.fireRateLevel = upgradeDefaults.fireRateStartingLevel;
            upgradeData.maxHealthLevel = upgradeDefaults.maxHealthStartingLevel;
            upgradeData.maxShieldsLevel = upgradeDefaults.maxShieldsStartingLevel;
        }

        return upgradeData;
    }
}
