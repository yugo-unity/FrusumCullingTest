#ifndef SHADER_GRAPH_SUPPORT_INCLUDED
#define SHADER_GRAPH_SUPPORT_INCLUDED

#include "./InstanceData.hlsl"

StructuredBuffer<uint> _InstanceIndexes;
StructuredBuffer<InstanceData> _InstanceData;

// com.unity.render-pipelines.universal@14.0.11\ShaderLibrary\ParticleInstancing.hlsl
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(SHADERGRAPH_PREVIEW)
void instancingSetup()
{
    int instanceID = _InstanceIndexes[unity_InstanceID];
    // unity_ObjectToWorld = mul(unity_ObjectToWorld, _InstanceData[instanceID].worldMatrix);
    // unity_WorldToObject = mul(unity_WorldToObject, _InstanceData[instanceID].worldMatrixInverse);
    UNITY_MATRIX_M = _InstanceData[instanceID].worldMatrix;
    //unity_WorldToObject = Inverse(UNITY_MATRIX_M); not used
}
#else
void instancingSetup() {}
#endif

void GetInstanceID_float(out float Out)
{
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(SHADERGRAPH_PREVIEW)
    Out = _InstanceIndexes[unity_InstanceID];
    #else
    Out = 0;
    #endif
}

void Instancing_float(float3 Position, out float3 Out)
{
    Out = Position;
}
// float4x4 Inverse(float4x4 m)
// {
//     float4 det1 = mad(m._33_31_13_11, m._44_42_24_22, -m._34_32_14_12 * m._43_41_23_21);
//     float4 det2 = mad(m._23_21_13_11, m._44_42_34_32, -m._24_22_14_12 * m._43_41_33_31);
//     float4 det3 = mad(m._23_21_13_11, m._34_32_44_42, -m._24_22_14_12 * m._33_31_43_41);
// 	
//     float4x4 im;
//     im._11_21_31_41 = mad(m._22_21_24_23, det1.xxyy, mad(-m._32_31_34_33, det2.xxyy, m._42_41_44_43 * det3.xxyy));
//     im._12_22_32_42 = mad(m._12_11_14_13, det1.xxyy, mad(-m._32_31_34_33, det3.zzww, m._42_41_44_43 * det2.zzww));
//     im._13_23_33_43 = mad(m._12_11_14_13, det2.xxyy, mad(-m._22_21_24_23, det3.zzww, m._42_41_44_43 * det1.zzww));
//     im._14_24_34_44 = mad(m._12_11_14_13, det3.xxyy, mad(-m._22_21_24_23, det2.zzww, m._32_31_34_33 * det1.zzww));
// 	
//     im._21_41 = -im._21_41;
//     im._12_32 = -im._12_32;
//     im._23_43 = -im._23_43;
//     im._14_34 = -im._14_34;
// 	
//     float invDet = rcp(dot(m[0], im._11_21_31_41));
//     im._11_21_31_41 *= invDet;
//     im._12_22_32_42 *= invDet;
//     im._13_23_33_43 *= invDet;
//     im._14_24_34_44 *= invDet;
// 	
//     return im;
// }

#endif