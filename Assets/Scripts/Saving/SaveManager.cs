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

    [SerializeField] private float autoSaveDelay = 2.0f;

    private bool dirty;
    private float dirtyTimer;
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
        AddNewMoney(10);
        AddMoveForceLevel(1);
        SaveGame();
    }

    private void Update()
    {
        if (!autoSave || !dirty) return;
        dirtyTimer+=Time.unscaledDeltaTime;
        if (dirtyTimer >= autoSaveDelay)
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
        if(dirty) SaveGame();
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

    public int GetMoveForceLevel() => CurrentSaveData != null ? CurrentSaveData.spaceshipUpgradeData.moveForceLevel : 0;

    public void AddMoveForceLevel(int amt)
    {
        MakeDirty();
        CurrentSaveData.spaceshipUpgradeData.moveForceLevel += amt;
    }

    #endregion
}