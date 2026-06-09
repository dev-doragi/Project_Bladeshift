# BladeShift Refactoring Guide

## Goal
- Keep platformer combat feel intact while reducing coupling between player, weapon, and enemy systems.
- Favor small gameplay-safe extractions over broad rewrites.
- Keep `Bootstrapper + Singleton + EventBus` as the project spine for this phase.

## Current Target Architecture
- **Input**
  - `InputReader` only reads input and emits events/commands.
  - Low-level input events may remain, but gameplay code should prefer command events.
  - Command layer:
    - `PlayerLocomotionCommand`
    - `WeaponActionCommand`
- **Player**
  - `PlayerController` orchestrates movement intent and publishes player context.
  - `PlatformerMotor2D` owns jump, dash, coyote time, gravity, and movement feel.
  - `PlayerAimResolver` owns facing/aim resolution so animation, weapon, and camera read one source of truth.
  - `MotorAbilityPolicy2D` owns dash collision/layer side effects.
- **Weapon**
  - `WeaponController` is an orchestrator, not a rules dump.
  - `WeaponActionRouter` bridges command events to action modules.
  - `WeaponActionService` owns action dispatch and availability checks.
  - `WeaponAimPresentation` owns cursor suppression and visibility policy.
  - `CollisionPolicyService` owns shared `Physics2D.IgnoreLayerCollision` calls.
- **Enemy**
  - `EnemyController` owns detection and basic chase movement.
  - Pattern behavior should live in separate components implementing `IEnemyBehaviour`.
  - `ChargeAttackBehaviour` is the reference example for heavy enemy charge logic.
  - `EnemyBase` still owns combat state, but contact damage and death sequence are already split into helpers.

## Practical Rules
- Do not change movement tuning numbers unless the task is explicitly about feel.
- Prefer direct references for close gameplay interactions and `EventBus` only for scene-wide reactions.
- Avoid new static gameplay references; bind through scene references, spawn events, or bootstrap-time discovery.
- If a MonoBehaviour exceeds one gameplay responsibility, extract behavior first, then consider data extraction.

## Recommended Next Refactors
- Split `EnemyBase` further into:
  - `EnemyGroggyHandler`
  - `EnemyDamageReaction`
  - `EnemyDeathState`
- Convert ranged/flying enemy attack routines into `IEnemyBehaviour` components.
- Replace remaining duplicated target lookup patterns with a shared player-target binder.
- Move weapon mode-switch transition rules out of `WeaponController` into a dedicated transition service.

## Manual Verification
- Player:
  - move, jump, short-hop, fall, dash, input block/unblock
- Weapon:
  - melee/remote toggle, cursor visibility, pinned wall return, energy recovery
- Enemy:
  - detect player after spawn, chase, heavy charge warmup/charge/cancel, groggy interrupt, death fade
- Flow:
  - player death, stage fail, scene re-entry, duplicate singleton absence
