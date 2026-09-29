# 0003 — Establish assembly, dimensional-envelope, and build-plan authority

Status: Accepted for Phase 1 prototype

## Context

GDD tabs 21–26 add measured world clearances, physical parking and facilities, evidence-driven road law, system-grounded contracts, a vehicle-wide assembly contract, and explicit powertrain family/build-path requirements. The highest-risk common dependency is not a toll, pursuit, or mission UI. It is a trustworthy record of what the vehicle is, how large it is now, and whether a proposed build is complete.

## Decision

- Keep catalog part definitions separate from serialized physical part instances.
- Make a versioned `VehicleAssemblyManifest` authoritative for installed instances and typed cross-links. Mass and cost have explicit ownership so complete assemblies and their constituent parts cannot be counted twice.
- Tie every `VehicleOperatingEnvelope` to an exact manifest revision.
- Evaluate route and facility access using measured physical clearance, conservative safety margins, live vehicle dimensions, design class, trailer state, weight, ground clearance, approach/departure/breakover, and turning circle.
- Treat missing or contradictory clearance data as unverified. A posted height may be more conservative than physical clearance, but it may not promise more room than measured geometry safely provides.
- Store `PowertrainBuildPlan` separately from installed inventory. Resolve recursive dependencies, reject cycles and mutually exclusive selections, and emit a full bill of materials before installation.
- Extend the Kiyora Aven reference content first: an assembly manifest, operating envelope, verified passenger-garage profile, engineering-family identities, and a factory powertrain completion plan.

## Consequences

Navigation, garages, workshops, parking, contracts, equipment enforcement, insurance, audio, damage, and Digital Service Records can consume the same vehicle facts later. The current geometry and catalog values remain prototype baselines, not real-world certification or final production measurements. Dynamic swept paths, actual port/mate solving, detailed service routing, and server persistence still require later gates.
