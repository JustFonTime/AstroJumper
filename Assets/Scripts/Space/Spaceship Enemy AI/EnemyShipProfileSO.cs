using UnityEngine;

[CreateAssetMenu(menuName = "AI/Ship Profile", fileName = "ShipProfile")]
public class EnemyShipProfileSO : ScriptableObject
{
    [Header("Movement")]
    public float forwardThrust = 12f;
    public float maxSpeed = 10f;

    [Header("Steering")]
    public float rotationOffset = -90f;

    [Tooltip("Turning when stopped (deg/sec).")]
    public float minTurnDegPerSec = 10f;

    [Tooltip("Turning at full forward speed (deg/sec).")]
    public float maxTurnDegPerSec = 70f;

    [Tooltip("Maps forward speed ratio (0-1) to turn authority (0-1).")]
    public AnimationCurve turnAuthorityBySpeed = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Flight Assist")]
    [Tooltip("How aggressively sideways drift is removed.")]
    public float alignStrength = 6f;

    [Tooltip("Velocity damping when not thrusting.")]
    public float dampStrength = 1.5f;

    [Header("Formation Chase")]
    [Tooltip("Preferred distance for squad leaders from their focus target.")]
    public float focusDistance = 18f;

    [Tooltip("Distance treated as arrived when moving to a goal.")]
    public float arriveDistance = 1.25f;

    [Tooltip("Extra distance used to scale up to full throttle.")]
    public float fullThrottleDistance = 8f;

    [Header("Combat")]
    public float combatRange = 20f;
    public float minFireRate = 1f;
    public float maxFireRate = 2f;

    [Header("Weapon Spread")]
    public bool useWeaponSpread = false;
    public float minSpreadAngle = -2.5f;
    public float maxSpreadAngle = 2.5f;

    [Header("Health")]
    public int maxHealth = 50;

    [Header("Shields")]
    public float maxShields = 50f;
    public float startingShields = 0f;
    public bool canSelfRechargeShields = false;
}


