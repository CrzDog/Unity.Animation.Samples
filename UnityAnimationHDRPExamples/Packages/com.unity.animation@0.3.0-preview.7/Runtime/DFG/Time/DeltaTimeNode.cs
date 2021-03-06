using Unity.Burst;
using Unity.DataFlowGraph;
using Unity.DataFlowGraph.Attributes;
using Unity.Profiling;
using UnityEngine;

namespace Unity.Animation
{
    [NodeDefinition(category:"Animation Core/Time", description:"Computes delta time")]
    public class DeltaTimeNode
        : NodeDefinition<DeltaTimeNode.Data, DeltaTimeNode.SimPorts, DeltaTimeNode.KernelData, DeltaTimeNode.KernelDefs, DeltaTimeNode.Kernel>
    {
        public struct SimPorts : ISimulationPortDefinition
        {
        }

        static readonly ProfilerMarker k_ProfileDeltaTime = new ProfilerMarker("Animation.DeltaTime");

        public struct KernelDefs : IKernelPortDefinition
        {
            [PortDefinition(description:"Delta time")]
            public DataOutput<DeltaTimeNode, float> DeltaTime;
        }

        public struct Data : INodeData
        {
        }

        public struct KernelData : IKernelData
        {
            // Assets.
            public ProfilerMarker ProfileDeltaTime;

            // Instance data.
            public float DeltaTime;
        }

        [BurstCompile/*(FloatMode = FloatMode.Fast)*/]
        public struct Kernel : IGraphKernel<KernelData, KernelDefs>
        {
            public void Execute(RenderContext context, KernelData data, ref KernelDefs ports)
            {
                data.ProfileDeltaTime.Begin();

                context.Resolve(ref ports.DeltaTime) = data.DeltaTime;

                data.ProfileDeltaTime.End();
            }
        }

        protected override void Init(InitContext ctx)
        {
            ref var kData = ref GetKernelData(ctx.Handle);
            kData.ProfileDeltaTime = k_ProfileDeltaTime;
        }

        protected override void OnUpdate(in UpdateContext ctx)
        {
            ref var kData = ref GetKernelData(ctx.Handle);
            kData.DeltaTime = Time.deltaTime;
        }
    }
}
