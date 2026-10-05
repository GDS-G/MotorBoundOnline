# Garage test loop — milestone 0.2

The milestone connects one Kiyora Aven's installed assembly to a playable local workshop, test drive, and explicit save/reload loop. It uses the existing vehicle-core foundations to make a supported part change observable in the same vehicle that is driven and saved.

The current checkout is `C:\Users\Michael\OneDrive\Documents\ASR_MotorBound Online\MotorBoundOnline`. Instructions and commands use the repository root, so another checkout can be used without editing source paths. The prototype Editor baseline remains Unity `2022.3.62f2`; the current product version is `0.2.3`.

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

The steering command still maps directly to the authored maximum wheel angle at every speed; there is no 50 km/h cutoff. At high speed, requesting a tight full-lock turn can exceed front grip and produce understeer: the front tires skid while the rear largely holds. A rear slide depends on the balance of grip, wheelspin, braking, and load transfer, not just the steering key.

Version 0.2.2 replaces independent longitudinal/lateral saturation with one direction-preserving combined-slip demand. The force curve retains the authored stiffness at small slip, peaks continuously at the friction limit, then decreases toward an authored sliding fraction of 0.78. A locked or spinning wheel therefore cannot simultaneously retain almost full lateral grip. This is a compact provisional model, not a fitted full Pacejka model or measured-car calibration. The sine/atan curve structure is informed by [MathWorks' tire-road interaction documentation](https://www.mathworks.com/help/sdl/ref/tireroadinteractionmagicformula.html); combining the demand vector is this prototype's simplifying assumption.

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

# Engine-independent checks complement native Unity tests.
dotnet run --project tools/MotorBound.DomainVerification/MotorBound.DomainVerification.csproj

# Windows development-player build.
& $unityEditor -batchmode -nographics -quit `
  -projectPath $motorBoundProject `
  -executeMethod MotorBound.Editor.PrototypeProjectSetup.BuildWindowsPrototype `
  -logFile (Join-Path $motorBoundProject 'artifacts/windows-build.log')
```

Inspect each run's result before proceeding; a successful compilation is not a successful test or player build. A successful Windows build produces `Builds/Windows/MotorBoundVehiclePrototype.exe` and adjacent required data files. The Editor equivalents are **Window > General > Test Runner > EditMode**, **MotorBound > Validate Driving Physics**, **MotorBound > Validate Cornering Physics**, and **MotorBound > Build Windows Prototype**. Save modified scenes before driving validation.

## Boundaries and next work

This milestone supports one reference vehicle, two authored matched four-wheel packages, one required hardware kit, and one explicitly incompatible example. Parts are supplied without inventory purchases or labor. Package previews use the existing dependency evaluator and authored fitment checks; they do not solve arbitrary wheel/body modifications.

The deterministic wheel instance IDs currently identify reference mounting slots. A package change replaces the definition at the same slot ID. This does **not** implement unique physical replacement-item identity, provenance, ownership, removed-parts inventory, wear, or service history. Those semantics require a distinct inventory and installation lifecycle before the GDD's physical-parts model is complete.

The save is a local prototype convenience. There is no network authority, multiplayer persistence, MMO economy, contracts, traffic, property ownership, production content pipeline, production-grade art/audio, or console certification. The generic engine/powertrain plans remain domain foundations rather than an interactive engine-building workshop. These tests establish the bounded loop and regression evidence; they do not establish production readiness.
