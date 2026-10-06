# MotorBound Online

MotorBound Online's design targets a persistent automotive world built around mechanical consequence, physical travel, unique property ownership, and a living player economy.

This repository currently contains milestone **0.2: a local garage → test drive → return-and-save loop** for one rear-wheel-drive Kiyora Aven. The garage's installed assembly determines vehicle mass, supported wheel visuals, tire parameters, and the operating envelope. Stock and touring wheel packages connect the assembly manifest, dependency planner, fitment checks, driving controller, and versioned local save.

The driving prototype uses a 360 Hz critical-vehicle step, tire-width contact sampling, water/roughness surface state, and custom tire forces without Unity `WheelCollider`. Version 0.2.4 adds stable wheel-slip integration, more progressive keyboard steering, a gentler sliding tail, and visible switchable road traction control. It retains the packaged-player shader fix and quick steering return. Multiplayer, the economy, property ownership, and production vehicle inventory remain future work. See [the garage milestone guide](docs/prototypes/GARAGE_TEST_LOOP.md) for the acceptance loop, evidence, and limitations.

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
- `F2`: toggle road traction control (starts ON; OFF permits unassisted power slides)

The course includes a workshop entrance with clearance checks, a skidpad, a 2.5 mm water-film section, a physical rough-road strip, lane markers, and obstacles. Parts are supplied for testing. The stock configuration is 1120 kg with 305 mm-radius, 205 mm-wide tires; touring plus required hardware is 1130 kg with 315 mm-radius, 225 mm-wide tires. These are provisional authored values, not measured real-vehicle calibration.

Steering retains its full authored 32° range at speed, reached in half a second of held-key input; short taps are less abrupt and release remains quick. Full steering above 50 km/h can exceed the front tires' grip and push the car into a wide **front-tire skid**; that is not an input limiter or automatically a rear-wheel drift. Road traction control reduces positive engine torque to limit wheelspin; the HUD shows when it intervenes, and F2 disables it. The handbrake remains available for deliberate slides. Try a brief Space press while turning, then release it and countersteer to catch the slide. Tire marks indicate actual post-peak sliding contacts; the F1 HUD distinguishes front, rear, and both-axle sliding.

Save files use `Application.persistentDataPath/garage-v1.json`. A successful replacement retains the prior valid save as `garage-v1.json.bak`. Invalid, incomplete, or unsupported saves are preserved and block automatic replacement. Saving is explicit; leaving the game does not automatically save changes.

## Repository layout

- `Packages/com.gdsg.motorbound.foundation`: stable identity, SI units, and deterministic utilities.
- `Packages/com.gdsg.motorbound.vehicle.core`: layered vehicle identities, authoritative assembly manifests, dimensional access checks, physical fitment validation, dependency-complete build plans, and data-driven powertrain definitions.
- `Packages/com.gdsg.motorbound.vehicle.physics`: tire-force math and the Unity vehicle prototype.
- `Assets/MotorBound/Product`: product composition, garage session and saves, procedural scene bootstrap, camera, HUD, and integration tests.
- `docs`: architecture, decisions, research brief, and milestone notes.

## Verification

On October 6, 2026, **164 native Unity EditMode tests passed**, covering stable wheel integration, torque/force consistency, tire grip bounds and independent sliding tails, ROAD torque-control limits/bypasses, steering press/release/countersteer, garage persistence, and retained skid-shader loading. **15 engine-independent checks passed**. The actual dry/wet/rough-road driving run passed **16 maneuvers / 267 assertions** across stock/touring wheels and both steering directions.

At 60 Hz, a held key reaches full steering in half a second and full-lock release centers in **83 ms**. All driving-run release cases had yaw magnitude below **0.22°/s at 500 ms**. Their press duration is now 0.55 seconds rather than 0.35 seconds, so heading totals should not be treated as same-input comparisons with older builds.

The cornering run passed **70 maneuvers / 501 assertions**: 22 unassisted tire-limit and handbrake-recovery cases plus 24 assisted routine cases on each wheel package. Mild full-throttle bends at 30/50/70 km/h left no marks; the 70 km/h bends peaked at about **1.68° stock / 1.70° touring** body sideslip and regained sustained near-straight travel about **1.03 / 1.20 seconds** after release. Full 32° steering remains available at every tested speed, and intentional unassisted slides remain possible. Results: `artifacts/cornering-validation.json`; stock before/after comparison: `artifacts/road-handling-comparison.json`. Human driving-feel playtesting remains necessary.

The **Windows player version 0.2.4 built successfully** at `Builds/Windows/0.2.4/MotorBoundVehiclePrototype.exe` (build summary: 91,800,770 bytes). This versioned folder leaves a running older root-folder player untouched. Keep the adjacent data files when running or copying it. The retained skid-shader Resources dependency and material binding still pass native tests; human high-speed feel remains playtest work.

The new rendered startup/F2 key path remains unverified because computer-use launch attempts timed out. Refreshed process/window checks showed only the user's existing player; no save was created or overwritten. `artifacts/handling-release-validation.json` records that limitation separately from successful native tests/building.

Measured mean controller-plus-`Physics.Simulate` step time was approximately **77–84 µs** in the 0.2.4 driving run at 360 Hz. This excludes rendering and UI and is not a rendered frame-rate or hardware performance guarantee.

From Unity, run **Window > General > Test Runner > EditMode**, then **MotorBound > Validate Driving Physics** outside Play Mode with modified scenes saved. To produce a Windows development player, run this from the repository root (adjust the Editor executable for your installation):

```powershell
$motorBoundProject = (Get-Location).Path
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
& $unityEditor `
  -batchmode -nographics -quit `
  -projectPath $motorBoundProject `
  -executeMethod MotorBound.Editor.PrototypeProjectSetup.BuildVersionedWindowsPrototype `
  -logFile '-'
```

After a successful versioned build, launch `Builds/Windows/0.2.4/MotorBoundVehiclePrototype.exe` with its adjacent data files intact. The original **MotorBound > Build Windows Prototype** menu still targets the root Windows folder; do not use it while that older player is running. For native test, driving-validation, and engine-independent commands, see [the milestone guide](docs/prototypes/GARAGE_TEST_LOOP.md#verification).

## Production engine gate

The GDD recommends a Unity 6-class LTS production branch only after console compatibility, middleware, licensing, HDRP, and physics profiling are validated. Unity `2022.3.62f2` is used here solely because it is installed in the current development environment; the simulation packages avoid version-specific rendering dependencies to keep that engine decision reversible.
