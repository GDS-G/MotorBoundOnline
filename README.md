# MotorBound Online

MotorBound Online's design targets a persistent automotive world built around mechanical consequence, physical travel, unique property ownership, and a living player economy.

This repository currently contains milestone **0.2: a local garage → test drive → return-and-save loop** for one rear-wheel-drive Kiyora Aven. The garage's installed assembly determines vehicle mass, supported wheel visuals, tire parameters, and the operating envelope. Stock and touring wheel packages connect the assembly manifest, dependency planner, fitment checks, driving controller, and versioned local save.

The driving prototype uses a 360 Hz critical-vehicle step, tire-width contact sampling, water/roughness surface state, and custom tire forces without Unity `WheelCollider`. Version 0.2.2 retains the quicker steering return introduced in 0.2.1, corrects how wheelspin/braking and cornering share tire grip, and adds a smooth peak-to-sliding transition with contact-driven skid marks. Multiplayer, the economy, property ownership, and production vehicle inventory remain future work. See [the garage milestone guide](docs/prototypes/GARAGE_TEST_LOOP.md) for the acceptance loop, evidence, and limitations.

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

Steering retains its full authored 32° range at speed. Full steering above 50 km/h can exceed the front tires' grip and push the car into a wide **front-tire skid**; that is not an input limiter or automatically a rear-wheel drift. Wheelspin and the handbrake now reduce the rear tires' available cornering grip. Try a brief Space press while turning, then release it and countersteer to catch the slide. Tire marks indicate actual post-peak sliding contacts; the F1 HUD distinguishes front, rear, and both-axle sliding.

Save files use `Application.persistentDataPath/garage-v1.json`. A successful replacement retains the prior valid save as `garage-v1.json.bak`. Invalid, incomplete, or unsupported saves are preserved and block automatic replacement. Saving is explicit; leaving the game does not automatically save changes.

## Repository layout

- `Packages/com.gdsg.motorbound.foundation`: stable identity, SI units, and deterministic utilities.
- `Packages/com.gdsg.motorbound.vehicle.core`: layered vehicle identities, authoritative assembly manifests, dimensional access checks, physical fitment validation, dependency-complete build plans, and data-driven powertrain definitions.
- `Packages/com.gdsg.motorbound.vehicle.physics`: tire-force math and the Unity vehicle prototype.
- `Assets/MotorBound/Product`: product composition, garage session and saves, procedural scene bootstrap, camera, HUD, and integration tests.
- `docs`: architecture, decisions, research brief, and milestone notes.

## Verification

On October 5, 2026, **108 native Unity EditMode tests passed**, including the combined-slip/sliding-force curve, bounded skid geometry and cleanup, garage persistence, and steering release in both directions at 30, 60, and 144 Hz input updates. The actual Unity driving validation passed **16 maneuvers and 267 assertions**, including the eight original handling maneuvers plus eight keyboard steering-release scenarios. The generated report is `artifacts/driving-validation.json`, with historical 0.2.1 before/after release measurements in `artifacts/steering-release-comparison.json`.

At 60 Hz input updates, the full steering command still centers in **83 ms**, compared with **300 ms** before the 0.2.1 release fix. With the 0.2.2 tire model, the faster dry-road release cases (approximately 51–53 km/h) added about **6.1–6.3°** of heading change over two seconds, with turning rate below **0.3°/s** by 500 ms without braking. These measurements cover both stock and touring configurations; subjective feel remains part of the player playtest.

The dedicated cornering run passed **22 maneuvers and 157 assertions** at 30, 45, 50, 55, and 70 km/h. It measures actual front-wheel pivot angle, axle slip, friction limits, contact-driven marks, and brief-handbrake recovery with countersteering. Full-lock turns retained 32° of steering at every tested speed; gentle 30/45 km/h turns stayed below the sliding peak and left no marks. The two brief-handbrake cases recovered after two seconds of slip-directed countersteering, retaining about 50.5 and 62.2 km/h from 55.2 and 70.0 km/h starts. Results are in `artifacts/cornering-validation.json`, with same-maneuver before/after tire-model measurements in `artifacts/cornering-comparison.json`.

The **Windows player version 0.2.2 built successfully** at `Builds/Windows/MotorBoundVehiclePrototype.exe` (build summary: 91,787,094 bytes). Keep its adjacent data files when running or copying the player. The 0.2.0 live player checks confirmed workshop startup, missing-hardware rejection, touring installation, incompatible-package rejection, the driving transition, and recovery. Visual inspection led to visible wheel openings and two-sided workshop signage. The new 0.2.2 skid feedback is covered by native contact/geometry checks and awaits the player's feel/visual assessment.

Measured mean controller-plus-`Physics.Simulate` step time was approximately **72–88 µs** in the 0.2.2 driving run at 360 Hz. This excludes rendering and UI and is not a rendered frame-rate or hardware performance guarantee.

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
