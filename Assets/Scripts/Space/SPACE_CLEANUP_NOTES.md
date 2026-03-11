# Space Cleanup Notes

_Last updated: 2026-03-11_

## Active Core (Level 1 Scene)
These scripts directly drive the flagship-only battle loop in `Assets/Level/Scenes/Space/Level 1.unity`:

- `Assets/Scripts/Space/Spawning/SimpleTeamSpawner.cs`
- `Assets/Scripts/Space/Spawning/FleetSpawner.cs`
- `Assets/Scripts/Space/Component/SpaceArenaBoundaryController.cs`
- `Assets/Scripts/Space/Squads/SquadDebugHotkeys.cs`
- `Assets/Scripts/Space/Spaceship Player/Space CameraFollow.cs`

## Active Core (Space Prefabs)
These scripts are attached to active Space prefabs in the flagship mode:

- `Assets/Scripts/Space/Teams/TeamAgent.cs`
- `Assets/Scripts/Space/Teams/TargetSlots.cs`
- `Assets/Scripts/Space/Teams/TargetingComponent.cs`
- `Assets/Scripts/Space/Component/SpaceshipHealthComponent.cs`
- `Assets/Scripts/Space/Spaceship Enemy AI/Enemy Spaceship AI.cs`
- `Assets/Scripts/Space/Spaceship Enemy AI/Enemy Spaceship Combat AI.cs`
- `Assets/Scripts/Space/Spaceship Enemy AI/TriggerRelay2D.cs`
- `Assets/Scripts/Space/Spaceship Enemy AI/Kamakazy.cs`
- `Assets/Scripts/Space/Spaceship Enemy AI/Shield Giver.cs`
- `Assets/Scripts/Space/Flagship/FlagshipController.cs`
- `Assets/Scripts/Space/Flagship/FlagshipShieldNode.cs`
- `Assets/Scripts/Space/Flagship/FlagshipNoFlyZone.cs`
- `Assets/Scripts/Space/Flagship/FlagshipSlowMovement.cs`
- `Assets/Scripts/Space/Weapons/SpaceshipLaser.cs`
- `Assets/Scripts/Space/Weapons/Spaceship Spinning Mine.cs`

## Flagship-Only Refactor (Complete)
Wave/infinite game mode paths were removed. `FleetSpawner` now only handles:

- Team-based ship/squad spawning
- Reinforcement request queue + optional auto-fulfill
- Alive team tracking for HUD/debug
- Pooling via `PooledFleetShip`

## Removed In Cleanup
The following Space scripts/assets were removed as legacy or non-flagship mode:

- `Assets/Scripts/Space/Component/EnemyCount.cs`
- `Assets/Scripts/Space/Flagship/FlagshipBoardingTrigger.cs`
- `Assets/Scripts/Space/Spaceship AI/SpaceshipAI.cs`
- `Assets/Scripts/Space/Spaceship AI/SpaceshipCombatAI.cs`
- `Assets/Scripts/Space/Spaceship AI/ShipProfileSO.cs`
- `Assets/Scripts/Space/Spaceship Enemy AI/SquadCommander.cs`
- `Assets/Scripts/Space/Friendly Spaceship Spawner.cs`
- `Assets/Scripts/Space/Component/PooledTeamate.cs`
- `Assets/Scripts/Space/Spaceship Enemy AI/ShipOrderController.cs`
- `Assets/Scripts/Space/Enemy Spaceship Spawner.cs`
- `Assets/Scripts/Space/Enemy Spaceship Spawner Settings SO.cs`
- `Assets/Scripts/Space/Component/WaveSpawnSettings.cs`
- `Assets/Scripts/Space/Component/PooledEnemy.cs`
- `Assets/Scripts/Space/Squads/SquadController.cs`
- `Assets/Scripts/Space/Squads/SquadMember.cs`
- `Assets/Scripts/SO/EnemySpaceshipSpawnerSettingsSO.asset`

## Inspector + Debug Cleanup
- Focused custom inspectors are in `Assets/Editor/Space/SpaceFocusedInspectors.cs`.
- FleetSpawner and SimpleTeamSpawner inspectors now surface flagship-mode fields only.
- Active Level 1 debug overlays/radius gizmos default to off.
- Flagship movement/no-fly debug gizmos default to off on the prefab.
- Player and teammate prefab debug toggles default to off.

