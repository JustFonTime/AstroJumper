using UnityEngine;

[CreateAssetMenu(menuName = "AI/Enemy Ship Profile", fileName = "EnemyShipProfile")]
public class EnemyShipProfileSO : ScriptableObject
{
    [Header("Movement")]
    public float forwardThrust = 12f;
    public float maxSpeed = 10f;

    [Header("Steering")]
    public float rotationOffset = -90f;

    [Tooltip("Turning when nearly stopped (deg/sec). Keep > 0 so they don't get 'stuck'.")]
    public float minTurnDegPerSec = 10f;

    [Tooltip("Turning at full forward speed (deg/sec).")]
    public float maxTurnDegPerSec = 60f;

    [Tooltip("Maps speed01 (0..1) -> turn authority (0..1).")]
    public AnimationCurve turnAuthorityBySpeed = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Flight Assist ")]
    [Tooltip("Higher = kills sideways drift harder.")]
    public float alignStrength = 6f;

    [Tooltip("Only applied when NOT thrusting (or returning).")]
    public float dampStrength = 1.5f;

    [Header("Random Speed")]
    public bool useRandomSpeed = false;
    public float minSpeedMultiplier = 0.8f;
    public float maxSpeedMultiplier = 1.2f;
    public float oscillationHz = 0.5f;

    [Header("Random Barrel Roll")]
    public bool useRandomBarrelRoll = false;
    public float barrelRollDistance = 10f;
    public float barrelRollDuration = 0.5f;
    public float barrelRollSpinDegrees = 360f;
    public float barrelRollMinTime = 4f;
    public float barrelRollMaxTime = 10f;

    [Header("Combat ")]
    public float combatRange = 20f;
    public float minFireRate = 1f;
    public float maxFireRate = 2f;

    [Header("Health")]
    public int maxHealth = 50;
}