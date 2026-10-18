# Sekiro Cloth support feasibility — 2026-10-16

Latest managed follow-up: Core 0.2 VolumeMx plus evaluated authored Skin references
improves the strict Sekiro sample to **12/14** at 120 managed frames (no HCT).
The two five-influence cases remain refused. The 4/14 results below describe the
earlier input-only stage. See `managed-playback-audit.md` for current behavior.

## Production follow-up — implemented 2026-10-16

The production reader now accepts SDK 20160200 while retaining its true source
identity. Inspector shows **Native HCT (Sekiro compatibility)** only for the
matching profile. The native session checks explicit profile/backend agreement;
ordinary HCT does not silently convert. 2016/2018 documents share read-only author
guards and the 2014 writer refuses every TAG0 source marker, including unknown
versions. SDK switching resets stale compatibility choices. ER behavior is retained.

Production tests: 14 Inspector files / 66 domains; 12 actual native sessions /
51 domains at 120 finite frames; 2 explicit five-influence refusals. Twenty-two
GUI opt-in/read-only/native+managed/lifecycle/SDK-switch checks passed. Sixteen
asset-free TAG0 reader/identity/export/invalid-data checks passed.

OpenPhysicsHKX's **Editor adapter** now accepts the same reviewed TAG0 view before
its existing strict binding and constraint validation. Four Sekiro assets passed
120 managed frames without HCT: bd_m_9100, hd_m_9110, hd_m_9510 and hd_m_9520.
The native twelve-asset result must not be reported as managed coverage. Other
strict managed samples: BB 1/4, DS3 1/4, ER 0/7. Volume and reference bindings
still block broad playback. No Core algorithm/API change or replacement DLL was
necessary for this input-adapter change; the standalone 60 tests passed.

The following sections retain the original isolated research findings; their
statements about not yet deploying refer to the research phase, not this follow-up.

## Result

The supplied primary armor sample can be supported in two layers:

1. Read-only native TAG0 inspection for all 14 files / 66 simulation domains.
2. Explicit **2016 -> HCT 2014 compatibility physics**, experimentally working
   for 12 files / 51 domains. This is not the Sekiro runtime or a gameplay-parity
   result. Two files need additional deformer support.

The production Editor has **not** been changed to accept SDK 20160200 in this
research phase. Experimental reader/adapter sources, fixtures, executable and
results are isolated in ignored `helper-ext/cloth/openphysics-checks/`.

## Format findings

All 14 Cloth files in the 32 standard primary armor binders use TAG0 SDK
20160200 with embedded TYPE data. They contain TSTR/FSTR, TNA1/TBDY, ITEM and
PTCH sections usable by the existing metadata-driven reader machinery. This
does not imply that hard-coded 2018 class layouts are safe for 2016 objects.

The isolated experiment accepts the real 2016 SDK, keeps the original bytes
unchanged, and emits `sourceFormat=TAG0-20160200` / `hk_2016.2.0`, rather than
relabeling a game file as 2018. Each field offset, width, inherited field and
array stride is taken from that file's TYPE metadata. Existing bounds, type,
finite-value and pointer-coverage checks remain enabled.

All 14 decode and pass normal document construction in a private harness.
An independent Python byte audit checks 121,008 particle/pose float components
bit-for-bit, particle counts, triangle indices, fixed indices and collision
masks against the generated XML. It also enumerates all serialized typed pointer
slots and verifies the PTCH coverage: 21,049 patches and 2,799 top-level objects.
This is a second implementation checking file data, not an official SDK oracle
or proof of complete Havok semantics.

## Physics experiment

The existing field-based compatibility adapter was compiled privately to accept
the 2016 source marker. It preserves operator and constraint references and
solver parameters, maps moved simulation flags, and flattens packed-vector words
without requantizing. Twelve views pass 2014 Pack/full semantic readback.

Those twelve views were passed with their matching Witchy-unpacked FLVER and
available CLM2 to the existing isolated HCT helper. Each completed 120 finite
frames with authored gravity and wind off. Dynamic changes were observed in
each asset; fixed-particle displacement after frame 4 was zero. No production
backend setting was modified or silently used to classify 2016 as ER.

Successful assets:

```
bd_m_9000  bd_m_9030  bd_m_9040  bd_m_9100  bd_m_9110  bd_m_9120
bd_m_9500  bd_m_9911  bd_m_9912  hd_m_9110  hd_m_9510  hd_m_9520
```

The largest frame-4-to-120 displacement was about 0.982 m in the 9510/9520 head
assets. Finite positions and stationary pins alone do not establish reasonable
visual behavior; these need focused visual/binding inspection before being
described as normal, stable, or game-equivalent playback.

## Remaining cases

`bd_m_9130` and `bd_m_9530` decode successfully but have active
`hclObjectSpaceDeformer` / `hclBoneSpaceDeformer` five-blend entry blocks. The
current compatibility adapter explicitly refuses these. The inventory found
five-blend block counts 9/4/43/33 in 9130 and 29/27 in 9530.

Do not truncate skin weights or silently omit the operators. A subsequent
implementation must audit the block/control-byte semantics and either evaluate
the full deformer in a suitable backend or use a separately verified newer
runtime. It is not enough to add XML metadata for the new block class.

## Proposed production changes

1. Add SDK 20160200 explicitly to the reader, preserving distinct source identity;
   continue using in-file metadata and rejecting unknown SDKs/compendium-only data.
2. Generalize document detection, author/export guards and UI labels currently
   hard-coded to `TAG0-20180100`. In particular the 2014 Pack export guard must
   reject 2016 **before** the new reader is deployed. Keep both TAG0 versions
   read-only until matching native writers are implemented.
3. Add a separately named `Native HCT (Sekiro compatibility)` backend, or a clearly
   versioned TAG0 compatibility choice. Ordinary HCT must not silently convert.
   Display `2016 -> 2014 approximation / NOT Sekiro runtime` while playing.
   Preserve ER option/API compatibility and avoid treating Sekiro as ER.
4. Keep the current strict feature refusals. Managed OpenPhysicsHKX does not gain
   a 2016 operator binding graph merely because deserialization works. Core source
   changes, if later required, belong in the independent OpenPhysicsHKX repository,
   with only a rebuilt DLL copied to the Editor.
5. Add synthetic 2016/2018 SDK identity, malformed-pointer and no-export tests;
   run all 14 Inspector loads, all 12 native sessions, pause/stop/backend-switch
   tests, author-document preservation, and existing BB/DS3/ER regressions. Inspect
   the moving particle/wire overlays visually, especially the two head assets.

The initial supported experience would therefore be **14/14 inspection and
12/14 experimental compatibility physics**, not native Sekiro-equivalent cloth
rendering, animation playback, or author HKX export.

## Local evidence and safety

- `sekiro-corpus-20261016/corpus-results.json`: original deployed-reader baseline.
- `sekiro-research-20261016/results.json`: experimental decode/document/pack results.
- `sekiro-research-20261016/input-audit.json`: independent critical-input byte audit.
- `sekiro-research-20261016/native-results.json`: actual compatibility stepping.
- Scripts: `research_sekiro.py`, `audit_sekiro_inputs.py`,
  `test_sekiro_research.py`, `SekiroResearch.cs` (private tooling only).

All 32 original binder hashes were checked unchanged in the baseline; all 14
original Cloth-containing binders were checked again after native experiments.
WitchyBND only unpacked copies. No game launch, game writes, production build or
source commit/push occurred. This scope excludes NPC/chr assets, LOD, weapons,
face/hair, nonstandard-suffix binders and backup directories.
