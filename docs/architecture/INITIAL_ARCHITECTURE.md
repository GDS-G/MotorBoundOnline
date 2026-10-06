# Initial Architecture

## Dependency direction

```text
MotorBound.Foundation
        ↓
MotorBound.Vehicle.Core
        ↓
MotorBound.Vehicle.Physics
        ↓
MotorBound.Product (prototype composition)
```

`Foundation` and most of `Vehicle.Core` use plain serializable C# data. `Vehicle.Physics` owns reusable force calculations plus the first Unity integration. Product code creates the temporary test world and presents telemetry.

## Boundaries established now

- `StableId` is value-based and invariant across display-name changes.
- `VehicleAssemblyManifest` distinguishes versioned catalog definitions from physical instances and prevents complete assemblies and constituents from both owning the same mass.
- `VehicleOperatingEnvelope` is derived from a named manifest revision and is evaluated against machine-readable route/facility restrictions.
- `PowertrainBuildPlan` is a proposal with dependencies and a bill of materials; it is never treated as installed inventory.
- Definition validation returns structured issues instead of logging from domain code.
- Torque maps and tire-force math can run without a scene.
- `AxleContactSolver` advances each driven wheel pair in shared tire/rotation microsteps. `DifferentialModel` exchanges equal-and-opposite angular impulses, bounded by mechanical capacity and the non-overshooting equalization impulse. It cannot add axle angular momentum or coupling energy.
- `VehicleAssistController` observes inputs and physical motion separately from those solvers. Its only decision is whether the positive-engine-torque limiter may intervene; it cannot command steering, tire grip, chassis yaw or speed.
- Unity-specific rigid-body and raycast work is isolated in the physics package.
- Input is converted into a timestamp-independent `VehicleInputState` before physics consumes it.
- Telemetry is a read-only snapshot for HUD, future audio, replay, and networking consumers.

## Intentional prototype simplifications

- A single rigid chassis is used; unsprung masses are represented through wheel inertia but not independent rigid bodies.
- Tire relaxation length, temperature, wear, pressure, and aligning moment are deferred.
- Open and passive clutch-limited-slip axle definitions are supported. The Club prototype's provisional rear clutch LSD is authored within its reference running-gear assembly, not a newly purchasable garage upgrade. AWD still has an equal front/rear center torque split, not a center differential model.
- The engine/transmission clutch is an automatic launch approximation. The axle differential clutch does not simulate engine rotational inertia, clutch-kicks, stalling, gear-change duration or shaft compliance.
- ROAD assistance prioritizes grip; SPORT uses the same road policy until a deliberate handbrake turn, then leaves the physical slide to the driver through entry/sustain/recovery; OFF bypasses propulsion assistance. SPORT is the default. These are session preferences, not installed factory ECU equipment or assembly-save fields.
- The course is procedural primitive geometry and not the road-corridor pipeline.
- The current rendering path is built-in and disposable; production HDRP remains a validation decision.

These simplifications are isolated behind types that can be replaced without changing vehicle catalog identities.
