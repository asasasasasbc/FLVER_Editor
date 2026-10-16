# OpenPhysicsHKX Core

Prebuilt managed dependency: `../../OpenPhysicsHKX.Core.dll` (version 0.1.0.0).
MIT license: `LICENSE`. Source repository:
https://github.com/asasasasasbc/OpenPhysicsHKX
Source revision: `12f8e7c1efc647cd5792ac4903e3e3570cf186d0`.

The Editor references this DLL directly and copies it to its output directory.
Building the Editor does not compile Core or require an external source checkout.
No Core source, project, CLI, or research fixtures are included in this folder.

## Updating the DLL

Maintain Core in its independent source project, not in this repository.
For the local development workspace:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:\OpenPhysicsHKX\build.ps1 -Test
Copy-Item D:\OpenPhysicsHKX\bin\Release\OpenPhysicsHKX.Core.dll D:\FLVER_Editor\MySFformat\OpenPhysicsHKX.Core.dll
```

Then rebuild the Editor. Record the source revision in this file when updating
the dependency. Do not copy the CLI, test
executables, game assets or native vendor runtimes alongside it.

Experimental numeric cloth preview, not a general HKX parser or complete HCT
replacement. Kernel arithmetic and known baseline limitations are unchanged.
Core includes opt-in BendLink distance-band and Transition reference-envelope
approximations; these are not part of the imported parity baseline.
No vendor runtime, game data, or research evidence is included here.

Current Core includes explicit frame-input updates from commit `12f8e7c`.
These inputs do not by themselves implement the Editor's missing HKX binding graph.
