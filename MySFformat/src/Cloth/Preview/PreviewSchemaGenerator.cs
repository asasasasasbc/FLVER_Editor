// GPL-3.0. Private scene/normalization schemas for the isolated preview builder.
// Layout provenance: SoulsAssetPipeline HKX2 d11caf989c917c7a43e2c7559915b1c5af218153.
// Does not extend or replace the editor's normal cloth schemas.
using System;
using System.Linq;
using System.Xml.Linq;

namespace MySFformat.Cloth.Preview
{
    internal static class PreviewSchemaGenerator
    {
        static XElement Class(string name,int size,uint signature,string parent,string fields)
        {
            var c=new XElement("class",new XAttribute("name",name),new XAttribute("size",size),new XAttribute("signature","0x"+signature.ToString("x")),new XAttribute("parent",parent));var members=new XElement("members");c.Add(members);
            foreach(string row in fields.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))
            {
                var f=row.Trim().Split(',');members.Add(new XElement("member",new XAttribute("name",f[0]),new XAttribute("offset",f[1]),new XAttribute("vtype","TYPE_"+f[2]),new XAttribute("vsubtype",f.Length>3&&f[3].Length>0?"TYPE_"+f[3]:""),new XAttribute("ctype",f.Length>4?f[4]:""),new XAttribute("etype",""),new XAttribute("arrsize",0),new XAttribute("flags","FLAGS_NONE")));
            }
            return c;
        }
        static void Merge(string input,string output,XElement[] compatibilityClasses=null)
        {
            var tree=XDocument.Load(input);var root=tree.Root;
            Action<XElement> optional=c=>{if(!root.Elements("class").Any(e=>(string)e.Attribute("name")== (string)c.Attribute("name")))root.Add(c);};
            // 仅供预览运行副本：BendLink / 旧 MeshMeshDeform 在已审核的
            // 2014 BB schema 中标注两种 ABI；普通 DS3 writer 不扩展。
            if(compatibilityClasses!=null)foreach(var c in compatibilityClasses)optional(new XElement(c));
            optional(Class("hclSphereShape",48,3615093445,"hclShape","sphere,32,STRUCT,,hkSphere"));
            optional(Class("hkSphere",16,339607449,"","pos,0,VECTOR4"));
            optional(Class("hclGatherAllVerticesOperator",64,3664999062,"hclOperator","vertexInputFromVertexOutput,32,ARRAY,INT16;inputBufferIdx,48,UINT32;outputBufferIdx,52,UINT32;gatherNormals,56,BOOL;partialGather,57,BOOL"));
            root.Add(Class("hkxAttributeHolder",32,4235330075,"hkReferencedObject","attributeGroups,16,ARRAY,STRUCT,hkReferencedObject"));
            root.Add(Class("hkxNode",128,451561904,"hkxAttributeHolder","name,32,STRINGPTR;object,40,POINTER,,hkReferencedObject;keyFrames,48,ARRAY,MATRIX4;children,64,ARRAY,POINTER,hkxNode;annotations,80,ARRAY,STRUCT,hkReferencedObject;linearKeyFrameHints,96,ARRAY,REAL;userProperties,112,STRINGPTR;selected,120,BOOL;bone,121,BOOL"));
            root.Add(Class("hkxScene",240,3230915880,"hkReferencedObject","modeller,16,STRINGPTR;asset,24,STRINGPTR;sceneLength,32,REAL;numFrames,36,UINT32;rootNode,40,POINTER,,hkxNode;selectionSets,48,ARRAY,POINTER; cameras,64,ARRAY,POINTER;lights,80,ARRAY,POINTER;materials,112,ARRAY,POINTER,hkxMaterial;inplaceTextures,128,ARRAY,POINTER;externalTextures,144,ARRAY,POINTER;skinBindings,160,ARRAY,POINTER;splines,176,ARRAY,POINTER;appliedTransform,192,MATRIX3"));
            root.Add(Class("hkxMesh",48,3235576879,"hkReferencedObject","sections,16,ARRAY,POINTER,hkxMeshSection;userChannelInfos,32,ARRAY,POINTER"));
            root.Add(Class("hkxMeshSection",112,1414739601,"hkReferencedObject","vertexBuffer,16,POINTER,,hkxVertexBuffer;indexBuffers,24,ARRAY,POINTER,hkxIndexBuffer;material,40,POINTER,,hkxMaterial;userChannels,48,ARRAY,POINTER;vertexAnimations,64,ARRAY,POINTER;linearKeyFrameHints,80,ARRAY,REAL;boneMatrixMap,96,ARRAY,STRUCT,hkReferencedObject"));
            root.Add(Class("hkxVertexBuffer",144,3340843844,"hkReferencedObject","data,16,STRUCT,,hkxVertexBufferVertexData;desc,120,STRUCT,,hkxVertexDescription"));
            root.Add(Class("hkxVertexBufferVertexData",104,1115423220,"","vectorData,0,ARRAY,UINT32;floatData,16,ARRAY,UINT32;uint32Data,32,ARRAY,UINT32;uint16Data,48,ARRAY,UINT16;uint8Data,64,ARRAY,UINT8;numVerts,80,UINT32;vectorStride,84,UINT32;floatStride,88,UINT32;uint32Stride,92,UINT32;uint16Stride,96,UINT32;uint8Stride,100,UINT32"));
            root.Add(Class("hkxVertexDescription",16,1197541056,"","decls,0,ARRAY,STRUCT,hkxVertexDescriptionElementDecl"));
            root.Add(Class("hkxVertexDescriptionElementDecl",24,2254172831,"","byteOffset,0,UINT32;type,4,UINT16;usage,6,UINT16;byteStride,8,UINT32;numElements,12,UINT8;channelID,16,STRINGPTR"));
            root.Add(Class("hkxIndexBuffer",64,2178010478,"hkReferencedObject","indexType,16,UINT8;indices16,24,ARRAY,UINT16;indices32,40,ARRAY,UINT32;vertexBaseOffset,56,UINT32;length,60,UINT32"));
            root.Add(Class("hkxMaterial",224,2019476322,"hkxAttributeHolder","name,32,STRINGPTR;stages,40,ARRAY,STRUCT,hkReferencedObject;diffuseColor,64,VECTOR4;ambientColor,80,VECTOR4;specularColor,96,VECTOR4;emissiveColor,112,VECTOR4;subMaterials,128,ARRAY,POINTER,hkxMaterial;extraData,144,POINTER;uvMapScale1,152,REAL;uvMapScale2,156,REAL;uvMapOffset1,160,REAL;uvMapOffset2,164,REAL;uvMapRotation,168,REAL;uvMapAlgorithm,172,UINT32;specularMultiplier,176,REAL;specularExponent,180,REAL;transparency,184,UINT8;userData,192,UINT64;properties,200,ARRAY,STRUCT,hkReferencedObject"));
            tree.Save(output);
        }
        static void Main(string[] args)
        {
            if(args.Length!=4)throw new ArgumentException("Expected BB input/output and DS3 input/output");
            string[] names={"hclBendLinkConstraintSet","hclBendLinkConstraintSetLink","hclMeshMeshDeformOperator","hclMeshMeshDeformOperatorTriangleVertexPair"};
            var compatibility=XDocument.Load(args[0]).Root.Elements("class").Where(c=>names.Contains((string)c.Attribute("name"))).ToArray();
            if(compatibility.Length!=names.Length)throw new InvalidOperationException("Missing audited preview compatibility schema");
            Merge(args[0],args[1]);Merge(args[2],args[3],compatibility);
        }
    }
}
