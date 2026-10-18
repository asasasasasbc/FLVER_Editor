# ER TAG0 inspection and compatibility preview — 2026-10-15

Follow-up (2026-10-16): the shared reader additionally supports Sekiro SDK
20160200 with a distinct source marker/backend. The later OpenPhysicsHKX Core 0.2
extension adds an approximate VolumeMx kernel, and the Editor evaluates authored
Skin references. The original ER sample now passes 7/7 managed 120-frame sessions.
See `managed-playback-audit.md`; this is not native/game physics equivalence.
Core 0.3 subsequently adds independent reference IDs and the Editor's serial
MeshMeshDeform graph; the ER corpus now passes 357/395 at 12 managed frames,
without treating the remaining unsupported features as missing format schemas.

## Reader

`HkxTagfileReader.cs` interprets bounded in-file TYPE metadata for SDK 20180100:
TNA1/TBDY type descriptions, indexed ITEM storage, PTCH pointer locations, typed
scalars, strings, fixed/dynamic arrays, inheritance and inline records. It does
not relabel a TAG0 binary as a packfile. Source documents retain the explicit
`sourceFormat="TAG0-20180100"` marker. The 2014 writer rejects that marker.

Format reference: The12thAvenger/HKLib, MIT, commit
`ac40bc9915d8caff0df154c2b9a79656dfca1d77`; notice in `LICENSE-HKLib.txt`.
HKLib was built only as an independent local comparison tool. Its generated
classes, registry, DLLs and .NET 8 test application are not runtime dependencies
or shipped here. Normal ER inspection remains C#/.NET Framework 4.8.

Limits include file/section bounds, item/type/expansion counts, inheritance depth,
reference kind/type checks, finite scalars and pointer patch coverage. In-file
metadata is not a claim of trusted source provenance. Other SDK versions and
compendium-only files are refused. No proprietary runtime or game data is bundled.

## Preview compatibility, not ER runtime equivalence

`HkxTagfilePreviewAdapter.cs` creates a separate, temporary 2014 CPU-runtime view
only after the user chooses the separately named ER compatibility backend. It
does not modify the author document, write a game asset, omit physical operators
or replace constraint reference indices with particle-rest indices.

- Single authored `simulateOpConfigs` is mapped to the 2014 solver operator;
  substeps, iterations, constraint execution and stiffness adaptation are retained.
- Simulation feature flags moved in 2018 are placed in the old simulation-info
  structure. Its legacy collision tolerance comes from authored landscape data;
  legacy simulation-info substeps are zero, retaining operator substeps.
- `hkPackedVector3[16]` records are represented by their exact four-short words
  in the legacy short[64] layout: no unpacking/requantization or weight truncation.
- 2018 per-operator access metadata, author IDs and job dependency graphs are not
  physical operations in this view; the complete authored serial operator list
  and constraint references are retained. SPU-only batch sizes are zero for CPU.
- The 2014 bend behavior is an approximation of the 2018 clamp/height behavior.
  The running status explicitly identifies 2014 physics, not the ER runtime.
- Active virtual collision points, 5–8-influence skinning, custom deform modes,
  unsupported collider subsets/configuration/state transitions, disabled collider
  semantics and any unrecognized fields are refused, not silently dropped.

The additional legacy BoneSpace Skin, ObjectSpace Skin P, GatherSome and
SimpleMeshBoneDeform metadata comes from the existing MIT hkxpack classxml
archive. Source hashes are in `equipment-schema-provenance.json`. These are
2014 runtime layouts, **not** guessed offsets for reading ER binaries. The
SimpleMeshBoneDeform CPU ABI has input/output scalars at 32/36 and arrays 40/56.

## Tests and limits

The local primary-armor census contains 670 standard numeric-name body/head/arm/
leg binders, excluding low-detail `_l`, face/hair, weapons and backup directories.
All 395 Cloth TAG0 files decode and load normally; native ER export is refused.
All 670 source binder hashes remained unchanged.

390/395 compatibility views passed 2014 Pack/full semantic readback. Five variants
are explicitly unsupported due to more than four skin influences:
`bd_f_1425`, `bd_f_1426`, `bd_f_1427`, `bd_m_1425`, `bd_m_1426`.

Seven random Cloth assets plus three operator-targeted assets passed isolated HCT
startup and 120 finite frames, authored gravity, wind off. Fixed-particle changes
from frame 4 were zero in the random sample and dynamic motion was observed.
Seven Inspector documents / 29 domains were exercised, plus ER lifecycle/read-only
tests. Against the independent HKLib oracle, the random sample's 20,696 particle/
pose float components were bit-identical, with exact topology, collision masks
and constraint-class lists. This verifies those decoded inputs, not physics parity.

`tests/HkxTagfileTests.cs` supplies asset-free valid/malformed TAG0 checks and an
export guard. Private game fixtures, oracle, corpus tools and logs remain under
ignored helper-ext. No game was launched. No ER engine, native cloth-render mesh,
animation-clip or gameplay equivalence is claimed.
