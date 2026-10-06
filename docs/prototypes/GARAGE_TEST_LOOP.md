# Garage test loop — milestone 0.2

The milestone connects one Kiyora Aven's installed assembly to a playable local workshop, test drive, and explicit save/reload loop. It uses the existing vehicle-core foundations to make a supported part change observable in the same vehicle that is driven and saved.

The current checkout is `C:\Users\Michael\OneDrive\Documents\ASR_MotorBound Online\MotorBoundOnline`. Instructions and commands use the repository root, so another checkout can be used without editing source paths. The prototype Editor baseline remains Unity `2022.3.62f2`; the current product version is `0.2.5`.

## Play the acceptance loop

1. Open the project in Unity and open `Assets/MotorBound/Product/Scenes/VehicleDynamicsPrototype.unity`. If the scene is absent, use **MotorBound > Configure Prototype Project**. Enter Play Mode, or launch a successfully built Windows player.
2. The vehicle starts parked in the workshop. With no save, it uses stock road wheels; with a valid save, it restores that installed assembly and revision.
3. Select **Touring — 225 mm**. The preview explains that the touring companion hardware is missing, and installation remains unavailable.
4. Install the companion hardware, then install the matched four-wheel touring package. Inspect the changed mass, tire dimensions, visible wheels, and assembly revision. Selecting the incompatible five-lug option explains its rejected hub pattern and envelope.
5. Press `T` or **Test drive**. Try acceleration, braking, steering, the wet handling pad, and the rough-road strip. `F1` shows or hides tire/surface telemetry.
6. Return to the marked workshop bay, upright and below 0.5 m/s, then press `G`. If stranded, use the explicit `F6` recovery control.
7. Press `F5` or **Save**. Confirm the successful save message. Change the package again, then press `F9` or **Reload** to verify restoration. Closing and reopening the prototype should restore the last explicitly saved assembly.

The workshop is centered at world `(0, 0, -121)`. Entry checks position, height, upright orientation, speed, and the compiled facility compatibility result. This is a bounded reference bay, not a general parking or swept-volume solver.

## Controls

| Input | Action |
| --- | --- |
| `W` / Up | Throttle |
| `S` / Down | Brake |
| `A`, `D` / Left, Right | Steer |
| Space | Handbrake |
| `T` | Leave the workshop |
| `G` | Enter the workshop when parked in the bay |
| `F5` | Save while in the workshop |
| `F9` | Reload while in the workshop; replaces unsaved session changes |
| `F6` | Recover to the workshop, retaining the installed configuration |
| Backspace | Upright and lift the car at its current driving location |
| `F1` | Toggle driving telemetry/help |
| `F2` | Cycle Road / Sport / Off while driving (starts in Sport) |

## What the installed assembly changes

| Configuration | Vehicle mass | Tire radius | Tire section width | Tread water-evacuation parameter |
| --- | ---: | ---: | ---: | ---: |
| Stock road package | 1120 kg | 305 mm | 205 mm | 0.68 |
| Stock wheels with companion hardware installed | 1122 kg | 305 mm | 205 mm | 0.68 |
| Touring package with required companion hardware | 1130 kg | 315 mm | 225 mm | 0.78 |

The touring kit owns 2 kg; each touring wheel/tire owns 20 kg instead of the stock 18 kg. The original running-gear assembly excludes the four newly explicit wheels, so they do not contribute mass twice. Returning to stock wheels retains the installed hardware and its 2 kg.

These values are authored prototype inputs. The native driving run measured the resulting controller/Rigidbody configuration and confirmed 1120 kg stock versus 1130 kg touring, with the corresponding tire dimensions. It does not establish real-world performance accuracy or a universal advantage for wider tires. Dry compound and stiffness parameters remain unchanged; rotational inertia follows the same reference mass-distribution assumption at the new wheel mass and radius.

The controller, four wheel meshes, and operating envelope are compiled from approved part IDs and revisions. The local save cannot grant arbitrary mass, wheel poses, or tire tuning. Tire meshes and their transforms retain unit scale. The envelope tracks the source manifest ID/revision and uses actual configured mass, a conservative 55% maximum axle-load share, and a nominal level-ground suspension estimate:

```text
settled root height = tire radius + suspension rest length
                      - vehicle mass × 9.80665 / (4 × spring rate)
```

Body height, ground clearance, nominal center-of-mass height, and reference approach/departure/breakover angles follow the change in that nominal ride height. Maximum width includes the 1.91 m mirror envelope and track plus tire width. The workshop profile supplies the physical opening and a 2.00 m posted entrance limit. Dynamic pitch, load transfer, suspension motion, and arbitrary accessories require broader envelope handling in later work.

## Tire grip and skid feedback

The steering command still maps directly to the authored maximum wheel angle at every speed; there is no 50 km/h cutoff. Digital steering presses progress at 2 normalized units/s, reaching the complete 32° range after half a second. Release and unwinding an old turn during countersteering use 12 units/s. At high speed, requesting a tight full-lock turn can exceed front grip and produce understeer: the front tires skid while the rear largely holds. A rear slide depends on the balance of grip, wheelspin, braking, and load transfer, not just the steering key.

Version 0.2.2 replaces independent longitudinal/lateral saturation with one direction-preserving combined-slip demand. The force curve retains the authored stiffness at small slip, peaks continuously at the friction limit, then decreases toward an authored sliding fraction of 0.78. A locked or spinning wheel therefore cannot simultaneously retain almost full lateral grip. This is a compact provisional model, not a fitted full Pacejka model or measured-car calibration. The sine/atan curve structure is informed by [MathWorks' tire-road interaction documentation](https://www.mathworks.com/help/sdl/ref/tireroadinteractionmagicformula.html); combining the demand vector is this prototype's simplifying assumption.

Version 0.2.4 independently softens the stock sliding tail to 0.90 while preserving the existing peak location, dry friction, stiffness, and shared-force direction. Grounded wheel rotation uses adaptive substeps with averaged transmitted forces, preventing low-speed wheel-slip integration chatter. The contact/chassis simulation remains 360 Hz; this is not a new chassis stabilization force.

Version 0.2.5 starts in visible SPORT assistance. Ordinary driving uses the established ROAD torque limiter. A moving handbrake turn opens a slide-initiation window; a confirmed body/driven-tire slide then bypasses the limiter through sustain and recovery rather than being canceled immediately on Space release. The driver still supplies all steering, throttle and braking. Unconfirmed initiation expires after 0.85 seconds of handbrake release; confirmed sliding has no arbitrary timer. Continuous recovery below 2 degrees body sideslip and 5 degrees/second yaw for 0.35 seconds restores the road policy. Stop, loss of driven contacts, mode change, pause and motion reset clear the intent latch. ROAD retains strict grip-first behavior; OFF permits unassisted power-oversteer initiation. F2 cycles these modes, and the HUD shows mode, phase, sideslip and delivered torque. No mode adds yaw, maintained-speed force, automatic steering, extra tire grip or ABS. These are session preferences, not installed factory ECU parts or assembly-save fields.

The physical axle mechanism is separate: open and clutch-limited-slip definitions feed shared wheel/tire microsteps. The provisional Club rear LSD uses 25 Nm preload, 0.12 power / 0.03 coast lock fractions of total axle torque, and 4 Nm s/rad slip-speed gain. Equal-and-opposite internal impulses are capacity- and equalization-bounded; coupling conserves angular momentum and dissipates wheel-speed-difference energy. It neither welds wheel speeds nor invents tire forces. This is authored within the existing reference running gear, not a new garage part. FWD/RWD driven-axle routing and independent AWD axle coupling are supported; AWD center distribution remains an equal split. Tire curve, dry friction, suspension, CG, chassis angular drag and keyboard response are unchanged from 0.2.4. Engine/transmission clutch dynamics and integrated engine inertia remain deferred. See [the source-grounded architecture audit](VEHICLE_DYNAMICS_RESEARCH_BRIEF.md#vehicle-architecture-audit---october-6-2026).

Skid marks use grounded, loaded, post-peak contacts and actual tire width, with a fixed 2,048-segment pool. Contact loss, pause, recovery, and large jumps break trails. They do not appear merely because a steering key is held. The F1 HUD reports front, rear, or both-axle sliding. Gentle steering should remain composed; test hard steering around 50–70 km/h, then compare a brief handbrake input followed by release and countersteering. Holding the handbrake through the entire turn can spin the car.

## Save format and recovery

The active file is `garage-v1.json` under Unity's `Application.persistentDataPath`, not inside the Git checkout. With this project's current company/product settings, the Windows location is normally:

```text
%USERPROFILE%\AppData\LocalLow\GDS-G\MotorBound Online - Vehicle Dynamics Prototype\garage-v1.json
```

Saving is explicit and allowed only in the workshop. The file contains schema version 1 and the installed assembly manifest, not the current driving position, telemetry, or compiled tire/engine parameters. A valid saved configuration is loaded at startup; invalid input produces an explanatory message and a stock session without replacing the original file.

The save store:

- Requires explicit supported schema, manifest revision, and part-definition revisions before compiling the restored assembly.
- Rejects unknown IDs/revisions, incomplete or duplicate wheel sets/relationships, missing required hardware, changed accounting/poses/condition, nonfinite values, and files over 1 MiB.
- Writes a uniquely named temporary file next to the destination and reloads/validates it before replacing the active save.
- Keeps the previous valid active file as `garage-v1.json.bak` when replacing it. This is one previous version, not a complete history; the first save has no backup.
- Preserves unreadable, corrupt, or unsupported active files and blocks implicit replacement. The game does not silently load a backup or overwrite newer-format data.
- Keeps the maximum positive manifest revision readable/drivable; further changes are rejected before mutation instead of overflowing. Reinstalling an already installed package/kit remains harmless.

If recovery is needed, close the prototype, preserve copies of both the active file and any backup, and inspect the reported problem. An independently verified valid backup can be copied into the active location after the original is retained elsewhere. Do not edit arbitrary part values to bypass validation. A future schema needs an explicit migration; none is implemented here.

## Verification

Vehicle-mechanics update (version 0.2.5) on October 6, 2026:

- Reviewed Criterion's developer presentation, Kylotonn's vehicle-dynamics interview and MCO's producer explanation for actual component architecture, separately from player tuning controls. The research brief records source claims, our interpretations and missing mechanics; exact proprietary models are not available here.
- **226/226 native EditMode tests passed**, including passive differential momentum/energy bounds, power/coast and reverse behavior, paired wheel/contact torque accounting, brake locking, zero-grip contacts, assist intent/lifecycle and FWD/RWD/AWD routing. **15 engine-independent checks passed**.
- Retained cornering passed **70 scenarios / 501 assertions** and dry/wet/rough driving passed **16 / 267**. All six mild full-throttle 30/50/70 km/h bends left no marks. At 70 km/h, stock/touring peak sideslip was **1.23° / 1.25°**, final yaw **0.19°/s / 0.20°/s**, and sustained near-straight settlement began **0.52 / 0.60 seconds** after release. Full-lock keyboard release remains **83 ms**; yaw at 500 ms remained below **0.30°/s** in driving release cases.
- The new dry-road slide suite passed **44 scenarios / 256 assertions**. All **28 required slide cases** initiated through physical tire/drivetrain behavior, held controlled sliding for three seconds with the handbrake released, applied countersteering and recovered without a reset. Coverage includes stock/touring, left/right, 50/70 km/h entry targets, Sport/Off handbrake entry, eight Off power-only cases and four stock 50 km/h digital-only keyboard cases. Actual powered rear slip continued for at least 1.95 seconds inside each strict hold; force-envelope and stability bounds passed.
- The automated driver acts only through production inputs, not by adding forces or setting moving-car velocity/yaw. It is test code, not automatic player steering. Entry targets are not fixed drift speeds. There are **36 completed physical-slide outcomes**; eight identical-input Road/Sport open-loop comparisons explicitly fail the full-slide outcome and remain diagnostic. Wet drift acceptance, analog controls and subjective human feel remain unverified.
- **Windows development player 0.2.5 built successfully** at `Builds/Windows/0.2.5/MotorBoundVehiclePrototype.exe`, reporting **91,816,217 bytes**. An isolated twelve-second headless player loaded its assemblies/scene without managed exceptions. Null-graphics shader-support diagnostics are expected for that headless device and do not verify rendering. Only that smoke-test process was stopped; the user's existing root-folder player was left running. No player save existed before or after the check.
- Mean controller-plus-`Physics.Simulate` time was **81–102 µs/step** at 360 Hz on this machine, excluding rendering/UI/networking. Rendered startup/F2 interaction and driving feel still need playtesting. Current evidence is in `artifacts/vehicle-mechanics-release-validation.json` and the compact `artifacts/slide-validation.json`; generated 20 Hz dense traces are ignored by Git.
- Engine/transmission clutch, dynamically integrated engine inertia, shift duration, transient tires and calibrated suspension geometry remain missing. The axle clutch does not provide clutch-kicks or stalling; AWD center distribution is still equal-split. No new automatic yaw, grip or maintained-speed force was added, and tire/steering/suspension parameters are unchanged from 0.2.4.

The following sections preserve historical release measurements. Shared driving/cornering report paths now contain the latest 0.2.5 run; older contents remain in Git history (0.2.4 commit `4381b58`) and dedicated comparison artifacts.

Road-handling update (version 0.2.4) on October 6, 2026:

- Reviewed developer explanations/manuals for NFS Heat/Unbound, Test Drive Unlimited, and Motor City Online. The implementation uses their documented design lessons, not proprietary equations or coefficients; sources and interpretations are recorded in the research brief.
- **164/164 native EditMode tests passed** and **15 engine-independent checks passed**. Coverage includes wheel integration without low-speed sign chatter, angular momentum/force averaging, genuine over-grip wheelspin, static brake locking, unchanged tire peak/prepeak response, smooth independent sliding tails, steering press/release/countersteer, explicit traction-control torque limits and bypasses, and the existing garage/shader regressions.
- The expanded cornering run passed **70 maneuvers / 501 assertions**: 22 unassisted historical limit/recovery cases, plus the same 24 full-throttle/coasting routine cases on each of stock and touring wheels. Small-correction recovery and mild-bend settlement are now acceptance conditions, not merely recorded observations. Release heading is accumulated rather than inferred from a wrapped end angle.
- All six mild 0.06-steering, full-throttle bends at 30/50/70 km/h produced **no skid marks**. At 70 km/h, peak body sideslip was about **1.68° stock / 1.70° touring**, final yaw about **0.34°/s / 0.68°/s**, and sustained near-straight settlement began about **1.03 / 1.20 seconds** after release. Near-/over-limit 0.12/0.20 commands remain stress tests and can still leave tire marks; traction control does not create grip beyond the tire limit.
- `artifacts/road-handling-comparison.json` preserves 21 matching 0.2.3-versus-0.2.4 stock cases and the additional mild-bend candidate measurements. Baseline routine checks originally permitted spins: first unassisted 0.2.4 candidate's mild 70 km/h full-throttle bend reached about 47° sideslip. Visible ROAD torque control, with stronger cornering reserve, removed that runaway behavior. This comparison combines several changes and is not a single-coefficient causal experiment.
- The separate driving run passed **16 maneuvers / 267 assertions** across dry/wet/rough surfaces, both wheel packages, and left/right steering releases. Full-lock release still centers in **83 ms**, with yaw magnitude below **0.22°/s at 500 ms** in those release scenarios. Their held-key ramp lasts 0.55 seconds to reach full lock with the new input response, so their heading totals are not directly comparable to earlier 0.35-second press scenarios.
- ROAD mode caps a common positive engine torque, using the lower loaded driven contact's capacity; it neither creates chassis yaw forces nor clamps wheel rotation to road speed. The target slip is 0.045 through 0.1° measured tire slip angle, smoothly reduced to 0.020 at 1°. OFF, handbrake use, and nonpositive drive torque bypass the actuator. Wet/low-speed feedback can still have small torque modulation; refined ECU/drivetrain dynamics and human feel calibration remain future work.
- Mean controller-plus-`Physics.Simulate` time was about **77–84 µs/step** on this machine. This excludes rendering/UI/networking and is not a target-machine frame-rate guarantee.
- **Windows development player 0.2.4 built successfully**, reporting 91,800,770 bytes, in `Builds/Windows/0.2.4/MotorBoundVehiclePrototype.exe`. The already running root-folder 0.2.3 player was not closed or overwritten. Keep the new executable's adjacent data directory/files together.
- The new player's rendered startup/F2 key path could not be checked: two computer-use launch attempts timed out, and refreshed window/process lists showed only the user's existing player. No player save was created or overwritten. `artifacts/handling-release-validation.json` distinguishes native verification from this UI limitation. Human playtesting is still required.

Packaged-player startup hotfix (version 0.2.3) on October 5, 2026:

- Reproduced the rendered 0.2.2 startup failure. The fresh player log traced `ArgumentNullException`, parameter `shader`, to `PrototypeSkidTrails.Configure`: `Standard` existed in the Editor but had no retained player-build reference.
- Added a lightweight `MotorBound/PrototypeSkidRubber` shader under Resources and explicitly loaded it for the skid material. Fallbacks are checked before allocation; optional material properties are assigned only when present. [Unity's Shader.Find documentation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Shader.Find.html) describes this Editor-versus-player inclusion difference.
- **109/109 native EditMode tests passed**, including the new resource-loading, shader-import, and actual material-binding check. The build log confirms compilation and inclusion of the skid shader. **Windows player 0.2.3 built successfully**, reporting 91,792,018 bytes.
- Rendered Windows checks passed for workshop startup, `T` test-drive transition, and `F6` recovery with no player-log exceptions. No player save was created or overwritten. This verifies the startup correction, not a new high-speed driving-feel playtest; tire forces and input response remain unchanged from 0.2.2.

High-speed tire mechanics update (version 0.2.2) on October 5, 2026:

- **108/108 native EditMode tests passed**, including combined-demand force direction and bounds, post-peak sliding grip, locked/spinning-wheel cornering grip, validated tire parameters, skid geometry at stock/touring widths, bounded mark pooling, lifecycle cleanup, and the existing garage/steering regressions. **15 engine-independent checks passed**.
- The existing driving run passed **16 maneuvers / 267 assertions** on dry, wet, and rough roads with both packages. Steering input still centered in 83 ms; faster release cases added about 6.1–6.3° of heading over two seconds, with yaw below 0.3°/s at 500 ms.
- The new native cornering run passed **22 maneuvers / 157 assertions**: gentle/full steering at 30/45/50/55/70 km/h, coast/power/handbrake at 55/70, a left/right mirror, touring checks, and two brief-handbrake recoveries. Actual front-wheel pivots reached full 32° at every tested speed. The force envelope, contact surfaces, upright stability, real post-peak contacts, and marks are measured rather than inferred from key presses.
- Stock full-lock 50 km/h still produces front-tire understeer, now with contact-driven skid marks. At 70 km/h under full power, body sideslip increased from about 5.3° to 9.6° and late rear tire slip from 4.6° to 10.4°, showing the revised sharing of wheelspin/cornering grip. Holding the handbrake for 1.2 seconds can spin the car; it is not the recovery maneuver.
- Brief 0.2-second handbrake inputs followed by two seconds of countersteering recovered from 55.2/70.0 km/h starts to 50.5/62.2 km/h. Final yaw magnitude was below 0.04°/s and body sideslip magnitude below 0.002°. Recovery bounds require meaningful opposite steering, final yaw below 5°/s, sideslip below 3°, restored rear rolling throughout the last second, and more than half the starting speed.
- `artifacts/cornering-validation.json` contains the current measurements; `artifacts/cornering-comparison.json` preserves matching before/after runs. These scripted dry-road cases and skid-mesh tests do not replace human feel/visual playtesting or measured tire calibration.
- **Windows development player 0.2.2 built successfully**, with a build summary of 91,787,094 bytes. The existing executable path is unchanged; new gameplay code is in its adjacent data directory.

Steering-release update (version 0.2.1) on October 5, 2026:

- Released keyboard steering now centers at 12 normalized units per second, while steering key presses retain the original 3.5 units per second. The actual keyboard update and the native regression scenarios share the same input response.
- **80/80 native EditMode tests passed**, including full left/right release within 100 ms at 30, 60, and 144 Hz input updates, no opposite steering command during centering, and clearing steering while parked in the garage.
- Eight added release maneuvers exercise full left/right key presses at two speeds with both wheel packages, followed by two seconds of release without braking. At 60 Hz, input centering improved from **300 ms to 83 ms**. In the faster dry-road cases, additional heading change fell from **17.7–18.4° to 7.2–7.6°**, and yaw rate at 500 ms fell from **6.5–9.6°/s to at most 0.3°/s**.
- Release regressions require input centering within 100 ms and turning below 1°/s at 500 ms and one second. That release-only update left the tire-force and Rigidbody settings unchanged; version 0.2.2 subsequently changed the tire-force curve as described above.
- These release measurements cover dry-road scenarios. Wet-surface acceleration/braking remains covered by the existing handling tests; wet steering-release calibration and human driving-feel assessment remain future work.
- The final driving run passed **16 maneuvers and 267 assertions**. The before/after measurements are preserved in `artifacts/steering-release-comparison.json`. The **Windows player 0.2.1 built successfully**, with a build summary of 91,779,237 bytes.

Native verification on October 1, 2026:

- **73/73 Unity EditMode tests passed**, including 12 product integration tests for persistence, rejected saves, reconfiguration, and the garage/drive/return/reopen loop.
- **Windows development player version 0.2.0 built successfully** at `Builds/Windows/MotorBoundVehiclePrototype.exe`; the final native build summary reports 91,778,643 bytes.
- **Live player/UI checks passed** for startup, missing-hardware rejection, touring installation with changed mass/dimensions, incompatible-package rejection, the driving transition, and garage recovery. The visual review prompted wheel-opening and workshop-sign fixes, followed by a rebuild and another 73/73 native test pass. Save/reload persistence is covered by the native integration tests; the UI check did not create or overwrite a player save.
- **8 actual Unity driving maneuvers and 211 assertions passed** across stock/touring dry acceleration and braking, wet acceleration and braking, dry steering, and rough road.
- `artifacts/driving-validation.json` contains the per-maneuver results, surface contacts, suspension ranges, speeds, steering response, and measured timings.
- Mean controller plus `UnityEngine.Physics.Simulate` time ranged from approximately **78–91 µs per step** on this machine, with a 360 Hz simulation step. This measurement excludes rendering, UI, scene streaming, networking, and general game overhead. It is not a target-machine frame-rate or performance guarantee.

The driving bounds detect broad regressions; they are not manufacturer-data calibration, comprehensive handling validation, or a human assessment of driving feel.

Run these commands from the repository root. Adjust the Editor path if Unity is installed elsewhere, and avoid opening the same project in another Editor process during a batch run.

```powershell
$motorBoundProject = (Get-Location).Path
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
New-Item -ItemType Directory -Path (Join-Path $motorBoundProject 'artifacts') -Force | Out-Null

# Native tests: the test runner exits when finished; do not add -quit.
& $unityEditor -batchmode -nographics `
  -projectPath $motorBoundProject `
  -runTests -testPlatform EditMode `
  -testResults (Join-Path $motorBoundProject 'artifacts/editmode-results.xml') `
  -logFile (Join-Path $motorBoundProject 'artifacts/editmode.log')

# Actual controller and Unity physics regression maneuvers.
& $unityEditor -batchmode -nographics -quit `
  -projectPath $motorBoundProject `
  -executeMethod MotorBound.Editor.PrototypeDrivingValidation.Run `
  -logFile (Join-Path $motorBoundProject 'artifacts/driving-validation.log')

# Rolling-wheel 30–70 km/h cornering, skid marks, and handbrake recovery.
& $unityEditor -batchmode -nographics -quit `
  -projectPath $motorBoundProject `
  -executeMethod MotorBound.Editor.PrototypeCorneringValidation.Run `
  -logFile (Join-Path $motorBoundProject 'artifacts/cornering-validation.log')

# Physical slide initiation, holding and recovery.
& $unityEditor -batchmode -nographics -quit `
  -projectPath $motorBoundProject `
  -executeMethod MotorBound.Editor.PrototypeSlideValidation.Run `
  -logFile (Join-Path $motorBoundProject 'artifacts/slide-validation.log')

# Engine-independent checks complement native Unity tests.
dotnet run --project tools/MotorBound.DomainVerification/MotorBound.DomainVerification.csproj

# Windows development-player build.
& $unityEditor -batchmode -nographics -quit `
  -projectPath $motorBoundProject `
  -executeMethod MotorBound.Editor.PrototypeProjectSetup.BuildVersionedWindowsPrototype `
  -logFile (Join-Path $motorBoundProject 'artifacts/windows-build.log')
```

Inspect each run's result before proceeding; a successful compilation is not a successful test or player build. A successful versioned Windows build produces `Builds/Windows/0.2.5/MotorBoundVehiclePrototype.exe` and adjacent required data files. The Editor equivalents are **Window > General > Test Runner > EditMode**, **MotorBound > Validate Driving Physics**, **MotorBound > Validate Cornering Physics**, **MotorBound > Validate Slide Mechanics**, and **MotorBound > Build Versioned Windows Prototype**. The original **Build Windows Prototype** targets `Builds/Windows` directly and must not overwrite a running player's files. Save modified scenes before driving validation.

## Boundaries and next work

This milestone supports one reference vehicle, two authored matched four-wheel packages, one required hardware kit, and one explicitly incompatible example. Parts are supplied without inventory purchases or labor. Package previews use the existing dependency evaluator and authored fitment checks; they do not solve arbitrary wheel/body modifications.

The deterministic wheel instance IDs currently identify reference mounting slots. A package change replaces the definition at the same slot ID. This does **not** implement unique physical replacement-item identity, provenance, ownership, removed-parts inventory, wear, or service history. Those semantics require a distinct inventory and installation lifecycle before the GDD's physical-parts model is complete.

The save is a local prototype convenience. There is no network authority, multiplayer persistence, MMO economy, contracts, traffic, property ownership, production content pipeline, production-grade art/audio, or console certification. The generic engine/powertrain plans remain domain foundations rather than an interactive engine-building workshop. These tests establish the bounded loop and regression evidence; they do not establish production readiness.
