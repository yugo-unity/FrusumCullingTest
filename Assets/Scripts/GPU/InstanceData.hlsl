#ifndef INSTANCE_DATA_INCLUDED
#define INSTANCE_DATA_INCLUDED

struct InstanceData
{
    float4x4 worldMatrix;
    //float4x4 worldMatrixInverse;
    float3 boundPoint;  // boundMin or boundCenter
    float3 boundSize;   // boundExtents if using SoA
};

#endif