# 0002 — Incorporate the expanded GDD through vehicle architecture and contact fidelity

Status: Accepted for Phase 1 prototype

## Context

The design document now separates 20 system domains and specifies a layered vehicle identity, a ten-stage physical fitment pipeline, a 360 Hz target for player and critical vehicles, contact wider than one infinitely thin ray, and explicit rough-road and water-patch validation.

## Decision

- Preserve stable IDs for family, generation, platform, body shell, chassis configuration, powertrain configuration, interior configuration, and trim manifest.
- Classify installation only after identity, mount, static geometry, dynamic sweep, alignment, capacity, serviceability, control, regulatory, and visual-completeness checks.
- Run the Phase 1 player prototype at 360 Hz and expose the effective rate in telemetry.
- Sample three rays across tire section width and report the number of grounded samples.
- Represent wetness as water-film depth evaluated with speed and tire evacuation capability; keep visual water independent from physics state.
- Add physical roughness geometry and a separate roughness state to the test facility.

## Consequences

The prototype better attacks the GDD's vehicle-feel and construction risks, but 360 Hz cost and the simplified contact/water equations remain hypotheses. They require profiling and measured-data calibration before production. Fitment records are server-safe and can later feed workshop, marketplace, inspection, damage, audio, insurance, and Digital Service Record systems without those systems being implemented prematurely.
