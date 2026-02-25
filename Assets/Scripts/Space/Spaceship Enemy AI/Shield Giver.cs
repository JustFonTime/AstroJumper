using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldGiver : MonoBehaviour
{
    [Header("Refs")] [SerializeField] private TeamAgent teamAgent;
    [SerializeField] private TriggerRelay2D triggerRelay; // a trigger collider that defines the tether range
    [SerializeField] private GameObject tetherLinePrefab; // prefab for the visual tether line
    [SerializeField] private Transform tetherLineParent;

    [Header("Settings")] [SerializeField] LayerMask enemyLayer;
    [SerializeField] private bool ShieldBuffingEnabled = true;
    [SerializeField] private float shieldAmount = 5f;
    [SerializeField] private int maxTethers = 5;
    [SerializeField] private float tetherDuration = 5f;
    [SerializeField] private float tetherCooldown = 3f;


    private readonly List<SpaceshipHealthComponent> tetheredEnemies = new List<SpaceshipHealthComponent>();

    void OnEnable()
    {
        StartCoroutine(SendShields());
    }

    void OnDisable()
    {
        ShieldBuffingEnabled = false;
        StopAllCoroutines();
        tetheredEnemies.Clear();
    }

    void Awake()
    {
        if (!teamAgent) teamAgent = GetComponent<TeamAgent>();

        if (!tetherLineParent)
        {
            GameObject childObject = new GameObject("EmptyChild");

            // 2. Set the current GameObject's transform as the parent of the new child's transform
            childObject.transform.parent = this.transform;

            // Optional: Reset local position and rotation to (0,0,0)
            // This places the child exactly at the parent's pivot point
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;

            tetherLineParent = childObject.transform;
        }

        triggerRelay.Enter += OnTetherRangeTriggerEnter2D;
        triggerRelay.Exit += OnTetherRangeTriggerExit2D;
    }

    private void OnTetherRangeTriggerEnter2D(Collider2D other)
    {
        if (tetheredEnemies.Count >= maxTethers) return; // already at max tethers
        if (((1 << other.gameObject.layer) & enemyLayer) == 0) return; // not an enemy
        if (other.TryGetComponent<SpaceshipHealthComponent>(out var health))
        {
            if (health.IsBuffedByShieldEnemy) return; // already buffed by another shield giver

            AddTether(health);
        }
    }

    private void AddTether(SpaceshipHealthComponent health)
    {
        tetheredEnemies.Add(health);

        // Optionally, you can instantiate a tether line here and parent it to the tetherLineParent for visual effect
        GameObject tetherLine = Instantiate(tetherLinePrefab, tetherLineParent);
        if (tetherLine.TryGetComponent<TetherLine>(out var tetherLineComp))
        {
            tetherLineComp.SetupLine(this.transform, health.transform);
        }
    }

    private void OnTetherRangeTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) == 0) return; // not an enemy
        if (other.TryGetComponent<SpaceshipHealthComponent>(out var health))
        {
            RemoveTether(health);
        }
    }

    private void RemoveTether(SpaceshipHealthComponent health)
    {
        tetheredEnemies.Remove(health);

        // Optionally, you can also destroy the corresponding tether line here if you instantiated one in AddTether
        foreach (var gau in tetherLineParent.GetComponentsInChildren<TetherLine>())
        {
            // Check if this tether line is connected to the health component we want to remove
            //by checking if either end of the tether line is the health component's transform
            Transform[] endpoints = gau.GetEndpoints();
            if (endpoints[0] == health.transform || endpoints[1] == health.transform)
            {
                Destroy(gau.gameObject); // Destroy the tether line
                break; // Exit the loop since we found the line to remove
            }
        }
    }

    IEnumerator SendShields()
    {
        ShieldBuffingEnabled = true;

        while (ShieldBuffingEnabled)
        {
            if (tetheredEnemies.Count > 0)
            {
                for (int i = tetheredEnemies.Count - 1; i >= 0; i--)
                {
                    var health = tetheredEnemies[i];
                    if (health == null)
                    {
                        tetheredEnemies.RemoveAt(i);
                        continue;
                    }

                    health.HealShields(shieldAmount);
                }

                yield return new WaitForSeconds(tetherCooldown);
            }
            else
            {
                yield return null;
            }
        }
    }
}