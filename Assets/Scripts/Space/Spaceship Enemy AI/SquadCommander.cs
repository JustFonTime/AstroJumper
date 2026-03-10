using System;
using UnityEngine;

[Obsolete("SquadCommander is deprecated. Use squad/spawner reinforcement contracts (team-agnostic AI layer) instead.", false)]
public class SquadCommander : MonoBehaviour
{
    [Header("Legacy")]
    [SerializeField] private bool legacyModeEnabled = false;

    [Header("Refs")] [SerializeField] private TeamAgent playerTeam;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera cam;

    [Header("Command Keys")] [SerializeField]
    private KeyCode escortKey = KeyCode.Alpha1; // default keys: 1-3 for orders, F for focus fire, r for regroup

    [SerializeField] private KeyCode protectKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode skirmishKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode regroupKey = KeyCode.R;
    [SerializeField] private KeyCode focusFireKey = KeyCode.F;

    [Header("Focus Fire Pick")] [SerializeField]
    private LayerMask shipMask = ~0;

    [SerializeField] private float pickRadius = 3f;

    private readonly Collider2D[] pickHits = new Collider2D[24];

    private void Awake()
    {
        if (!playerTransform) playerTransform = transform;
        if (!cam) cam = Camera.main;
        if (!playerTeam) playerTeam = GetComponent<TeamAgent>();
    }

    private void Update()
    {
        if (!legacyModeEnabled)
            return;

        if (!playerTeam) return;

        if (Input.GetKeyDown(escortKey))
            IssueEscort();

        if (Input.GetKeyDown(protectKey))
            IssueProtect();

        if (Input.GetKeyDown(skirmishKey))
            IssueSkirmish();

        if (Input.GetKeyDown(regroupKey))
            IssueRegroup();

        if (Input.GetKeyDown(focusFireKey))
            IssueFocusFireAtMouse();
    }

    private void IssueEscort()
    {
        if (!legacyModeEnabled)
            return;

        foreach (var ship in ShipOrderController.Active)
        {
            if (!IsMyTeammate(ship)) continue;
            ship.IssueEscort(playerTransform);
        }

        Debug.Log("Squad: ESCORT");
    }

    private void IssueProtect()
    {
        if (!legacyModeEnabled)
            return;

        foreach (var ship in ShipOrderController.Active)
        {
            if (!IsMyTeammate(ship)) continue;
            ship.IssueProtect(playerTransform);
        }

        Debug.Log("Squad: PROTECT");
    }

    private void IssueSkirmish()
    {
        if (!legacyModeEnabled)
            return;

        foreach (var ship in ShipOrderController.Active)
        {
            if (!IsMyTeammate(ship)) continue;
            ship.IssueSkirmish();
        }

        Debug.Log("Squad: SKIRMISH");
    }

    private void IssueRegroup()
    {
        if (!legacyModeEnabled)
            return;

        foreach (var ship in ShipOrderController.Active)
        {
            if (!IsMyTeammate(ship)) continue;
            ship.IssueRegroup(playerTransform);
        }

        Debug.Log("Squad: REGROUP");
    }

    private void IssueFocusFireAtMouse()
    {
        if (!legacyModeEnabled)
            return;

        TeamAgent target = PickHostileNearMouse();
        if (target == null)
        {
            Debug.Log("FocusFire: no hostile found near mouse.");
            return;
        }

        foreach (var ship in ShipOrderController.Active)
        {
            if (!IsMyTeammate(ship)) continue;
            ship.IssueFocusFire(target);
        }

        Debug.Log($"Squad: FOCUS FIRE -> {target.name}");
    }

    private bool IsMyTeammate(ShipOrderController ship)
    {
        if (ship == null) return false;
        if (!ship.isActiveAndEnabled) return false;
        if (!ship.SquadControllable) return false;
        return ship.TeamId == playerTeam.TeamId;
    }

    private TeamAgent PickHostileNearMouse()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return null;

        Vector3 mouse = Input.mousePosition;
        float zDist = Mathf.Abs(playerTransform.position.z - cam.transform.position.z);
        mouse.z = zDist;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(mouse);

        int count = Physics2D.OverlapCircleNonAlloc(mouseWorld, pickRadius, pickHits, shipMask);
        if (count <= 0) return null;

        TeamAgent best = null;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            var col = pickHits[i];
            if (col == null) continue;

            var ta = col.GetComponentInParent<TeamAgent>();
            if (ta == null) continue;
            if (!ta.isActiveAndEnabled) continue;
            if (!TeamRegistry.IsHostile(playerTeam.TeamId, ta.TeamId)) continue;

            float d2 = ((Vector2)ta.transform.position - mouseWorld).sqrMagnitude;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = ta;
            }
        }

        return best;
    }
}

