using UnityEngine;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

[DefaultExecutionOrder(-11)] // should be early
public class CullingArea : MonoBehaviour
{
    public float3 boundsExtent = new float3(1f, 1f, 1f);
    public float4 boundsSphere = new float4(0f, 0f, 0f, 1f);

    Renderer[] renderers;
    bool visible = true;

    public void SetBoundingSphere(float radius)
    {
        this.boundsSphere = new float4(this.transform.position, radius);
    }
    public void SetBoundingBox(Vector3 extent)
    {
        this.SetBoundingSphere(0f);
        this.boundsExtent = extent * 0.5f;
    }

    void OnEnable()
    {
        if (this.renderers == null || this.renderers.Length == 0)
            this.renderers = this.GetComponentsInChildren<MeshRenderer>();
    }

    //double start, end;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetVisible(bool enabled)
    {
        if (this.visible == enabled)
            return;
        this.visible = enabled;

        //start = Time.realtimeSinceStartupAsDouble;
        foreach (var r in this.renderers)
            //r.forceRenderingOff = !active; // 最も軽いがCullingがスキップされるわけではない
            r.enabled = enabled;
        //this.gameObject.SetActive(active); // Renderer.enabledと同等だがenabled時に重い
        // end = Time.realtimeSinceStartupAsDouble;
        // if (enabled)
        //     Debug.LogWarning($"ENABLED----- {end-start}");
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (this.renderers == null || this.renderers.Length == 0)
            return;
        if (!this.renderers[0].enabled)
            return;
        
        this.SetBoundingSphere(this.boundsSphere.w);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(this.boundsSphere.xyz, this.boundsSphere.w);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(this.boundsSphere.xyz, this.boundsExtent * 2f);
    }
#endif
}
