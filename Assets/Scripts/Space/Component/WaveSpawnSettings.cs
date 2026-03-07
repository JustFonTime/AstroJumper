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

    [Header("Squad Spawning")]
    public bool spawnAsSquad = false;
    [Range(2, 5)] public int minSquadSize = 2;
    [Range(2, 5)] public int maxSquadSize = 4;
    public EnemySquadFormationType formationType = EnemySquadFormationType.Vee;
    public EnemySquadState initialSquadState = EnemySquadState.Engage;
    public float squadSpacing = 4f;
    public float squadEngageDistance = 18f;
    public float squadAnchorMoveSpeed = 12f;
}
