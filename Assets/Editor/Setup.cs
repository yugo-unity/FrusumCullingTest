using UnityEngine;
using UnityEditor;

public static class Setup
{
    const float RANGE_H = 50f;
    const float RANGE_V = 50f;
    const int NUM = 1024 * 10;
    
    [MenuItem("Custom/Cube with BoundingSphere")]
    public static void SetupCUbes()
    {
        var prefab = Resources.Load<GameObject>("Cube");
        var randArea = new Vector2(RANGE_H, RANGE_V);
        var randExtent = Vector3.one;
        CreateRandomize(NUM, randArea, randExtent, prefab, false);
    }
    [MenuItem("Custom/Sphere with BoundingBox")]
    public static void SetupSpheres()
    {
        var prefab = Resources.Load<GameObject>("Sphere");
        var randArea = new Vector2(RANGE_H, RANGE_V);
        var randExtent = Vector3.one;
        CreateRandomize(NUM, randArea, randExtent, prefab, true);
    }

    static void CreateRandomize(int num, Vector2 randArea, Vector3 randExtent, GameObject prefab, bool aabb)
    {
        var root = GameObject.Find("Root");
        if (root != null)
            Object.DestroyImmediate(root);

        root = new GameObject("Root");
        for (var i = 0; i < num; i++)
        {
            var pos = RandomCenter(randArea);
            var e0 = Vector3.one + randExtent * UnityEngine.Random.value;
            var go = Object.Instantiate(prefab, pos, Quaternion.identity, root.transform);
            go.transform.localScale = e0;
            var cullingArea = go.AddComponent<CullingArea>();
            if (aabb)
                cullingArea.SetBoundingBox(e0);
            else
                cullingArea.SetBoundingSphere((e0 * 0.5f).magnitude);
        }
        // var cullingArea = root.AddComponent<CullingArea>();
        // if (aabb)
        // {
        //     var box = new Vector3(randArea.x, randArea.y, randArea.x) * 2f;
        //     box += Vector3.one + randExtent;
        //     cullingArea.SetBoundingBox(box);
        // }
        // else
        //     cullingArea.SetBoundingSphere((randArea * 0.5f).magnitude);
    }

    static Vector3 RandomCenter(Vector2 randArea)
    {
        var center = Vector3.zero;
        center.x = UnityEngine.Random.value * randArea.x * 2.0f - randArea.x;
        center.z = UnityEngine.Random.value * randArea.x * 2.0f - randArea.x;
        center.y = UnityEngine.Random.value * randArea.y * 2.0f - randArea.y;
        return center;
    }
}
