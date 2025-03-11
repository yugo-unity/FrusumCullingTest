using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using System.Diagnostics;
using System.Collections;
using Debug = System.Diagnostics.Debug;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class AllocateTest {
	//System.Action handler = null;
	int hoge = 0;
	Stopwatch sw = new Stopwatch();

	
	public class Hoge
	{
		public void InstanceMethod()
		{
			UnityEngine.Debug.Log("HOGE FUNCTION");
		}
	}
	public class Fuga : Hoge
	{
		public new void InstanceMethod()
		{
			UnityEngine.Debug.LogWarning("FUGA FUNCTION");
		}
	}  
	[MovedFrom(true, "UnityEngine.Experimental.Rendering.RenderGraphModule", "UnityEngine.Rendering.RenderGraphModule")]
	public delegate void BaseRenderFunc<PassData, ContextType>(PassData data, ContextType renderGraphContext) where PassData : class, new();
	void SetRenderFunc<PassData>(BaseRenderFunc<PassData, RasterGraphContext> renderFunc) where PassData : class, new()
	{
		//((RenderGraphPass<PassData>)m_RenderPass).renderFunc = renderFunc;
	}
	private class PassDataA
	{
		public Matrix4x4 viewMatrix;
		public Matrix4x4 projMatrix;
		public RendererListHandle rendererListHandle;
	}
	static void ExecutePass(PassDataA data, RasterGraphContext rgContext)
	{
	}
	SortingSettings CreateSortingSettings(Camera cam, SortingCriteria critia)
	{
		var settings = new SortingSettings()
		{
			criteria = critia,
			worldToCameraMatrix = cam.worldToCameraMatrix,
			cameraPosition = cam.transform.position,
			customAxis = cam.transparencySortAxis,
		};
		switch (cam.transparencySortMode)
		{
			case TransparencySortMode.Perspective:
				settings.distanceMetric = DistanceMetric.Perspective;
				break;
			case TransparencySortMode.Orthographic:
				settings.distanceMetric = DistanceMetric.Orthographic;
				break;
			case TransparencySortMode.CustomAxis:
				settings.distanceMetric = DistanceMetric.CustomAxis;
				break;
			//case TransparencySortMode.Default:
			default:
				settings.distanceMetric = cam.orthographic ? DistanceMetric.Orthographic : DistanceMetric.Perspective;
				break;
		}

		return settings;
	}

	[Test]
	public void QuickAllocateTest() {

		// // Assert.That(() => {
		// // 	this.SetRenderFunc((PassDataA data, RasterGraphContext context) => ExecutePass(data, context));
		// // }, Is.Not.AllocatingGCMemory());
		// var go = new GameObject();
		// var camera = go.AddComponent<Camera>();
		// sw.Reset();
		// sw.Start();
		// var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
		// sw.Stop();
		// 	UnityEngine.Debug.LogWarning(sw.ElapsedTicks + hoge.ToString());
		// sw.Reset();
		// sw.Start();
		// sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
		// sw.Stop();
		// 	UnityEngine.Debug.LogWarning(sw.ElapsedTicks + hoge.ToString());
		// sw.Reset();
		// sw.Start();
		// sortingSettings = CreateSortingSettings(camera, SortingCriteria.CommonOpaque);
		// sw.Stop();
		// 	UnityEngine.Debug.LogWarning(sw.ElapsedTicks + hoge.ToString());
	}
	//
	// [UnityTest]
	// public IEnumerator YieldAllocateTest() {
	//
	// 	GameObject prefab = Resources.Load<GameObject>("HogeRoot");
	// 	GameObject go = Object.Instantiate<GameObject>(prefab, null);
	// 	go.SetActive(false);
	//
	// 	yield return null;
	//
	// 	Assert.That(() => {
	// 		sw.Reset();
	// 		sw.Start();
	// 		go.SetActive(true);
	// 		sw.Stop();
	// 	}, Is.Not.AllocatingGCMemory());
	// 	UnityEngine.Debug.LogWarning(sw.ElapsedTicks + hoge.ToString());
	// }
}
