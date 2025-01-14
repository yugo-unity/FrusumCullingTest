using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace InstancingFeature
{
    static class ShaderProp
    {
        public static readonly int CameraPos = Shader.PropertyToID("_CameraPos");
        public static readonly int LodThreshold = Shader.PropertyToID("_LODThreshold");
        public static readonly int TanFOV = Shader.PropertyToID("_TanFOV");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int InstanceCount = Shader.PropertyToID("_InstanceCount");
        public static readonly int FrustumPlanesID = Shader.PropertyToID("_FrustumPlanes");
        public static readonly int InstanceDataID = Shader.PropertyToID("_InstanceData");
        public static readonly int InstanceIndexes = Shader.PropertyToID("_InstanceIndexes");
        public static readonly int[] LodIndexId =
        {
            Shader.PropertyToID("_Lod0Indexes"),
            Shader.PropertyToID("_Lod1Indexes"),
        };
    }

    static class Constants
    {
        public static readonly int MaxLOD = 2;
        public static readonly int FrustumPlanes = 4;//6;
        public static readonly int Float4ByteSize = Marshal.SizeOf<float4>();
        public static readonly int IntByteSize = Marshal.SizeOf<int>();
        public static readonly int CullLODKernelIndex = 0;
    }

    // NOTE: Instance毎にカラーを変えたい場合はBaseColorにCustomFunctionで差し込む必要がある
    [StructLayout(LayoutKind.Sequential), System.Serializable]
    public struct InstancingStatic
    {
        public Matrix4x4 worldMatrix;
        //public Matrix4x4 worldMatrixInverse;
        public Vector3 boundPoint;
        public Vector3 boundSize;
    }

    public class InstancingChunk<T> where T : struct
    {
        static readonly int InstanceSize = Marshal.SizeOf<T>();

        GraphicsBuffer instanceBuffer, lodArgsBuffer, frustumPlaneBuffer;
        GraphicsBuffer[] lodIndexesBuffers;
        GraphicsBuffer.IndirectDrawIndexedArgs[] drawArgs;
        Mesh[] lodMeshes;
        MaterialPropertyBlock[] mpb;
        RenderParams[] renderParams;
        Material material;
        int threadGroupX;
        ComputeShader computeCullLOD;
        float4[] planes = new float4[Constants.FrustumPlanes];

        GraphicsBuffer.IndirectDrawIndexedArgs SetupArgs(in Mesh mesh)
        {
            var args = new GraphicsBuffer.IndirectDrawIndexedArgs();
            args.indexCountPerInstance = mesh.GetIndexCount(0);
            args.startIndex = mesh.GetIndexStart(0);
            args.baseVertexIndex = mesh.GetBaseVertex(0);
            args.startInstance = args.instanceCount = 0;
            return args;
        }

        public void Initialize(ComputeShader compute, Mesh[] lod, Material mat, T[] dataList, float lodThreshold)
        {
            Debug.Assert(compute != null, "Shader is null!");
            Debug.Assert(lod != null && lod.Length == Constants.MaxLOD, $"LOD level is supported only 2! - current level is {lod.Length}");

            var maxCount = dataList.Length;
            compute.GetKernelThreadGroupSizes(Constants.CullLODKernelIndex, out var x, out var y, out var z);
            this.threadGroupX = Mathf.CeilToInt((float)maxCount / x);
            
            this.computeCullLOD = compute;
            this.material = mat;
            this.instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxCount, InstanceSize);
            this.instanceBuffer.SetData(dataList);

            this.lodMeshes = lod;
            this.lodIndexesBuffers = new GraphicsBuffer[Constants.MaxLOD];
            this.mpb = new MaterialPropertyBlock[Constants.MaxLOD];
            this.renderParams = new RenderParams[Constants.MaxLOD];

            this.drawArgs = new [] { SetupArgs(this.lodMeshes[0]), SetupArgs(this.lodMeshes[1]) };
            this.lodArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, Constants.MaxLOD, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            this.lodArgsBuffer.SetData(this.drawArgs);

            this.computeCullLOD.SetBuffer(Constants.CullLODKernelIndex, ShaderProp.InstanceDataID, this.instanceBuffer);
            this.computeCullLOD.SetFloat(ShaderProp.LodThreshold, lodThreshold);
            this.computeCullLOD.SetInt(ShaderProp.InstanceCount, maxCount);

            var indexesTarget = GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Counter;
            for (var i = 0; i < Constants.MaxLOD; i++)
            {
                this.lodIndexesBuffers[i] = new GraphicsBuffer(indexesTarget, maxCount, Constants.IntByteSize);
                this.computeCullLOD.SetBuffer(Constants.CullLODKernelIndex, ShaderProp.LodIndexId[i], this.lodIndexesBuffers[i]);
                this.mpb[i] = new MaterialPropertyBlock();
                this.mpb[i].SetBuffer(ShaderProp.InstanceDataID, this.instanceBuffer);
                this.mpb[i].SetBuffer(ShaderProp.InstanceIndexes, this.lodIndexesBuffers[i]);
                this.renderParams[i] = new RenderParams()
                {
                    layer = 0,
                    renderingLayerMask = 1, //RenderingLayerMask.defaultRenderingLayerMask, // for Unity6
                    rendererPriority = 0,
                    worldBounds = new Bounds(Vector3.zero, 300 * Vector3.one), // NOTE: need to adjust this param 
                    camera = null,
                    motionVectorMode = MotionVectorGenerationMode.Camera,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    material = this.material,
                    matProps = this.mpb[i],
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    lightProbeUsage = LightProbeUsage.Off,
                    lightProbeProxyVolume = null,
                };
            }

            // for check
            this.mpb[0].SetColor(ShaderProp.Color, Color.green);
            this.mpb[1].SetColor(ShaderProp.Color, Color.yellow);

            this.frustumPlaneBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Constants.FrustumPlanes, Constants.Float4ByteSize);
            this.computeCullLOD.SetBuffer(Constants.CullLODKernelIndex, ShaderProp.FrustumPlanesID, this.frustumPlaneBuffer);
        }

        public void Dispose()
        {
	        this.frustumPlaneBuffer?.Dispose();
            this.instanceBuffer?.Release();
            this.lodArgsBuffer?.Release();
            for (var i = 0; i < Constants.MaxLOD; i++)
                this.lodIndexesBuffers[i]?.Release();

            this.lodIndexesBuffers = null;
            this.frustumPlaneBuffer = this.instanceBuffer = this.lodArgsBuffer = null;
            this.computeCullLOD = null;
            this.lodMeshes = null;
            this.material = null;
            this.mpb = null;
        }

        public void Execute(Camera cam, Vector3 cameraPosition, bool useSOA)
        {
			UpdatePlanes(cam, this.planes, useSOA);
			this.frustumPlaneBuffer.SetData(this.planes);

			for (var i = 0; i < Constants.MaxLOD; i++)
			    this.lodIndexesBuffers[i].SetCounterValue(0);
			this.computeCullLOD.SetFloat(ShaderProp.TanFOV, Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
			this.computeCullLOD.SetVector(ShaderProp.CameraPos, cameraPosition);
			this.computeCullLOD.Dispatch(Constants.CullLODKernelIndex, this.threadGroupX, 1, 1);

			// NOTE: if need
#if !UNITY_EDITOR
			this.renderParams[0].camera = this.renderParams[1].camera = cam;
#endif
			
            var argOffset = Constants.IntByteSize; // instanceCountは4byte先
            var argSize = GraphicsBuffer.IndirectDrawIndexedArgs.size;
			for (var i = 0; i < Constants.MaxLOD; i++, argOffset += argSize)
			{
				GraphicsBuffer.CopyCount(this.lodIndexesBuffers[i], this.lodArgsBuffer, argOffset);
				Graphics.RenderMeshIndirect(this.renderParams[i], this.lodMeshes[i], this.lodArgsBuffer, 1, i);
			}
        }

        static void UpdatePlanes(Camera camera, float4[] planes, bool useSOA)
        {
	        // NOTE: the same value
	        // var proj = this.cullCamera.projectionMatrix;
	        // var view = this.cullCamera.worldToCameraMatrix;
	        float4x4 vp = camera.cullingMatrix;
	        // var proj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false); // if need
	        // var view = camera.worldToCameraMatrix;
	        // float4x4 vp = proj * view;
	        var c0 = vp.c0;
	        var c1 = vp.c1;
	        var c2 = vp.c2;
	        var c3 = vp.c3;
	        var row0 = new float4(c0.x, c1.x, c2.x, c3.x);
	        var row1 = new float4(c0.y, c1.y, c2.y, c3.y);
	        //var row2 = new float4(c0.z, c1.z, c2.z, c3.z);
	        var row3 = new float4(c0.w, c1.w, c2.w, c3.w);

	        // SOA
	        var p0 = NormalizePlane(row3 + row0); // L
	        var p1 = NormalizePlane(row3 - row0); // R
	        var p2 = NormalizePlane(row3 + row1); // B
	        var p3 = NormalizePlane(row3 - row1); // T
	        if (useSOA)
	        {
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
}
