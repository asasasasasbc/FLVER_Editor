# Managed equipment playback — 2026-10-16

This path runs OpenPhysicsHKX in the Editor process. It does not launch or load
HCT, call the compatibility CLI, or require FLVER/runtime-folder inputs. The
display remains a particle/wire preview, not a native cloth-render mesh.

## Current 0.3: multiple buffers and serial deformation graph

Core now accepts separately indexed `ReferenceBufferData` dictionaries. Each
constraint retains its authored buffer ID and reference vertex; LocalRange sets
also retain independent reference-normal mappings. Live replacements are atomic,
copied and dimension-checked, with no Verlet-history reset. Legacy buffer-0 API
behavior is preserved; ambiguous writes are rejected. Core remains format-free.

The Editor's `ManagedClothGraph` executes the physics-required slice of each
owner's default serial operator sequence. Persistent independent Core sessions
advance each simulation domain at its authored point in that sequence. An
upstream simulation's freshly solved positions become the input to subsequent
deformers and downstream constraint/pin buffers in the same frame. Authored
read-before-write edges retain the actual previous-frame buffer; they are not
silently reordered. Display-only branches are pruned, not substituted for solves.

Supported required producers include Skin PN/PNT, legacy MeshMeshDeform,
ObjectSpace/BoneSpace MeshMeshDeform P/PN/PNT, CopyVertices, GatherAll and
GatherSome. Earlier writes are retained for partial deformers. Static Skin is
evaluated from the rest skeleton; dynamic MeshMeshDeform is re-evaluated every
frame from simulation output. Missing input channels/vertices/producers, unknown
operators, dynamic transform-set producers and undefined degenerate unit/inverse
normal frames fail explicitly. A failed coupled frame faults the session until restart.

The triangle frame uses `g=(a+b+c)/3`, rows `(a-g)`, `(b-g)`, a cross-product
normal, and translation `g`. Normal scale modes preserve the authored selection:
APPLY uses the raw cross product (including a zero cross product), IGNORE its unit direction, INVERT its reciprocal
area scaling. ObjectSpace matrices compose the saved triangle-from-mesh matrix
with the current frame. BoneSpace and legacy local XYZW data transform directly
through triangle frames, retaining weights and partial writes. Mesh normals are
transported as authored frame directions. This is an explicitly labeled
**triangle-frame deformation approximation**, not native-kernel parity.

A private rest audit compared reconstructed frames to saved inverse matrices in
representative DS3/ER data (maximum identity-product residual below 2.9e-4,
including authored float/quantization roundoff). Legacy saved normal vectors
reconstruct approximately unit world vectors through the centroid frame. This
validates the frame convention, not all runtime normal/deformation semantics.

### Current validation

- Corpus **12-frame screening**: DS3 **213/213** (previously 208); ER **357/395**
  (previously 281). No previously passing ER files regressed. Of the former
  62 MeshMeshDeform refusals, 60 now pass; of the former 30 multi-buffer refusals,
  16 now pass and others expose a subsequent unsupported feature.
- The remaining 38 ER first blockers: 18 dynamic transform-set producers,
  10 cylindrical LocalRange, 5 five-influence assets, 2 unwritten channels/vertices,
  2 missing input producers and 1 Skin P producer. No omission flag or rest fallback.
- Six formerly blocked assets pass **600 actual managed frames**: DS3
  bd_f_5650/bd_m_6650, ER bd_m_2320/bd_f_1250, BB bd_f_6000/bd_f_2200.
  The original sample regression passes DS3 4/4, ER 7/7, BB 4/4, Sekiro 12/14
  at 120 frames. Short corpus screening is not all-asset 600-frame certification.
- Moving child pins are supposed to move with their reference mesh. A separate
  120-frame check measured **zero error against the actual per-frame pin targets**
  in Hunter and DS3 5650, including visibly nonzero target motion. This replaces
  the incorrect criterion that every child pin must stay fixed in world space.
- Hunter's batch-4 versus single-frame stepping is bit-identical over 120 frames
  across 1,809 double components. Author XML remains unchanged.
- Core **88** synthetic tests; **15** asset-free serial graph/frame tests;
  **20** Skin-reference checks; **26** coupled UI checks. Older baseline tests
  remain, with Hunter's former expected refusal updated to require actual graph playback.

All playback remains managed (no HCT host/path/FLVER required), with the dynamic
deformation approximation visible in status. Private evidence is under
`managed-graph-20261016`; no game launch, original-file edits or native-equivalence
claim is involved.

## Core 0.2 (earlier baseline)

Core source remains in the independent OpenPhysicsHKX project. The Editor keeps
only its prebuilt DLL, license and source/version note. New `VolumeShapeMatching`
is independent weighted rigid shape matching: it fits current particle positions
to authored frame vectors, then applies authored stiffness on the apply subset.
It is not a recovered native VolumeMx algorithm. Approximation opt-in and visible
class reporting remain mandatory. See the independent `docs/APPROXIMATIONS.md`
for equations and limitations. Original imported kernels/CLI are unchanged.

Frame/apply batch and single records are flattened by the Editor with original
particle indices, vectors, frame weights and stiffness. Exactly repeated SIMD
padding lanes are deduplicated; conflicting repetitions are rejected by Core.
The unused homogeneous lane allows only float roundoff up to 1e-6. It is not
used as a force coefficient. Explicit authored inactive constraint execution
can set `AllowEmptyExecution`; this is not inferred from unknown constraints or
used to bypass a missing kernel. Default API rejection of accidental empty
schedules is retained.

## Reference evaluation (0.2 baseline; expanded by 0.3 above)

`ManagedReferenceBinding` evaluates the actual required Skin producer before a
domain's simulator in the default authored schedule. Reference positions come
from its local vector blocks, not particle-rest substitution.

- ObjectSpace PN/PNT: interpret each control block in order (0/1/2/3 = 4/3/2/1
  blends), retain interleaved subset indices and byte weights divided by 255,
  compose bone-from-skin matrices with the rest skeleton, and transform each
  decoded position and normal. Weights are validated, not renormalized/truncated.
- Packed XYZ uses signed short mantissas with the shared float scale stored by
  the fourth short. Packed words are read directly or from the lossless TAG0
  compatibility view. Uniform unpacked block streams are also supported.
- BoneSpace PN/PNT: each vertex has floor(16/influence-count) lanes per block.
  Local positions already contain weighted XYZ and the translation weight in W;
  full Vector4 transformation prevents zero-weight lanes adding translations.
  Weighted normal contributions are combined and normalized.
- Reference constraints keep their original vertex indices. Fixed particles use
  the authored MoveParticles vertex/particle mapping and explicit Core frame
  targets. LocalRange normals use the corresponding evaluated reference normal.
- Required unwritten/out-of-range slots are refused. Only unused dense storage
  may contain zeros, after verifying that no constraint/pin reads those slots.

The evaluator remains intentionally bounded: no general MeshMeshDeform dependency
graph, live animation, dynamically produced transform sets, mixed packed/unpacked
streams, or multiple distinct required reference buffers per domain. Unsupported
paths fail explicitly. There is no rest-binding fallback, maximum-radius
substitution, five-influence truncation or implicit constraint omission.

## Validation (0.2 baseline)

The original sample sets now pass actual 120-frame managed stepping with omission
off: DS3 4/4, ER 7/7, Sekiro 12/14, BB 3/4. All successful results were finite,
fixed positions stayed unchanged after frame 4, and dynamic changes were observed.
The two Sekiro five-influence cases and the BB apron MeshMeshDeform dependency
remain refused. These are isolated solver tests, not gameplay equivalence.

Four representative assets additionally completed 600 managed frames: DS3
bd_f_7550 / bd_m_4700, ER bd_f_1380 / bd_f_1770. All remained finite with zero
fixed-particle displacement after frame 4. The DS3 full primary-Cloth corpus
short test passes 208/213 at 12 frames; five assets retain explicit dynamic
MeshMeshDeform dependency refusals. Twelve-frame corpus screening is not the
same evidence as the representative 120/600-frame tests or a visual/game test.

The final ER primary-Cloth corpus short test passes **281/395** at 12 frames.
The other 114 are explicitly unsupported, not nonfinite/crashed solves: 62
MeshMeshDeform dependencies, 30 multi-reference-buffer domains, 10 cylindrical
LocalRange cases, 6 dynamically produced transform sets, 5 five-influence assets,
and 1 ObjectSpace Skin P producer. The classification records the first blocker
per asset, so resolving one can reveal another. No omission/rest fallback was
enabled. DS3's five remaining assets are bd_f_5650, bd_m_1000, bd_m_2950,
bd_m_5650 and bd_m_6650, all currently blocked by MeshMeshDeform dependencies.

Core's 75 synthetic numerical/API checks pass, including 90/180-degree rigid
transform equivariance, active restoration of a distorted shape, fixed points,
batch/reset determinism, input validation and prior imported-kernel bit parity.
Twenty asset-free Skin reference checks cover PN/PNT, compact index mapping, packed vectors,
BoneSpace weighted-W transformation, normals and missing-producer rejection.
Eighteen GUI checks verify actual DS3/ER managed frames, no runtime path/host/FLVER,
visible Volume approximation, pause/Apply/Stop and author XML preservation.

Private fixtures/reports are under ignored helper-ext, not bundled. Original
binders are only read and verified; no game launch or game writes are involved.
