# Experimental local HCT preview host

An isolated x64 .NET Framework process, a trace-driven DLL-only runtime subset,
and native Cloth-window playback. Only `ClothPreviewSession.cs` is compiled into
the editor (along with shared `PreviewEnvironment` settings); native HCT calls remain in the separate helper process. This is not
a portable Havok replacement.

## Cloth-window integration

The playback toolbar has **Play Cloth / Pause Cloth**, **Stop Cloth**, **Reset Cloth**
and **Reset View** (camera only); **HCT Runtime…** lives in Preview Environment. Runtime selection uses the same standard file-picker style as
Load Cloth, selecting `hctPreviewPlugin.dll` and inferring its runtime root.
Matching CLM2 companions load with HKX, including direct Binder entries; manual
comparison data and unsaved CLM2 edits are not silently replaced.

The editor snapshots current in-memory XML, FLVER and CLM2 into a new work
directory. A background builder prepares the scene, then the isolated host
streams real particle positions through a current-user-only local named pipe.
One frame request is allowed in flight, with up to four fixed 1/60 s substeps.
The Cloth canvas draws those positions without mutating authored rest arrays.
Pause retains the session; Reset recreates native history. Loading/editing source
data, invalidating the FLVER snapshot or closing the form retires the owned host.
Cancelled startup cannot resurrect a disposed/changed window.

The HKX Animation tab can stream mapped poses to the native host. The Cloth canvas
also offers an off-by-default, bone-weight-skinned FLVER overlay with independent
extra model loading. That shaded geometry layer is not native Cloth mesh output or
game-material rendering, and no main-Viewer deformation/live-game input is implied.
GUI acceptance covers focused BB/DS3 movement and animation checks, source protection
and mesh-overlay/layout checks, not certification of every asset.

### Hunter fixture initialization and metadata (2026-10-14)

The original Hunter body packages contain compact/remapped reference meshes and
dependent simulations (coat → chain, belt → dangling belt). The former identity-only
CopyVertices initialization rejected them. For those audited routes, initialization
seeds the native simulation's XYZ from its own saved simClothPose, retaining the native
fourth component. The temporary zero-count copy bypasses only the initialization
solver; the original move/deformation/solver operators are restored afterward.
The same rest-pose reconstruction (0.0005 tolerance), history-copy, index and finite
gates remain. This is initialization from file data, never a fabricated frame displacement.

Private runtime schema normalization includes audited legacy BendLink and
MeshMeshDeform classes absent from the narrow DS3 closure. Ordinary DS3 metadata
is not expanded with those legacy classes. Hunter male additionally needed the
PNT mesh deformer and corrected BB-only parent-tail offsets; see
`tools/Cloth/hunter-schema-audit.md`. Both original female/male packages now show
finite native movement in every simulation domain and play a real character HKX.

The Preview Environment tab configures **preview-only environment**. Gravity override modifies
only the temporary runtime HKX before loading, with codec readback validation.
Native wind is routed through `hctPreviewClothWindForm.setWindEnabled`,
`setWindDirection`, `setWindMinSpeed` / `setWindMaxSpeed` (equal for constant wind),
`setWindMaxDrag`, and `applyWindSettings`. Native value readback detects clamping.
Original wind settings are restored before vendor shutdown. No fake particle
offsets, guessed gravity setter or source-file writeback is used.

Apply validates settings, recreates the current host and retains play/pause;
Stop retires it without restarting. File Gravity / Wind Off restores defaults.
Protocol v3 hello is `0x33565043`; step payload includes wind enabled plus xyz,
speed and max-drag floats after command/sequence/substep count. Gravity is baked
only into that session's temporary asset copy. The client snapshots settings and
enforces at most one in-flight request; changing UI fields alone does not change
the active session.

Focused environment comparisons on DS3 at 120 frames showed native trajectory
differences for zero vs downward gravity and ±X wind. The local handoff directory
contains input/output evidence and the exact version-bound ABI inventory. Those
differences demonstrate response, not discovery of the native wind algorithm.

**2026-10-13 correction:** environment controls now live in the Preview Environment
tab; native wind is enabled by default. The old direct world stepping path skipped
the complete preview environment behavior. The adapter now invokes managed
`PreviewPlugin.hctPreviewControl.step(Single)` (metadata token `0x0600049B`) for
initialization and normal frames, retaining the explicit readback/world post
sequence (`0x733DD0`). The old scratch-size/pre/world-step RVAs remain historical
reference only, not current direct calls. Mixed direct/wrapper initialization with
omitted post failed in an isolated probe and is not shipped. The wrapper's internal
algorithm was not decompiled; claims here are based on controlled input/output.

DS3 600-frame off/strong-X wind particle max component difference: old path
`0.000292226`, corrected wrapper path `0.222235747`; all outputs were finite and
source hashes matched. BB v10 with wind also passed 600 frames. Editing GUI tests
covered both formats, applied range/gravity/constraint/mass edits, exported copies,
Undo/Redo, stopped/playing/paused Apply semantics and source preservation.

`PreviewBuild.targets` builds/deploys helpers under `bin/<configuration>/tools/
ClothPreview` using C# only. `PreviewSchemaGenerator.cs` creates private augmented
schemas in `obj`, without Python, HCT, archived tests or the local DLL subset as
build dependencies. Vendor DLLs are never copied by the application build.
The Python runners below remain optional local research tools.

```powershell
py -3.12 MySFformat/src/Cloth/Preview/probe_runtime.py --runtime "<your HCT installation>"
```

The runner keeps build evidence in a fresh `helper-ext/cloth/preview-work/<run>/`, initializes
the original installation, records loaded native modules and managed assemblies,
copies only observed vendor DLLs (preserving paths and the EULA), then starts a
fresh process using the subset. It reports original-installation fallbacks and
vendor assemblies loaded elsewhere. Reports, executables and vendor DLLs remain
Git-ignored. No existing editor/game process is controlled or terminated.

The executable is now deployed at the stable owned path
`helper-ext/cloth/preview-host/HctPreviewProbe.exe`; timestamped directories
contain evidence, not a newly executed program path each time. The manifest
explicitly requests `asInvoker`, not administrator elevation.

The probe has a private executable configuration enabling CLR development
probing and sets `DEVPATH` to the tested runtime's `tools/gac` directory. This
prevents the installed Havok assemblies in the machine-wide GAC from masking
missing app-local dependencies. It also explicitly preloads the runtime-local
`utils/hkCompatFormats.dll`, which otherwise resolves to the installation.
Neither machine-wide CLR configuration, GAC nor the installation is modified.

### Offline network guard

The earlier popup was a **Windows Defender Firewall network prompt**, not an
elevation request in the executable manifest. The HCT graphics/preview code
starts debugger/discovery sockets (one warning mentions VDB port 25002).
Changing executable paths repeatedly caused new firewall prompts.

The host now denies vendor `socket`/`WSASocket*` calls with `WSAEACCES`, using
process-local import bindings before initializing the vendor subsystem. Only
this child's loaded Havok/HCT modules are affected. The guard restores those
bindings after shutdown; it changes neither vendor files/solver instructions,
system hooks nor firewall rules. Named pipes and the editor are unaffected.
Reports record guarded imports and denied calls. Both successful fixture runs
guarded five socket imports and denied two socket creations. The expected
"VDB server could not create connection" warning does not mean cloth failure.

## First observed result (2026-10-12)

Installed-runtime and copied-subset probes both completed native initialization
and cleanup. The successful subset contained six DLLs, 73,489,920 bytes:

```text
tools/hctPreviewPlugin.dll
tools/gac/Havok.Tool.UI.dll
tools/gac/Havok.Tool.Interfaces.dll
tools/gac/Havok.Graphics.dll
tools/gac/Havok.Graphics.Bridge.dll
utils/hkCompatFormats.dll
```

Loaded-module and managed-assembly snapshots showed no original-installation or
external Havok assembly fallback for that run. Evidence and SHA-256 inventory
are in the local runner output. This does not establish operation on a clean
machine without HCT registry entries, native VC runtimes or graphics drivers.

## Native cloth PoC (2026-10-12)

```powershell
py -3.12 MySFformat/src/Cloth/Preview/run_native_preview.py --runtime "<local DLL subset>" --cloth "<cloth.hkx>" --flver "<model.flver>" --clm2 "<cloth.clm2>" --frames 600
```

`--clm2` is optional but should be supplied for a companion-file test. The
builder verifies its mesh count and records entries. Preview seam copies are
restricted to authored-written source vertices, unwritten targets, valid bounds
and coincident reference positions; unknown CLM2 semantics are not generalized.
Copies affect returned preview positions only, not native constraint data.

The scene adapter preserves node names/hierarchy, transforms, mesh topology and
the HKX-authored section index. It is generated with private scene schemas and
the existing native packfile codec's full-field write/readback check. Scene
schemas are **not** added to the editor's embedded cloth resources. Unsupported
scene arrays remain empty; this is not an arbitrary HKX scene editor.

| Fixture | Frames | Instances / particles | Changed particles | CLM2 | Observed ms/frame |
|---|---:|---|---:|---|---:|
| DS3 `BD_M_3000` | 600 | 5 / 699 | 511 | 15 mesh records / 15 entries; 13 preview aliases | 0.159 |
| Bloodborne Clorinde v10 cape/shawl | 600 | 2 / 1180 | 901 | 2 mesh records / 0 entries | 0.387 |

Both used a constant authored reference pose and native 1/60 s steps. Particle
and display positions stayed finite and changed measurably. Measurements include
pose submission, native steps and readback, exclude startup, and are **not**
Viewer FPS or an animation/IPC benchmark. Long-frame or game-parity certification
is not claimed. All source HKX/FLVER/CLM2 SHA-256 values remained unchanged;
vendor modules stayed inside the copied runtime directory.

### Bloodborne runtime-layout adapter

This HCT's CompatFormats rejects BB `8,1,1,1` packfiles. For the tested BB graph,
the builder preserves the original file separately and generates an ephemeral
`8,1,0,1` runtime copy with complete known-field pack/readback comparison. The
probe-only closure adds HKX2-audited sphere (payload at 32, not BB's 16) and
GatherAll layouts; it never broad-copies missing BB offsets into the DS3 schema.
The editor/game files and normal BB write profile remain unchanged. This is not
a universal inter-game conversion or an output intended for game installation.

The tested v10 has no authored transition states. Its identity particle input
route is audited, including padding beyond the particle count. One initialization
pass temporarily substitutes native CopyVertices for each Simulate entry, checks
that copied positions reconstruct the authored rest pose within 0.0005 units,
synchronizes previous-particle history, and restores the original operator list
before solving. This mechanism follows the upstream host approach; it does not
change forces/constraints or author files. General animation rewind, transfer-
motion history and collision history are still pending.

Run summaries, input hashes, module snapshots, original/runtime HKX copies,
scene XML/binary and initial/final particle/display arrays remain under
`helper-ext/cloth/preview-work/`. Invalid layouts, missing bindings and unsupported
initialization routes fail closed; a stalled/crashed native call is bounded by
the runner timeout, which terminates only its owned child.

Repeat acceptance (requires `psutil`, two or more `--assets` values are allowed):

```powershell
py -3.12 MySFformat/src/Cloth/Preview/test_preview.py --runtime "<local DLL subset>" --assets "<DS3 run/assets>" --assets "<BB run/assets>"
```

The 2026-10-12 acceptance passed repeated deterministic movement checks for both
fixtures, CLM2 presence, version rejection before native loading, refusal to
overwrite an existing report, unchanged vendor DLL hashes, and no observed
TCP/UDP endpoints on the owned child PID. No global network policy was changed.

## Animation integration notes

Current implementation streams per-substep mapped poses using protocol v4
(`0x34565043`). HKX decoding and mapping reuse `HkxAnimationClip`/`HkxPoseBinding`.
Play, a separate Pause button, Stop, seeking and looping are available in the
Cloth window. Seek/loop recreates owned physics history, not preceding-frame replay.
The older design notes below are not an implementation-completeness checklist.

Client and host use separate 64 KiB read/write buffers on their duplex pipe.
Every complete request/response is flushed before waiting on its peer. This preserves
the protocol bytes, bounds/finite-value checks and solver/wind lifecycle while avoiding
thousands of kernel I/O calls for float matrices. Focused BB measurements (60 steps)
fell from 4460 ms to 245 ms end-to-end; GUI playback advanced 59 steps in about 1 s.

Keep author inspection/editing in the existing independent Cloth window.
Place playback-time physics controls/status in **HKX Animation**, sharing its
selected model, clip and playback clock rather than adding a second timeline.

```text
animation sampling -> immutable input bone pose + root transform
                   -> physics worker -> isolated native host
                   -> completed bone/cloth vertex frame -> Viewer
```

- Do not perform synchronous pipe reads, initialization or physics steps from
  WinForms timer callbacks or the MonoGame draw thread.
- Use a bounded queue with generation/sequence IDs. Discard outdated render
  results after clip/model changes; physics must retain ordered time steps.
- Render cadence and solver cadence are separate. Interpolate animation input
  over fixed simulation substeps; bound catch-up work and define overload/reset
  behavior instead of building an unbounded queue or silently skipping history.
- Seeking, looping and teleports require authored initialization and motion-
  history reset. Pause freezes the preview by default; an optional settle mode
  must have explicit behavior. Closing playback/disabling physics restores the
  animation-only preview and stops only the owned host.
- Never feed simulated bone outputs back into the next animation input pose.
  Apply only authored output bones/vertices and keep FLVER author data unchanged.
- The current playback uses a 16 ms WinForms timer, which is not a 60 FPS
  guarantee. `HkxPlaybackForm.ApplyFrame` publishes a pose; Viewer consumes it
  through `Program.UpdateVerticesCore(true)`. Bind/static geometry is cached,
  but pose and render geometry are still rebuilt. Profile sampling, CPU skinning,
  geometry generation/upload, native steps and IPC separately before optimizing.
- Consider cached/indexed geometry, reduced per-frame allocation and dynamic
  vertex buffers before adding a second full geometry rebuild for cloth. Report
  measured render FPS and solver milliseconds, not the 60 Hz sample-rate label.

## Scope and acceptance

- SHA-256 version gate for `hctPreviewPlugin.dll`; no guessed native offsets.
- Reflection-based HCT initialization, hidden Direct3D11 graphics context and
  deterministic cleanup in a separate process.
- `PASS_ISOLATED_BOOTSTRAP` proves initialization only. It does **not** prove
  particle simulation, animation binding, collision response, game parity,
  absence of registry/config access or a mathematically minimal dependency set.
- Deferred asset-loader and solver dependencies may expand the DLL subset.
- `PASS_NATIVE_CLOTH_POC` proves the tested constant-pose native steps, finite
  changing particle/display outputs, source protection and local vendor loading.
  It does not certify animations, live viewport integration or game parity.
- The next milestone requires streamed animation poses, history reset and
  validated output routing into the Viewer. Do not feed cloth-only data to the public
  preview loader: it may enable an asset before its scene exists.
- CPU stepping does not imply a graphics-free host. Removing the hidden graphics
  context is a separate, unproven goal.

## Credits and licensing

Initialization sequence informed by
[KamiyamaShiki0704/DSAnimStudio](https://github.com/KamiyamaShiki0704/DSAnimStudio/tree/feat/root-motion-bullet-physics-fx-preview),
`NativePhysicsHost/NativePhysicsHost.cs` (GPL-3.0); original DSAnimStudio and fork
contributors retain credit. This adapter is GPL-3.0-or-later; see the repository
`LICENSE.txt`. It contains no Havok implementation or binaries.

Scene layouts/adaptation also reference GPL-3.0
[SoulsAssetPipeline / HKX2](https://github.com/KamiyamaShiki0704/SoulsAssetPipeline/tree/d11caf989c917c7a43e2c7559915b1c5af218153),
including Meowmaritus, HKX2 and fork contributors. No incomplete downloaded
submodule or external upstream source directory is needed to build the PoC.

User-supplied HCT is proprietary. The examined installation's EULA permits
limited internal evaluation/prototyping, restricts redistribution and explicitly
prohibits reverse engineering. This experiment does not decompile/extract vendor
solver code, rewrite vendor files, bypass license checks, grant license rights or determine
whether the user's current authorization is valid. Do not distribute the local
DLL subset; verify applicable licensing before further use or distribution.
