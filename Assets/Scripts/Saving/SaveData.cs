using UnityEngine;
using System;

[System.Serializable]
public class SaveData
{
    public float version = 0.67f;

    public int newMoney; //Important

    //------Upgrades (Player Spaceship current levels)-------
    public SpaceshipUpgradeData spaceshipUpgradeData;

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
    }

    public static SaveData CreateDefualtSaveData(DefualtGameSaveSO defaults)
    {
        var d = new SaveData();
        //Init everything to the defualt value
        d.newMoney = (defaults != null) ? defaults.startingNewMoney : 0;

        if (defaults != null && defaults.playerSpaceshipUpgradesSO != null)
        {
            var u = defaults.playerSpaceshipUpgradesSO;
            d.spaceshipUpgradeData.moveForceLevel = u.moveForceStartingLevel;
            d.spaceshipUpgradeData.maxSpeedLevel = u.maxSpeedStartingLevel;
            d.spaceshipUpgradeData.boostForceLevel = u.boostForceStartingLevel;
            d.spaceshipUpgradeData.barrelRollDistanceLevel =
                u.barrelRollDistanceStartingLevel;
            d.spaceshipUpgradeData.barrelRollSpeedLevel =
                u.barrelRollSpeedStartingLevel;
            d.spaceshipUpgradeData.fireRateLevel = u.fireRateStartingLevel;
        }
        else
        {
            //Fallback
            d.spaceshipUpgradeData.moveForceLevel = 0;
            d.spaceshipUpgradeData.maxSpeedLevel = 0;
            d.spaceshipUpgradeData.boostForceLevel = 0;
            d.spaceshipUpgradeData.barrelRollDistanceLevel = 0;
            d.spaceshipUpgradeData.barrelRollSpeedLevel = 0;
            d.spaceshipUpgradeData.fireRateLevel = 0;
        }

        return d;
    }
}