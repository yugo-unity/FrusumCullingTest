using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Collections.Generic;

namespace UTJ
{
    public interface IReferenceforCamera
    {
        void SetCamera(Camera camera);
    }

    [RequireComponent(typeof(Camera))]
    public class SubCameraStack : MonoBehaviour
    {
        public enum FEATURE_TYPE
        {
            SAME_CULLRESULTS,
            NO_CULLING,
        }

        public FEATURE_TYPE featureType = FEATURE_TYPE.SAME_CULLRESULTS;
        Camera subCamera;
        IReferenceforCamera subCameraFeature;

        void Initialize()
        {
            if (this.subCamera != null)
                return;
            this.subCamera = this.GetComponent<Camera>();
            var cameraData = this.GetComponent<UniversalAdditionalCameraData>();
            this.subCameraFeature = this.GetFeature(cameraData.scriptableRenderer);
        }

        IReferenceforCamera GetFeature(ScriptableRenderer scriptableRenderer)
        {
            var field = typeof(ScriptableRenderer).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return null;

            IReferenceforCamera feature = null;
            if (field.GetValue(scriptableRenderer) is List<ScriptableRendererFeature> features)
            {
                foreach (var f in features)
                {
                    if (f is IReferenceforCamera refCameraFeature)
                    {
                        switch (this.featureType)
                        {
                            case FEATURE_TYPE.SAME_CULLRESULTS when f is SubCameraFeature:
                            case FEATURE_TYPE.NO_CULLING when f is SubCameraNoCullingFeature:
                                feature = refCameraFeature;
                                f.SetActive(true);
                                break;
                            default:
                                f.SetActive(false);
                                break;
                        }
                    }
                }
            }
            return feature;
        }

        void OnEnable()
        {
            this.Initialize(); 
            this.subCameraFeature?.SetCamera(this.subCamera);
            this.subCamera.enabled = false;
        }
        
        void OnDestroy()
        {
            this.subCameraFeature?.SetCamera(null);
            this.subCamera.enabled = true;
        }
    }
}