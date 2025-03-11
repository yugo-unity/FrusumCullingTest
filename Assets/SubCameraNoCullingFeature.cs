using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using UnityEngine.Rendering.RenderGraphModule;

namespace UTJ
{
	/// <summary>
	/// integrate sub_camera
	/// </summary>
	public class SubCameraNoCullingFeature : ScriptableRendererFeature, IReferenceforCamera
	{
		[SerializeField] LayerMask opaqueLayerMask;
		[SerializeField] LayerMask transparentLayerMask;
		
		SubCameraPass opaquePass, transparentPass;
		Camera subCamera;

		// set Camera instance from GameScripts
		// WARNING: must set to null 
		public void SetCamera(Camera camera)
		{
			this.subCamera = camera;
		}

		void OnValidate()
		{
			// Called enabled/disabled on Editor 
			this.subCamera = null;
		}

		public override void Create()
		{
			// we should use ??= to avoid new instance (Create is called many times without Dispose), I think
			this.opaquePass ??= new SubCameraPass()
			{
				renderQueue = RenderQueueRange.opaque,
				criteria = SortingCriteria.CommonOpaque,
				layerMask = this.opaqueLayerMask,
			};
			this.transparentPass ??= new SubCameraPass()
			{
				renderQueue = RenderQueueRange.transparent,
				criteria = SortingCriteria.CommonTransparent,
				layerMask = this.transparentLayerMask,
			};

			// do not clear subCamera here because "Create" is called after OnEnable/Start of GameScripts again
			// this.subCamera = null;
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (renderingData.cameraData.cameraType != CameraType.Game)
				return;
			if (this.subCamera is null) 
				return;

			this.opaquePass.camera = this.transparentPass.camera = this.subCamera;
			this.opaquePass.layerMask = this.opaqueLayerMask;;
			this.transparentPass.layerMask = this.transparentLayerMask;
			
			renderer.EnqueuePass(this.opaquePass);
			renderer.EnqueuePass(this.transparentPass);
		}
		
		class PassData
		{
			public Matrix4x4 viewMatrix;
			public Matrix4x4 projMatrix;
			public RendererListHandle rendererListHandle;
		}

		internal class SubCameraPass : ScriptableRenderPass
		{
			static readonly List<ShaderTagId> SHADER_TAG_IDs = new ()
			{
				new ShaderTagId("SRPDefaultUnlit"),
				new ShaderTagId("UniversalForward"),
			};

			class SubCullResultsData : ContextItem
			{
				public CullingResults results;
				public override void Reset()
				{
					results = default;
				}
			}

			internal Camera camera = null;
			internal LayerMask layerMask = -1;
			internal RenderQueueRange renderQueue = RenderQueueRange.all;
			internal SortingCriteria criteria = SortingCriteria.None;

			public SubCameraPass()
			{
				this.renderPassEvent = RenderPassEvent.AfterRendering;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				var renderingData = frameData.Get<UniversalRenderingData>();
				var cameraData = frameData.Get<UniversalCameraData>();
				var lightData = frameData.Get<UniversalLightData>();
				var resourceData = frameData.Get<UniversalResourceData>();
				//var context = frameData.Get<ScriptableRenderContext>();
				var cullData = frameData.Get<CullContextData>();
					
				using (var builder = renderGraph.AddRasterRenderPass<PassData>(this.passName, out var passData))
				{
					CullingResults cullResults;
					if (frameData.Contains<SubCullResultsData>())
					{
						cullResults = frameData.Get<SubCullResultsData>().results;
					}
					else
					{
						var subCullResultsData = frameData.GetOrCreate<SubCullResultsData>();
						if (!this.camera.TryGetCullingParameters(false, out var cullingParams))
							return;

						cullingParams.cullingOptions = CullingOptions.DisablePerObjectCulling |
						                               CullingOptions.ForceEvenIfCameraIsNotActive |
						                               CullingOptions.NeedsLighting;
						cullResults = cullData.Cull(ref cullingParams);
						subCullResultsData.results = cullResults;
					}

					// override camera instance
					//var drawSettings = RenderingUtils.CreateDrawingSettings(SHADER_TAG_IDs, renderingData, cameraData, lightData, this.criteria);
					var sortingSettings = new SortingSettings(this.camera) { criteria = this.criteria };
					var drawSettings = new DrawingSettings(SHADER_TAG_IDs[0], sortingSettings)
					{
						perObjectData = renderingData.perObjectData,
						mainLightIndex = lightData.mainLightIndex,
						enableDynamicBatching = renderingData.supportsDynamicBatching,
						enableInstancing = cameraData.cameraType != CameraType.Preview,
					};
					for (var i = 1; i < SHADER_TAG_IDs.Count; ++i)
						drawSettings.SetShaderPassName(i, SHADER_TAG_IDs[i]);
					
					var filteringSettings = new FilteringSettings(this.renderQueue, this.layerMask);
					var param = new RendererListParams(cullResults, drawSettings, filteringSettings);
					passData.rendererListHandle = renderGraph.CreateRendererList(param);
					if (!passData.rendererListHandle.IsValid())
						return;
					
					passData.viewMatrix = this.camera.worldToCameraMatrix;
					passData.projMatrix = this.camera.projectionMatrix;
					
					builder.UseRendererList(passData.rendererListHandle);
					builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
					builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);
					
					builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
					{
						// if (data.opaque)
						// 	context.cmd.ClearRenderTarget(RTClearFlags.All, Color.black, 1f, 0);
						context.cmd.SetViewProjectionMatrices(data.viewMatrix, data.projMatrix);
						context.cmd.DrawRendererList(data.rendererListHandle);
					});
				}
			}
		}
	}
}

