using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.DataFlowGraph;
using Unity.DataFlowGraph.Attributes;
using Unity.Profiling;

namespace Unity.Animation
{
    [NodeDefinition(category:"Animation Core/Root Motion", description:"Computes and sets the total root motion offset amount based on the number of cycles for a given clip. This node is internally used by the UberClipNode.")]
    public class CycleRootMotionNode
        : NodeDefinition<CycleRootMotionNode.Data, CycleRootMotionNode.SimPorts, CycleRootMotionNode.KernelData, CycleRootMotionNode.KernelDefs, CycleRootMotionNode.Kernel>
        , IMsgHandler<Rig>
        , IRigContextHandler
    {
        static readonly ProfilerMarker k_ProfileMarker = new ProfilerMarker("Animation.CycleRootMotionNode");

        public struct SimPorts : ISimulationPortDefinition
        {
            [PortDefinition(isHidden:true)]
            public MessageInput<CycleRootMotionNode, Rig> Rig;
        }

        public struct KernelDefs : IKernelPortDefinition
        {
            [PortDefinition(description:"Clip cycle count")]
            public DataInput<CycleRootMotionNode, int> Cycle;
            [PortDefinition(description:"Animation stream at the start of the clip, when t = 0")]
            public DataInput<CycleRootMotionNode, Buffer<AnimatedData>> Start;
            [PortDefinition(description:"Animation stream at the end of the clip, when t = duration")]
            public DataInput<CycleRootMotionNode, Buffer<AnimatedData>> Stop;
            [PortDefinition(description:"The current animation stream")]
            public DataInput<CycleRootMotionNode, Buffer<AnimatedData>> Input;

            [PortDefinition(description:"Resulting animation stream with updated root motion values")]
            public DataOutput<CycleRootMotionNode, Buffer<AnimatedData>> Output;
        }

        public struct Data : INodeData
        {
        }

        public struct KernelData : IKernelData
        {
            public BlobAssetReference<RigDefinition> RigDefinition;

            public ProfilerMarker ProfileMarker;
        }

        [BurstCompile/*(FloatMode = FloatMode.Fast)*/]
        public struct Kernel : IGraphKernel<KernelData, KernelDefs>
        {
            public void Execute(RenderContext context, KernelData data, ref KernelDefs ports)
            {
                if (data.RigDefinition == default)
                    return;

                data.ProfileMarker.Begin();

                // Fill the destination stream with default values.
                var startStream = AnimationStream.CreateReadOnly(data.RigDefinition,context.Resolve(ports.Start));
                var stopStream = AnimationStream.CreateReadOnly(data.RigDefinition,context.Resolve(ports.Stop));
                var inputStream = AnimationStream.CreateReadOnly(data.RigDefinition,context.Resolve(ports.Input));
                var outputStream = AnimationStream.Create(data.RigDefinition,context.Resolve(ref ports.Output));

                AnimationStreamUtils.MemCpy(ref outputStream, ref inputStream);

                var startX = new RigidTransform(startStream.GetLocalToParentRotation(0), startStream.GetLocalToParentTranslation(0));
                var stopX = new RigidTransform(stopStream.GetLocalToParentRotation(0), stopStream.GetLocalToParentTranslation(0));
                var x = new RigidTransform(outputStream.GetLocalToParentRotation(0), outputStream.GetLocalToParentTranslation(0));
                var cycleX = GetCycleX(x, startX, stopX, context.Resolve(ports.Cycle));

                outputStream.SetLocalToParentRotation(0, cycleX.rot);
                outputStream.SetLocalToParentTranslation(0, cycleX.pos);

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

        public static RigidTransform GetCycleX(RigidTransform x, RigidTransform startX, RigidTransform stopX, int cycle)
        {
            if (cycle == 0)
            {
                return x;
            }
            else
            {
                bool swapStartStop = cycle < 0;

                RigidTransform swapStartX = mathex.select(startX, stopX, swapStartStop);
                RigidTransform swapStopX = mathex.select(stopX, startX, swapStartStop);
                RigidTransform cycleX = mathex.rigidPow(math.mul(swapStopX, math.inverse(swapStartX)), math.asuint(math.abs(cycle)));

                return math.mul(cycleX, x);
            }
        }

        InputPortID ITaskPort<IRigContextHandler>.GetPort(NodeHandle handle) =>
            (InputPortID)SimulationPorts.Rig;
    }
}
