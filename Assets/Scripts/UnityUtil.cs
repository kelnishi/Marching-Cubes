using Unity.Mathematics;
using UnityEngine;
using Unity.Mathematics;

public static class VectorExtensions
{
    public static int3 ToInt3(this Vector3Int v)
    {
        return new int3(v.x, v.y, v.z);
    }
    
    public static float3 ToFloat3(this Vector3Int v)
    {
        return new float3(v.x, v.y, v.z);
    }

    public static Vector3 ToVector(this float3 f)
    {
        return new Vector3(f.x, f.y, f.z);
    }
    
    public static float2 ToFloat2(this Vector2 v)
    {
        return new float2(v.x, v.y);
    }
    
    public static Vector2 ToVector(this float2 f)
    {
        return new Vector2(f.x, f.y);
    }
}

