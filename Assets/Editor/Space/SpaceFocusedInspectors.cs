using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal abstract class SpaceFocusedInspectorBase : Editor
{
    private bool showAdvanced;
    private bool showCore = true;

    protected abstract string[] CoreProperties { get; }
    protected virtual string InspectorTitle => target != null ? target.GetType().Name : "Focused Inspector";
    protected virtual string CoreHelpText => "Core gameplay fields are shown below. Expand Advanced for the full config.";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptReference();
        EditorGUILayout.Space(2f);

        EditorGUILayout.LabelField(InspectorTitle, EditorStyles.boldLabel);
        if (!string.IsNullOrWhiteSpace(CoreHelpText))
            EditorGUILayout.HelpBox(CoreHelpText, MessageType.None);

        showCore = EditorGUILayout.Foldout(showCore, "Core", true);
        if (showCore)
        {
            EditorGUI.indentLevel++;
            DrawCoreProperties();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4f);
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced", true);
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            DrawAdvancedProperties();
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptReference()
    {
        MonoBehaviour mono = target as MonoBehaviour;
        if (mono == null)
            return;

        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromMonoBehaviour(mono);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }
    }

    private void DrawCoreProperties()
    {
        string[] core = CoreProperties;
        if (core == null || core.Length == 0)
        {
            EditorGUILayout.LabelField("No core properties configured.");
            return;
        }

        for (int i = 0; i < core.Length; i++)
        {
            string propertyName = core[i];
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                continue;

            EditorGUILayout.PropertyField(property, includeChildren: true);
        }
    }

    private void DrawAdvancedProperties()
    {
        HashSet<string> coreSet = new HashSet<string>(CoreProperties ?? new string[0]);

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == "m_Script")
                continue;

            if (coreSet.Contains(iterator.propertyPath))
                continue;

            EditorGUILayout.PropertyField(iterator, includeChildren: true);
        }
    }
}

[CustomEditor(typeof(SimpleTeamSpawner))]
[CanEditMultipleObjects]
internal sealed class SimpleTeamSpawnerFocusedInspector : SpaceFocusedInspectorBase
{
    protected override string InspectorTitle => "Simple Team Spawner";

    protected override string CoreHelpText =>
        "Use these fields for day-to-day battle setup. Advanced contains orbit tuning and debug visuals.";

    protected override string[] CoreProperties => new[]
    {
        "autoSpawnOnStart",
        "clearExistingBattleOnSpawn",
        "updateObjectiveProxies",
        "enableSpawnHotkey",
        "spawnBattleKey",
        "flagshipPrefab",
        "sharedShipPrefab",
        "playerTeamShipPrefab",
        "enemyTeamShipPrefab",
        "squadsPerTeam",
        "shipsPerSquad",
        "formationType",
        "squadState",
        "squadSpacing",
        "squadEngageDistance",
        "squadAnchorMoveSpeed",
        "playerFlagshipSpawnPoint",
        "enemyFlagshipSpawnPoint",
        "useArenaBoundaryIfAvailable",
        "boundarySpawnRadiusRatio",
        "fallbackFlagshipSeparation",
        "fleetSpawnRadiusAroundFlagship",
        "enableFlagshipSlowMovement",
        "hookReinforcementRequests",
        "disableFleetSpawnerAutoFulfill",
        "reinforcementSpawnRadius",
        "maxReinforcementsPerRequest"
    };
}

[CustomEditor(typeof(FleetSpawner))]
[CanEditMultipleObjects]
internal sealed class FleetSpawnerFocusedInspector : SpaceFocusedInspectorBase
{
    protected override string InspectorTitle => "Fleet Spawner";

    protected override string CoreHelpText =>
        "Core fleet/team/reinforcement setup for flagship battles. Advanced contains pooling and deeper squad tuning.";

    protected override string[] CoreProperties => new[]
    {
        "teamConfigs",
        "fallbackShipPrefab",
        "defaultFocusTarget",
        "autoAssignShipsToSquads",
        "defaultAutoSquadMaxMembers",
        "defaultAutoFormationType",
        "defaultAutoSquadState",
        "defaultAutoSquadSpacing",
        "defaultAutoSquadEngageDistance",
        "defaultAutoSquadAnchorMoveSpeed",
        "squadSeparationDistance",
        "enableReinforcementRequests",
        "autoFulfillReinforcementRequests",
        "maxPendingReinforcementRequests",
        "defaultRequestDelaySeconds",
        "defaultRequestCooldownSeconds",
        "trackedEnemyTeamIds",
        "defaultPoolCapacity",
        "maxPoolSize"
    };
}

[CustomEditor(typeof(EnemySquadController))]
[CanEditMultipleObjects]
internal sealed class EnemySquadControllerFocusedInspector : SpaceFocusedInspectorBase
{
    protected override string InspectorTitle => "Enemy Squad Controller";

    protected override string CoreHelpText =>
        "Core formation/state/reinforcement and high-level pathing toggles. Advanced contains detailed movement tuning.";

    protected override string[] CoreProperties => new[]
    {
        "formationType",
        "slotSpacing",
        "maxFormationMembers",
        "leaderDesiredDistanceFromFocus",
        "leaderArriveDistance",
        "leaderFullThrottleDistance",
        "desiredMemberCount",
        "requestDelaySeconds",
        "requestCooldownSeconds",
        "enableReinforcementRequests",
        "currentState",
        "useHostileEngagementArea",
        "useWaypointEngagementPaths",
        "useLeaderCorridorPath",
        "usePredictiveLeaderCollisionAvoidance",
        "useSquadLaneOffsets",
        "paceLeaderToFollowers",
        "drawNavigationDebug",
        "drawNavigationOnlyWhenSelected",
        "drawNavigationLabels"
    };
}

[CustomEditor(typeof(EnemySpaceshipAI))]
[CanEditMultipleObjects]
internal sealed class EnemySpaceshipAIFocusedInspector : SpaceFocusedInspectorBase
{
    protected override string InspectorTitle => "Enemy Spaceship AI";

    protected override string CoreHelpText =>
        "Core targeting/chase behavior and key steering toggles. Advanced contains detailed avoidance probe tuning.";

    protected override string[] CoreProperties => new[]
    {
        "player",
        "shipProfile",
        "fallbackChaseDistance",
        "fallbackArriveDistance",
        "fallbackFullThrottleDistance",
        "minimumThrottleWhenMoving",
        "useLocalAvoidance",
        "useLineOfSightSteering",
        "useFlagshipNoFlyZones",
        "drawDebug",
        "drawOnlyWhenSelected"
    };
}

[CustomEditor(typeof(EnemySpaceshipCombatAI))]
[CanEditMultipleObjects]
internal sealed class EnemySpaceshipCombatAIFocusedInspector : SpaceFocusedInspectorBase
{
    protected override string InspectorTitle => "Enemy Spaceship Combat AI";

    protected override string CoreHelpText =>
        "Core firing points/turret behavior and safety toggles. Advanced contains extra fallback and debug ray settings.";

    protected override string[] CoreProperties => new[]
    {
        "firePoint",
        "turretPivots",
        "shipProfile",
        "laserPrefab",
        "orbitFirePointAroundShip",
        "rotateTurretPivotsTowardAim",
        "turretTurnSpeedDegPerSec",
        "avoidFriendlyFire",
        "retargetIfUnsafe",
        "drawSafetyRays",
        "drawTurretAimLines"
    };
}

[CustomEditor(typeof(FlagshipSlowMovement))]
[CanEditMultipleObjects]
internal sealed class FlagshipSlowMovementFocusedInspector : SpaceFocusedInspectorBase
{
    protected override string InspectorTitle => "Flagship Slow Movement";

    protected override string CoreHelpText =>
        "Core side-lane donut roaming controls and visible debug toggles. Advanced contains trail sampling/detail settings.";

    protected override string[] CoreProperties => new[]
    {
        "moveSpeed",
        "turnSpeedDegPerSec",
        "arriveDistance",
        "minRetargetDelay",
        "maxRetargetDelay",
        "useBoundaryControllerRoam",
        "boundaryController",
        "autoFindBoundaryController",
        "boundaryRoamRadiusRatio",
        "sidePreference",
        "sideBoundaryBuffer",
        "innerExclusionRadiusRatio",
        "innerExclusionPadding",
        "drawRoamRadiusDebug",
        "drawRoamRadiusOnlyWhenSelected",
        "drawPathTrailDebug",
        "drawPathForNonPlayerTeamsOnly",
        "drawCurrentTargetDebug"
    };
}

[CustomEditor(typeof(SpaceshipHealthComponent))]
[CanEditMultipleObjects]
internal sealed class SpaceshipHealthFocusedInspector : SpaceFocusedInspectorBase
{
    protected override string InspectorTitle => "Spaceship Health Component";

    protected override string CoreHelpText =>
        "Core health/shield/death VFX fields. Advanced contains baseline values used mostly for defaults or profile overrides.";

    protected override string[] CoreProperties => new[]
    {
        "isPlayer",
        "playerUpgradeState",
        "shipProfile",
        "shieldVFX",
        "deathSfx",
        "deathVolume",
        "deathVfxPrefab",
        "deathVfxDestroyPadding",
        "maxHealth",
        "maxShileds",
        "currentShields",
        "rechargeShieldDelay",
        "rechargeShieldRatePerHalfSecond"
    };
}


