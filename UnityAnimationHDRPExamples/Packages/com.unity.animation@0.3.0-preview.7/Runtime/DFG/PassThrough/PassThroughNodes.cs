using Unity.DataFlowGraph;
using Unity.DataFlowGraph.Attributes;
using Unity.Profiling;
using Unity.Burst;

namespace Unity.Animation
{
    [NodeDefinition(isHidden:true)]
    public class SimPassThroughNode<T>
        : NodeDefinition<SimPassThroughNode<T>.Data, SimPassThroughNode<T>.SimPorts>
        , IMsgHandler<T>
        where T : struct
    {
        public struct SimPorts : ISimulationPortDefinition
        {
            public MessageInput<SimPassThroughNode<T>, T> Input;
            public MessageOutput<SimPassThroughNode<T>, T> Output;
        }

        public struct Data : INodeData { }

        public void HandleMessage(in MessageContext ctx, in T msg) =>
            ctx.EmitMessage(SimulationPorts.Output, msg);
    }

    [NodeDefinition(isHidden:true)]
    public abstract class KernelPassThroughNode<TFinalNodeDefinition, T, TKernel>
        : NodeDefinition<KernelPassThroughNode<TFinalNodeDefinition, T, TKernel>.Data, KernelPassThroughNode<TFinalNodeDefinition, T, TKernel>.SimPorts, KernelPassThroughNode<TFinalNodeDefinition, T, TKernel>.KernelData, KernelPassThroughNode<TFinalNodeDefinition, T, TKernel>.KernelDefs, TKernel>
        where TFinalNodeDefinition : NodeDefinition
        where T : struct
        where TKernel : struct, IGraphKernel<KernelPassThroughNode<TFinalNodeDefinition, T, TKernel>.KernelData, KernelPassThroughNode<TFinalNodeDefinition, T, TKernel>.KernelDefs>
    {
        public struct SimPorts : ISimulationPortDefinition { }

        public struct KernelDefs : IKernelPortDefinition
        {
            public DataInput<TFinalNodeDefinition, T> Input;
            public DataOutput<TFinalNodeDefinition, T> Output;
        }

        public struct Data : INodeData { }

        public struct KernelData : IKernelData
        {
            public ProfilerMarker ProfilePassThrough;
        }
    }

    // Until this is fixed in Burst, we must avoid IGraphKernel implementations which include any generics.
    [NodeDefinition(isHidden:true)]
    public class KernelPassThroughNodeFloat : KernelPassThroughNode<KernelPassThroughNodeFloat, float, KernelPassThroughNodeFloat.Kernel>
    {
        static readonly ProfilerMarker k_ProfileKernelPassThrough = new ProfilerMarker("Animation.KernelPassThrough");

        [BurstCompile/*(FloatMode = FloatMode.Fast)*/]
        public struct Kernel : IGraphKernel<KernelData, KernelDefs>
        {
            public void Execute(RenderContext context, KernelData data, ref KernelDefs ports)
            {
                data.ProfilePassThrough.Begin();
                context.Resolve(ref ports.Output) = context.Resolve(ports.Input);
                data.ProfilePassThrough.End();
            }
        }

        protected override void Init(InitContext ctx)
        {
            ref var kData = ref GetKernelData(ctx.Handle);
            kData.ProfilePassThrough = k_ProfileKernelPassThrough;
        }
    }

    [NodeDefinition(isHidden:true)]
    public abstract class KernelPassThroughNodeBuffer<TFinalNodeDefinition, T, TKernel>
        : NodeDefinition<KernelPassThroughNodeBuffer<TFinalNodeDefinition, T, TKernel>.Data, KernelPassThroughNodeBuffer<TFinalNodeDefinition, T, TKernel>.SimPorts, KernelPassThroughNodeBuffer<TFinalNodeDefinition, T, TKernel>.KernelData, KernelPassThroughNodeBuffer<TFinalNodeDefinition, T, TKernel>.KernelDefs, TKernel>
        , IMsgHandler<int>
        where TFinalNodeDefinition : NodeDefinition, IMsgHandler<int>
        where T : struct
        where TKernel : struct, IGraphKernel<KernelPassThroughNodeBuffer<TFinalNodeDefinition, T, TKernel>.KernelData, KernelPassThroughNodeBuffer<TFinalNodeDefinition, T, TKernel>.KernelDefs>
    {
        public struct SimPorts : ISimulationPortDefinition
        {
            public MessageInput<TFinalNodeDefinition, int> BufferSize;
        }

        public struct KernelDefs : IKernelPortDefinition
        {
            public DataInput<TFinalNodeDefinition, Buffer<T>> Input;
            public DataOutput<TFinalNodeDefinition, Buffer<T>> Output;
        }

        public struct Data : INodeData
        {
        }

        public struct KernelData : IKernelData
        {
            public ProfilerMarker ProfilePassThrough;
        }

        public void HandleMessage(in MessageContext ctx, in  int msg)
        {
            Set.SetBufferSize(ctx.Handle, (OutputPortID)KernelPorts.Output, Buffer<T>.SizeRequest(msg));
        }
    }


    // Until this is fixed in Burst, we must avoid IGraphKernel implementations which include any generics.
    [NodeDefinition(isHidden:true)]
    public class KernelPassThroughNodeBufferFloat : KernelPassThroughNodeBuffer<KernelPassThroughNodeBufferFloat, AnimatedData, KernelPassThroughNodeBufferFloat.Kernel>
    {
        static readonly ProfilerMarker k_ProfileKernelPassThroughBuffer = new ProfilerMarker("Animation.KernelPassThroughBuffer");

        [BurstCompile/*(FloatMode = FloatMode.Fast)*/]
        public struct Kernel : IGraphKernel<KernelData, KernelDefs>
        {
            public void Execute(RenderContext context, KernelData data, ref KernelDefs ports)
            {
                data.ProfilePassThrough.Begin();
                context.Resolve(ref ports.Output).CopyFrom(context.Resolve(ports.Input));
                data.ProfilePassThrough.End();
            }
        }

        protected override void Init(InitContext ctx)
        {
            ref var kData = ref GetKernelData(ctx.Handle);
            kData.ProfilePassThrough = k_ProfileKernelPassThroughBuffer;
        }
    }
}
