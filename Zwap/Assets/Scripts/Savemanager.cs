using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Central place to save/load all game data.
/// Attach this to an empty GameObject called "SaveManager" in your first scene
/// (e.g. your main menu or a persistent bootstrap scene), and it will survive
/// scene loads automatically.
///
/// Usage examples:
///   SaveManager.Instance.Data.coins += 50;
///   SaveManager.Instance.Save();
///
///   if (score > SaveManager.Instance.Data.highScore)
///       SaveManager.Instance.Data.highScore = score;
///
///   SaveManager.Instance.Data.unlockedSkins.Add("GoldenRock");
///   SaveManager.Instance.Save();
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // This is the object that actually holds your game's data.
    // Add new fields here whenever you want to save something new.
    public SaveData Data { get; private set; } = new SaveData();

    private string SavePath => Application.persistentDataPath + "/save.json";

    private void Awake()
    {
        // Singleton setup - only one SaveManager ever exists.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    /// <summary>Writes the current Data object to disk as JSON.</summary>
    public void Save()
    {
        string json = JsonUtility.ToJson(Data, prettyPrint: true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[SaveManager] Saved to {SavePath}");
    }

    /// <summary>Loads Data from disk. If no save file exists yet, keeps defaults.</summary>
    public void Load()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            Data = JsonUtility.FromJson<SaveData>(json);
            Debug.Log("[SaveManager] Save file loaded.");
        }
        else
        {
            Data = new SaveData();
            Debug.Log("[SaveManager] No save file found, starting fresh.");
        }
    }

    /// <summary>Wipes all saved progress and resets to defaults. Handy for a "Reset Data" debug button.</summary>
    public void ResetData()
    {
        Data = new SaveData();
        Save();
    }

    // ---- Quick-access helpers for the tiny stuff (optional) ----
    // Use these for one-off flags/settings you don't want to add to SaveData.
    // Anything you'll query/update often should live in SaveData instead.

    public void SetSetting(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }

    public float GetSetting(string key, float defaultValue = 0f)
    {
        return PlayerPrefs.GetFloat(key, defaultValue);
    }
}

/// <summary>
/// All the actual save values live here. Add a new public field any time
/// you want to save something new — no other code needs to change.
/// </summary>
[Serializable]
public class SaveData
{
    public int highScore = 0;
    public int runs = 0;
}