using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;


[DisallowMultipleComponent]
[RequireComponent(typeof(TeamAgent))]
/// <summary>
/// AI objects use this to handle retargeting +claim/release attack tokens on enemyys
/// </summary>
public class TargetingComponent : MonoBehaviour
{
    [Header("Targeting ")] [SerializeField]
    private float aggroRadius = 40f;

    [SerializeField] private float retargetInterval = 0.25f;

    [Tooltip(
        "If true, only targets with available TartSlots, meaning if too many things are already attacking that enemy, this wont join in in jumping them")]
    [SerializeField]
    private bool enforceTargetSlots = true;

    [Tooltip("If true, keep current target as long as it stayus valid (aka alive)")] [SerializeField]
    private bool stickToTarget = true;

    public TeamAgent CurrentTarget { get; private set; }

    private TeamAgent self;
    private Coroutine co;

    private void Awake()
    {
        self = GetComponent<TeamAgent>();
    }

    private void OnEnable()
    {
        co = StartCoroutine(TargetingLoop());
    }

    private void OnDisable()
    {
        if (co != null) StopCoroutine(co);
        co = null;
        ReleaseCurrentTarget();
    }

    IEnumerator TargetingLoop()
    {
        //small dysnc so all enemies dont retarget same frame
        yield return new WaitForSeconds(Random.Range(0f, retargetInterval));

        while (true)
        {
            RetargetNow();
            yield return new WaitForSeconds(retargetInterval);
        }
    }

    public void RetargetNow()
    {
        if (self == null || !self.isActiveAndEnabled) return;

        //keep target if still valid 
        if (stickToTarget && IsTargetValid(CurrentTarget))
        {
            //if target uses slots but we somehow lost claim, find new target
            if (CurrentTarget.Slots != null && !CurrentTarget.Slots.HasAttacker(self))
            {
                ReleaseCurrentTarget();
            }
        } //Since we arent sticking to target release and get new target 
        else ReleaseCurrentTarget();


        if (enforceTargetSlots)
        {
            CurrentTarget = TeamRegistry.FindAndClaim(self, transform.position, aggroRadius);
        }
        else
        {
            CurrentTarget = TeamRegistry.FindNearestHostile(self, transform.position, aggroRadius, false);
            //if it has slots, try claim but dont require it
            if (CurrentTarget != null && CurrentTarget.Slots != null)
                CurrentTarget.Slots.TryClaim(self);
        }
    }


    public void ReleaseCurrentTarget()
    {
        if (CurrentTarget != null && CurrentTarget.Slots != null && self != null)
            CurrentTarget.Slots.Release(self);

        CurrentTarget = null;
    }

    private bool IsTargetValid(TeamAgent t)
    {
        if (t == null) return false;
        if (!t.isActiveAndEnabled) return false;
        if (!TeamRegistry.IsHostile(self.TeamId, t.TeamId)) return false;

        float r2 = aggroRadius <= 0f ? float.PositiveInfinity : aggroRadius * aggroRadius;
        float d2 = ((Vector2)t.transform.position - (Vector2)self.transform.position).sqrMagnitude;

        return d2 <= r2;
    }
}