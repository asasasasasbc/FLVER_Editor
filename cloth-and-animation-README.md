# Cloth and HKX Animation

FLVER Editor offers two separate workflows: skeletal **HKX animation playback**
in the Viewer, and a dedicated **Cloth** window for serialized cloth inspection,
editing and writeback. Animation playback does not run cloth physics.

## HKX animation playback

1. Open a FLVER and its 3D Viewer, then open **HKX Animation**.
2. Choose the character's `skeleton.hkx`, then load the matching animation HKX.
3. Use Play/Pause, Stop, previous/next frame, timeline seeking, speed and Loop.
4. Use Bind Pose to clear the animation preview and restore the static pose.

The timeline uses 60 Hz samples. Preview binds the animation skeleton to the
model and updates displayed geometry without saving changes to the FLVER or
game files. Supported decoding depends on the bundled animation tool and input
format; this is not an animation HKX writer or a cloth solver.

Animated screenshot rendering also supports `--hkx-skeleton`, `--hkx-animation`
and `--hkx-frame`; see [CLI render help](CLI_RENDER_HELP.md).

## Local Bloodborne Cloth Inspector (2026-10-12)

The **Cloth** button sits alongside Material, Mesh and Dummy. It opens an
independent window. UI labels are English; tooltips are English/Chinese.

- Load a cloth `_c.hkx`, parts Binder (`.dcx`) or author XML.
- An empty window automatically looks for `<FLVER name>_c.hkx` beside the
  loaded FLVER and loads its matching CLM2 companion at the same time. Direct
  Binder inputs can supply both entries. Reopening does not replace an existing
  comparison document; manually selected or unsaved CLM2 data is preserved.
- Inspect simulation domains, fixed/dynamic particles, triangles, constraints,
  solver settings, collision masks, bone subsets and rest-space colliders.
- Rotate/zoom the rest wireframe and select particles to view LocalRange
  `maximumDistance` spheres. Fixed status uses particle inverse mass, not just
  the length of a `fixedParticles` array.
- **Save Range XML Copy** exports a separate author XML, scaling LocalRange
  distances only for the selected domain.
- **Edit HKX** opens a full-XML editor on a clone. **Validate / Apply** checks
  supported schemas, required fields, references, counts, particle/solver
  indices and a complete native pack/readback before applying edits in memory.
  Cancel leaves the previous document intact.
- **Save HKX Copy** writes an actual binary HKX using the selected layout.
  Export range scale affects only the selected domain; use 1.00 for no scaling.
  Invalid data and existing/source output paths are refused. Files are staged
  beside the destination and renamed only after validation and complete write.

The codec is a **local C# implementation informed by hkxpack-souls**.
It handles the audited Havok 2014.1 v11 64-bit little-endian cloth profiles:
object/inline-struct layouts, declared array strides, strings, enums and
source-address fixups. Unsupported SDKs/classes/signatures fail closed.
Known-field equivalence does not mean identical file layout or preservation of
runtime-only ignored fields and padding. Credits and licenses are in the
[main README](README.md#credits-and-third-party-components).

### CLM2 editing and companion verification

The second toolbar row contains **Load CLM2 / Edit CLM2 / Save CLM2 /
Verify Clothes**. The modal editor exposes the known mesh-record layout and
two unknown signed Int16 entry fields (`Unk00`, `Unk02`). Apply updates a clone
in memory; Cancel discards it; saves use SoulsFormats and require a new path.

Verify Clothes compares CLM2 mesh count with the **current in-memory FLVER**.
HKX display names/vertex counts are checked separately: simulation-domain count
is not the CLM2 mesh count. Reports distinguish PASS, FAIL, WARN and SKIP.
Entry vertex-pair meaning is only a hypothesis; empty entries or valid bounds
do not certify runtime sufficiency.

### Dark Souls III Cloth

DS3 uses a **separate layout schema**, not BB offsets. The supplied `BD_M_3000`
equipment passes full-field native read/write, independent binary checks,
native CLI, companion matching and actual popup editing. It has 170 objects,
five simulation domains and 15 FLVER/CLM2 mesh records with 15 CLM2 entries;
CLM2 serialization is byte-identical. One `skirt1` display name matches multiple
FLVER meshes and remains an ambiguous-binding warning.

The tested DS3 header `8,1,0,1` selects its 81-class schema; BB regression layout
`8,1,1,1` selects the corrected 90-class closure. Identical class signatures do
not guarantee identical offsets: DS3 Simulate's execution array is at 48,
BB's at 40; capsule and converter fields differ too. Native writeback preserves
input layout. These mappings certify the tested profiles, not all DS3 assets
or a universal game identifier.

Native DS3 XML carries `nativeLayout="8,1,0,1"`. Legacy XML without layout
defaults to BB: **select DS3 in the Layout version box on the toolbar before saving**, or use
the CLI layout option below. Changing layout selects the write schema; it is
not a complete automatic conversion between games.

### Native Cloth playback in the Cloth window

**Preview Environment → Render FLVER Meshes (Bone Weights)** is off by default.
Enable it to overlay the current FLVER with its original bone-weight skinning.
**Add FLVER…** loads independent models without replacing the editor's model or
automatically enabling rendering; **Remove Selected** removes only the overlay.
Loaded HKX clips map to each model by bone name. Unmatched models stay in rest pose
with a warning. This is shaded, depth-sorted geometry, not game textures/MTD shaders
or Cloth-deformed display meshes. Extra meshes do not add physics colliders.
Particle/collider overlays remain separate.

The HKX save/layout controls occupy a fixed second toolbar row even in wide windows;
CLM2 actions remain available on their own following row.

Original Hunter-set female and male body Cloth fixtures can now initialize and play,
including real HKX input. Reduced/remapped reference meshes use their saved simulation
pose at initialization instead of an invalid contiguous particle copy. This is not a
claim that every unsupported Havok class or game asset is now supported.

Optional native playback is now available on the playback toolbar row:
**Play Cloth / Pause Cloth**, **Stop Cloth**, **Reset Cloth** (new native history)
and **Reset View** (camera only); **HCT Runtime…** lives on the Preview Environment tab.
Choose `hctPreviewPlugin.dll` with the standard file picker; the runtime root is
detected automatically. The selected local path is remembered for this user.

The Cloth canvas shows real simulated particles and wireframes. In its **HKX Animation**
tab, select the character `skeleton.hkx`, load an animation HKX, enable **Drive Native Cloth**
and use **Play HKX**, **Pause HKX** or **Stop HKX**. Pause keeps the current frame and
physics history; Play resumes; Stop returns to frame 0. Seeking and looping reset
physics rather than replaying previous frames. Collider overlays follow animation;
range circles and parameter tables still show saved data. This does not deform the
main Viewer model or read live-game forces. Closing the window stops its owned host.

Animation IPC uses buffered, batched pipe transfers instead of one system call per
float; native physics and wind stepping are unchanged. A focused local BB test advanced
59 physics steps in approximately one second of GUI playback; this is not a guarantee
for every model, machine or animation.

**Stop Cloth** ends the host without restarting and keeps the environment controls.
The Preview Environment tab contains preview-only gravity XYZ, native wind direction XYZ,
constant wind speed and native maximum drag. **Apply Environment** recreates
the current host with those settings, retaining its playing/paused state;
**File Gravity / Wind Off** restores author gravity and disables preview wind.
Changed controls take effect on Apply, Reset or the next new Play, not by silently
editing the running solver or author data.

Gravity overrides only the temporary runtime HKX `simulationInfo.gravity` values.
Wind uses native HCT `hctPreviewClothWindForm` set-direction/min/max-speed/max-drag
and apply-settings methods; there are no fabricated particle translations.
The tested samples respond differently to zero/downward gravity and ±X wind.
Native wind participation/drag remains asset-dependent and is not a simulation
of all game-world wind behavior. Applying environment settings does not change
HKX exports, source files or the editor's author document.

### Direct author editing and particle selection (2026-10-13)

The Cloth window now has editable author tables and a separate **Preview Environment**
tab. The CLM2 action row is retained. **Edit HKX XML…** is the advanced full-XML
editor, not the normal parameter editor. Export range scaling lives in the Raw XML
tab and remains an export-only operation.

- **Parameters / Binding**: edit simulation gravity `(x y z 0)`, damping/tolerance,
  supported booleans, and solver iteration/substep counts. Bindings and index arrays
  remain read-only.
- **Particles / Range**: edit dynamic mass (updates reciprocal inverse mass), radius,
  friction and existing LocalRange maximum distances. IDs, fixed state, inverse mass
  and collision masks are read-only. Missing LocalRange records are not invented;
  setting a particle's distance updates all its existing LocalRange records.
- **Constraints**: select a record in the upper table, then edit known stiffness,
  bend-stiffness, rest/compression length or maximum-distance fields in the lower
  table. Particle indices and derived bend weights/curvature are read-only.
- **Modify Selected…** stages a set/multiply operation for selected particles as
  one batch. **Apply Changes** performs semantic and native pack/readback validation
  before committing to memory. **Discard Pending** drops only uncommitted changes.
  **Undo / Redo** restore applied author snapshots. Save/export requires pending
  changes to be applied or discarded first; source files are never overwritten.
- Apply while playing automatically recreates and resumes the native host; applying
  while paused recreates it and stays paused; applying while stopped does not start
  physics. The camera and particle selection are retained.

Left mouse selects, box-selects or paints a **Circle Brush** with adjustable screen
radius. Ctrl/Shift adds selection, Alt removes. **Visible only** checks occlusion
against the cloth's triangles; disable it for rear/through selection. Reference
colliders do not occlude selections. View and table selection are synchronized.
**Select Similar…** matches any selected seed by fixed/dynamic state, absolute range
or inverse-mass tolerance, authored Y height, or shared constraint-set membership.
Constraint-set selection can select most of a cloth; it is not an edge-loop tool.
Selection is confined to the current simulation domain; no skin weights are guessed.

**Middle drag pans**, right drag rotates, wheel zooms and **F / Focus** centers the
selection. This version does not edit topology, fixed-particle membership, skin
bindings or collider-derived geometry; those still require advanced author work.

### Native wind stepping correction

Wind is now **enabled by default** and labeled **Wind Enabled (Native HCT)**; there
is no alternative fake wind backend. **Wind Test (+X)** stages a stronger diagnostic
preset (speed 20, max drag 5); press Apply Environment, and Play if stopped.

The previous low-level world-only step path accepted the wind UI settings but did
not reproduce HCT's complete preview wind behavior. Normal and initialization
stepping now call managed `PreviewPlugin.hctPreviewControl.step(float)` at 1/60,
followed by the adapter's audited particle/display readback and world post call.
Do not mix the old direct stepping path with that wrapper or omit the post sequence.
The wrapper is invoked only in the offline x64 helper, never the editor process.

In the DS3 600-frame off/strong-wind comparison, the maximum particle-component
difference increased from roughly `0.00029` (old path) to `0.22224` (new path).
BB Clorinde v10 also passed 600 finite frames with wind. These are preview behavior
checks, not proof of game-world wind equivalence or a recovered force algorithm.

The current in-memory HKX, FLVER and CLM2 are snapshotted, including unsaved
edits. Native stepping and named-pipe I/O stay off the UI thread. HCT remains
optional and proprietary; it is not loaded into the editor process or bundled.
The tested HCT 2018.1 x64 runtime needs these **six vendor DLLs**:

```text
tools/hctPreviewPlugin.dll
tools/gac/Havok.Tool.UI.dll
tools/gac/Havok.Tool.Interfaces.dll
tools/gac/Havok.Graphics.dll
tools/gac/Havok.Graphics.Bridge.dll
utils/hkCompatFormats.dll
```

This subset is about 70 MiB, in addition to standard Windows/.NET/VC runtime
dependencies. Ordinary FLVER editing and native HKX/CLM2 read/write require none
of these six DLLs. No Cloth authoring/filter/setup DLL is needed for playback.

### Headless cloth conversion

```bat
MySFformat.exe --cloth-pack input.xml --out new_c.hkx
MySFformat.exe --cloth-unpack input_c.hkx --out new.xml
MySFformat.exe --cloth-pack legacy_ds3.xml --out new_ds3_c.hkx --layout 8,1,0,1
MySFformat.exe --cloth-help
```

These commands bypass editor/Viewer startup, refuse overwrites and do not
repack Binders or launch games. Pack includes semantic/schema/full-readback
checks. Unpack accepts a supported cloth HKX or parts Binder.

### Limits and validation

Rest inspection is **not Havok simulation or live-game monitoring**. Optional
native playback runs a local HCT solver, not the game's runtime. Range
spheres do not model normal limits or combined constraints. Rest collider
wireframes are not proof of runtime collision response; serialized inspection
cannot confirm or exclude world-space wind. Gameplay remains a separate test.
Static skirts, hair and arbitrary new constraint formulas are not automatically
converted into working cloth by this tool.

BB Hunter (117 objects), Hiro (279), Clorinde v10 (67), and DS3 (170) fixtures
cover complete known-field comparisons, native read/write, modified-parameter
persistence and independent selected binary-layout checks. Rejected saves
produce no output. GUI tests cover automatic discovery, real editing/cancel,
companion mismatch detection, bilingual tooltips and preserved source hashes.
Animation regression includes binding/playback, repeated resize and 18 distinct
GPU-rendered views. These are offline checks, not blanket runtime certification.

## Source organization and local tests

Experimental runtime research lives in
[`MySFformat/src/Cloth/Preview/`](MySFformat/src/Cloth/Preview/README.md).
The isolated HCT bootstrap probe has passed using a local six-DLL subset,
without loading vendor DLLs from the original installation or global GAC.
Constant-pose native Cloth stepping and Cloth-window playback pass for DS3
`BD_M_3000` and BB Clorinde v10, with CLM2 loaded and source files protected.
Animated input and main-Viewer mesh integration are not yet implemented.
User-supplied HCT remains proprietary;
no vendor DLLs are included in the editor/source distribution.

- `MySFformat/src/HkxPack/`: native packfile codec and HKX XML editor.
- `MySFformat/src/Clm2/`: CLM2 document, serialization checks and editor.
- `MySFformat/src/ClothDocument.cs`, `ClothInspectorForm.cs`, `ClothCli.cs`:
  cloth interpretation, independent UI and command-line entry points.
- `MySFformat/src/HkxAnimationClip.cs`, `HkxPoseBinding.cs`, `HkxPlayback*.cs`:
  animation decoding, binding and playback.
- `MySFformat/tools/Cloth/`: required embedded schemas, portable provenance and
  retained MIT notice. Existing animation dependencies remain unchanged.
- **`helper-ext/`** (Git-ignored): historical tools/baselines, machine-specific
  tests, game-derived fixtures/golden data, logs/screenshots and compiled tests.

Local development commands, from the repository root:

```powershell
py -3.12 helper-ext/cloth/tests/run_native_hkx_tests.py
py -3.12 helper-ext/cloth/tests/run_cloth_tests.py
py -3.12 MySFformat/tests/run_hkx_tests.py
```

The first two require the local archive and licensed game fixtures; they are
not part of the GitHub source distribution. Output stays in `helper-ext/cloth`,
`helper-ext/animation/hkx-work` and `helper-ext/test-binaries`. That directory is
not an application build dependency and should not be force-added to Git.
Do not publish game/model test assets. Preserve applicable third-party notices
and source-distribution obligations.
