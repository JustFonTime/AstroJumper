using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Space/Squads/Squad Debug Hotkeys")]
public class SquadDebugHotkeys : MonoBehaviour
{
    private enum SquadScope
    {
        AllTeams,
        PlayerTeamOnly,
        NonPlayerTeams
    }

    [Header("Hotkeys")]
    [SerializeField] private bool enableHotkeys = true;
    [SerializeField] private SquadScope controlScope = SquadScope.AllTeams;
    [SerializeField] private KeyCode cycleScopeKey = KeyCode.Tab;

    [Header("Formation Keys")]
    [SerializeField] private KeyCode veeKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode lineKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode diamondKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode ringKey = KeyCode.Alpha4;
    [SerializeField] private KeyCode escortKey = KeyCode.Alpha5;

    [Header("Spacing Keys")]
    [SerializeField] private KeyCode spacingDecreaseKey = KeyCode.LeftBracket;
    [SerializeField] private KeyCode spacingIncreaseKey = KeyCode.RightBracket;
    [SerializeField] private float spacingStep = 0.5f;

    [Header("State Keys")]
    [SerializeField] private KeyCode formUpKey = KeyCode.Z;
    [SerializeField] private KeyCode engageKey = KeyCode.X;
    [SerializeField] private KeyCode regroupKey = KeyCode.C;
    [SerializeField] private KeyCode retreatKey = KeyCode.V;

    [Header("Overlay")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F1;
    [SerializeField] private KeyCode toggleControlsCategoryKey = KeyCode.F2;
    [SerializeField] private KeyCode toggleSquadsCategoryKey = KeyCode.F3;
    [SerializeField] private KeyCode toggleSystemCategoryKey = KeyCode.F4;
    [SerializeField] private bool showControlsCategory = true;
    [SerializeField] private bool showSquadsCategory = true;
    [SerializeField] private bool showSystemCategory = true;
    [SerializeField] [Range(12, 36)] private int fontSize = 20;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Vector2 overlayPosition = new Vector2(14f, 14f);
    [SerializeField] private float overlayWidth = 900f;
    [SerializeField] private int maxSquadsToDisplay = 14;
    [SerializeField] private int maxRequestsToDisplay = 5;

    private readonly List<EnemySquadController> scopedSquads = new List<EnemySquadController>(64);
    private readonly StringBuilder overlayBuilder = new StringBuilder(4096);

    private GUIStyle overlayStyle;
    private GameObject player;
    private int playerTeamId;
    private float nextPlayerResolveTime;
    private string lastActionMessage = "None";
    private float lastActionTime = -999f;

    private void Start()
    {
        ResolvePlayerTeam();
    }

    private void Update()
    {
        HandleOverlayToggles();

        if (player == null && Time.time >= nextPlayerResolveTime)
            ResolvePlayerTeam();

        if (!enableHotkeys)
            return;

        HandleCommandHotkeys();
    }

    private void OnGUI()
    {
        if (!showOverlay)
            return;

        EnsureOverlayStyle();
        BuildOverlayText();

        float height = Mathf.Max(120f, Screen.height - (overlayPosition.y * 2f));
        GUI.Label(new Rect(overlayPosition.x, overlayPosition.y, overlayWidth, height), overlayBuilder.ToString(), overlayStyle);
    }

    private void HandleOverlayToggles()
    {
        if (Input.GetKeyDown(toggleOverlayKey))
            showOverlay = !showOverlay;

        if (Input.GetKeyDown(toggleControlsCategoryKey))
            showControlsCategory = !showControlsCategory;

        if (Input.GetKeyDown(toggleSquadsCategoryKey))
            showSquadsCategory = !showSquadsCategory;

        if (Input.GetKeyDown(toggleSystemCategoryKey))
            showSystemCategory = !showSystemCategory;
    }

    private void HandleCommandHotkeys()
    {
        if (Input.GetKeyDown(cycleScopeKey))
        {
            controlScope = NextScope(controlScope);
            RegisterAction($"Scope -> {GetScopeLabel(controlScope)}", CountScopedSquads());
        }

        if (Input.GetKeyDown(veeKey))
            ApplyFormation(EnemySquadFormationType.Vee, "Formation -> Vee");
        else if (Input.GetKeyDown(lineKey))
            ApplyFormation(EnemySquadFormationType.Line, "Formation -> Line");
        else if (Input.GetKeyDown(diamondKey))
            ApplyFormation(EnemySquadFormationType.Diamond, "Formation -> Diamond");
        else if (Input.GetKeyDown(ringKey))
            ApplyFormation(EnemySquadFormationType.Ring, "Formation -> Ring");
        else if (Input.GetKeyDown(escortKey))
            ApplyFormation(EnemySquadFormationType.Escort, "Formation -> Escort");

        if (Input.GetKeyDown(spacingDecreaseKey))
            ApplySpacing(-Mathf.Max(0.1f, spacingStep), "Spacing -");
        else if (Input.GetKeyDown(spacingIncreaseKey))
            ApplySpacing(Mathf.Max(0.1f, spacingStep), "Spacing +");

        if (Input.GetKeyDown(formUpKey))
            ApplyState(EnemySquadState.FormUp, "State -> FormUp");
        else if (Input.GetKeyDown(engageKey))
            ApplyState(EnemySquadState.Engage, "State -> Engage");
        else if (Input.GetKeyDown(regroupKey))
            ApplyState(EnemySquadState.Regroup, "State -> Regroup");
        else if (Input.GetKeyDown(retreatKey))
            ApplyState(EnemySquadState.Retreat, "State -> Retreat");
    }

    private void ApplyFormation(EnemySquadFormationType formation, string actionLabel)
    {
        int affected = ApplyToScopedSquads(squad => squad.SetFormation(formation));
        RegisterAction(actionLabel, affected);
    }

    private void ApplySpacing(float delta, string actionLabel)
    {
        int affected = ApplyToScopedSquads(squad => squad.AdjustSlotSpacing(delta));
        RegisterAction(actionLabel, affected);
    }

    private void ApplyState(EnemySquadState state, string actionLabel)
    {
        int affected = ApplyToScopedSquads(squad => squad.SetState(state));
        RegisterAction(actionLabel, affected);
    }

    private int ApplyToScopedSquads(Action<EnemySquadController> action)
    {
        int count = 0;
        IReadOnlyList<EnemySquadController> activeSquads = EnemySquadController.Active;

        for (int i = 0; i < activeSquads.Count; i++)
        {
            EnemySquadController squad = activeSquads[i];
            if (!IsEligibleSquad(squad))
                continue;

            action(squad);
            count++;
        }

        return count;
    }

    private int CountScopedSquads()
    {
        int count = 0;
        IReadOnlyList<EnemySquadController> activeSquads = EnemySquadController.Active;

        for (int i = 0; i < activeSquads.Count; i++)
        {
            if (IsEligibleSquad(activeSquads[i]))
                count++;
        }

        return count;
    }

    private bool IsEligibleSquad(EnemySquadController squad)
    {
        if (squad == null || !squad.isActiveAndEnabled)
            return false;

        switch (controlScope)
        {
            case SquadScope.PlayerTeamOnly:
                return squad.TeamId == playerTeamId;

            case SquadScope.NonPlayerTeams:
                return squad.TeamId != playerTeamId;

            case SquadScope.AllTeams:
            default:
                return true;
        }
    }

    private void FillScopedSquadBuffer()
    {
        scopedSquads.Clear();
        IReadOnlyList<EnemySquadController> activeSquads = EnemySquadController.Active;

        for (int i = 0; i < activeSquads.Count; i++)
        {
            EnemySquadController squad = activeSquads[i];
            if (IsEligibleSquad(squad))
                scopedSquads.Add(squad);
        }
    }

    private void BuildOverlayText()
    {
        FillScopedSquadBuffer();

        overlayBuilder.Clear();
        overlayBuilder.AppendLine("SQUAD DEBUG");
        overlayBuilder.AppendLine($"Scope: {GetScopeLabel(controlScope)} | Player Team: {playerTeamId} | Scoped Squads: {scopedSquads.Count}");

        if (Time.time - lastActionTime <= 7f)
            overlayBuilder.AppendLine($"Last Action: {lastActionMessage}");

        if (showControlsCategory)
        {
            overlayBuilder.AppendLine();
            overlayBuilder.AppendLine("[Controls]");
            overlayBuilder.AppendLine($"{toggleOverlayKey}: overlay  {toggleControlsCategoryKey}: controls  {toggleSquadsCategoryKey}: squads  {toggleSystemCategoryKey}: system");
            overlayBuilder.AppendLine($"{cycleScopeKey}: cycle scope");
            overlayBuilder.AppendLine($"{veeKey}/{lineKey}/{diamondKey}/{ringKey}/{escortKey}: Vee/Line/Diamond/Ring/Escort");
            overlayBuilder.AppendLine($"{spacingDecreaseKey}/{spacingIncreaseKey}: spacing -/+ ({spacingStep:0.0})");
            overlayBuilder.AppendLine($"{formUpKey}/{engageKey}/{regroupKey}/{retreatKey}: FormUp/Engage/Regroup/Retreat");
        }

        if (showSquadsCategory)
        {
            overlayBuilder.AppendLine();
            overlayBuilder.AppendLine("[Squads]");

            if (scopedSquads.Count == 0)
            {
                overlayBuilder.AppendLine("No squads in current scope.");
            }
            else
            {
                int shown = Mathf.Min(maxSquadsToDisplay, scopedSquads.Count);
                for (int i = 0; i < shown; i++)
                {
                    EnemySquadController squad = scopedSquads[i];
                    EnemySquadMember leader = squad.LeaderMember;
                    string leaderName = leader != null ? leader.name : "none";
                    string focusName = squad.FocusTarget != null ? squad.FocusTarget.name : "none";

                    overlayBuilder.AppendLine(
                        $"[{squad.name}] T{squad.TeamId} {squad.FormationType}/{squad.CurrentState} m={squad.MemberCount}/{squad.DesiredMemberCount} spacing={squad.SlotSpacing:0.0} leader={leaderName} focus={focusName}");

                    if (squad.IsUnderStrength)
                    {
                        overlayBuilder.AppendLine(
                            $"  under-strength: missing={squad.MissingMemberCount} nextReq={squad.SecondsUntilNextReinforcementRequest:0.0}s");
                    }
                }

                if (scopedSquads.Count > shown)
                    overlayBuilder.AppendLine($"... +{scopedSquads.Count - shown} more squads");
            }
        }

        if (showSystemCategory)
        {
            overlayBuilder.AppendLine();
            overlayBuilder.AppendLine("[System]");

            FleetSpawner fleet = FleetSpawner.Instance;
            if (fleet == null)
            {
                overlayBuilder.AppendLine("FleetSpawner: not found");
            }
            else
            {
                overlayBuilder.AppendLine(
                    $"FleetSpawner: wave={fleet.CurrentWave} trackedEnemies={fleet.AliveTrackedEnemies} pendingRequests={fleet.PendingReinforcementRequestCount}");

                IReadOnlyList<ReinforcementRequest> pending = fleet.PendingReinforcementRequests;
                int shown = Mathf.Min(maxRequestsToDisplay, pending.Count);
                for (int i = 0; i < shown; i++)
                {
                    ReinforcementRequest request = pending[i];
                    string squadName = request.Squad != null ? request.Squad.name : "null";
                    string focusName = request.FocusTarget != null ? request.FocusTarget.name : "none";
                    overlayBuilder.AppendLine(
                        $"  Req {i + 1}: squad={squadName} team={request.TeamId} missing={request.MissingCount} focus={focusName}");
                }

                if (pending.Count > shown)
                    overlayBuilder.AppendLine($"  ... +{pending.Count - shown} more pending requests");
            }
        }
    }

    private void RegisterAction(string actionLabel, int affectedSquads)
    {
        lastActionMessage = $"{actionLabel} (affected {affectedSquads})";
        lastActionTime = Time.time;
    }

    private void ResolvePlayerTeam()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            playerTeamId = 0;
            nextPlayerResolveTime = Time.time + 2f;
            return;
        }

        TeamAgent teamAgent = player.GetComponent<TeamAgent>();
        playerTeamId = teamAgent != null ? teamAgent.TeamId : 0;
        nextPlayerResolveTime = Time.time + 5f;
    }

    private void EnsureOverlayStyle()
    {
        if (overlayStyle != null && overlayStyle.fontSize == fontSize && overlayStyle.normal.textColor == textColor)
            return;

        overlayStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = false,
            wordWrap = false,
            alignment = TextAnchor.UpperLeft
        };

        overlayStyle.normal.textColor = textColor;
    }

    private static SquadScope NextScope(SquadScope scope)
    {
        switch (scope)
        {
            case SquadScope.AllTeams:
                return SquadScope.PlayerTeamOnly;

            case SquadScope.PlayerTeamOnly:
                return SquadScope.NonPlayerTeams;

            case SquadScope.NonPlayerTeams:
            default:
                return SquadScope.AllTeams;
        }
    }

    private static string GetScopeLabel(SquadScope scope)
    {
        switch (scope)
        {
            case SquadScope.PlayerTeamOnly:
                return "Player Team";

            case SquadScope.NonPlayerTeams:
                return "Non-Player Teams";

            case SquadScope.AllTeams:
            default:
                return "All Teams";
        }
    }
}
