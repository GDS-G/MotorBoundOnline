# Vehicle Dynamics Prototype Research Brief

## Question

Can a compact, inspectable custom force model provide a credible and controllable rear-drive reference car while establishing the architecture needed for later measured-data calibration?

## Assumptions

- Three lateral ray samples per tire are an adequate first contact-patch approximation for the Phase 1 facility; this remains replaceable by a swept/contact-volume method.
- A spring-damper normal force, slip-stiffness tire response, load-sensitive peak friction, and combined-force ellipse are enough to expose meaningful setup and surface differences.
- Player/critical-vehicle integration runs at the GDD target of 360 Hz. Profiling must determine fidelity-tier and server budgets before that rate becomes a production guarantee.
- The Kiyora Aven is an original lightweight rear-drive roadster appropriate for the first reference vehicle.

## Acceptance criteria

- The vehicle launches, steers, brakes, shifts, and recovers without `WheelCollider`.
- Longitudinal and lateral forces never exceed the configured friction envelope.
- Wet surface grip produces a visible telemetry change and a meaningfully longer/slipperier response.
- Water-film response depends on depth, speed, and tire evacuation capability rather than a single universal wet multiplier.
- A physical rough-road strip exercises tire-width contact sampling and suspension response.
- The reference wheel passes all ten fitment stages; overload and pattern conflicts produce exact classifications and remedies.
- Torque interpolation and definition validation are deterministic and covered by EditMode tests.
- The Windows player builds without compile errors.

## Telemetry

Capture fixed-step duration, speed, RPM, gear, control inputs, driven-wheel slip ratio, front slip angle, grounded wheel count, surface grip, and longitudinal/lateral acceleration.

## Decision required after playtest

Continue, replace, or simplify each of: contact model, tire curve, drivetrain launch behavior, fixed-step rate, steering assist, and telemetry sampling.
