# Contributing

## Principles

- Keep authoritative simulation data independent from scenes, prefabs, and presentation assets.
- Use SI units internally and name unit-bearing fields explicitly.
- Give persistent definitions stable IDs; display names are never identifiers.
- Do not introduce generic upgrade stages in place of physical components.
- Do not use `WheelCollider` for player-vehicle physics.
- Add an automated test for each pure simulation or validation rule.
- Record assumptions and prototype results in `docs/decisions`.

## Branches and commits

Use focused branches under `codex/` for agent work and descriptive imperative commit messages. Keep generated build output out of source control.

## Definition of done for prototypes

A prototype change includes its assumptions, acceptance criteria, telemetry, automated tests where practical, and a written continue/replace/simplify recommendation.
