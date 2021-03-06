using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Animation
{
    public struct Clip
    {
        public BlobArray<float> Samples;
        public BindingSet Bindings;
        public float Duration;
        public float SampleRate;

        internal int m_HashCode;

        public int FrameCount => (int)math.ceil(Duration * SampleRate);
        public float LastFrameError => FrameCount - Duration * SampleRate;

        public override int GetHashCode() => m_HashCode;
    }
}
