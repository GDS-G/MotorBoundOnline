# Garage test loop — milestone 0.2

The milestone connects one Kiyora Aven's installed assembly to a playable local workshop, test drive, and explicit save/reload loop. It uses the existing vehicle-core foundations to make a supported part change observable in the same vehicle that is driven and saved.

The current checkout is `C:\Users\Michael\OneDrive\Documents\ASR_MotorBound Online\MotorBoundOnline`. Instructions and commands use the repository root, so another checkout can be used without editing source paths. The prototype Editor baseline remains Unity `2022.3.62f2`; the product version is `0.2.0`.

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

# Engine-independent checks complement native Unity tests.
dotnet run --project tools/MotorBound.DomainVerification/MotorBound.DomainVerification.csproj

# Windows development-player build.
& $unityEditor -batchmode -nographics -quit `
  -projectPath $motorBoundProject `
  -executeMethod MotorBound.Editor.PrototypeProjectSetup.BuildWindowsPrototype `
  -logFile (Join-Path $motorBoundProject 'artifacts/windows-build.log')
```

Inspect each run's result before proceeding; a successful compilation is not a successful test or player build. A successful Windows build produces `Builds/Windows/MotorBoundVehiclePrototype.exe` and adjacent required data files. The Editor equivalents are **Window > General > Test Runner > EditMode**, **MotorBound > Validate Driving Physics**, and **MotorBound > Build Windows Prototype**. Save modified scenes before driving validation.

## Boundaries and next work

This milestone supports one reference vehicle, two authored matched four-wheel packages, one required hardware kit, and one explicitly incompatible example. Parts are supplied without inventory purchases or labor. Package previews use the existing dependency evaluator and authored fitment checks; they do not solve arbitrary wheel/body modifications.

The deterministic wheel instance IDs currently identify reference mounting slots. A package change replaces the definition at the same slot ID. This does **not** implement unique physical replacement-item identity, provenance, ownership, removed-parts inventory, wear, or service history. Those semantics require a distinct inventory and installation lifecycle before the GDD's physical-parts model is complete.

The save is a local prototype convenience. There is no network authority, multiplayer persistence, MMO economy, contracts, traffic, property ownership, production content pipeline, production-grade art/audio, or console certification. The generic engine/powertrain plans remain domain foundations rather than an interactive engine-building workshop. These tests establish the bounded loop and regression evidence; they do not establish production readiness.
