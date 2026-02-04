using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class WaveSpawnSettings
{
    public List<EnemyCount> enemies = new List<EnemyCount>();
    public float spawnSpacing = 0.05f;
    public float timeToSpawnAfterFinalDeath = 2f;
}