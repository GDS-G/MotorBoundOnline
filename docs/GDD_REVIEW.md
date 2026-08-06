# GDD Review and Development Start

Source reviewed: **MotorBound Online Game Design Document**, including all 20 child tabs nested beneath **MotorBound Online Game Design**, Google Drive document ID `1GwRPpUUJm0_amb49_rhrVoM1Qxt2Jr3-Gkd31TACFvE`.

## Product reading

The game's differentiator is not merely open-world racing. Its core promise is that vehicle construction, parts, workshop capacity, property, travel, business, and transactions all have physical or economic consequence. The initial implementation must therefore prove deep, explainable systems before attempting the beta's geographic scale.

The GDD's protected priority order is:

1. Vehicle physics, construction, parts, and workshop.
2. Persistent economy and unique property.
3. One excellent continuous region.
4. Cross-platform shared play.
5. Careers and businesses.

## Implementation consequences

- Simulation remains data-driven and server-safe; art assets are not required to answer mass, compatibility, price, or identity questions.
- SI units are authoritative, and unit conversions happen at presentation boundaries.
- Player vehicle physics uses a custom, testable force model rather than relying solely on Unity `WheelCollider`.
- Local full-fidelity simulation is designed as one tier in a future fidelity ladder.
- Stable IDs and versioned definitions are established before persistence or network services.
- Vehicle data preserves the Family → Generation → Platform → Body Shell → Chassis → Powertrain → Interior → Trim Manifest identity chain.
- Fitment reports one of the six GDD classifications only after identity, mount, static geometry, dynamic sweep, alignment, capacity, serviceability, control, regulatory, and visual-completeness checks.
- Unity packages and explicit assembly boundaries prevent the product from becoming one coupled `Assets` tree.
- The first map is a test facility, not an attempt to fake the 48 km by 32 km beta world.

## First milestone selected

The first coherent milestone combines Phase 0 repository foundations with the Phase 1 vehicle-dynamics prototype:

- One rear-drive Kiyora Aven reference car.
- Custom suspension contact using a three-ray tire-width sample and a tire-force model.
- Engine torque curve, gearbox, final drive, brakes, aero, and automatic shifting.
- A 360 Hz critical-vehicle step, dry asphalt, a speed-sensitive water-film patch, a physical rough-road strip, skidpad geometry, and recovery controls.
- On-screen telemetry for speed, RPM, gear, controls, tire slip, and surface.
- Pure EditMode tests for units, identity, torque interpolation, validation, and tire-force invariants.
- Repeatable project setup and Windows build automation.

This slice attacks Gate A (vehicle feel) while creating reusable foundations for fitment, parts, network state, audio state, and telemetry.

## Deferred by the risk-first roadmap

The remaining tabs materially expand world corridors, property, workshop interaction, audio synthesis, body and lighting systems, condition and restoration, AVIN/DSR history, insurance, and market channels. Those requirements are recorded as downstream consumers of the same stable identities and component records; they are not being collapsed into the vehicle-feel prototype before Gate A is evaluated.
