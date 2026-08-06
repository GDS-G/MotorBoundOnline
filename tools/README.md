# Verification tools

`MotorBound.DomainVerification` compiles and runs the engine-independent identity, units, vehicle-definition, torque-map, and tire-force checks without requiring a Unity license:

```powershell
dotnet run --project tools/MotorBound.DomainVerification/MotorBound.DomainVerification.csproj
```

`MotorBound.UnityRuntimeCompileCheck` compiles all runtime and editor source against installed Unity reference assemblies without launching the editor:

```powershell
dotnet build tools/MotorBound.UnityRuntimeCompileCheck/MotorBound.UnityRuntimeCompileCheck.csproj `
  -p:UnityManagedPath='C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Data\Managed'
```

These checks complement, but do not replace, Unity EditMode tests and a player build. They are especially useful on machines where the editor is installed but has not been activated.
