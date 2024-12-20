using System.Collections.Generic;
using System.Runtime.CompilerServices;
using InstancingFeature;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class GpuFrustumCuller : MonoBehaviour
{
    [SerializeField]
    Mesh[] lodMeshes = new Mesh[2];
    [SerializeField]
    Material material;
    [SerializeField]
    ComputeShader computeCulling;
    [SerializeField]
    ComputeShader computeCullingSOA;
    [SerializeField]
    float lodThreshold = 0.1f;

    public bool enabledFrustumSOA = false; 
    
    InstancingChunk<InstancingStatic> chunk;
    
    Camera cam;
    Transform camTransform;
    float4[] planes = new float4[Constants.FrustumPlaneCount];
    GraphicsBuffer frustumPlaneBuffer;
    
    void Start()
    {
        this.cam = GetComponent<Camera>();
        this.camTransform = this.cam.transform;
        this.chunk = new InstancingChunk<InstancingStatic>();

        // NOTE: これは既存配置をランタイムで差し替える検証処理なので本実装はデータは静的に用意すること
        var obj = GameObject.FindGameObjectsWithTag("Respawn");
        var list = new List<InstancingStatic>(obj.Length);
        for (var i = 0; i < obj.Length; i++)
        {
            var mat = obj[i].transform.localToWorldMatrix;
            var renderer = obj[i].GetComponent<MeshRenderer>();
            var data = new InstancingStatic();
            data.worldMatrix = mat;
            data.worldMatrixInverse = mat.inverse;
            if (this.enabledFrustumSOA)
            {
                data.boundPoint = renderer.bounds.center;
                data.boundSize = renderer.bounds.extents;
            }
            else
            {
                data.boundPoint = renderer.bounds.min;
                data.boundSize = renderer.bounds.size;
            };
            list.Add(data);
            obj[i].SetActive(false);
        }
        var compute = this.enabledFrustumSOA ? this.computeCullingSOA : this.computeCulling;
        this.chunk.Initialize(compute, this.lodMeshes, this.material, list, this.lodThreshold);
        
        this.frustumPlaneBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Constants.FrustumPlaneCount, Constants.Float4ByteSize);
    }

    private void OnDestroy()
    {
        this.chunk?.Dispose();
        this.frustumPlaneBuffer?.Dispose();
    }

    void Update()
    {
        if (!this.cam.enabled)
            return;

        UpdatePlanes(this.cam, this.planes, this.enabledFrustumSOA);
        
        var cameraPosition = this.camTransform.position;
        this.frustumPlaneBuffer.SetData(this.planes);
        chunk.Execute(this.cam, cameraPosition, this.frustumPlaneBuffer);
        
#if UNITY_EDITOR
        // for SceneView
        chunk.Execute(SceneView.lastActiveSceneView.camera, cameraPosition, this.frustumPlaneBuffer);
#endif
    }

    static void UpdatePlanes(Camera camera, float4[] planes, bool enabledFrustumSOA)
    {
        // NOTE: the same value
        // var proj = this.cullCamera.projectionMatrix;
        // var view = this.cullCamera.worldToCameraMatrix;
        //var vp = proj * view;
        float4x4 vp = camera.cullingMatrix;
        var c0 = vp.c0;
        var c1 = vp.c1;
        var c2 = vp.c2;
        var c3 = vp.c3;
        var row0 = new float4(c0.x, c1.x, c2.x, c3.x);
        var row1 = new float4(c0.y, c1.y, c2.y, c3.y);
        //var row2 = new float4(c0.z, c1.z, c2.z, c3.z);
        var row3 = new float4(c0.w, c1.w, c2.w, c3.w);

        if (enabledFrustumSOA)
        {
            var p0 = NormalizePlane(row3 + row0); // L
            var p1 = NormalizePlane(row3 - row0); // R
            var p2 = NormalizePlane(row3 + row1); // B
            var p3 = NormalizePlane(row3 - row1); // T
            planes[0] = new float4(p0.x, p1.x, p2.x, p3.x);
            planes[1] = new float4(p0.y, p1.y, p2.y, p3.y);
            planes[2] = new float4(p0.z, p1.z, p2.z, p3.z);
            planes[3] = new float4(p0.w, p1.w, p2.w, p3.w);
        }
        else
        {
            var row2 = new float4(c0.z, c1.z, c2.z, c3.z);
            planes[0] = NormalizePlane(row3 + row0); // L
            planes[1] = NormalizePlane(row3 - row0); // R
            planes[2] = NormalizePlane(row3 + row1); // B
            planes[3] = NormalizePlane(row3 - row1); // T
            planes[4] = NormalizePlane(row3 + row2); // N
            planes[5] = NormalizePlane(row3 - row2); // F
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float4 NormalizePlane(float4 plane)
    {
        return plane / math.length(plane.xyz);
    }
}
