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

## Handling references reviewed — October 6, 2026

The user requested comparisons with Need for Speed, Test Drive Unlimited, and Motor City Online after the combined-slip update made ordinary driving too loose. These sources document design intentions and player controls, not proprietary force equations or coefficients. Different entries in a series are not one handling model.

| Primary evidence | Documented behavior | MotorBound interpretation |
| --- | --- | --- |
| [Ghost's NFS Heat handling explanation](https://www.ea.com/games/battlefield/news/under-the-hood-the-handling-model) | Balanced stock handling; Race tuning favors cornering without drift; Drift tuning changes entry and angles. Throttle, brake bias, and handbrake can initiate slides. | Establish approachable road handling first; preserve intentional power/handbrake slides rather than triggering automatic drift from every sharp steer. |
| [Criterion's Unbound Vol. 7.0.1 notes](https://www.ea.com/security/news/vol7-0-1-patch-notes) | Drift Pro behavior is tied to dedicated tires, with feedback about approaching loss of control. | Specialized drift behavior belongs to a distinct setup; visible slip feedback should correspond to actual contact behavior. |
| [TDU Solar Crown developer letter 6](https://www.testdriveunlimited.com/en-US/news/solar-club-letter-six) | Adjustable ABS/TCS, assistance presets, and car driving modes distinguish cruising and competitive response. | Keep input accessibility separate from tire forces. A visible, switchable road traction-control mode reduces positive engine torque; it does not add artificial yaw damping, automatic steering, ABS, or a speed-dependent steering-angle cutoff. |
| [Original Atari TDU manual, archived copy](https://manuals.plus/m/1b65c2e1f24c2414d90077a0e60eb8c101e9f20edb79b4d6b1351818f25a0e46.pdf) | Steering responsiveness can be adjusted; driving assistance can be disabled. | Digital steering needs a controllable ramp without losing full held-key steering authority. |
| [MCO producer Michael Waite's firsthand Q&A](https://www.gamespot.com/articles/motor-city-online-qanda/1100-2684321/) | A dynamic four-point model emphasizes enjoyable feel and distinct vehicle character, rather than strict realism alone. | Calibrate the reference roadster to be predictable without flattening future differences in mass, tires, drivetrain, and suspension. Only the producer's answers are treated as primary evidence. |

These interpretations are design choices for MotorBound, not claims that those games use this tire model. No numerical coefficient is copied from these sources. No copyrighted game assets or source code are used.

### Implementation hypotheses to verify

- Explicit wheel-reaction integration at 360 Hz has a small-slip feedback gain approximately `dt × longitudinal stiffness × radius² / (wheel inertia × reference speed)`. The current stock inputs exceed the explicit stability bound at low speeds; this can create artificial longitudinal slip that steals lateral force through the combined-slip model. Use stable wheel substeps and average the actual transmitted forces rather than adding a chassis stabilization force.
- Short digital key taps currently reach excessive wheel angles. Reduce the press ramp from 3.5 to 2.0 normalized units/s; keep centering at 12 units/s and the complete 32° range at every speed.
- Tune the post-peak sliding fraction independently of the force curve's peak location. Stock tires use 0.90 rather than 0.78; dry peak friction and small-slip stiffness remain unchanged.
- Validate real full-throttle pulses, lane changes, bends, release, and countersteering. The old 0.12 steering command at 70 km/h implies roughly 1.1 g of geometric corner demand and is a near-limit test, not a mild cruise. Add a 0.06 command for ordinary bends; retain the old cases as stress tests.

### Candidate test finding and explicit assistance choice

The stable wheel solver, gentler keyboard ramp, and 0.90 sliding tail improve short inputs but do not by themselves make sustained full-throttle bends approachable. The first native candidate still reached about 47° body sideslip in the 70 km/h, 0.06-steering bend and continued rotating after release. This is distinct from low-speed numeric chatter: engine torque can overpower a lightly loaded inside rear tire and consume its cornering authority. Keyboard W is an on/off pedal.

The road prototype therefore starts with clearly labeled traction control ON. F2 switches it OFF for unassisted power/drift testing. This is a provisional game driving preference, not a claim of factory equipment in the fictional vehicle, not part of the installed assembly save, and not a replacement for a later complete drivetrain/ECU model. It limits positive propulsion torque using contact load, surface grip, lateral demand, and wheelspin. Handbrake initiation remains explicit. Both assisted routine driving and unassisted limit behavior need separate validation; reducing torque cannot overcome a corner whose tire grip is already exceeded by speed alone.
