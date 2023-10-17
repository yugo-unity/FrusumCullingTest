#define AABB // using BoundingBox

using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// 自前CullingGroup
/// Frustum外のRendererを明示的にdisabledにすることでSceneCullingの負荷を下げる
/// WorkerThread（≒CPU core）が十分にある環境下では正直あまり意味ないかも
/// </summary>
[DefaultExecutionOrder(-10)] // should be early
[RequireComponent(typeof(Camera))]
public class PreFrustumCuller : MonoBehaviour
{
    enum TEST_TYPE
    {
        NORMAL,
        SOA,
        CULLING_GROUP,
    }
    
    #region BURST SOA
    // Plane by SoA
    struct PlanePacket4
    {
        public float4 nx;
        public float4 ny;
        public float4 nz;
        public float4 d;
    }

    [Unity.Burst.BurstCompile]
    struct CullingSoAJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<bool> visibilities;
#if AABB
        [ReadOnly]
        public NativeArray<float3> bounds;
        [ReadOnly]
        public NativeArray<float3> extents;
#else
        [ReadOnly]
        public NativeArray<float4> bounds;
#endif
        [ReadOnly]
        public PlanePacket4 planePacket;
    
        public void Execute(int i)
        {
            var cx = this.bounds[i].xxxx;
            var cy = this.bounds[i].yyyy;
            var cz = this.bounds[i].zzzz;
            var distances = this.planePacket.nx * cx
                                + this.planePacket.ny * cy
                                + this.planePacket.nz * cz
                                + this.planePacket.d;
#if AABB
            var ex = this.extents[i].xxxx;
            var ey = this.extents[i].yyyy;
            var ez = this.extents[i].zzzz;
            var radii = math.abs(this.planePacket.nx) * ex
                            + math.abs(this.planePacket.ny) * ey
                            + math.abs(this.planePacket.nz) * ez;
#else
            var radii = this.bounds[i].wwww;
#endif
            var isCulled = distances + radii < float4.zero;
            this.visibilities[i] = !math.any(isCulled);
        }
    }

    #endregion
    
    #region NORMAL
    [Unity.Burst.BurstCompile]
    struct CullingJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<bool> visibilities;
#if AABB
        [ReadOnly]
        public NativeArray<float3> bounds;
        [ReadOnly]
        public NativeArray<float3> extents;
#else
        [ReadOnly]
        public NativeArray<float4> bounds;
#endif
        [ReadOnly]
        public NativeArray<float4> planes;

        public void Execute(int i)
        {
            this.visibilities[i] = true;
            for (var index = 0; index < this.planes.Length; index++)
            {
                var normal = planes[index].xyz;
                var dist = math.dot(normal, this.bounds[i].xyz) + planes[index].w;
#if AABB
                var radius = math.dot(this.extents[i], math.abs(normal));
#else
                var radius = this.bounds[i].w;
#endif
                if (dist + radius < 0f)
                {
                    this.visibilities[i] = false;
                    break;
                }
            }
        }
    }
    #endregion

    #region MEMBER
    [SerializeField] TEST_TYPE testType = TEST_TYPE.NORMAL; 
    [SerializeField] int jobBatchCount = 32; 
    
    CullingSoAJob cullingSoAJob;
    CullingJob cullingJob;
    JobHandle jobHandle;

    CullingGroup cullGroup;

#if AABB
    NativeArray<float3> bounds;
    NativeArray<float3> extents;
#else
    NativeArray<float4> bounds;
#endif
    NativeArray<float4> planes;
    NativeArray<bool> visibilities;
    PlanePacket4 planeShuffle;

    Camera cullCamera;
    CullingArea[] cullingArea;
    #endregion

    void Start()
    {
        this.cullingSoAJob = new CullingSoAJob();
        this.cullCamera = this.GetComponent<Camera>();
        this.cullingArea = FindObjectsByType<CullingArea>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        
        var numBounds = this.cullingArea.Length;
        if (this.visibilities.IsCreated)
            this.visibilities.Dispose();
        this.visibilities = new NativeArray<bool>(numBounds, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        if (this.bounds.IsCreated)
            this.bounds.Dispose();
#if AABB
        this.bounds = new NativeArray<float3>(numBounds, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        this.extents = new NativeArray<float3>(numBounds, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
#else
        this.bounds = new NativeArray<float4>(numBounds, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
#endif
        if (!this.planes.IsCreated)
            this.planes = new NativeArray<float4>(4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        
        this.cullGroup = new CullingGroup();
        this.cullGroup.targetCamera = this.cullCamera;
        var spheres = new BoundingSphere[numBounds];

        for (var i = 0; i < numBounds; i++)
        {
#if AABB
            this.bounds[i] = this.cullingArea[i].boundsSphere.xyz;
            this.extents[i] = this.cullingArea[i].boundsExtent;
#else
            this.bounds[i] = this.cullingArea[i].boundsSphere;
#endif
            
            spheres[i] = new BoundingSphere(this.cullingArea[i].boundsSphere.xyz, this.cullingArea[i].boundsSphere.w);
        }

        switch (this.testType)
        {
            case TEST_TYPE.CULLING_GROUP:
                this.cullGroup.SetBoundingSpheres(spheres);
                this.cullGroup.SetBoundingSphereCount(numBounds);
                this.cullGroup.onStateChanged = this.StateChangedMethod;
                foreach (var area in this.cullingArea)
                    area.SetVisible(false);
                break;
            case TEST_TYPE.NORMAL:
                this.cullingJob.bounds = this.bounds;
#if AABB
                this.cullingJob.extents = this.extents;
#endif
                this.cullingJob.visibilities = this.visibilities;
                break;
            case TEST_TYPE.SOA:
                this.cullingSoAJob.bounds = this.bounds;
#if AABB
                this.cullingSoAJob.extents = this.extents;
#endif
                this.cullingSoAJob.visibilities = this.visibilities;
                break;
        }
    }
    void StateChangedMethod(CullingGroupEvent evt)
    {
        if (evt.hasBecomeVisible)
        {
            this.cullingArea[evt.index].SetVisible(true);
            Debug.LogFormat("Sphere {0} has become visible!", evt.index);
        }
        if (evt.hasBecomeInvisible)
        {
            this.cullingArea[evt.index].SetVisible(false);
            Debug.LogFormat("Sphere {0} has become invisible!", evt.index);
        }
    }

    void OnDisable()
    {
        this.bounds.Dispose();
        this.visibilities.Dispose();
#if AABB
        this.extents.Dispose();
#endif
        this.planes.Dispose();
        
        this.cullGroup.Dispose();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float4 NormalizePlane(float4 plane)
    {
        return plane / math.length(plane.xyz);
    }

    void UpdatePlanes()
    {
        // NOTE: the same value
        // var proj = this.cullCamera.projectionMatrix;
        // var view = this.cullCamera.worldToCameraMatrix;
        //var vp = proj * view;
        float4x4 vp = this.cullCamera.cullingMatrix;
        var c0 = vp.c0;
        var c1 = vp.c1;
        var c2 = vp.c2;
        var c3 = vp.c3;
        var row0 = new float4(c0.x, c1.x, c2.x, c3.x);
        var row1 = new float4(c0.y, c1.y, c2.y, c3.y);
        //var row2 = new float4(c0.z, c1.z, c2.z, c3.z);
        var row3 = new float4(c0.w, c1.w, c2.w, c3.w);

        // this.frustumData[0] = NormalizePlane(row3 + row0); // L
        // this.frustumData[1] = NormalizePlane(row3 - row0); // R
        // this.frustumData[2] = NormalizePlane(row3 + row1); // B
        // this.frustumData[3] = NormalizePlane(row3 - row1); // T
        // this.frustumData[4] = NormalizePlane(row3 + row2); // N
        // this.frustumData[5] = NormalizePlane(row3 - row2); // F
        var p0 = NormalizePlane(row3 + row0);
        var p1 = NormalizePlane(row3 - row0);
        var p2 = NormalizePlane(row3 + row1);
        var p3 = NormalizePlane(row3 - row1);
        this.planeShuffle.nx = new float4(p0.x, p1.x, p2.x, p3.x);
        this.planeShuffle.ny = new float4(p0.y, p1.y, p2.y, p3.y);
        this.planeShuffle.nz = new float4(p0.z, p1.z, p2.z, p3.z);
        this.planeShuffle.d = new float4(p0.w, p1.w, p2.w, p3.w);

        this.planes[0] = p0;
        this.planes[1] = p1;
        this.planes[2] = p2;
        this.planes[3] = p3;
    }

    void Update()
    {
        // NOTE: you can parallelize with Job if this processing is stuck
        this.UpdatePlanes();

        switch (this.testType)
        {
            case TEST_TYPE.NORMAL:
                this.cullingJob.planes = this.planes;
                this.jobHandle = this.cullingJob.Schedule(this.visibilities.Length, this.jobBatchCount);
                break;
            case TEST_TYPE.SOA:
                this.cullingSoAJob.planePacket = this.planeShuffle;
                this.jobHandle = this.cullingSoAJob.Schedule(this.visibilities.Length, this.jobBatchCount);
                break;
        }
    }

    unsafe void LateUpdate()
    {
        if (this.testType == TEST_TYPE.CULLING_GROUP)
            return;

        this.jobHandle.Complete();

        // NOTE: when Mono, NativeArray with indexer is slow. but also when IL2CPP...?  
        var length = this.visibilities.Length;
        var ptr = (bool*)this.visibilities.GetUnsafePtr();
        for (var i = 0; i < length; i++, ptr++)
            this.cullingArea[i].SetVisible(*ptr);
    }
}
