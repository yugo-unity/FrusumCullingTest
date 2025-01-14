using UnityEngine;
using System.Collections.Generic;

namespace InstancingFeature
{
    [RequireComponent(typeof(Camera))]
    public class GpuFrustumCuller : MonoBehaviour
    {
        [SerializeField] Mesh[] lodMeshes = new Mesh[Constants.MaxLOD];
        [SerializeField] Material material;
        [SerializeField] ComputeShader computeCulling;
        [SerializeField] ComputeShader computeCullingSOA;
        [SerializeField, Range(0f, 1f)] float lodThreshold = 0.3f;
        [SerializeField] InstancingSO instanceDataSO;
        
        public bool enabledFrustumSOA = false;

        InstancingChunk<InstancingStatic> chnk;
        Camera cam;
        Transform camTransform;
#if UNITY_EDITOR
        public bool createData = false;
        public GameObject originalRoot;

        void GenerateInstanceData()
        {
            Debug.LogWarning("CREATE INSTANCE DATA !!!!!!!!!!!!!!!");
            // NOTE: これは既存配置をランタイムで差し替える検証処理なので本実装はデータは静的に用意すること
            var obj = originalRoot.GetComponentsInChildren<MeshRenderer>(false);
            var vatList = new InstancingStatic[obj.Length];
            for (var i = 0; i < obj.Length; i++)
            {
                var mat = obj[i].transform.localToWorldMatrix;
                var renderer = obj[i].GetComponent<MeshRenderer>();
                var data = new InstancingStatic();
                data.worldMatrix = mat;
                if (this.enabledFrustumSOA)
                {
                    data.boundPoint = renderer.bounds.center;
                    data.boundSize = renderer.bounds.extents;
                }
                else
                {
                    data.boundPoint = renderer.bounds.min;
                    data.boundSize = renderer.bounds.size;
                }
                vatList[i] = data;
            }

            this.instanceDataSO.vatInstances = vatList;
            UnityEditor.EditorUtility.SetDirty(this.instanceDataSO);
        }
#endif

        void Start()
        {
#if UNITY_EDITOR
            if (this.createData)
                this.GenerateInstanceData();
#endif
            this.originalRoot.SetActive(false);
            
            this.cam = GetComponent<Camera>();
            this.camTransform = this.cam.transform;
            this.chnk = new InstancingChunk<InstancingStatic>();

            var compute = this.enabledFrustumSOA ? this.computeCullingSOA : this.computeCulling;
            this.chnk.Initialize(compute, this.lodMeshes, this.material, this.instanceDataSO.vatInstances, this.lodThreshold);
        }

        private void OnDestroy()
        {
            this.chnk?.Dispose();
        }

        void LateUpdate()
        {
            if (!this.cam.enabled)
                return;

            this.chnk.Execute(this.cam, this.camTransform.position, this.enabledFrustumSOA);
        }

    }
}
