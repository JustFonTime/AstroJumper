using UnityEngine;

[CreateAssetMenu(menuName = "AI/Enemy Ship Profile", fileName = "EnemyShipProfile")]
public class EnemyShipProfileSO : ScriptableObject
{
    [Header("Movement")] public float moveForce = 10f;
    public float maxSpeed = 10f;

    [Header("Rotation")] public float rotationOffset = -90f;
    public float rotationLerp = 12f;

    [Header("Distancing")] public float minDistanceFromPlayer = 10f;
    public float maxDistanceFromPlayer = 15f;

    [Header("Strafing")] public float strafeForce = 20f;
    public bool startStrafeRight = true;

    [Header("Random Strafe Direction")] public bool useRandomStrafeDirection = false;
    public float minStrafeInterval = 2f;
    public float maxStrafeInterval = 5f;

    [Header("Random Speed")] public bool useRandomSpeed = false;
    public float minSpeedMultiplier = 0.8f;
    public float maxSpeedMultiplier = 1.2f;
    public float oscillationHz = 0.5f;

    [Header("Random Barrel Roll")] public bool useRandomBarrelRoll = false;
    public float barrelRollDistance = 10f;
    public float barrelRollDuration = 0.5f;
    public float barrelRollSpinDegrees = 360f;
    public float barrelRollMinTime = 4f;
    public float barrelRollMaxTime = 10f;

    [Header("Combat")] public float combatRange = 20f;
    public float minFireRate = 1f;
    public float maxFireRate = 2f;
}