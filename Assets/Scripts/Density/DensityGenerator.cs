using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class DensityGenerator : ScriptableObject 
{
    const int threadGroupSize = 8;
    public ComputeShader densityShader;
    
    [Space ()]
    
    [Vector3IntRange(1, 2048, 1, 2048, 1, 2048, true)]
    public Vector3Int dimensions = Vector3Int.one * 16;
    public int PointCount => dimensions.z * dimensions.y * dimensions.x;

    [Vector2MinMax(-10f, 10f)]
    public Vector2 densityRange = Vector2.up;

    public float boundsSize = 16;
    public Vector3 offset = Vector3.zero;
    


    void OnValidate() {
        if (FindObjectOfType<MeshGenerator>()) {
            FindObjectOfType<MeshGenerator>().RequestMeshUpdate();
        }
    }

    protected List<ComputeBuffer> buffersToRelease = new List<ComputeBuffer>();
    ComputeBuffer pointsBuffer;
    
    protected void CreateBuffer()
    {
        ReleaseBuffers();
        pointsBuffer = new ComputeBuffer (PointCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(float)));
        buffersToRelease.Add(pointsBuffer);
    }

    protected void ReleaseBuffers()
    {
        foreach (var buffer in buffersToRelease)
        {
            if (buffer != null)
                buffer.Release();
        }
    }
    

    public virtual float[] Generate ()
    {
        CreateBuffer();

        float3 worldBounds = new float3(1f) * boundsSize;
        float3 center = worldBounds * 0.5f;
        
        int3 numThreads = new int3(math.ceil(dimensions.ToFloat3() / (float)threadGroupSize));
        // Points buffer is populated inside shader with pos (xyz) + density (w).
        // Set paramaters
        densityShader.SetBuffer (0, "points", pointsBuffer);
        densityShader.SetInts("dimensions",dimensions.x,dimensions.y,dimensions.z);
        densityShader.SetFloat("boundsSize", boundsSize);
        densityShader.SetVector("densityRange", densityRange);
        densityShader.SetVector("center", center.ToVector());
        densityShader.SetVector("offset", offset);
        densityShader.SetVector("worldSize", worldBounds.ToVector());

        // Dispatch shader
        densityShader.Dispatch (0, numThreads.x, numThreads.y, numThreads.z);

        float[] destinationBuffer = new float[PointCount];
        pointsBuffer.GetData (destinationBuffer, 0, 0, PointCount);
        
        if (buffersToRelease != null) {
            foreach (var b in buffersToRelease) {
                b.Release();
            }
        }

        return destinationBuffer;
    }
}