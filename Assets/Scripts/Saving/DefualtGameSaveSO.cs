using UnityEngine;

[CreateAssetMenu(fileName = "DefualtGameSaveSO", menuName = "Scriptable Objects/DefualtGameSaveSO")]
public class DefualtGameSaveSO : ScriptableObject
{
    [Header("Player Defualts")] public int startingNewMoney = 0;

    [Header("Upgrade Defualts   ")] public PlayerSpaceshipUpgradesSO playerSpaceshipUpgradesSO;
}