# MotorBound Online

MotorBound Online is a persistent automotive-world simulation built around mechanical consequence, physical travel, unique property ownership, and a living player economy.

This repository currently contains the first engineering prototype: a playable rear-wheel-drive Kiyora Aven test vehicle with a 360 Hz critical-vehicle step, tire-width contact sampling, layered water/roughness surface state, and a custom tire-force simulation. It intentionally does not use Unity `WheelCollider`.

## Open the prototype

1. Open the repository root in Unity `2022.3.62f2` (the locally available prototype baseline).
2. Use **MotorBound > Configure Prototype Project** if the prototype scene has not yet been generated.
3. Open `Assets/MotorBound/Product/Scenes/VehicleDynamicsPrototype.unity`.
4. Enter Play Mode.

Controls:

- `W`: throttle
- `S`: brake
- `A` / `D`: steer
- `Space`: handbrake
- `Backspace`: recover the vehicle
- `F1`: toggle telemetry/help

The course includes a skidpad, a 2.5 mm water-film section, a physical rough-road strip, lane markers, and obstacles for early handling validation.

## Repository layout

- `Packages/com.gdsg.motorbound.foundation`: stable identity, SI units, and deterministic utilities.
- `Packages/com.gdsg.motorbound.vehicle.core`: layered vehicle identities, physical fitment validation, and data-driven powertrain definitions.
- `Packages/com.gdsg.motorbound.vehicle.physics`: tire-force math and the Unity vehicle prototype.
- `Assets/MotorBound/Product`: product composition, procedural scene bootstrap, camera, and HUD.
- `docs`: architecture, decisions, research brief, and milestone notes.

## Verification

From Unity, run **Window > General > Test Runner > EditMode**. A command-line build can be produced with:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'E:\Repositories\MotorBoundOnline' `
  -executeMethod MotorBound.Editor.PrototypeProjectSetup.BuildWindowsPrototype `
  -logFile '-'
```

## Production engine gate

The GDD recommends a Unity 6-class LTS production branch only after console compatibility, middleware, licensing, HDRP, and physics profiling are validated. Unity `2022.3.62f2` is used here solely because it is installed in the current development environment; the simulation packages avoid version-specific rendering dependencies to keep that engine decision reversible.
