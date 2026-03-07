using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ShipOrderController))]
public class EnemySquadMember : MonoBehaviour
{
    [SerializeField] private EnemySquadRole role = EnemySquadRole.Wingman;
    [SerializeField] private int slotIndex = -1;

    private ShipOrderController orders;
    private bool clearingAssignment;

    public EnemySquadController Squad { get; private set; }
    public EnemySquadRole Role => role;
    public int SlotIndex => slotIndex;

    private void Awake()
    {
        orders = GetComponent<ShipOrderController>();
    }

    private void OnDisable()
    {
        if (clearingAssignment || Squad == null)
            return;

        Squad.UnregisterMember(this);
    }

    public bool TryGetTravelGoal(Vector2 myPos, out Vector2 goalPos, out float throttle01)
    {
        goalPos = myPos;
        throttle01 = 0f;

        return Squad != null && Squad.TryGetTravelGoal(this, myPos, out goalPos, out throttle01);
    }

    public bool TryGetCombatGoal(
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

        return Squad != null &&
               Squad.TryGetCombatGoal(this, myPos, targetPos, out goalPos, out throttle01, out blendWeight,
                   out suppressAttackRuns);
    }

    internal void SetSquad(EnemySquadController squad, EnemySquadRole assignedRole, int assignedSlotIndex)
    {
        Squad = squad;
        role = assignedRole;
        slotIndex = assignedSlotIndex;

        if (orders == null)
            orders = GetComponent<ShipOrderController>();

        if (orders != null)
            orders.SetSquadMember(this);
    }

    internal void ClearSquad(EnemySquadController squad)
    {
        if (Squad != squad)
            return;

        clearingAssignment = true;

        Squad = null;
        slotIndex = -1;

        if (orders == null)
            orders = GetComponent<ShipOrderController>();

        if (orders != null)
            orders.SetSquadMember(null);

        clearingAssignment = false;
    }
}
