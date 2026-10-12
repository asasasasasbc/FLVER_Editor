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
  loaded FLVER. Reopening does not replace an existing comparison document.
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
defaults to BB: **select DS3 in the third toolbar row before saving**, or use
the CLI layout option below. Changing layout selects the write schema; it is
not a complete automatic conversion between games.

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

Cloth inspection is **not Havok simulation or live-game monitoring**. Range
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
