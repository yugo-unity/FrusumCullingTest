using UnityEngine;
using Unity.Jobs.LowLevel.Unsafe;

public class JobWorkerThreadCount : MonoBehaviour
{
    public int workerThreadCount = -1;
    
    void OnEnable()
    {
        if (this.workerThreadCount >= 0)
            JobsUtility.JobWorkerCount = this.workerThreadCount;
    }

    // Update is called once per frame
    void OnDisable()
    {
        JobsUtility.ResetJobWorkerCount();
    }

    double time = 0d;
    double start, end;
    void Update()
    {
        // parallel test
        System.Threading.Thread.Sleep(10);
        
        end = Time.realtimeSinceStartupAsDouble;
        time += end - start;
        if (Time.frameCount % 1000 == 0)
        {
            Debug.Log($"TIME----- {time * 0.001}");
            time = 0d;
        }
        start = Time.realtimeSinceStartupAsDouble;
        
        //this.transform.Rotate(Vector3.up, Mathf.PI * Time.deltaTime);
    }
}
