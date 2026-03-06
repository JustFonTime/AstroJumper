using UnityEngine;

[CreateAssetMenu(menuName = "AI/Enemy Ship Profile", fileName = "EnemyShipProfile")]
public class EnemyShipProfileSO : ScriptableObject
{
    [Header("Movement")] public float forwardThrust = 12f;
    public float maxSpeed = 10f;

    [Header("Steering")] public float rotationOffset = -90f;

    [Tooltip("Turning when stopped (deg/sec).")]
    public float minTurnDegPerSec = 10f;

    [Tooltip("Turning at full forward speed (deg/sec).")]
    public float maxTurnDegPerSec = 60f;

    [Tooltip("maps speed to turning authority (0-1)")]
    public AnimationCurve turnAuthorityBySpeed = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Flight Assist ")] [Tooltip("kills sideways drift")]
    public float alignStrength = 6f;

    [Tooltip(
        "Dmping for all movement, including forward. Higher values = stronger damping, 1 = no damping, <1 = anti-damping (aka acceleration)")]
    public float dampStrength = 1.5f;

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

    [Header("Combat ")] public float combatRange = 20f;
    public float minFireRate = 1f;
    public float maxFireRate = 2f;
    public bool useWeaponSpread = false;
    public float minSpreadAngle = -2.5f;
    public float maxSpreadAngle = 2.5f;

    [Header("Attack Runs")]
    public bool useAttackRuns = true;
    public float attackRunStartDistanceMultiplier = 1.35f;
    public float attackRunOvershootDistance = 10f;
    public float attackRunStrafeOffset = 6f;
    public float minAttackRunDuration = 1.1f;
    public float maxAttackRunDuration = 1.8f;
    public float minAttackRunCooldown = 1.2f;
    public float maxAttackRunCooldown = 2.5f;
    public float minPeelOffDuration = 0.8f;
    public float maxPeelOffDuration = 1.4f;
    public float peelOffDistance = 14f;

    [Header("Retreat")]
    public bool retreatWhenLow = true;
    [Range(0f, 1f)] public float retreatShieldRatioThreshold = 0.15f;
    [Range(0f, 1f)] public float retreatHullRatioThreshold = 0.35f;
    public float retreatDuration = 2.25f;
    public float retreatSideOffset = 8f;

    [Header("Forward Avoidance")]
    public bool useForwardAvoidance = true;
    public float forwardAvoidanceDistance = 10f;
    public float forwardAvoidanceRadius = 4f;
    public float forwardAvoidanceStrength = 1.5f;
    [Range(-1f, 1f)] public float forwardAvoidanceDotThreshold = 0.35f;

    [Header("Health")] public int maxHealth = 50;
    [Header("Shields")] public float maxShields = 50f;
    public float startingShields = 0f;
    public bool canSelfRechargeShields = false;
}
