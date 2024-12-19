using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

namespace InstancingFeature
{
    public class InstancingDrawFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public struct Settings
        {
            public RenderPassEvent renderPassEvent;
        }

        [SerializeField] Settings settings;
        InstancingDrawPass pass;

        public override void Create()
        {
            if (pass != null)
            {
                //pass.Dispose();
                //Debug.LogWarning("InstancingDrawPass already created");
                return;
            }

            pass = new InstancingDrawPass(this.settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null)
            {
                Debug.LogError("Missing Pass........");
                return;
            }

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this.pass?.Dispose();
            this.pass = null;
        }
    }

    public class InstancingDrawPass : ScriptableRenderPass
    {
        InstancingDrawFeature.Settings settings;
        Plane[] frustumPlanes;
        float4[] planes;

        GraphicsBuffer frustumPlaneBuffer;
        public static List<IChunkExecute> chunkStack;

        public InstancingDrawPass(InstancingDrawFeature.Settings settings)
        {
            chunkStack = new List<IChunkExecute>();
            this.planes = new float4[Constants.FrustumPlaneCount];

            this.frustumPlanes = new Plane[Constants.FrustumPlaneCount];
            this.frustumPlaneBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Constants.FrustumPlaneCount, Constants.Float4ByteSize);

            this.renderPassEvent = settings.renderPassEvent;
            this.profilingSampler = new ProfilingSampler(nameof(InstancingDrawPass));
        }

        public void Dispose()
        {
            chunkStack.Clear();
            chunkStack = null;
            this.frustumPlaneBuffer?.Dispose();
        }

        // public override void FrameCleanup(CommandBuffer cmd)
        // {
        //     chunkStack.Clear();
        // }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = Camera.main;//renderingData.cameraData.camera;
            if (camera == null)
                return;

            var cmd = CommandBufferPool.Get(nameof(InstancingDrawPass));
            cmd.Clear();
            using (new ProfilingScope(cmd, this.profilingSampler))
            {
                // FrustumPlaneの生成
                // var camera = renderingData.cameraData.camera;
                var cameraPosition = camera.transform.position;
                
                var mat = camera.projectionMatrix * camera.worldToCameraMatrix;
                //var mat = camera.cullingMatrix;
                GeometryUtility.CalculateFrustumPlanes(mat, this.frustumPlanes);
                for (var i = 0; i < Constants.FrustumPlaneCount; ++i)
                    this.planes[i] = new float4(frustumPlanes[i].normal, frustumPlanes[i].distance);
                // TODO: 合ってるか確認
                //this.UpdatePlanes(camera, planes);
                
                cmd.SetBufferData(this.frustumPlaneBuffer, this.planes);
                //this.frustumPlaneBuffer.SetData(this.planes);
                
                // 積まれた描画リクエストの実行
                foreach (var chunk in chunkStack)
                {
                    //chunk.ExecuteWithCommandBuffer(cmd, cameraPosition, this.frustumPlaneBuffer);
                    break;
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void UpdatePlanes(Camera camera, float4[] planes)
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

            // TODO: for SOA
            // this.planeShuffle.nx = new float4(p0.x, p1.x, p2.x, p3.x);
            // this.planeShuffle.ny = new float4(p0.y, p1.y, p2.y, p3.y);
            // this.planeShuffle.nz = new float4(p0.z, p1.z, p2.z, p3.z);
            // this.planeShuffle.d = new float4(p0.w, p1.w, p2.w, p3.w);

            planes[0] = p0;
            planes[1] = p1;
            planes[2] = p2;
            planes[3] = p3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float4 NormalizePlane(float4 plane)
        {
            return plane / math.length(plane.xyz);
        }
    }
}