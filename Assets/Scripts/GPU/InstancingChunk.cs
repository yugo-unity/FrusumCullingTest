using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace InstancingFeature
{
    static class ShaderProp
    {
        public static readonly int CameraPos = Shader.PropertyToID("_CameraPos");
        public static readonly int LodThreshold = Shader.PropertyToID("_LODThreshold");
        public static readonly int Color = Shader.PropertyToID("_Color");
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
        public static readonly int FrustumPlaneCount = 6;
        public static readonly int Float4ByteSize = Marshal.SizeOf<float4>();
        public static readonly int IntByteSize = Marshal.SizeOf<int>();
        public static readonly int CullLODKernelIndex = 0;
    }

    // NOTE: Instance毎にカラーを変えたい場合はBaseColorにCustomFunctionで差し込む必要がある
    [StructLayout(LayoutKind.Sequential)]
    public struct InstancingStatic
    {
        public float4x4 worldMatrix;
        public float4x4 worldMatrixInverse;
        public float3 boundPoint;
        public float3 boundSize;
    }
    
    public interface IChunkExecute
    {
        void Execute(Camera camera, Vector3 cameraPosition, GraphicsBuffer planeBuffer);
    }
    
    public class InstancingChunk<T> : IChunkExecute where T : struct
    {
        static readonly int InstanceSize = Marshal.SizeOf<T>();

        GraphicsBuffer instanceBuffer, lodArgsBuffer;
        GraphicsBuffer[] lodIndeciesBuffers;
        Mesh[] lodMeshes;
        MaterialPropertyBlock[] mpb;
        RenderParams[] renderParams;
        Material material;
        int threadGroupX;
        ComputeShader computeCullLOD;

        GraphicsBuffer.IndirectDrawIndexedArgs SetupArgs(in Mesh mesh)
        {
            var args = new GraphicsBuffer.IndirectDrawIndexedArgs();
            args.indexCountPerInstance = mesh.GetIndexCount(0);
            args.startIndex = mesh.GetIndexStart(0);
            args.baseVertexIndex = mesh.GetBaseVertex(0);
            args.startInstance = args.instanceCount = 0;
            return args;
        }
        
        /// <summary>
        /// IndirectでのInstancingデータ
        /// </summary>
        /// <param name="compute"></param>
        /// <param name="lod"></param>
        /// <param name="mat"></param>
        /// <param name="dataList"></param>
        /// <param name="lodThreshold"></param>
        public void Initialize(ComputeShader compute, Mesh[] lod, Material mat, List<T> dataList, float lodThreshold)
        {
            Debug.Assert(compute != null, "Shader is null!");
            Debug.Assert(lod != null && lod.Length == Constants.MaxLOD, $"LOD level is supported only 2! - current level is {lod.Length}");

            var maxCount = dataList.Count;
            compute.GetKernelThreadGroupSizes(Constants.CullLODKernelIndex, out var x, out var y, out var z);
            this.threadGroupX = Mathf.CeilToInt((float)maxCount / x);

            this.computeCullLOD = compute;
            this.material = mat;
            this.instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxCount, InstanceSize);
            this.instanceBuffer.SetData(dataList);
            
            this.lodMeshes = lod;
            this.lodIndeciesBuffers = new GraphicsBuffer[Constants.MaxLOD];
            this.mpb = new MaterialPropertyBlock[Constants.MaxLOD];
            this.renderParams = new RenderParams[Constants.MaxLOD];
            
            var argsArray = new [] { SetupArgs(this.lodMeshes[0]), SetupArgs(this.lodMeshes[1]) };
            this.lodArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, Constants.MaxLOD, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            this.lodArgsBuffer.SetData(argsArray);
            
            compute.SetBuffer(Constants.CullLODKernelIndex, ShaderProp.InstanceDataID, this.instanceBuffer);
            compute.SetFloat(ShaderProp.LodThreshold, lodThreshold);
            
            var indexBufferTarget = GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Counter;
            for (var i = 0; i < Constants.MaxLOD; i++)
            {
                this.lodIndeciesBuffers[i] = new GraphicsBuffer(indexBufferTarget, maxCount, Constants.IntByteSize);
                compute.SetBuffer(Constants.CullLODKernelIndex, ShaderProp.LodIndexId[i], this.lodIndeciesBuffers[i]);
                this.mpb[i] = new MaterialPropertyBlock();
                this.mpb[i].SetBuffer(ShaderProp.InstanceDataID, this.instanceBuffer);
                this.mpb[i].SetBuffer(ShaderProp.InstanceIndexes, this.lodIndeciesBuffers[i]);
                this.renderParams[i] = new RenderParams()
                {
                    layer = 0,
                    renderingLayerMask = GraphicsSettings.defaultRenderingLayerMask,
                    rendererPriority = 0,
                    worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one),
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
            // NOTE: 個々で色を変えたい場合はInstanceDataに追加する
            this.mpb[0].SetColor(ShaderProp.Color, Color.green);
            this.mpb[1].SetColor(ShaderProp.Color, Color.yellow);
        }

        public void Dispose()
        {
            this.instanceBuffer?.Release();
            this.lodArgsBuffer?.Release();
            for (var i = 0; i < Constants.MaxLOD; i++)
            {
                this.lodIndeciesBuffers[i]?.Release();
            }

            this.mpb = null;
            this.lodIndeciesBuffers = null;
            this.lodArgsBuffer = null;
            this.computeCullLOD = null;
            this.lodMeshes = null;
            this.material = null;
        }

        // not support Lit
        // public void ExecuteWithCommandBuffer(CommandBuffer cmd, Vector3 cameraPosition, GraphicsBuffer planeBuffer)
        // {
        //     foreach (var lod in this.lodIndecies)
        //         cmd.SetBufferCounterValue(lod, 0);
        //     cmd.SetComputeVectorParam(this.computeCullLOD, ShaderProp.CAMERA_POSITION, cameraPosition);
        //     cmd.SetComputeBufferParam(this.computeCullLOD, Constants.CullLODKernelIndex, ShaderProp.FrustumPlanesID, planeBuffer);
        //     cmd.DispatchCompute(this.computeCullLOD, Constants.CullLODKernelIndex, Mathf.CeilToInt(this.maxCount / 64f), 1, 1);
        //
        //     var argOffset = Constants.IntByteSize; // instanceCountは4byte先
        //     var argSize = GraphicsBuffer.IndirectDrawIndexedArgs.size;
        //     for (var i = 0; i < Constants.MaxLOD; i++)
        //     {
        //          this.mpb.SetBuffer(ShaderProp.InstancingIndexes, this.lodIndecies[i]);
        //          cmd.CopyCounterValue(this.lodIndecies[i], this.lodArgsBuffer, (uint)argOffset);
        //          cmd.DrawMeshInstancedIndirect(this.lodMeshes[i], 0, this.material, 0, this.lodArgsBuffer, argSize * i, this.mpb);
        //          argOffset += argSize;
        //     }
        // }
        public void Execute(Camera camera, Vector3 cameraPosition, GraphicsBuffer planeBuffer)
        {
            for (var i = 0; i < Constants.MaxLOD; i++)
                this.lodIndeciesBuffers[i].SetCounterValue(0);
            this.computeCullLOD.SetVector(ShaderProp.CameraPos, cameraPosition);
            this.computeCullLOD.SetBuffer(Constants.CullLODKernelIndex, ShaderProp.FrustumPlanesID, planeBuffer);
            this.computeCullLOD.Dispatch(Constants.CullLODKernelIndex, this.threadGroupX, 1, 1);

            var argOffset = Constants.IntByteSize; // instanceCountは4byte先
            var argSize = GraphicsBuffer.IndirectDrawIndexedArgs.size;
            for (var i = 0; i < Constants.MaxLOD; i++)
            {
                GraphicsBuffer.CopyCount(this.lodIndeciesBuffers[i], this.lodArgsBuffer, argOffset);
                Graphics.RenderMeshIndirect(this.renderParams[i], this.lodMeshes[i], this.lodArgsBuffer, 1, i);
                argOffset += argSize;
            }
        }
    }
}