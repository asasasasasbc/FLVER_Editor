# Equipment schema extension — 2026-10-15

The original schema was a fixture-derived closure, not the complete set of
classes used by BB/DS3 equipment. Missing metadata must not be bypassed by
dropping virtual objects, suppressing signature checks or substituting layouts.

## Sources and layout checks

The additional class declarations and inline POD closure come from the existing
MIT hkxpack-souls classxml archive. `equipment-schema-provenance.json` records the
archive hash and each source declaration hash. Existing license notices apply.
No vendor DLL, game asset or detailed game-derived evidence is distributed.

BB and DS3 layouts remain separate. The new BoneSpace mesh-deform parent starts
input/output scalars at 28/32 in BB, versus 32/36 in DS3. Its inline deformer is
at 56 versus 64; parent size is 144 versus 152. Derived P/PN/PNT arrays start at
144/160 (BB) versus 152/168 (DS3), total size 176 versus 184. Original BB fixup
slots and hkArray headers were checked directly, independently of XML roundtrip.

ObjectSpace PN follows the previously reviewed ObjectSpace parent rule: arrays
at 160/176 in BB, versus 168/184 in DS3. DS3 sphere data is aligned at 32, not
the BB offset 16. Volume and VolumeMx structs retain their declared vector
alignment, record stride, 16-lane fixed arrays and original constraint classes.

Private native-preview schemas also retain legacy BB scalar Volume metadata;
this enables runtime normalization without rewriting it into VolumeMx or
omitting the constraint. This does not implement either kernel in OpenPhysicsHKX.

The reader also accepts the explicit unbound-collider case: transformSetIndex=-1
and both map arrays empty. The collider uses its authored transform, not an
invented skeleton binding. Negative/partial maps outside that exact case remain
invalid. The native builder recognizes reviewed ObjectSpace/BoneSpace mesh
deformers as initialization producers and retains their runtime operator graph.

Fixture-free layout checks: `tests/verify_cloth_equipment_schema.py`.
Asset-free bound/unbound/invalid-map checks: `tests/ClothColliderBindingTests.cs`.
Private corpus tests exercise normal document loading, in-memory Pack with full
semantic readback, and separate real HCT preview. A loading/roundtrip pass is not
a claim of equivalent managed simulation, animation or gameplay behavior.

## Local validation scope

The installed primary equipment census covered 1017 standard numeric-name body,
leg, head and arm binders: BB 398 binders containing 156 Cloth HKX; DS3 619
containing 213. All **369/369** passed normal document loading and in-memory
Pack/full semantic readback (baseline: 216/369 loaded). Low-detail `_l` archives,
weapons, face/hair, backups and other installations are outside this census.

All original binder SHA256 hashes were unchanged. Original relocation coverage
for the new virtual classes checked 660 objects / 1710 fixups with no unmodeled
slots. Existing 91 BB and 82 DS3 class declarations were unchanged; the extension
adds 19 and 28 declarations, respectively (including required POD closure).

Sixteen separate representative assets passed real HCT startup and 120 finite
frames, authored gravity, wind off, no animation. Eight targeted Inspector
documents covering 31 domains loaded and populated their grids. The five metadata
tests, nine synthetic collider-map checks and 28 GUI/lifecycle checks passed.
This is not 369-asset native simulation coverage or managed/native equivalence.

The subsequent ER compatibility work adds eight further **2014-runtime** class
declarations to the DS3 ABI profile (source hashes in the same provenance file),
without changing existing declarations. See `er-tagfile-audit.md`. These are not
metadata offsets used to decode ER TAG0; that reader uses bounded in-file TYPE.
