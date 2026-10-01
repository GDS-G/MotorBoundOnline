# MotorBound Online

MotorBound Online's design targets a persistent automotive world built around mechanical consequence, physical travel, unique property ownership, and a living player economy.

This repository currently contains milestone **0.2: a local garage → test drive → return-and-save loop** for one rear-wheel-drive Kiyora Aven. The garage's installed assembly determines vehicle mass, supported wheel visuals, tire parameters, and the operating envelope. Stock and touring wheel packages connect the assembly manifest, dependency planner, fitment checks, driving controller, and versioned local save.

The driving prototype uses a 360 Hz critical-vehicle step, tire-width contact sampling, water/roughness surface state, and custom tire forces without Unity `WheelCollider`. Multiplayer, the economy, property ownership, and production vehicle inventory remain future work. See [the garage milestone guide](docs/prototypes/GARAGE_TEST_LOOP.md) for the acceptance loop, evidence, and limitations.

## Open the prototype

1. Open the repository root in Unity `2022.3.62f2` (the locally available prototype baseline).
2. Use **MotorBound > Configure Prototype Project** if the prototype scene has not yet been generated.
3. Open `Assets/MotorBound/Product/Scenes/VehicleDynamicsPrototype.unity`.
4. Enter Play Mode. The vehicle starts parked in the workshop and loads a valid local save if one exists.
5. Select **Touring — 225 mm**, install its companion hardware, then install the selected package.
6. Press `T` to drive. Return to the marked workshop bay, stop, and press `G`; then press `F5` to save. Reopen the prototype to check that the same configuration loads.

The current working checkout is `C:\Users\Michael\OneDrive\Documents\ASR_MotorBound Online\MotorBoundOnline`. Open the folder containing `Assets`, `Packages`, and `ProjectSettings`; command examples below adapt to whichever checkout is current.

Controls:

- `W`: throttle
- `S`: brake
- `A` / `D`: steer
- `Space`: handbrake
- `T`: leave the garage for a test drive
- `G`: enter the garage when upright, stopped, and within the marked bay
- `F5` / `F9`: save / reload the installed configuration while in the garage
- `F6`: recover to the garage with the current installed configuration
- `Backspace`: upright and lift the vehicle at its current driving location
- `F1`: toggle telemetry/help

The course includes a workshop entrance with clearance checks, a skidpad, a 2.5 mm water-film section, a physical rough-road strip, lane markers, and obstacles. Parts are supplied for testing. The stock configuration is 1120 kg with 305 mm-radius, 205 mm-wide tires; touring plus required hardware is 1130 kg with 315 mm-radius, 225 mm-wide tires. These are provisional authored values, not measured real-vehicle calibration.

Save files use `Application.persistentDataPath/garage-v1.json`. A successful replacement retains the prior valid save as `garage-v1.json.bak`. Invalid, incomplete, or unsupported saves are preserved and block automatic replacement. Saving is explicit; leaving the game does not automatically save changes.

## Repository layout

- `Packages/com.gdsg.motorbound.foundation`: stable identity, SI units, and deterministic utilities.
- `Packages/com.gdsg.motorbound.vehicle.core`: layered vehicle identities, authoritative assembly manifests, dimensional access checks, physical fitment validation, dependency-complete build plans, and data-driven powertrain definitions.
- `Packages/com.gdsg.motorbound.vehicle.physics`: tire-force math and the Unity vehicle prototype.
- `Assets/MotorBound/Product`: product composition, garage session and saves, procedural scene bootstrap, camera, HUD, and integration tests.
- `docs`: architecture, decisions, research brief, and milestone notes.

## Verification

On October 1, 2026, **73 native Unity EditMode tests passed**, including 12 product integration tests. The actual Unity driving validation passed **8 maneuvers and 211 assertions** across stock/touring dry acceleration and braking, wet driving, steering, and rough road. The generated report is `artifacts/driving-validation.json`.

The **Windows development player built successfully** as version `0.2.0` at `Builds/Windows/MotorBoundVehiclePrototype.exe` (final build summary: 91,778,643 bytes). Keep its adjacent data files when running or copying the player. Live player checks confirmed workshop startup, missing-hardware rejection, touring installation, incompatible-package rejection, the driving transition, and recovery. Visual inspection led to visible wheel openings and two-sided workshop signage.

Measured mean controller-plus-`Physics.Simulate` step time was approximately **78–91 µs** in that run at 360 Hz. This excludes rendering and UI and is not a rendered frame-rate or hardware performance guarantee.

From Unity, run **Window > General > Test Runner > EditMode**, then **MotorBound > Validate Driving Physics** outside Play Mode with modified scenes saved. To produce a Windows development player, run this from the repository root (adjust the Editor executable for your installation):

```powershell
$motorBoundProject = (Get-Location).Path
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
& $unityEditor `
  -batchmode -nographics -quit `
  -projectPath $motorBoundProject `
  -executeMethod MotorBound.Editor.PrototypeProjectSetup.BuildWindowsPrototype `
  -logFile '-'
```

After a successful build, launch `Builds/Windows/MotorBoundVehiclePrototype.exe` with its adjacent data files intact. For native test, driving-validation, and engine-independent commands, see [the milestone guide](docs/prototypes/GARAGE_TEST_LOOP.md#verification).

## Production engine gate

The GDD recommends a Unity 6-class LTS production branch only after console compatibility, middleware, licensing, HDRP, and physics profiling are validated. Unity `2022.3.62f2` is used here solely because it is installed in the current development environment; the simulation packages avoid version-specific rendering dependencies to keep that engine decision reversible.
