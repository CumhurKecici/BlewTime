using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class AmbientOcclusionSettings
{
    [Header("Sample Settings")]
    public AOMethod Method;
    [Range(1, 500)]
    public int Sample;
    [Range(16, 64)]
    public int JitterScale;

    [Header("Cavity Settings")]
    [Range(0.1f, 1.0f)]
    public float CavityDistance;
    [Range(1.0f, 100.0f)]
    public float CavityAttenuation;
    [Range(0.0f, 2.5f)]
    public float CavityRidge;
    [Range(0.0f, 2.5f)]
    public float CavityValley;

    [Header("Curvature Settings")]
    [Range(0.0f, 2.0f)]
    public float CurvatureRidge;
    [Range(0.0f, 2.0f)]
    public float CurvatureValley;


    public AmbientOcclusionSettings()
    {
        //Default Values
        Method = AOMethod.Blender;
        Sample = 16;
        JitterScale = 16;

        CavityDistance = 0.2f;
        CavityAttenuation = 1.0f;
        CavityRidge = 1.0f;
        CavityValley = 1.0f;

        CurvatureRidge = 1.0f;
        CurvatureValley = 1.0f;
    }

    public enum AOMethod
    {
        Blender,
        Enhanced
    }
}
