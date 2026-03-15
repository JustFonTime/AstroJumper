using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using System;
using System.IO;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance { get; private set; }

    [Header("Defualts + Files")] [SerializeField]
    private DefualtGameSaveSO defualtGameSaveSO;

    private string saveFileName = "importantYAaaaa.json";

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    public SaveData CurrentSaveData { get; private set; }


    [SerializeField] private float dirtyDelaySaveTime = 2.0f;

    private bool dirty;
    private float dirtyTimer;

    [Header("AutoSave")] [SerializeField] private bool autoSave = true;
    [FormerlySerializedAs("autoSaveDelay")]
    [SerializeField] private float autoSaveDelaySaveTime = 10.0f;

    [Header("Debug")]
    [SerializeField] private bool enableMoneyTestHotkey = true;
    [SerializeField] private KeyCode addMoneyTestKey = KeyCode.M;
    [SerializeField] private int addMoneyTestAmount = 100;


    private void Awake()
    {
        Debug.Log(Application.persistentDataPath);
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this.gameObject);

        LoadGame();

        if (autoSave)
        {
            StartCoroutine(AutoSave());
        }
    }

    private IEnumerator AutoSave()
    {
        while (autoSave)
        {
            yield return new WaitForSeconds(autoSaveDelaySaveTime);
            if (dirty)
            {
                SaveGame();
            }
        }
    }

    private void Update()
    {
        HandleDebugHotkeys();

        if (!autoSave || !dirty) return;
        dirtyTimer += Time.unscaledDeltaTime;
        if (dirtyTimer >= dirtyDelaySaveTime)
        {
            SaveGame();
        }
    }

    private void MakeDirty()
    {
        dirty = true;
        dirtyTimer = 0f;
    }

    private void HandleDebugHotkeys()
    {
        if (!enableMoneyTestHotkey || !Input.GetKeyDown(addMoneyTestKey))
            return;

        AddNewMoney(addMoneyTestAmount);
        Debug.Log($"Save test hotkey pressed. Added {addMoneyTestAmount} money. Current money is now {CurrentSaveData.newMoney}.");
    }

    private void LoadGame()
    {
        if (!File.Exists(SaveFilePath))
        {
            CreateDefaultSaveFile("No save file found.");
            return;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);

            CurrentSaveData = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception ex)
        {
            CreateDefaultSaveFile($"Failed reading save file at {SaveFilePath}. {ex.Message}");
            return;
        }

        if (CurrentSaveData == null)
        {
            CreateDefaultSaveFile($"Save file was empty or invalid JSON at {SaveFilePath}.");
            return;
        }

        bool repairedMissingUpgradeData = CurrentSaveData.spaceshipUpgradeData == null;
        CurrentSaveData.EnsureInitialized(defualtGameSaveSO);

        if (repairedMissingUpgradeData)
        {
            Debug.LogWarning($"Save file at {SaveFilePath} was missing upgrade data. Restored defaults for that section.");
            WriteToDisk();
        }
    }

    public void SaveGame()
    {
        EnsureCurrentSaveData();

        dirty = false;
        dirtyTimer = 0f;

        WriteToDisk();
    }

    private void WriteToDisk()
    {
        EnsureCurrentSaveData();

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            string json = JsonUtility.ToJson(CurrentSaveData, true);
            File.WriteAllText(SaveFilePath, json);
            Debug.Log($"Saved game to {SaveFilePath}. Money={CurrentSaveData.newMoney}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed writing save file at {SaveFilePath}. {ex}");
        }
    }

    private void OnApplicationQuit()
    {
        if (dirty) SaveGame();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && dirty) SaveGame();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && dirty) SaveGame();
    }

    #region Helper Functions

    public int GetNewMoney()
    {
        EnsureCurrentSaveData();
        return (CurrentSaveData != null) ? CurrentSaveData.newMoney : 0;
    }

    public void SetNewMoney(int newMoney)
    {
        EnsureCurrentSaveData();
        if (CurrentSaveData == null) return;

        CurrentSaveData.newMoney = newMoney;
        MakeDirty();
    }

    public void AddNewMoney(int amount)
    {
        EnsureCurrentSaveData();
        if (CurrentSaveData == null) return;

        CurrentSaveData.newMoney += amount;
        MakeDirty();
    }

    public int GetUpgradeLevel(PlayerUpgradeState.UpgradeType upgradeType)
    {
        EnsureCurrentSaveData();
        if (CurrentSaveData == null) return 0;

        switch (upgradeType)
        {
            case PlayerUpgradeState.UpgradeType.MoveForce:
                return CurrentSaveData.spaceshipUpgradeData.moveForceLevel;
            case PlayerUpgradeState.UpgradeType.MaxSpeed:
                return CurrentSaveData.spaceshipUpgradeData.maxSpeedLevel;
            case PlayerUpgradeState.UpgradeType.BoostForce:
                return CurrentSaveData.spaceshipUpgradeData.boostForceLevel;
            case PlayerUpgradeState.UpgradeType.BarrelRollDistance:
                return CurrentSaveData.spaceshipUpgradeData.barrelRollDistanceLevel;
            case PlayerUpgradeState.UpgradeType.BarrelRollSpeed:
                return CurrentSaveData.spaceshipUpgradeData.barrelRollSpeedLevel;
            case PlayerUpgradeState.UpgradeType.FireRate:
                return CurrentSaveData.spaceshipUpgradeData.fireRateLevel;
            case PlayerUpgradeState.UpgradeType.MaxHealth:
                return CurrentSaveData.spaceshipUpgradeData.maxHealthLevel;
            case PlayerUpgradeState.UpgradeType.MaxShields:
                return CurrentSaveData.spaceshipUpgradeData.maxShieldsLevel;
            
        }

        return 0;
    }

    public void AddUpgradeLevel(PlayerUpgradeState.UpgradeType upgradeType)
    {
        EnsureCurrentSaveData();
        if (CurrentSaveData == null) return;

        switch (upgradeType)
        {
            case PlayerUpgradeState.UpgradeType.MoveForce:
                CurrentSaveData.spaceshipUpgradeData.moveForceLevel++;
                break;
            case PlayerUpgradeState.UpgradeType.MaxSpeed:
                CurrentSaveData.spaceshipUpgradeData.maxSpeedLevel++;
                break;
            case PlayerUpgradeState.UpgradeType.BoostForce:
                CurrentSaveData.spaceshipUpgradeData.boostForceLevel++;
                break;
            case PlayerUpgradeState.UpgradeType.BarrelRollSpeed:
                CurrentSaveData.spaceshipUpgradeData.barrelRollSpeedLevel++;
                break;
            case PlayerUpgradeState.UpgradeType.BarrelRollDistance:
                CurrentSaveData.spaceshipUpgradeData.barrelRollDistanceLevel++;
                break;
            case PlayerUpgradeState.UpgradeType.FireRate:
                CurrentSaveData.spaceshipUpgradeData.fireRateLevel++;
                break;
            case PlayerUpgradeState.UpgradeType.MaxHealth:
                CurrentSaveData.spaceshipUpgradeData.maxHealthLevel++;
                break;
            case PlayerUpgradeState.UpgradeType.MaxShields:
                CurrentSaveData.spaceshipUpgradeData.maxShieldsLevel++;
                break;
        }

        MakeDirty();
    }

    #endregion

    private void EnsureCurrentSaveData()
    {
        if (CurrentSaveData == null)
        {
            CurrentSaveData = SaveData.CreateDefualtSaveData(defualtGameSaveSO);
        }

        CurrentSaveData.EnsureInitialized(defualtGameSaveSO);
    }

    private void CreateDefaultSaveFile(string reason)
    {
        CurrentSaveData = SaveData.CreateDefualtSaveData(defualtGameSaveSO);
        Debug.LogWarning($"{reason} Created a new save file at {SaveFilePath}.");
        WriteToDisk();
    }
}
