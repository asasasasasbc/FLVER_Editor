"""Fixture-free checks for the reviewed BB/DS3 equipment metadata extension.

Run: python tests/verify_cloth_equipment_schema.py
Game-file loading, semantic roundtrip and native preview are separate local tests.
"""
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1] / 'tools' / 'Cloth'
SCHEMAS = {
    game: {c.get('name'): c for c in ET.parse(ROOT / filename).getroot()}
    for game, filename in [('BB', 'native-schema.xml'), ('DS3', 'native-schema-ds3.xml')]
}
PRIMITIVES = {'TYPE_BOOL': 1, 'TYPE_INT8': 1, 'TYPE_UINT8': 1,
              'TYPE_INT16': 2, 'TYPE_UINT16': 2, 'TYPE_INT32': 4,
              'TYPE_UINT32': 4, 'TYPE_REAL': 4, 'TYPE_ENUM': 4,
              'TYPE_VECTOR4': 16, 'TYPE_ARRAY': 16, 'TYPE_POINTER': 8,
              'TYPE_STRINGPTR': 8, 'TYPE_MATRIX4': 64}


class EquipmentSchemaTests(unittest.TestCase):
    def member(self, game, cls, member):
        return SCHEMAS[game][cls].find(f"members/member[@name='{member}']")

    def test_new_member_bounds_and_no_serialized_overlap(self):
        audit = json.loads((ROOT / 'equipment-schema-provenance.json').read_text())
        for game, records in audit['profiles'].items():
            classes = SCHEMAS[game]

            def ranges(name):
                cls = classes[name]
                result = ranges(cls.get('parent')) if cls.get('parent') else []
                for m in cls.findall('members/member'):
                    if 'SERIALIZE_IGNORED' in m.get('flags', ''):
                        continue
                    kind = m.get('vtype')
                    size = int(classes[m.get('ctype')].get('size')) if kind == 'TYPE_STRUCT' else PRIMITIVES[kind]
                    start = int(m.get('offset'))
                    result.append((start, start + size * max(1, int(m.get('arrsize', '0'))), m.get('name')))
                return result

            for record in records:
                with self.subTest(game=game, cls=record['class']):
                    cls = classes[record['class']]
                    self.assertEqual(int(cls.get('signature'), 16), int(record['signature'], 16))
                    intervals = sorted(ranges(record['class']))
                    self.assertTrue(all(0 <= lo < hi <= int(cls.get('size')) for lo, hi, _ in intervals))
                    self.assertTrue(all(a[1] <= b[0] for a, b in zip(intervals, intervals[1:])))

    def test_bone_space_parent_tail_profiles(self):
        for game, size, input_offset, deformer_offset in [('BB', 144, 28, 56), ('DS3', 152, 32, 64)]:
            self.assertEqual(int(SCHEMAS[game]['hclBoneSpaceMeshMeshDeformOperator'].get('size')), size)
            self.assertEqual(int(self.member(game, 'hclBoneSpaceMeshMeshDeformOperator', 'inputBufferIdx').get('offset')), input_offset)
            self.assertEqual(int(self.member(game, 'hclBoneSpaceMeshMeshDeformOperator', 'boneSpaceDeformer').get('offset')), deformer_offset)
            for suffix in ('P', 'PN', 'PNT'):
                cls = SCHEMAS[game]['hclBoneSpaceMeshMeshDeform' + suffix + 'Operator']
                self.assertEqual([int(m.get('offset')) for m in cls.findall('members/member')], [size, size + 16])
                self.assertEqual(int(cls.get('size')), size + 32)

    def test_object_space_pn_profiles(self):
        for game, start in [('BB', 160), ('DS3', 168)]:
            cls = SCHEMAS[game]['hclObjectSpaceMeshMeshDeformPNOperator']
            self.assertEqual([int(m.get('offset')) for m in cls.findall('members/member')], [start, start + 16])
            self.assertEqual(int(cls.get('size')), start + 32)

    def test_volume_batch_layout(self):
        for suffix in ('FrameBatchData', 'ApplyBatchData'):
            cls = SCHEMAS['DS3']['hclVolumeConstraintMx' + suffix]
            self.assertEqual(int(cls.get('size')), 352)
            self.assertEqual([int(m.get('arrsize')) for m in cls.findall('members/member')], [16, 16, 16])
            self.assertEqual([int(m.get('offset')) for m in cls.findall('members/member')], [0, 256, 288])

    def test_ds3_gather_and_sphere(self):
        self.assertEqual(int(self.member('DS3', 'hclGatherAllVerticesOperator', 'vertexInputFromVertexOutput').get('offset')), 32)
        self.assertEqual(int(self.member('DS3', 'hclSphereShape', 'sphere').get('offset')), 32)
        self.assertEqual(int(SCHEMAS['DS3']['hclSphereShape'].get('size')), 48)


if __name__ == '__main__':
    unittest.main()
