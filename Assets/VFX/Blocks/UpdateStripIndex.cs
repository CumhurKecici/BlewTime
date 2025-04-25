using System;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEngine.VFX
{
    class UpdateStripIndex : VFXSpawnerCallbacks
    {
        public class InputProperties
        {
            [Tooltip("Maximum Strip Count (Used to cycle indices)")]
            public uint StripMaxCount = 8;
            [Tooltip("How particle can be attached to current index (Used to determine when to increase strip index)")]
            public uint ParticlePerStripCount = 32;
        }

        static private readonly int stripMaxCountID = Shader.PropertyToID("StripMaxCount");
        static private readonly int particlePerStripCountID = Shader.PropertyToID("ParticlePerStripCount");
        static private readonly int stripIndexID = Shader.PropertyToID("stripIndex");

        uint m_Index = 0;
        uint m_spawnCount = 0;

        public override void OnPlay(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent)
        {
        }

        public override void OnStop(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent)
        {
            m_Index = 0;
            m_spawnCount = 0;
        }

        public override void OnUpdate(VFXSpawnerState state, VFXExpressionValues vfxValues, VisualEffect vfxComponent)
        {
            m_spawnCount += (uint)state.spawnCount;
            m_Index = (Math.Max(1, m_spawnCount) / vfxValues.GetUInt(particlePerStripCountID)) % Math.Max(1, vfxValues.GetUInt(stripMaxCountID));
            state.vfxEventAttribute.SetUint(stripIndexID, m_Index);
        }
    }
}

