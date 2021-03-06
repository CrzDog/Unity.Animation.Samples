using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.DataFlowGraph;
using Unity.DataFlowGraph.Attributes;
using Unity.Profiling;

namespace Unity.Animation
{
    [NodeDefinition(category:"Animation Core/Root Motion", description:"Computes the delta root motion from a previous and current animation stream. This node is internally used by the UberClipNode.")]
    public class DeltaRootMotionNode
        : NodeDefinition<DeltaRootMotionNode.Data, DeltaRootMotionNode.SimPorts, DeltaRootMotionNode.KernelData, DeltaRootMotionNode.KernelDefs, DeltaRootMotionNode.Kernel>
        , IMsgHandler<Rig>
        , IRigContextHandler
    {
        static readonly ProfilerMarker k_ProfileMarker = new ProfilerMarker("Animation.DeltaRootMotionNode");

        public struct SimPorts : ISimulationPortDefinition
        {
            [PortDefinition(isHidden:true)]
            public MessageInput<DeltaRootMotionNode, Rig> Rig;
        }

        public struct KernelDefs : IKernelPortDefinition
        {
            [PortDefinition(description:"Previous animation stream with root motion")]
            public DataInput<DeltaRootMotionNode, Buffer<AnimatedData>> Previous;
            [PortDefinition(description:"Current animation stream with root motion")]
            public DataInput<DeltaRootMotionNode, Buffer<AnimatedData>> Current;

            [PortDefinition(description:"Resulting animation stream with updated delta root motion values")]
            public DataOutput<DeltaRootMotionNode, Buffer<AnimatedData>> Output;
        }

        public struct Data : INodeData
        {
        }

        public struct KernelData : IKernelData
        {
            public BlobAssetReference<RigDefinition> RigDefinition;

            public ProfilerMarker ProfileMarker;
        }

        [BurstCompile]
        public struct Kernel : IGraphKernel<KernelData, KernelDefs>
        {
            public void Execute(RenderContext context, KernelData data, ref KernelDefs ports)
            {
                if (data.RigDefinition == BlobAssetReference<RigDefinition>.Null)
                    return;

                data.ProfileMarker.Begin();

                // Fill the destination stream with default values.
                var prevStream = AnimationStream.CreateReadOnly(data.RigDefinition,context.Resolve(ports.Previous));
                var currentStream = AnimationStream.CreateReadOnly(data.RigDefinition,context.Resolve(ports.Current));
                var outputStream = AnimationStream.Create(data.RigDefinition,context.Resolve(ref ports.Output));

                AnimationStreamUtils.MemCpy(ref outputStream, ref currentStream);

                // current = prev * delta
                // delta = Inv(prev) * current
                var prevX = new RigidTransform(prevStream.GetLocalToParentRotation(0), prevStream.GetLocalToParentTranslation(0));
                var x = new RigidTransform(currentStream.GetLocalToParentRotation(0), currentStream.GetLocalToParentTranslation(0));
                var deltaX = math.mul(math.inverse(prevX), x);

                outputStream.SetLocalToParentTranslation( 0,deltaX.pos);
                outputStream.SetLocalToParentRotation(0, deltaX.rot);

                data.ProfileMarker.End();
            }
        }

        protected override void Init(InitContext ctx)
        {
            ref var kData = ref GetKernelData(ctx.Handle);
            kData.ProfileMarker = k_ProfileMarker;
        }

        public void HandleMessage(in MessageContext ctx, in Rig rig)
        {
            ref var kData = ref GetKernelData(ctx.Handle);

            kData.RigDefinition = rig;
            Set.SetBufferSize(
                ctx.Handle,
                (OutputPortID)KernelPorts.Output,
                Buffer<AnimatedData>.SizeRequest(rig.Value.IsCreated ? rig.Value.Value.Bindings.StreamSize : 0)
                );
        }

        internal KernelData ExposeKernelData(NodeHandle handle) => GetKernelData(handle);

        InputPortID ITaskPort<IRigContextHandler>.GetPort(NodeHandle handle) =>
            (InputPortID)SimulationPorts.Rig;
    }
}
