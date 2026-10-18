# Cloth and HKX Animation

FLVER Editor offers two separate workflows: skeletal **HKX animation playback**
in the Viewer, and a dedicated **Cloth** window for serialized cloth inspection,
editing and writeback. Animation playback does not run cloth physics.

Both tool windows open centered over the main editor on its current monitor,
with their bounds fitted to the screen's working area. Moving an already open
window is not overridden when its tool command is clicked again.

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

### Cloth Setup Wizard (experimental)

**Cloth Setup Wizard**, to the left of **Load Cloth**, creates a new Bloodborne
single-bone / single-domain setup from the current FLVER snapshot. No existing
Cloth HKX, Java, Python or proprietary runtime is required for generation.

1. Select a connected mesh with one base FaceSet; LOD/motion blur copies are excluded.
2. Select its single support bone (global FLVER index 0..255). Keep existing
   uniform weight-1 binding, or explicitly assign that bone in the output copy.
3. Use **Fix Top Y Rows**, or select particles and **Fix Selected / Release Selected**.
   At least one fixed and one dynamic particle are required.
4. Set basic mass, gravity, damping, radius, range and normalized link strengths.
5. Review the new display node, cloned `_al_Cloth.mtd` material and two-buffer
   output layout. Texture compatibility is the user's responsibility.
6. **Generate / Validate**, then **Save New Folder**. Choose a parent directory;
   a new timestamped folder contains `Setup_Cloth.flver`, `_c.hkx`, `_c.clm2`,
   author XML and a report. Open the generated FLVER to inspect/preview that set.

Other meshes and existing nodes are retained. Duplicate display positions map to
simulation particles; multi-bone input is rejected unless single-bone assignment
is explicitly chosen. The prototype has no body collisions or Binder packaging,
does not extend existing multi-domain Cloth, and never starts the game. Empty
CLM2 records cover the complete mesh list; readback is not game certification.

The **Cloth** button sits alongside Material, Mesh and Dummy. It opens an
independent window. UI labels are English; tooltips are English/Chinese.

**Render Mode**, beside Invert / Clear / Focus, switches the current Cloth domain
between **Points / Lines** (default), **Solid**, and **Points / Lines + Solid**.
Solid uses the current simulated particle triangles with light shaded faces;
selected particles remain marked. It does not enable FLVER overlays or change
physics. Colliders, skeleton and range overlays keep their own controls.

Under **Preview Environment**, **Toggle Submesh Visibility…** lists each submesh
of the current and extra FLVER overlays by model, mesh index and material name.
Checkboxes apply immediately; Show All / Hide All / Invert are available.
Visibility survives animation updates and overlay off/on within the current
preview, but is reset when a model snapshot is replaced. These controls do not
change the main Viewer's visibility, source models, or Cloth physics.

For orientation comparisons, use the same viewing direction in both windows:
the Inspector uses orthographic projection while the main Viewer is perspective.
Both now use the main Viewer's explicit display-coordinate convention:
FLVER `(X,Y,Z)` → Viewer `(X,Z,Y)`, followed by the same MonoGame `LookAt`.
Inspector pitch is camera elevation, not a right-handed model rotation; mesh
depth ordering and visible-particle picking use the same camera-space distance.
These display transforms never change saved positions or physics inputs.
World axes match the main Viewer: **red +X, yellow +Y, blue +Z**, drawn from
the world origin. A small labeled indicator in the lower-left follows camera
rotation and remains visible when the origin is outside the viewport.
The initial camera and **Reset View** look from **−Z** toward the model, without
an initial yaw/pitch offset, matching the main Viewer's front-facing side.
Points/lines and diagnostic colliders are overlays and can be seen through the
model; use Solid to inspect the Cloth surface without ordinary particle dots.

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

The CLM2 toolbar row contains **Load CLM2 / Edit CLM2 / Save CLM2**.
Use **Verify** in the first row for companion checks. The modal editor exposes the known mesh-record layout and
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

#### Elden Ring equipment (Havok 2018 TAG0)

ER `_c.hkx` files and primary equipment binders can be loaded directly. TAG0 SDK
20180100 is read natively in C#; the Inspector supports particles, constraints,
colliders, topology and raw data inspection. ER HKX author editing/export and
layout switching are disabled: a 2014 packfile is not a game-compatible ER file.

For physics, load the matching FLVER, then select **Preview Environment → Physics
backend → Native HCT (ER compatibility)**, choose the existing local HCT runtime,
**Apply Environment**, and **Play Cloth**. This is an explicitly selected **2018
→ 2014 CPU physics approximation**, not the ER solver. The warning remains in
preview status. The ordinary Native HCT option does not silently convert ER data;
OpenPhysicsHKX also accepts reviewed TAG0 input conversion, but retains strict
constraint/reference-binding checks; accepting the format does not mean it can
simulate all ER equipment (see managed coverage below).

The compatibility path retains physical operators, constraint sets, reference
indices, skin weights and authored solver configuration. It uses 2014 bending
and a serial operator schedule. Unsupported active features are refused, not
omitted or replaced with rest bindings. In particular, equipment using 5–8 skin
influences is not approximated by truncating weights. The current mesh overlay
remains bone-weight visualization, not an ER cloth-deformed display mesh.

Local ER validation: 670 standard primary armor binders, 395 Cloth TAG0 loaded;
390 compatibility projections passed 2014 packing/full semantic readback. Five
body variants (female 1425/1426/1427, male 1425/1426) are refused because their
deformers use more than four skin influences. Ten representative assets passed
120 HCT compatibility frames; this is not 395-asset native simulation or gameplay
validation. See `tools/Cloth/er-tagfile-audit.md` for format and validation scope.

#### Sekiro equipment (Havok 2016 TAG0)

Load the matching FLVER and `_c.hkx` (or primary equipment binder), then select
**Preview Environment → Physics backend → Native HCT (Sekiro compatibility)**,
choose the local HCT runtime, **Apply Environment**, and **Play Cloth**. This
option is shown for SDK 20160200 documents, independently of the ER choice.
Switching between ER and Sekiro clears an incompatible backend selection.

This is **2016 → 2014 compatibility physics**, not the Sekiro runtime. The warning
remains visible during playback. Sekiro TAG0 data is read-only; changing layout,
author parameter editing and game HKX export are disabled. The ordinary Native
HCT option refuses silent conversion. No skin-weight truncation or rest-binding
fallback is used.

All 14 Cloth files / 66 domains in the tested 32 primary armor binders load in
Inspector. Twelve assets / 51 domains passed 120 native compatibility frames;
`bd_m_9130` and `bd_m_9530` still refuse physics because of five-influence skinning.
Finite frames are not gameplay/visual equivalence; the 9510/9520 head assets have
large dynamic movement and need further visual verification. Mesh overlays retain
the existing bone-weight visualization, not native Sekiro cloth-render output.
See `tools/Cloth/sekiro-support-research.md` for evidence and limitations.

#### Bloodborne / Dark Souls III and shared controls

**Preview Environment → Solver** offers **Native HCT** and the bundled
**OpenPhysicsHKX** CPU solver. OpenPhysicsHKX needs no runtime folder or vendor DLL.
The Editor references the prebuilt `MySFformat/OpenPhysicsHKX.Core.dll`, not a
vendored source project. To update Core, build its independent source repository,
replace that DLL, then rebuild the Editor. See `MySFformat/lib/OpenPhysicsHKX/SOURCE.md`.
Select it and **Apply Environment**, then **Play Cloth**. Native HCT remains the
default. OpenPhysicsHKX supports rest-pose and **HKX animation-driven physics**
with basic capsule collisions. In **HKX Animation**, load a skeleton and clip,
enable **Drive Cloth Physics**, and use Play / Pause / Stop.
Wind, friction, collision-mask filtering, and the native Cloth display-mesh
deformation pipeline remain unsupported.

The OpenPhysicsHKX adapter normalizes supported Sekiro/ER TAG0 inputs using the
reviewed compatibility view, without modifying File Settings or loading
HCT. Its status identifies the TAG0 input approximation. Core **0.3** includes
the explicitly approximate VolumeMx weighted rigid shape-matching solve.
The Editor evaluates file-defined ObjectSpace/BoneSpace Skin PN/PNT buffers against
the current skeleton pose, including packed vectors, blend weights and fixed-particle
mapping. Compact reference indices keep their original meanings; they index the
evaluated buffer, not the simulation-particle array. Position and normal targets
are not replaced with independent particle-rest targets.

Each constraint now retains its independent reference buffer ID and normal
mapping. The Editor executes required MeshMeshDeform/copy/gather operations in
the order saved in the file, using upstream simulation output to update downstream
constraints and fixed targets every frame. MeshMeshDeform is labeled as a
triangle-frame approximation in status; it is not a baked rest-reference workaround.

Animation uses owned world-space bone matrices sampled at 60 Hz. The reference
positions/normals, fixed targets, and collider transforms update before each
physics frame. Packed Skin inputs are decoded once; rest outputs are not reused
for animated frames. Pose inputs are held constant within each frame's internal
solver substeps. Missing pose updates hold the last pose. Pause/resume retain
history; seek, loop and Rest Pose recreate the session. Seeking starts fresh at
that pose rather than simulating all preceding frames.

Collider bindings use each domain's saved transform-set, bone index and offset,
not a guessed bone-name match. Unbound colliders stay fixed. Animated collider
scale/shear/reflection and unsupported transform-producing operators are refused.
Basic collision projects dynamic particles to the capsule surface plus particle
radius. The HKX `simulationInfo.collisionTolerance` is retained in File Settings
but is **not** interpreted as a world-space radius reduction: its common `0.2`
value previously erased small colliders. Native HCT's tolerance behavior is not
reproduced. This is discrete particle collision, not swept/triangle collision;
fixed particles and the FLVER bone-weight overlay are not collision-deformed.
The canvas receives the solver's actual collider snapshot. Existing animation
decoding still uses the bundled HavokToolset, as it does for the main Viewer;
the OpenPhysicsHKX solver itself does not require HCT.

Focused animation checks: Maria and Hunter each completed 600 synthetic moving-pose
frames; DS3 5650 and ER 2320 each completed 120. Fixed targets tracked exactly,
batch/single stepping matched, invalid batches did not advance state, and source
files stayed unchanged. A real Bloodborne idle clip also passed Inspector
play/pause/resume/seek/loop checks. These are targeted tests, not a claim of
animation compatibility for the entire equipment library.

Earlier rest-pose 120-frame tests (no omission opt-in, no HCT process) pass **DS3 4/4**,
**ER 7/7**, **Sekiro 12/14**, and **BB 4/4** in the tested sets. These replace the
older 1/4 DS3 and 0/7 ER results, but are not a claim that every asset, animation
binding or game-equivalent physical response works. See
`tools/Cloth/managed-playback-audit.md` for implementation and validation scope.
Across the existing primary-Cloth corpus, 12-frame managed screening passes
**DS3 213/213** and **ER 357/395**. This is short playback coverage, not all-asset
120-frame or game validation. The remaining assets are explicitly unsupported.

BendLink, Transition and VolumeMx have experimental approximate implementations, identified
in preview status. Other unknown constraints are refused unless **Allow unmodeled
constraints to be omitted** is explicitly enabled; omitted classes remain listed.
Rest-binding fallback has been removed. Unsupported reference/input producers,
dynamic bone-transform dependencies, and variable
dynamic radii are refused rather than replaced with independent particle-rest
targets or maximum radii. Use Native HCT for unsupported bindings.
The common BB/DS3 reader now includes equipment Gather, Volume/VolumeMx,
BoneSpace P/PN/PNT and ObjectSpace PN deformers, legacy BendLink/MeshMeshDeform,
and the DS3 sphere layout. Missing or mismatched schemas still fail closed;
they are reader limitations, not proof that the original runtime rejects an asset.
Schema read/write support does not implement the corresponding managed kernels
or reference-buffer graph. See `tools/Cloth/equipment-schema-audit.md` for layout notes.
Local primary-equipment validation: 1017 binders, 369 Cloth HKX (BB 156 / DS3 213),
all loading and in-memory semantic roundtrips passed; 16 representative assets
also passed 120 native HCT frames. LOD/weapon/face/hair archives and gameplay were
not covered by this equipment census.

The Inspector now indexes staged XML objects/ranges and fills grids in batches;
selection, pending edits, Apply and Undo/Redo retain their original semantics.

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
tab, select the character `skeleton.hkx`, load an animation HKX, enable **Drive Cloth Physics**
and use **Play HKX**, **Pause HKX** or **Stop HKX**. Pause keeps the current frame and
physics history; Play resumes; Stop returns to frame 0. Seeking and looping reset
physics rather than replaying previous frames. Collider overlays follow animation;
range circles and parameter tables still show saved data. The main Viewer model
is unaffected. Closing the window stops its owned host.

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

Right mouse selects, box-selects or paints a **Circle Brush** with adjustable screen
radius. Ctrl/Shift adds selection, Alt removes. **Visible only** checks occlusion
against the cloth's triangles; disable it for rear/through selection. Reference
colliders do not occlude selections. View and table selection are synchronized.
**Select Similar…** matches any selected seed by fixed/dynamic state, absolute range
or inverse-mass tolerance, authored Y height, or shared constraint-set membership.
Constraint-set selection can select most of a cloth; it is not an edge-loop tool.
Selection is confined to the current simulation domain; no skin weights are guessed.

**Left drag rotates**, **middle drag or Shift + left drag pans**, wheel zooms and **F / Focus** centers the
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

Rest inspection shows saved particle and collider geometry. Native playback
runs a local HCT solver. Range
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
Animated input is available for Native HCT. Cloth-deformed main-Viewer mesh
integration is not implemented; the optional Cloth-window mesh layer uses bone weights.
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
