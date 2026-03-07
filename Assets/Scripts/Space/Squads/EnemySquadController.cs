using System.Collections.Generic;
using UnityEngine;

public class EnemySquadController : MonoBehaviour
{
    [Header("Squad")]
    [SerializeField] private EnemySquadFormationType formationType = EnemySquadFormationType.Vee;
    [SerializeField] private EnemySquadState currentState = EnemySquadState.Engage;
    [SerializeField] private float slotSpacing = 5f;

    [Header("Anchor Motion")]
    [SerializeField] private float anchorMoveSpeed = 12f;
    [SerializeField] private float engageDistanceFromTarget = 18f;
    [SerializeField] private float regroupDistanceFromTarget = 26f;

    [Header("Combat Influence")]
    [Range(0f, 1f)] [SerializeField] private float engageBlendWeight = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float regroupBlendWeight = 1f;

    private readonly List<EnemySquadMember> members = new List<EnemySquadMember>(8);
    private Transform focusTarget;

    public EnemySquadFormationType FormationType => formationType;
    public EnemySquadState CurrentState => currentState;
    public Transform FocusTarget => focusTarget;
    public int MemberCount => members.Count;

    public void Initialize(
        Transform target,
        EnemySquadFormationType formation,
        float spacing,
        EnemySquadState initialState,
        float engageDistance,
        float moveSpeed)
    {
        focusTarget = target;
        formationType = formation;
        slotSpacing = Mathf.Max(1f, spacing);
        currentState = initialState;
        engageDistanceFromTarget = Mathf.Max(1f, engageDistance);
        anchorMoveSpeed = Mathf.Max(0.1f, moveSpeed);
    }

    private void FixedUpdate()
    {
        UpdateAnchorPosition();
    }

    public void SetState(EnemySquadState nextState)
    {
        currentState = nextState;
    }

    public void SetFocusTarget(Transform target)
    {
        focusTarget = target;
    }

    public void RegisterMember(EnemySquadMember member, EnemySquadRole role)
    {
        if (member == null)
            return;

        if (!members.Contains(member))
            members.Add(member);

        RefreshMemberSlots(roleOverrideMember: member, roleOverride: role);
    }

    public void UnregisterMember(EnemySquadMember member)
    {
        if (member == null)
            return;

        if (members.Remove(member))
            member.ClearSquad(this);

        if (members.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        RefreshMemberSlots();
    }

    public Vector3 GetPreviewSlotWorldPosition(int slotIndex)
    {
        Vector2 offset = GetSlotOffset(slotIndex, Mathf.Max(1, slotIndex + 1));
        return transform.position + (Vector3)offset;
    }

    public bool TryGetTravelGoal(EnemySquadMember member, Vector2 myPos, out Vector2 goalPos, out float throttle01)
    {
        goalPos = myPos;
        throttle01 = 0f;

        if (member == null)
            return false;

        switch (currentState)
        {
            case EnemySquadState.FormUp:
            case EnemySquadState.Regroup:
                goalPos = GetWorldSlotPosition(member.SlotIndex);
                throttle01 = 1f;
                return true;

            case EnemySquadState.Retreat:
                if (focusTarget == null)
                    return false;

                Vector2 away = ((Vector2)transform.position - (Vector2)focusTarget.position).normalized;
                if (away.sqrMagnitude < 0.001f)
                    away = Vector2.up;

                goalPos = myPos + away * Mathf.Max(regroupDistanceFromTarget, slotSpacing * 2f);
                throttle01 = 1f;
                return true;
        }

        return false;
    }

    public bool TryGetCombatGoal(
        EnemySquadMember member,
        Vector2 myPos,
        Vector2 targetPos,
        out Vector2 goalPos,
        out float throttle01,
        out float blendWeight,
        out bool suppressAttackRuns)
    {
        goalPos = myPos;
        throttle01 = 0f;
        blendWeight = 0f;
        suppressAttackRuns = false;

        if (member == null)
            return false;

        switch (currentState)
        {
            case EnemySquadState.FormUp:
            case EnemySquadState.Regroup:
                goalPos = GetWorldSlotPosition(member.SlotIndex);
                throttle01 = 1f;
                blendWeight = regroupBlendWeight;
                suppressAttackRuns = true;
                return true;

            case EnemySquadState.Retreat:
            {
                Vector2 away = (myPos - targetPos).normalized;
                if (away.sqrMagnitude < 0.001f)
                    away = Vector2.up;

                goalPos = myPos + away * Mathf.Max(regroupDistanceFromTarget, slotSpacing * 2f);
                throttle01 = 1f;
                blendWeight = 1f;
                suppressAttackRuns = true;
                return true;
            }

            case EnemySquadState.Engage:
                goalPos = GetCombatSlotPosition(member.SlotIndex, targetPos);
                throttle01 = member.Role == EnemySquadRole.ShieldSupport ? 0.75f : 0.9f;
                blendWeight = engageBlendWeight;
                suppressAttackRuns = member.Role == EnemySquadRole.ShieldSupport ||
                                     member.Role == EnemySquadRole.Support;
                return true;
        }

        return false;
    }

    private void UpdateAnchorPosition()
    {
        if (focusTarget == null)
            return;

        Vector2 focusPos = focusTarget.position;
        Vector2 currentPos = transform.position;
        Vector2 fromTarget = currentPos - focusPos;

        if (fromTarget.sqrMagnitude < 0.001f)
            fromTarget = Vector2.down;

        float desiredDistance = currentState == EnemySquadState.Engage
            ? engageDistanceFromTarget
            : regroupDistanceFromTarget;

        Vector2 desiredPos = focusPos + fromTarget.normalized * desiredDistance;
        Vector2 nextPos = Vector2.MoveTowards(currentPos, desiredPos, anchorMoveSpeed * Time.fixedDeltaTime);
        transform.position = nextPos;
    }

    private Vector2 GetWorldSlotPosition(int slotIndex)
    {
        Vector2 offset = GetSlotOffset(slotIndex, members.Count);
        return (Vector2)transform.position + offset;
    }

    private Vector2 GetCombatSlotPosition(int slotIndex, Vector2 targetPos)
    {
        Vector2 forward = targetPos - (Vector2)transform.position;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector2.up;

        float angleDeg = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angleDeg);
        Vector2 localOffset = GetSlotOffset(slotIndex, members.Count);
        Vector2 formationCenter = targetPos - forward.normalized * engageDistanceFromTarget;

        return formationCenter + (Vector2)(rotation * localOffset);
    }

    private void RefreshMemberSlots(EnemySquadMember roleOverrideMember = null, EnemySquadRole roleOverride = EnemySquadRole.Wingman)
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember member = members[i];
            if (member == null)
                continue;

            EnemySquadRole assignedRole = member == roleOverrideMember ? roleOverride : member.Role;
            member.SetSquad(this, assignedRole, i);
        }
    }

    private Vector2 GetSlotOffset(int slotIndex, int totalMembers)
    {
        float spacing = Mathf.Max(1f, slotSpacing);

        switch (formationType)
        {
            case EnemySquadFormationType.Line:
            {
                float centerOffset = (Mathf.Max(1, totalMembers) - 1) * 0.5f;
                return new Vector2((slotIndex - centerOffset) * spacing, 0f);
            }

            case EnemySquadFormationType.Diamond:
            {
                Vector2[] points =
                {
                    new Vector2(0f, 0f),
                    new Vector2(-1f, -1f),
                    new Vector2(1f, -1f),
                    new Vector2(0f, -2f),
                    new Vector2(0f, 1f)
                };
                return points[Mathf.Clamp(slotIndex, 0, points.Length - 1)] * spacing;
            }

            case EnemySquadFormationType.Ring:
            {
                float count = Mathf.Max(1, totalMembers);
                float angle = (Mathf.PI * 2f * slotIndex) / count;
                float radius = Mathf.Max(spacing, spacing * 0.75f * count / Mathf.PI);
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            case EnemySquadFormationType.Escort:
            {
                Vector2[] points =
                {
                    new Vector2(0f, 0f),
                    new Vector2(-1.25f, -0.85f),
                    new Vector2(1.25f, -0.85f),
                    new Vector2(-2f, -1.75f),
                    new Vector2(2f, -1.75f)
                };
                return points[Mathf.Clamp(slotIndex, 0, points.Length - 1)] * spacing;
            }

            case EnemySquadFormationType.Vee:
            default:
            {
                Vector2[] points =
                {
                    new Vector2(0f, 0f),
                    new Vector2(-1f, -1f),
                    new Vector2(1f, -1f),
                    new Vector2(-2f, -2f),
                    new Vector2(2f, -2f)
                };
                return points[Mathf.Clamp(slotIndex, 0, points.Length - 1)] * spacing;
            }
        }
    }
}
