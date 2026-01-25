using UnityEngine;

[CreateAssetMenu(fileName = "PlayerUpgradesSO", menuName = "Scriptable Objects/PlayerUpgradesSO")]
public class PlayerUpgradesSO : ScriptableObject
{
    [Header("Movement Upgrades")]
    public float moveForceUpgrade = 2f;
}
