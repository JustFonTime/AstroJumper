using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(TeamAgent))]
/// <summary>
/// AI objects use this to handle retargeting +claim/release attack tokens on enemies
/// </summary>
public class TargetingComponent : MonoBehaviour
{
    [Header("Targeting")] [SerializeField] private float aggroRadius = 300f;
    [SerializeField] private float retargetInterval = 0.25f;

    [Tooltip("If true, only targets with available TargetSlots.")] [SerializeField]
    private bool enforceTargetSlots = true;

    [Tooltip("If true, keep current target as long as it stays valid (alive + hostile + in range)." )] [SerializeField]
    private bool stickToTarget = true;

    [Tooltip("If false, this component will NOT auto-pick targets (but it can still use external/forced targets).")]
    [SerializeField]
    private bool autoTargetingEnabled = true;

    public TeamAgent CurrentTarget { get; private set; }

    private TeamAgent self;
    private Coroutine co;

    // External override (used by orders like Protect/Escort/FocusFire)
    private bool hasExternalTarget = false;
    private TeamAgent externalTarget = null;

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
        hasExternalTarget = false;
        externalTarget = null;
    }

    IEnumerator TargetingLoop()
    {
        // small desync so all enemies don't retarget same frame
        yield return new WaitForSeconds(Random.Range(0f, retargetInterval));

        while (true)
        {
            RetargetNow();
            yield return new WaitForSeconds(retargetInterval);
        }
    }

    public void SetAutoTargetingEnabled(bool enabled)
    {
        autoTargetingEnabled = enabled;

        // If disabling auto targeting, we usually want to drop any random target immediately.
        if (!autoTargetingEnabled && !hasExternalTarget)
            ReleaseCurrentTarget();
    }

    public void SetExternalTarget(TeamAgent target, bool claimSlotIfPossible = true)
    {
        // Clear external override
        if (target == null)
        {
            if (hasExternalTarget)
            {
                // If our current target was external, release it cleanly
                ReleaseCurrentTarget();
            }

            hasExternalTarget = false;
            externalTarget = null;
            return;
        }

        // Validate
        if (!IsTargetValid(target))
        {
            // If invalid, clear external
            SetExternalTarget(null);
            return;
        }

        // If switching targets, release old claim
        if (CurrentTarget != null && CurrentTarget != target)
            ReleaseCurrentTarget();

        hasExternalTarget = true;
        externalTarget = target;
        CurrentTarget = target;

        // Slot claiming
        if (claimSlotIfPossible && CurrentTarget.Slots != null)
            CurrentTarget.Slots.TryClaim(self);
    }

    public void RetargetNow()
    {
        if (self == null || !self.isActiveAndEnabled) return;

        // External target has absolute priority
        if (hasExternalTarget)
        {
            if (IsTargetValid(externalTarget))
            {
                CurrentTarget = externalTarget;

                // Make sure we still hold a slot if needed
                if (CurrentTarget != null && CurrentTarget.Slots != null && !CurrentTarget.Slots.HasAttacker(self))
                    CurrentTarget.Slots.TryClaim(self);

                return;
            }

            // External became invalid
            SetExternalTarget(null);
            // Fall through to auto targeting only if enabled
        }

        if (!autoTargetingEnabled)
            return;

        // keep target if still valid
        if (stickToTarget && IsTargetValid(CurrentTarget))
        {
            // if target uses slots but we somehow lost claim, find new target
            if (CurrentTarget.Slots != null && !CurrentTarget.Slots.HasAttacker(self))
            {
                ReleaseCurrentTarget();
            }
            else
            {
                return; // keep it
            }
        }
        else
        {
            ReleaseCurrentTarget();
        }

        if (enforceTargetSlots)
        {
            CurrentTarget = TeamRegistry.FindAndClaim(self, transform.position, aggroRadius);
        }
        else
        {
            CurrentTarget = TeamRegistry.FindNearestHostile(self, transform.position, aggroRadius, false);
            // if it has slots, try claim but don't require it
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
