# OpenPhysicsHKX Core

Prebuilt managed dependency: `../../OpenPhysicsHKX.Core.dll` (version 0.3.1.0).
MIT license: `LICENSE`. Source repository:
https://github.com/asasasasasbc/OpenPhysicsHKX
Source base revision: `12f8e7c1efc647cd5792ac4903e3e3570cf186d0`, plus local
2026-10-16 VolumeMx shape-matching, explicit inactive schedules, multi-buffer /
per-constraint reference-normal API changes, and 2026-10-18 opt-in fixed-anchor
Stretch correction in
`D:\OpenPhysicsHKX` (not yet committed or pushed).

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
replacement. Imported baseline arithmetic and its known limitations are unchanged.
Core includes opt-in BendLink distance-band, Transition reference-envelope and
VolumeMx rigid shape-matching approximations; these are not part of the imported
parity baseline.
The Editor enables `RespectFixedStretchAnchors`: fixed/dynamic a3c Stretch pairs
correct the dynamic endpoint rather than moving/re-pinning the anchor. Both-dynamic
Stretch arithmetic and the default API/CLI baseline remain unchanged; this is not
a general native-equivalence claim. Core has 101 synthetic checks passing.
No vendor runtime, game data, or research evidence is included here.

Current Core includes explicit frame-input updates from commit `12f8e7c`.
The Editor evaluates rest-skeleton Skin and required MeshMeshDeform/copy/gather
reference buffers separately in its authored serial graph; Core stays a numeric
library and does not itself parse or execute a general HKX dependency graph.
