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

### Version 0.2.4 candidate finding and assistance choice

The stable wheel solver, gentler keyboard ramp, and 0.90 sliding tail improve short inputs but do not by themselves make sustained full-throttle bends approachable. The first native candidate still reached about 47° body sideslip in the 70 km/h, 0.06-steering bend and continued rotating after release. This is distinct from low-speed numeric chatter: engine torque can overpower a lightly loaded inside rear tire and consume its cornering authority. Keyboard W is an on/off pedal.

The 0.2.4 road prototype therefore started with clearly labeled traction control ON, with F2 switching it OFF for unassisted power/drift testing. This was a provisional game driving preference, not a claim of factory equipment in the fictional vehicle, not part of the installed assembly save, and not a replacement for a later complete drivetrain/ECU model. It limits positive propulsion torque using contact load, surface grip, lateral demand, and wheelspin. Handbrake initiation remains explicit. Both assisted routine driving and unassisted limit behavior need separate validation; reducing torque cannot overcome a corner whose tire grip is already exceeded by speed alone. Version 0.2.5 separates deliberate slide intent from the same ordinary-road actuator, as described below.

## Vehicle architecture audit - October 6, 2026

The follow-up playtest reported that 0.2.4 felt too difficult to slide. That is not evidence that the host game engine cannot simulate sliding. It requires an audit of our tire response, drivetrain, input and assistance layers, including whether the player can deliberately initiate, sustain and recover a slide. Passing ordinary-road recovery checks does not establish that the intentional-slide experience is good.

### What the reference developers actually disclosed

- Criterion's 2018 presentation diagrams a bidirectional engine/clutch/transmission/differential-wheel chain, with rotational inertia and gear-change time; it illustrates contact-patch tire friction. It separates input and simulation assists. Examples include simulation-informed brake-tap drift entry, center-input recalibration during a drift, and an added maintain-speed force. The bonus diagrams show that force opposing tire friction and varying smoothly with deviation from an ideal drift angle. These are disclosed design examples, not the complete implementation or a universal NFS model. The relevant PDF pages were visually verified: 27-29, 32, 38, 41-44 and 91-92. [Criterion's Vehicle Feel Masterclass slides](https://media.gdcvault.com/gdc2018/presentations/Harris_Matthew_VehicleFeelMasterclass.pdf).
- Ghost's Heat explanation identifies steering, differential, clutch, brake bias and tires as interacting handling components, with drivetrain-dependent drift behavior and throttle-controlled drift angles. It does not publish their integration scheme or force equations. [Ghost Driving Experience Team](https://www.ea.com/games/battlefield/news/under-the-hood-the-handling-model).
- Kylotonn's TDUSC vehicle-dynamics designer identifies engine torque curves, axle hybridization, tire lateral forces/stiffness, ABS, differential and chassis inertia as simulated elements. Wheel rotation/sliding and engine-load outputs feed other systems such as UI and sound. She describes a shared engine with WRC and deliberate tuning for realism, accessibility and design intent. This establishes component scope, not a particular tire equation or solver. [Naomi Heinis, official Kylotonn interview](https://kylotonn.com/en/news-kt/interview-naomi-heinis/).
- MCO producer Michael Waite described an original engine and a dynamic four-point physics model, with parts affecting behavior and vehicle character prioritized over strict realism. That supports a four-contact/component-oriented comparison, but does not identify the contact algorithm, tire law or differential model. [Producer's firsthand MCO Q&A](https://www.gamespot.com/articles/motor-city-online-qanda/1100-2684321/).

The original TDU games, TDUSC, Criterion's demonstration and Ghost's Heat must not be treated as interchangeable implementations. None of these sources establishes parity merely because a game uses a named engine. Exact proprietary equations, coefficients, contact sampling, integration frequencies and network-physics architecture remain unverified here. Community reverse-engineering tables and claims of a universal TDU 1000 Hz simulation are not treated as developer evidence. Criterion's added drift forces are also not evidence that TDU or MCO use the same approach.

### MotorBound 0.2.4 baseline, compared with the disclosed component scope

This is a versioned audit of the delivered 0.2.4 design, not a claim that subsequent work in progress is already shipped or validated. Local evidence is `RaycastVehicleController`, `WheelContactSolver`, `TireForceModel`, `RoadTractionControl`, `VehicleDefinition` and `PrototypeInputDriver`.

| Area | Implemented in 0.2.4 | Concrete limitation or next question |
| --- | --- | --- |
| Chassis and contact | Unity Rigidbody receives suspension and tire forces at four wheel contacts; lateral ray samples approximate tire width. | This is not a deformable contact patch or tire volume. Explicit chassis inertia calibration, curb/edge behavior and contact transitions need their own evidence. |
| Suspension and geometry | Spring/compression-rebound damping, authored mass/CG, wheelbase and track; the body responds dynamically to applied forces. | No explicit anti-roll bar, suspension kinematics/camber law or Ackermann steering model. Both front wheels use the same commanded steer angle. |
| Tires and wheel rotation | Load-sensitive combined slip, an independently tunable post-peak tail, wheel inertia, stable rotational substeps and averaged transmitted forces; surface/water-film response. | A compact analytic prototype, not measured tire data. No tire relaxation, temperature, wear or pressure dynamics; breakaway, sustained sliding and recovery need vehicle-level calibration. |
| Engine and gearbox | Authored torque/RPM interpolation, ratios, efficiency, engine-braking torque and automatic gear selection. | RPM is inferred algebraically from driven-wheel speed plus a launch-RPM floor; the authored engine inertia is not dynamically integrated. No engine-to-gearbox clutch state, stall/launch dynamics, shift duration or shaft compliance. |
| Axle torque distribution | Requested propulsion torque is divided equally between driven wheels. | Equal input torques alone are not a complete open-differential carrier/coupling simulation. No limited-slip clutch, locking/coast behavior or configurable differential torque bias in this baseline. |
| Brakes | Front-biased service torque and a rear handbrake, including physical wheel-lock/contact response. | No ABS or per-wheel brake modulation. Handbrake initiation being available is not proof of a controllable sustained drift. |
| Input and assistance | Progressive digital steering, fast centering/countersteer unwind; visible ROAD traction control with F2 ON/OFF. | W remains an on/off throttle. No analog/gamepad throttle path, clutch input, explicit drift intent or drift-support state. ROAD deliberately removes propulsion authority while reserving lateral grip. |
| Feedback and validation | Wheel force/slip/surface telemetry, skid marks, ordinary-road correction/recovery cases and unassisted hard-turn/handbrake cases. | Skid marks are not a quantitative drift-quality test. Assisted road checks and intentional-slide entry/sustain/recovery must be separate acceptance suites. |

The 0.2.4 ROAD actuator targets longitudinal slip ratio 0.045 at measured tire slip angles up to 0.1 degrees, smoothly reducing to 0.020 at 1 degree and above. Its common engine-torque ceiling uses the weakest previously loaded driven contact and cuts further during positive wheelspin. OFF, positive handbrake input and nonpositive drive torque bypass it. This is our calibration, not a coefficient copied from TDU or NFS. It explains why improved ordinary-road stability can coincide with suppressed power-slide initiation while ROAD remains enabled; OFF retains the underlying unassisted physics. It does not establish why every reported failed slide occurred without observing the player's mode, inputs and telemetry.

### Implemented mechanics and native evidence — version 0.2.5

1. `VehicleAssistController` makes ROAD/SPORT/OFF explicit. SPORT is the default and keeps ordinary-driving torque control, but a moving handbrake-and-turn input opens a short entry window. A confirmed driven-contact/body slide can continue with the limiter bypassed until settled recovery. OFF removes propulsion intervention and permits power-only initiation. The policy only decides whether the torque limiter may act: it cannot steer, add yaw, change tire grip or maintain speed. Brake-to-drift is not implemented.
2. `DifferentialModel` and `AxleContactSolver` introduce mechanical open/clutch-limited-slip axle definitions and paired tire/rotation integration. The Club reference rear axle has a provisional passive clutch LSD. Equal-and-opposite internal impulses are capacity- and equalization-bounded, conserving angular momentum and dissipating wheel-speed-difference energy. Native tests cover power/coast selection, reverse mechanical work, asymmetric inertia, unloaded/zero-grip contacts, braking and torque accounting. This differential clutch is not an engine/transmission clutch.
3. Native EditMode passed **226/226 tests**, including assist lifecycle and FWD/RWD/AWD routing. The retained road suite passed **70 scenarios / 501 assertions** and dry/wet/rough driving passed **16 / 267**. The new dry-road slide suite passed **44 scenarios / 256 assertions**, with all **28 required physical-slide cases** completing entry, three-second controlled sliding, powered continuation, actual countersteering and recovery without a reset. These cover stock/touring packages, left/right, 50/70 km/h entry targets, Sport/Off handbrake entry, eight Off power-only cases, and four digital-keyboard-only stock 50 km/h cases. The virtual test driver acts only through bounded production inputs; it is not automatic steering in the player. Entry targets are not constant slide speeds.
4. There are **36 completed physical-slide outcomes**, not 44: eight identical-input Road/Sport open-loop comparisons remain explicit diagnostic outcome failures. The compact report retains them in `artifacts/slide-validation.json`; dense traces are generated separately and ignored by Git. Wet/rough road regression coverage does not establish wet drift acceptance. Subjective feel and rendered player controls still require human playtesting.

### Remaining component gaps and next architecture work

Dynamic engine inertia, engine/transmission clutch and shift-time behavior, transient tire relaxation and analog throttle access remain missing. The current engine RPM and launch approximation cannot support a physically simulated clutch-kick or stall. AWD center distribution remains an equal split, not a center differential. Independently verify coasting/rolling-resistance energy balance: the present contact/reaction formulation can compensate rolling drag in steady free rolling. These gaps are explicit limits of the vehicle simulation, not fixed by the new axle clutch or assistance modes.

These are architecture and test recommendations inferred from our source audit and the public component disclosures. They are not claims that copying a named engine, buying a license, increasing simulation frequency or matching one grip number will reproduce another game's driving experience.
