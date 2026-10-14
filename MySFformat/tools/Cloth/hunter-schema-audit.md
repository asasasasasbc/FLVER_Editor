# Hunter body schema extension — 2026-10-14

No vendor binary was modified. Ordinary C# read/write remains fail-closed.
This supplements the historical baseline extraction records in the provenance JSON.

## PNT mesh deformer

Source declaration: HkxPack-Plus / hkxpack-souls classxml
`hclObjectSpaceMeshMeshDeformPNTOperator_1.xml`, signature `0xe20c4d3e`.
Source-file SHA-256: `aa9eb8e8456660bd5004173d7f2cc4742a8f8b9f6864f938b5d9634b38734b40`.
It declares two hkArrays of existing reviewed packed/unpacked PNT structs.

The original Hunter male HKX signature gate revealed that the old BB parent metadata
incorrectly used the DS3 tail-padding rule. The original binary's scalar values,
hkArray counts/capacities and relocation slots match the following layout:

| Field | BB 8,1,1,1 | Runtime/DS3 8,1,0,1 |
|---|---:|---:|
| inputBufferIdx | 28 | 32 |
| outputBufferIdx | 32 | 36 |
| scaleNormalBehaviour | 36 | 40 |
| inputTrianglesSubset | 40 | 48 |
| triangleFromMeshTransforms | 56 | 64 |
| objectSpaceDeformer | 72 | 80 |
| parent size | 160 | 168 |
| localPs / localPNTs | 160 | 168 |
| localUnpackedPs / localUnpackedPNTs | 176 | 184 |
| P / PNT derived size | 192 | 200 |

Do not apply a uniform shift to unrelated classes. The profiles are deliberately
separate. The existing P variant follows the same parent-tail rule.

Evidence fixture (not distributed): original Hunter male HKX SHA-256
`71abf783b03a6389bc27350d60a82cad18ea0783d301d8331c0f1125e26979ca`.
At object file offset `0xad310`, the input/output scalars at +28/+32 are 4/5;
triangle array counts are 18 at +48/+64; controlBytes count is 36 at +144;
localPNT count is 36 at +168. Using the old +80 deformer start read non-array data
as controlBytes count and correctly failed its bounds gate.

Acceptance: source signature parsing; normal BB pack with full semantic readback;
conversion to the separate runtime ABI and actual native solver movement for all
five male body simulation domains; real HKX playback; targeted DS3 regression.

## Private legacy normalization

PreviewSchemaGenerator copies reviewed BendLink and legacy MeshMeshDeform definitions
and their POD structs into the private runtime schema only. Original female Hunter
fixtures exercise their normal BB reader/writer and native loading. This does not
authorize guessed schema layouts, relaxed finite/count checks, or vendor code patches.

Upstream MIT notices remain in LICENSE. Golden game files and detailed logs are
local evidence under ignored helper-ext and are not build dependencies.
