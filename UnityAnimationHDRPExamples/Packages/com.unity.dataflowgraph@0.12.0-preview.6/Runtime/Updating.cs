using System;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.DataFlowGraph
{
    /// <summary>
    /// A context provided to a node's <see cref="NodeDefinition.OnUpdate"/> implementation.
    /// </summary>
    public readonly struct UpdateContext
    {
        /// <summary>
        /// A handle to the node being updated.
        /// </summary>
        public NodeHandle Handle => m_Handle.ToPublicHandle();

        /// <summary>
        /// Emit a message from yourself on a port. Everything connected to it
        /// will receive your message.
        /// </summary>
        /// 
        public void EmitMessage<T, TNodeDefinition>(MessageOutput<TNodeDefinition, T> port, in T msg)
            where TNodeDefinition : NodeDefinition
        {
            m_Set.EmitMessage(m_Handle, port.Port, msg);
        }

        readonly ValidatedHandle m_Handle;
        readonly NodeSet m_Set;

        internal UpdateContext(NodeSet set, in ValidatedHandle handle)
        {
            m_Set = set;
            m_Handle = handle;
        }
    }

    public partial class NodeSet
    {
        /// <summary>
        /// Updates the node set in two phases:
        /// 
        /// 1. A message phase (simulation) where nodes are updated and messages
        /// are passed around
        /// 2. Aligning the simulation world and the rendering world and initiate
        /// the rendering.
        /// 
        /// <seealso cref="RenderExecutionModel"/>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Can be thrown if invalid or missing dependencies were added through 
        /// <see cref="InjectDependencyFromConsumer(JobHandle)"/>.
        /// 
        /// Can also be thrown if this <see cref="NodeSet"/> was created with the ECS constructor 
        /// <see cref="NodeSet(Entities.JobComponentSystem)"/>, in which case you need to use the 
        /// <see cref="Update(JobHandle)"/> function instead.
        /// </exception>
        public void Update()
        {
            if (HostSystem != null)
                throw new InvalidOperationException($"This {typeof(NodeSet)} was created together with a job component system, you must use the update function with an input {nameof(JobHandle)} argument");

            UpdateInternal(inputDependencies: default);
        }

        void UpdateInternal(JobHandle inputDependencies)
        {
            m_FenceOutputConsumerProfilerMarker.Begin();
            FenceOutputConsumers();
            m_FenceOutputConsumerProfilerMarker.End();

            m_SimulateProfilerMarker.Begin();

            unsafe
            {
                // FIXME: Make this topologically ordered.
                for (int i = 0; i < m_Nodes.Count; ++i)
                {
                    var node = m_Nodes.Ref(i);

                    if (node->IsCreated)
                        m_NodeDefinitions[node->TraitsIndex].OnUpdate(new UpdateContext(this, node->Self));
                }
            }

            m_SimulateProfilerMarker.End();

            m_CopyWorldsProfilerMarker.Begin();
            m_RenderGraph.CopyWorlds(m_Diff, inputDependencies, RendererModel, m_GraphValues, m_Batches);
            PostRenderBatchProcess();
            m_Diff = new GraphDiff(Allocator.Persistent); // TODO: Could be temp?
            m_CopyWorldsProfilerMarker.End();

            
            m_SwapGraphValuesProfilerMarker.Begin();
            SwapGraphValues();
            m_SwapGraphValuesProfilerMarker.End();
        }
    }
}
