using Unity.Burst;
using Unity.Entities;
using Unity.DataFlowGraph;
using Unity.DataFlowGraph.Attributes;
using Unity.Profiling;

namespace Unity.Animation
{
    [NodeDefinition(category:"Animation Core/Mixers", description:"Blends two animation streams given per channel weight values. Weight masks can be built using the WeightBuilderNode.")]
    public class ChannelWeightMixerNode
        : NodeDefinition<ChannelWeightMixerNode.Data, ChannelWeightMixerNode.SimPorts, ChannelWeightMixerNode.KernelData, ChannelWeightMixerNode.KernelDefs, ChannelWeightMixerNode.Kernel>
        , IMsgHandler<Rig>
        , IRigContextHandler
    {
        public struct SimPorts : ISimulationPortDefinition
        {
            [PortDefinition(isHidden:true)]
            public MessageInput<ChannelWeightMixerNode, Rig> Rig;
        }

        static readonly ProfilerMarker k_ProfileMarker = new ProfilerMarker("Animation.ChannelWeightMixerNode");

        public struct KernelDefs : IKernelPortDefinition
        {
            [PortDefinition(description:"Input stream 0")]
            public DataInput<ChannelWeightMixerNode, Buffer<AnimatedData>> Input0;
            [PortDefinition(description:"Input stream 1")]
            public DataInput<ChannelWeightMixerNode, Buffer<AnimatedData>> Input1;
            [PortDefinition(description:"Blend weight that applies to all channels")]
            public DataInput<ChannelWeightMixerNode, float> Weight;
            [PortDefinition(description:"Channel specific weights which are also modulated by input Weight")]
            public DataInput<ChannelWeightMixerNode, Buffer<WeightData>> WeightMasks;

            [PortDefinition(description:"Resulting stream")]
            public DataOutput<ChannelWeightMixerNode, Buffer<AnimatedData>> Output;
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
                var outputStream = AnimationStream.Create(data.RigDefinition, context.Resolve(ref ports.Output));
                if (outputStream.IsNull)
                    throw new System.InvalidOperationException($"ChannelWeightMixerNode output is invalid.");

                var inputStream0 = AnimationStream.CreateReadOnly(data.RigDefinition, context.Resolve(ports.Input0));
                var inputStream1 = AnimationStream.CreateReadOnly(data.RigDefinition, context.Resolve(ports.Input1));

                data.ProfileMarker.Begin();

                var weight = context.Resolve(ports.Weight);
                var weightMasks = context.Resolve(ports.WeightMasks);

                if (Core.WeightDataSize(outputStream.Rig) != weightMasks.Length)
                    throw new System.InvalidOperationException($"ChannelWeightMixerNode: WeightMasks size does not match RigDefinition. WeightMasks size is '{weightMasks.Length}' but RigDefinition expects a size of '{Core.WeightDataSize(inputStream0.Rig)}'.");

                if (inputStream0.IsNull && inputStream1.IsNull)
                    AnimationStreamUtils.SetDefaultValues(ref outputStream);
                else if (inputStream0.IsNull && !inputStream1.IsNull)
                {
                    AnimationStreamUtils.SetDefaultValues(ref outputStream);
                    Core.Blend(ref outputStream, ref outputStream, ref inputStream1, weight, weightMasks);
                }
                else if (!inputStream0.IsNull && inputStream1.IsNull)
                {
                    AnimationStreamUtils.SetDefaultValues(ref outputStream);
                    Core.Blend(ref outputStream, ref inputStream0, ref outputStream, weight, weightMasks);
                }
                else
                    Core.Blend(ref outputStream, ref inputStream0, ref inputStream1, weight, weightMasks);

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

        InputPortID ITaskPort<IRigContextHandler>.GetPort(NodeHandle handle) =>
            (InputPortID)SimulationPorts.Rig;
    }
}
