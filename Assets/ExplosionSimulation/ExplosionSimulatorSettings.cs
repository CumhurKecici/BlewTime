using System;
using UnityEngine;

[Serializable]
public class ExplosionSimulatorSettings
{
    public ESDirections Directions;
    public ESQuality Quality;
    [Range(0.0f, 8.0f)]
    public int Size;

    public ExplosionSimulatorSettings()
    {
        //Default Values
        Directions = ESDirections.Default;
        Quality = ESQuality.High;
        Size = 8;
    }

    public enum ESDirections
    {
        Minimal = 4,
        Less = 8,
        Default = 16
    }

    public enum ESQuality
    {
        Minimal = 1,
        Low = 2,
        Medium = 3,
        High = 4
    }
}
