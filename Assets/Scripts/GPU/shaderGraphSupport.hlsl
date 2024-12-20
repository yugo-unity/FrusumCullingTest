#ifndef SHADER_GRAPH_SUPPORT_INCLUDED
#define SHADER_GRAPH_SUPPORT_INCLUDED

#include "Assets/Scripts/GPU/InstanceDta.hlsl"

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
    unity_WorldToObject = _InstanceData[instanceID].worldMatrixInverse;
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

#endif