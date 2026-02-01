using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class UpgradeButton : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private PlayerUpgradeState.UpgradeType upgradeType;

    [Header("Buttons")] [SerializeField] private PlayerSpaceshipUpgradesSO upgradesSO;
    [SerializeField] private GameObject upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeLabel;
    [SerializeField] private TextMeshProUGUI upgradeCostLabel;
    [SerializeField] private TextMeshProUGUI currentLevelLabel;

    public void Start()
    {
        InitButtonInit();
    }

    public void PrintToConsole()
    {
        print($"Player clickd on {this.upgradeType.ToString()} type button.");
        if (SaveManager.instance != null)
        {
            if (SaveManager.instance.GetNewMoney() >= upgradesSO.GetUpgradeCost(upgradeType))
            {
                print("can afford upgrade");
                SaveManager.instance.AddNewMoney(-(int)(upgradesSO.GetUpgradeCost(upgradeType)));
                SaveManager.instance.AddUpgradeLevel(upgradeType);
                InitButtonInit();
            }
        }
    }

    private void InitButtonInit()
    {
        upgradeLabel.text = upgradeType.ToString();
        upgradeCostLabel.text = ($"Cost: {upgradesSO.GetUpgradeCost(upgradeType).ToString()}");
        currentLevelLabel.text = $"Lv.  {SaveManager.instance.GetUpgradeLevel(upgradeType).ToString()}";
    }
}