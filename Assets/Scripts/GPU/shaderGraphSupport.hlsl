#ifndef SHADER_GRAPH_SUPPORT_INCLUDED
#define SHADER_GRAPH_SUPPORT_INCLUDED

struct InstanceData
{
    float4x4 worldMatrix;
    float4x4 worldMatrixInverse;
    float3 unused1;
    float3 unused2;
};

StructuredBuffer<uint> _InstancingIndexes;
StructuredBuffer<InstanceData> _PerInstanceData;

// com.unity.render-pipelines.universal@14.0.11\ShaderLibrary\ParticleInstancing.hlsl
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(SHADERGRAPH_PREVIEW)
void instancingSetup()
{
    int instanceID = _InstancingIndexes[unity_InstanceID];
    // unity_ObjectToWorld = mul(unity_ObjectToWorld, _PerInstanceData[instanceID].worldMatrix);
    // unity_WorldToObject = mul(unity_WorldToObject, _PerInstanceData[instanceID].worldMatrixInverse);
    UNITY_MATRIX_M = _PerInstanceData[instanceID].worldMatrix;
    unity_WorldToObject = _PerInstanceData[instanceID].worldMatrixInverse;
}
#else
void instancingSetup() {}
#endif

void GetInstanceID_float(out float Out)
{
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && !defined(SHADERGRAPH_PREVIEW)
    Out = _InstancingIndexes[unity_InstanceID];
    #else
    Out = 0;
    #endif
}

void Instancing_float(float3 Position, out float3 Out)
{
    Out = Position;
}

#endif