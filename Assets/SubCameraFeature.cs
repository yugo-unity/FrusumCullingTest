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
	public class SubCameraFeature : ScriptableRendererFeature, IReferenceforCamera
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
			// Editor will crash if we don't release reference to Game Scripts
			this.subCamera = null;
		}

		public override void Create()
		{
			this.opaquePass ??= new SubCameraPass(clearTarget: true)
			{
				renderQueue = RenderQueueRange.opaque,
				criteria = SortingCriteria.CommonOpaque,
				layerMask = this.opaqueLayerMask,
			};
			this.transparentPass ??= new SubCameraPass(clearTarget: false)
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
				// if (this.subCamera == null)
				return;
			
			this.opaquePass.camera = this.transparentPass.camera = this.subCamera;
			this.opaquePass.layerMask = this.opaqueLayerMask;
			this.transparentPass.layerMask = this.transparentLayerMask;
			
			renderer.EnqueuePass(this.opaquePass);
			renderer.EnqueuePass(this.transparentPass);
		}
		
		class PassData
		{
			public bool clearTarget;
			public Matrix4x4 viewMatrix;
			public Matrix4x4 projMatrix;
			public RendererListHandle rendererListHandle;
		}

		internal class SubCameraPass : ScriptableRenderPass
		{
			static readonly List<ShaderTagId> SHADER_TAG_IDs = new List<ShaderTagId>
			{
				new ShaderTagId("SRPDefaultUnlit"),
				new ShaderTagId("UniversalForward"),
			};

			internal bool clearTarget;
			internal Camera camera;
			internal RenderQueueRange renderQueue;
			internal LayerMask layerMask;
			internal SortingCriteria criteria;

			public SubCameraPass(bool clearTarget)
			{
				this.renderPassEvent = RenderPassEvent.AfterRendering;
				this.clearTarget = clearTarget;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				var renderingData = frameData.Get<UniversalRenderingData>();
				var cameraData = frameData.Get<UniversalCameraData>();
				var lightData = frameData.Get<UniversalLightData>();
				var resourceData = frameData.Get<UniversalResourceData>();

				using (var builder = renderGraph.AddRasterRenderPass<PassData>(this.passName, out var passData))
				{
					// override camera instance
					//var drawSettings = RenderingUtils.CreateDrawingSettings(SHADER_TAG_IDs, renderingData, cameraData, lightData, this.criteria);
					var sortingSettings = new SortingSettings(this.camera) { criteria = this.criteria };
					//var sortingSettings = CreateSortingSettings(camera, this.criteria); // 遅いので却下
					var drawSettings = new DrawingSettings(SHADER_TAG_IDs[0], sortingSettings)
					{
						perObjectData = renderingData.perObjectData,
						mainLightIndex = lightData.mainLightIndex,
						enableDynamicBatching = renderingData.supportsDynamicBatching,
						// Disable instancing for preview cameras. This is consistent with the built-in forward renderer. Also fixes case 1127324.
						enableInstancing = cameraData.cameraType != CameraType.Preview,
					};
					for (var i = 1; i < SHADER_TAG_IDs.Count; ++i)
						drawSettings.SetShaderPassName(i, SHADER_TAG_IDs[i]);

					var filteringSettings = new FilteringSettings(this.renderQueue, this.layerMask);
					var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
					passData.rendererListHandle = renderGraph.CreateRendererList(param);
					passData.clearTarget = this.clearTarget;
					passData.viewMatrix = this.camera.worldToCameraMatrix;
					passData.projMatrix = this.camera.projectionMatrix;

					builder.UseRendererList(passData.rendererListHandle);
					builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
					builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

					builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
					{
						if (data.clearTarget)
							context.cmd.ClearRenderTarget(RTClearFlags.All, Color.black, 1f, 0);
						context.cmd.SetViewProjectionMatrices(data.viewMatrix, data.projMatrix);
						context.cmd.DrawRendererList(data.rendererListHandle);
					});
				}
			}

			// SortingSettings CreateSortingSettings(Camera cam, SortingCriteria critia)
			// {
			// 	var settings = new SortingSettings()
			// 	{
			// 		criteria = critia,
			// 		worldToCameraMatrix = cam.worldToCameraMatrix,
			// 		cameraPosition = cam.transform.position,
			// 		customAxis = cam.transparencySortAxis,
			// 	};
			// 	switch (cam.transparencySortMode)
			// 	{
			// 		case TransparencySortMode.Perspective:
			// 			settings.distanceMetric = DistanceMetric.Perspective;
			// 			break;
			// 		case TransparencySortMode.Orthographic:
			// 			settings.distanceMetric = DistanceMetric.Orthographic;
			// 			break;
			// 		case TransparencySortMode.CustomAxis:
			// 			settings.distanceMetric = DistanceMetric.CustomAxis;
			// 			break;
			// 		//case TransparencySortMode.Default:
			// 		default:
			// 			settings.distanceMetric = cam.orthographic ? DistanceMetric.Orthographic : DistanceMetric.Perspective;
			// 			break;
			// 	}
			//
			// 	return settings;
			// }
		}
	}
}

