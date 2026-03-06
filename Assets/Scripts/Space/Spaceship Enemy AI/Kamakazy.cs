using System.Collections;
using UnityEngine;

public class Kamakazy : MonoBehaviour
{
    [Header("Refs")] [SerializeField] private TeamAgent teamAgent;
    [SerializeField] private TriggerRelay2D triggerRelay;
    [SerializeField] private Animator animator;

    [Header("Settings")] [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float explosionDamage = 20f;
    [SerializeField] private float explosionForce = 10f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Explostion Stuff")] [SerializeField]
    private bool startedExplosionSequence = false;


    void Awake()
    {
        if (!teamAgent) teamAgent = GetComponent<TeamAgent>();
    }

    void OnEnable()
    {
        startedExplosionSequence = false;
        triggerRelay.Enter += OnTetherRangeTriggerEnter2D;
    }

    void OnDisable()
    {
        triggerRelay.Enter -= OnTetherRangeTriggerEnter2D;
        StopAllCoroutines();
    }



    private void OnTetherRangeTriggerEnter2D(Collider2D obj)
    {
        if (startedExplosionSequence) return; // already started explosion sequence, ignore new triggers
        if (((1 << obj.gameObject.layer) & enemyLayer) == 0) return; // not an enemy
        if (obj.TryGetComponent<TeamAgent>(out var teamAgent))
        {
            if (teamAgent.TeamId != this.teamAgent.TeamId)
            {
                StartCoroutine(StartExplosionSequence());
            }
        }
    }

    IEnumerator StartExplosionSequence()
    {
        startedExplosionSequence = true;
        animator.SetTrigger("Explode");

        //wait for animation to play 
        yield return new WaitForSeconds(5f);

        //damage enemies in radius
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, enemyLayer);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<TeamAgent>(out var agent))
            {
                if (agent.TeamId != this.teamAgent.TeamId)
                {
                    if (hit.TryGetComponent<SpaceshipHealthComponent>(out var health))
                    {
                        health.TakeDamage((int)explosionDamage);
                    }
                }
            }
        }


        if (this.TryGetComponent<SpaceshipHealthComponent>(out var thisHealth))
        {
            thisHealth.TakeDamage(1000000); // kill self
            if (thisHealth != null)
            {
                thisHealth.TakeDamage(10000);
            }
        }
    }
}
