using UnityEngine;
using System.Collections;
using System.IO;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance { get; private set; }

    [Header("Defualts + Files")] [SerializeField]
    private DefualtGameSaveSO defualtGameSaveSO;

    private string saveFileName = "importantYAaaaa.json";

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    public SaveData CurrentSaveData { get; private set; }

    [Header("auto Save")] [SerializeField] private bool autoSave = true;

    [SerializeField] private float dirtyDelaySaveTime = 2.0f;

    private bool dirty;
    private float dirtyTimer;

    [Header("AutoSave")] [SerializeField] private bool autoSaveAutoSave = true;
    [SerializeField] private float autoSaveDelaySaveTime = 5.0f;


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
        AddNewMoney(100);
        AddMoveForceLevel(1);
    }

    private IEnumerator AutoSave()
    {
        while (autoSave)
        {
            yield return new WaitForSeconds(autoSaveDelaySaveTime);
            SaveGame();
        }
    }

    private void Update()
    {
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

    private void LoadGame()
    {
        if (!File.Exists(SaveFilePath))
        {
            //No file found, create new save data
            CurrentSaveData = SaveData.CreateDefualtSaveData(defualtGameSaveSO);
            WriteToDisk();
            Debug.Log("No save file found. Created new save data at ^^^^^^");
            return;
        }

        string json = File.ReadAllText(SaveFilePath);

        CurrentSaveData = JsonUtility.FromJson<SaveData>(json);

        if (CurrentSaveData == null)
        {
            CurrentSaveData = SaveData.CreateDefualtSaveData(defualtGameSaveSO);
            WriteToDisk();
            Debug.LogWarning("Save file was corrupted. Created new save data at ^^^^^^");
            return;
        }
    }

    public void SaveGame()
    {
        if (CurrentSaveData == null)
        {
            Debug.LogError("No save data to write to json using defualts!");
            CurrentSaveData = SaveData.CreateDefualtSaveData(defualtGameSaveSO);
        }

        dirty = false;
        dirtyTimer = 0f;

        WriteToDisk();
        Debug.Log($"Game Saved to {SaveFilePath} {CurrentSaveData.newMoney} ");
    }

    private void WriteToDisk()
    {
        string json = JsonUtility.ToJson(CurrentSaveData, true);
        File.WriteAllText(SaveFilePath, json);
        Debug.Log("Game saved to: " + SaveFilePath);
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

    public int GetNewMoney() => (CurrentSaveData.newMoney != null) ? CurrentSaveData.newMoney : 0;

    public void SetNewMoney(int newMoney)
    {
        if (CurrentSaveData.newMoney != null)
            CurrentSaveData.newMoney = newMoney;
        else
            Debug.LogError("CurrentSaveData.newMoney is null!");
        MakeDirty();
    }

    public void AddNewMoney(int amount)
    {
        if (CurrentSaveData.newMoney != null)
            CurrentSaveData.newMoney += amount;
        else
            Debug.LogError("CurrentSaveData.newMoney is null!");
        MakeDirty();
    }

    public int GetUpgradeLevel(PlayerUpgradeState.UpgradeType upgradeType)
    {
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
        }

        return -1;
    }

    public void AddUpgradeLevel(PlayerUpgradeState.UpgradeType upgradeType)
    {
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
        }
    }

    public int GetMoveForceLevel() => CurrentSaveData != null ? CurrentSaveData.spaceshipUpgradeData.moveForceLevel : 0;

    public void AddMoveForceLevel(int amt)
    {
        MakeDirty();
        CurrentSaveData.spaceshipUpgradeData.moveForceLevel += amt;
    }

    #endregion
}