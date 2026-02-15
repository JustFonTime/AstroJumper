using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class WaveSpawnSettings
{
    public List<WaveEnemyEntry> enemies = new List<WaveEnemyEntry>();
    public float spawnSpacing = 0f;
    public float timeToSpawnAfterFinalDeath = 2f;
}

[System.Serializable]
public class WaveEnemyEntry
{
    public GameObject prefab;
    public int count = 5;
}