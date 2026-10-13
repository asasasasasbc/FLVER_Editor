"""Narrow scene-adapter layout, NOT an application cloth schema extension.

Offsets/signatures informed by GPL-3.0 SoulsAssetPipeline HKX2 Autogen sources:
KamiyamaShiki0704/SoulsAssetPipeline d11caf989c917c7a43e2c7559915b1c5af218153.
Only the generated scene fields are supported. Unsupported inline-array fields
are kept empty and use a placeholder stride; never author arbitrary scenes here.
"""
import xml.etree.ElementTree as E

# name: (size, signature, parent, [(name, offset, type, subtype, target)])
def field(name, offset, kind, subtype='', target=''):
    return name, offset, 'TYPE_' + kind, 'TYPE_' + subtype if subtype else '', target


def arr(name, offset, subtype='POINTER', target='hkReferencedObject'):
    return field(name, offset, 'ARRAY', subtype, target)


CLASSES = {
    'hkxAttributeHolder': (32, 4235330075, 'hkReferencedObject', [arr('attributeGroups', 16, 'STRUCT')]),
    'hkxNode': (128, 451561904, 'hkxAttributeHolder', [field('name', 32, 'STRINGPTR'),
        field('object', 40, 'POINTER', target='hkReferencedObject'), arr('keyFrames', 48, 'MATRIX4'),
        arr('children', 64, target='hkxNode'), arr('annotations', 80, 'STRUCT'),
        arr('linearKeyFrameHints', 96, 'REAL'), field('userProperties', 112, 'STRINGPTR'),
        field('selected', 120, 'BOOL'), field('bone', 121, 'BOOL')]),
    'hkxScene': (240, 3230915880, 'hkReferencedObject', [field('modeller', 16, 'STRINGPTR'),
        field('asset', 24, 'STRINGPTR'), field('sceneLength', 32, 'REAL'), field('numFrames', 36, 'UINT32'),
        field('rootNode', 40, 'POINTER', target='hkxNode'), arr('selectionSets', 48), arr('cameras', 64),
        arr('lights', 80), arr('materials', 112, target='hkxMaterial'), arr('inplaceTextures', 128),
        arr('externalTextures', 144), arr('skinBindings', 160), arr('splines', 176),
        field('appliedTransform', 192, 'MATRIX3')]),
    'hkxMesh': (48, 3235576879, 'hkReferencedObject', [arr('sections', 16, target='hkxMeshSection'), arr('userChannelInfos', 32)]),
    'hkxMeshSection': (112, 1414739601, 'hkReferencedObject', [field('vertexBuffer', 16, 'POINTER', target='hkxVertexBuffer'),
        arr('indexBuffers', 24, target='hkxIndexBuffer'), field('material', 40, 'POINTER', target='hkxMaterial'),
        arr('userChannels', 48), arr('vertexAnimations', 64), arr('linearKeyFrameHints', 80, 'REAL'), arr('boneMatrixMap', 96, 'STRUCT')]),
    'hkxVertexBuffer': (144, 3340843844, 'hkReferencedObject', [field('data', 16, 'STRUCT', target='hkxVertexBufferVertexData'),
        field('desc', 120, 'STRUCT', target='hkxVertexDescription')]),
    'hkxVertexBufferVertexData': (104, 1115423220, '', [arr('vectorData', 0, 'UINT32'), arr('floatData', 16, 'UINT32'),
        arr('uint32Data', 32, 'UINT32'), arr('uint16Data', 48, 'UINT16'), arr('uint8Data', 64, 'UINT8'),
        field('numVerts', 80, 'UINT32'), field('vectorStride', 84, 'UINT32'), field('floatStride', 88, 'UINT32'),
        field('uint32Stride', 92, 'UINT32'), field('uint16Stride', 96, 'UINT32'), field('uint8Stride', 100, 'UINT32')]),
    'hkxVertexDescription': (16, 1197541056, '', [arr('decls', 0, 'STRUCT', 'hkxVertexDescriptionElementDecl')]),
    'hkxVertexDescriptionElementDecl': (24, 2254172831, '', [field('byteOffset', 0, 'UINT32'), field('type', 4, 'UINT16'),
        field('usage', 6, 'UINT16'), field('byteStride', 8, 'UINT32'), field('numElements', 12, 'UINT8'), field('channelID', 16, 'STRINGPTR')]),
    'hkxIndexBuffer': (64, 2178010478, 'hkReferencedObject', [field('indexType', 16, 'UINT8'), arr('indices16', 24, 'UINT16'),
        arr('indices32', 40, 'UINT32'), field('vertexBaseOffset', 56, 'UINT32'), field('length', 60, 'UINT32')]),
    'hkxMaterial': (224, 2019476322, 'hkxAttributeHolder', [field('name', 32, 'STRINGPTR'), arr('stages', 40, 'STRUCT'),
        field('diffuseColor', 64, 'VECTOR4'), field('ambientColor', 80, 'VECTOR4'), field('specularColor', 96, 'VECTOR4'),
        field('emissiveColor', 112, 'VECTOR4'), arr('subMaterials', 128, target='hkxMaterial'), field('extraData', 144, 'POINTER'),
        field('uvMapScale1', 152, 'REAL'), field('uvMapScale2', 156, 'REAL'), field('uvMapOffset1', 160, 'REAL'),
        field('uvMapOffset2', 164, 'REAL'), field('uvMapRotation', 168, 'REAL'), field('uvMapAlgorithm', 172, 'UINT32'),
        field('specularMultiplier', 176, 'REAL'), field('specularExponent', 180, 'REAL'), field('transparency', 184, 'UINT8'),
        field('userData', 192, 'UINT64'), arr('properties', 200, 'STRUCT')]),
}


def merge(input_path, output_path):
    tree = E.parse(input_path)
    names = {c.get('name') for c in tree.getroot()}
    # Probe-only closure for BB spheres normalized to the native HCT layout.
    # HKX2 hclShape writes 24 bytes; hclSphereShape pads 8, then hkSphere (16).
    # Never copy the BB offset 16 into this layout; the native payload starts 32.
    if 'hclSphereShape' not in names:
        c = E.SubElement(tree.getroot(), 'class', name='hclSphereShape', size='48',
                         signature=hex(3615093445), parent='hclShape')
        E.SubElement(E.SubElement(c, 'members'), 'member', name='sphere', offset='32',
                     vtype='TYPE_STRUCT', vsubtype='TYPE_VOID', ctype='hkSphere', etype='', arrsize='0', flags='FLAGS_NONE')
    if 'hkSphere' not in names:
        c = E.SubElement(tree.getroot(), 'class', name='hkSphere', size='16', signature=hex(339607449), parent='')
        E.SubElement(E.SubElement(c, 'members'), 'member', name='pos', offset='0',
                     vtype='TYPE_VECTOR4', vsubtype='TYPE_VOID', ctype='', etype='', arrsize='0', flags='FLAGS_NONE')
    if 'hclGatherAllVerticesOperator' not in names:
        c = E.SubElement(tree.getroot(), 'class', name='hclGatherAllVerticesOperator', size='64',
                         signature=hex(3664999062), parent='hclOperator')
        members = E.SubElement(c, 'members')
        for member, offset, kind, subtype, target in [arr('vertexInputFromVertexOutput', 32, 'INT16'),
                field('inputBufferIdx', 48, 'UINT32'), field('outputBufferIdx', 52, 'UINT32'),
                field('gatherNormals', 56, 'BOOL'), field('partialGather', 57, 'BOOL')]:
            E.SubElement(members, 'member', name=member, offset=str(offset), vtype=kind, vsubtype=subtype,
                         ctype=target, etype='', arrsize='0', flags='FLAGS_NONE')
    for name, (size, signature, parent, fields) in CLASSES.items():
        if name in names:
            raise ValueError('Unexpected scene class in original cloth schema: ' + name)
        c = E.SubElement(tree.getroot(), 'class', name=name, size=str(size), signature=hex(signature), parent=parent)
        members = E.SubElement(c, 'members')
        for member, offset, kind, subtype, target in fields:
            E.SubElement(members, 'member', name=member, offset=str(offset), vtype=kind, vsubtype=subtype,
                         ctype=target, etype='', arrsize='0', flags='FLAGS_NONE')
    tree.write(output_path, encoding='utf-8', xml_declaration=True)
