using UnityEngine;

public static class LevelCompletionRewards
{
    private const int SpaceLevelCompletionReward = 5;

    public static int BankGroundScrap(Inventory inventory)
    {
        if (inventory == null)
        {
            Debug.LogWarning("Ground level completed, but no Inventory was found to bank scrap from.");
            return 0;
        }

        int scrapCount = inventory.GetScrapCount();
        if (scrapCount <= 0)
        {
            Debug.Log("Ground level completed with no scrap to bank.");
            return 0;
        }

        if (!TryAwardMoney(scrapCount, $"Banked {scrapCount} scrap from the ground level."))
            return 0;

        inventory.ClearScrap();
        return scrapCount;
    }

    public static int AwardSpaceCompletionReward()
    {
        if (!TryAwardMoney(SpaceLevelCompletionReward, $"Awarded {SpaceLevelCompletionReward} scrap for space level completion."))
            return 0;

        return SpaceLevelCompletionReward;
    }

    private static bool TryAwardMoney(int amount, string logMessage)
    {
        if (amount <= 0)
            return false;

        if (SaveManager.instance == null)
        {
            Debug.LogError($"Tried to award {amount} money, but SaveManager.instance was null.");
            return false;
        }

        SaveManager.instance.AddNewMoney(amount);
        SaveManager.instance.SaveGame();
        Debug.Log(logMessage);
        return true;
    }
}
