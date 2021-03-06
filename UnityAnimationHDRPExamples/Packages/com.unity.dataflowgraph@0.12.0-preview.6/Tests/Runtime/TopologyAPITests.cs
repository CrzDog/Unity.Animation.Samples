using System;
using NUnit.Framework;

namespace Unity.DataFlowGraph.Tests
{
    class TopologyTests
    {
        public struct Node : INodeData
        {
            public int Contents;
        }

        class InOutTestNode : NodeDefinition<Node, InOutTestNode.SimPorts>, IMsgHandler<Message>
        {
            public struct SimPorts : ISimulationPortDefinition
            {
#pragma warning disable 649  // Assigned through internal DataFlowGraph reflection
                public MessageInput<InOutTestNode, Message> Input;
                public MessageOutput<InOutTestNode, Message> Output;
#pragma warning restore 649
            }

            public void HandleMessage(in MessageContext ctx, in Message msg)
            {
                ref var data = ref GetNodeData(ctx.Handle);

                Assert.That(ctx.Port == SimulationPorts.Input);
                data.Contents += msg.Contents;
                ctx.EmitMessage(SimulationPorts.Output, new Message(data.Contents + 1));
            }
        }

        class TwoInOutTestNode : NodeDefinition<Node, TwoInOutTestNode.SimPorts>, IMsgHandler<Message>
        {
            public struct SimPorts : ISimulationPortDefinition
            {
#pragma warning disable 649  // Assigned through internal DataFlowGraph reflection
                public MessageInput<TwoInOutTestNode, Message> Input1, Input2;
                public MessageOutput<TwoInOutTestNode, Message> Output1, Output2;
#pragma warning restore 649
            }

            public void HandleMessage(in MessageContext ctx, in Message msg) { }
        }

        [Test]
        public void CanConnectTwoNodes_AndKeepTopologyIntegrity()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<InOutTestNode>
                    a = set.Create<InOutTestNode>(),
                    b = set.Create<InOutTestNode>();

                set.Connect(b, InOutTestNode.SimulationPorts.Output, a, InOutTestNode.SimulationPorts.Input);

                set.Destroy(a, b);
            }
        }

        [Test]
        public void CannotCreate_MultiEdgeGraph()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<InOutTestNode>
                    a = set.Create<InOutTestNode>(),
                    b = set.Create<InOutTestNode>();

                set.Connect(a, InOutTestNode.SimulationPorts.Output, b, InOutTestNode.SimulationPorts.Input);
                Assert.Throws<ArgumentException>(() => set.Connect(a, InOutTestNode.SimulationPorts.Output, b, InOutTestNode.SimulationPorts.Input));

                set.Destroy(a, b);
            }
        }

        [Test]
        public void CannotMakeTheSameConnectionTwice()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<InOutTestNode>
                    a = set.Create<InOutTestNode>(),
                    b = set.Create<InOutTestNode>();

                set.Connect(a, InOutTestNode.SimulationPorts.Output, b, InOutTestNode.SimulationPorts.Input);
                Assert.Throws<ArgumentException>(() => set.Connect(a, InOutTestNode.SimulationPorts.Output, b, InOutTestNode.SimulationPorts.Input));
                set.Disconnect(a, InOutTestNode.SimulationPorts.Output, b, InOutTestNode.SimulationPorts.Input);
                set.Connect(a, InOutTestNode.SimulationPorts.Output, b, InOutTestNode.SimulationPorts.Input);

                set.Destroy(a, b);
            }
        }

        [Test]
        public void ConnectThrows_OnDefaultConstructedHandles()
        {
            using (var set = new NodeSet())
            {
                Assert.Throws<ArgumentException>(() => set.Connect(new NodeHandle(), new OutputPortID(), new NodeHandle(), new InputPortID()));
            }
        }

        [Test]
        public void DisconnectThrows_OnDefaultConstructedHandles()
        {
            using (var set = new NodeSet())
            {
                Assert.Throws<ArgumentException>(() => set.Disconnect(new NodeHandle(), new OutputPortID(), new NodeHandle(), new InputPortID()));
            }
        }

        [Test]
        public void ConnectingOutOfPortIndicesRange_ThrowsException()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<InOutTestNode>
                    a = set.Create<InOutTestNode>(),
                    b = set.Create<InOutTestNode>();

                set.Connect(a, InOutTestNode.SimulationPorts.Output, b, InOutTestNode.SimulationPorts.Input);

                var otherNodePortDef = set.GetDefinition<TwoInOutTestNode>().GetStaticPortDescription();

                Assert.Throws<ArgumentOutOfRangeException>(() => set.Connect(a, otherNodePortDef.Outputs[1], b, otherNodePortDef.Inputs[0]));
                Assert.Throws<ArgumentOutOfRangeException>(() => set.Connect(a, otherNodePortDef.Outputs[0], b, otherNodePortDef.Inputs[1]));
                Assert.Throws<ArgumentOutOfRangeException>(() => set.Connect(a, otherNodePortDef.Outputs[1], b, otherNodePortDef.Inputs[1]));

                set.Destroy(a, b);
            }
        }

        void ConnectingOutOfArrayPortIndicesRange_ThrowsException<TNodeDefinition>(InputPortID inputs, OutputPortID output)
            where TNodeDefinition : NodeDefinition, new()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<TNodeDefinition>
                    a = set.Create<TNodeDefinition>(),
                    b = set.Create<TNodeDefinition>();

                Assert.Throws<IndexOutOfRangeException>(() => set.Connect(a, output, b, inputs, 0));

                set.SetPortArraySize(a, inputs, 1);
                set.SetPortArraySize(b, inputs, 1);

                set.Connect(a, output, b, inputs, 0);

                Assert.Throws<IndexOutOfRangeException>(() => set.Connect(a, output, b, inputs, 1));

                set.Destroy(a, b);
            }
        }

        [Test]
        public void ConnectingOutOfArrayPortIndicesRange_ThrowsException()
        {
            using (var set = new NodeSet())
            {
                // Must touch the Node type first to ensure PortIDs have been assigned.
                set.GetDefinition<NodeWithAllTypesOfPorts>();
            }

            ConnectingOutOfArrayPortIndicesRange_ThrowsException<NodeWithAllTypesOfPorts>(
                (InputPortID)NodeWithAllTypesOfPorts.SimulationPorts.MessageArrayIn, (OutputPortID)NodeWithAllTypesOfPorts.SimulationPorts.MessageOut);
            ConnectingOutOfArrayPortIndicesRange_ThrowsException<NodeWithAllTypesOfPorts>(
                (InputPortID)NodeWithAllTypesOfPorts.KernelPorts.InputArrayScalar, (OutputPortID)NodeWithAllTypesOfPorts.KernelPorts.OutputScalar);
        }

        void DisconnectingOutOfArrayPortIndicesRange_ThrowsException<TNodeDefinition>(InputPortID inputs, OutputPortID output)
            where TNodeDefinition : NodeDefinition, new()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<TNodeDefinition>
                    a = set.Create<TNodeDefinition>(),
                    b = set.Create<TNodeDefinition>();

                Assert.Throws<IndexOutOfRangeException>(() => set.Connect(a, output, b, inputs, 0));

                set.SetPortArraySize(a, inputs, 1);
                set.SetPortArraySize(b, inputs, 1);

                set.Connect(a, output, b, inputs, 0);

                Assert.Throws<IndexOutOfRangeException>(() => set.Disconnect(a, output, b, inputs, 1));

                set.Disconnect(a, output, b, inputs, 0);

                set.SetPortArraySize(b, inputs, 0);

                Assert.Throws<IndexOutOfRangeException>(() => set.Connect(a, output, b, inputs, 0));

                set.Destroy(a, b);
            }
        }

        [Test]
        public void DisconnectingOutOfArrayPortIndicesRange_ThrowsException()
        {
            using (var set = new NodeSet())
            {
                // Must touch the Node type first to ensure PortIDs have been assigned.
                set.GetDefinition<NodeWithAllTypesOfPorts>();
            }

            DisconnectingOutOfArrayPortIndicesRange_ThrowsException<NodeWithAllTypesOfPorts>(
                (InputPortID)NodeWithAllTypesOfPorts.SimulationPorts.MessageArrayIn, (OutputPortID)NodeWithAllTypesOfPorts.SimulationPorts.MessageOut);
            DisconnectingOutOfArrayPortIndicesRange_ThrowsException<NodeWithAllTypesOfPorts>(
                (InputPortID)NodeWithAllTypesOfPorts.KernelPorts.InputArrayScalar, (OutputPortID)NodeWithAllTypesOfPorts.KernelPorts.OutputScalar);
        }

        public void ReducingConnectedArrayPort_ThrowsException<TNodeDefinition>(InputPortID inputs, OutputPortID output)
            where TNodeDefinition : NodeDefinition, new()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<TNodeDefinition>
                    a = set.Create<TNodeDefinition>(),
                    b = set.Create<TNodeDefinition>();

                set.SetPortArraySize(a, inputs, 1);
                set.SetPortArraySize(b, inputs, 1);

                set.Connect(a, output, b, inputs, 0);

                set.SetPortArraySize(a, inputs, 0);
                Assert.Throws<InvalidOperationException>(() => set.SetPortArraySize(b, inputs, 0));

                set.Disconnect(a, output, b, inputs, 0);

                set.SetPortArraySize(b, inputs, 0);

                set.Destroy(a, b);
            }
        }

        [Test]
        public void ReducingConnectedArrayPort_ThrowsException()
        {
            using (var set = new NodeSet())
            {
                // Must touch the Node type first to ensure PortIDs have been assigned.
                set.GetDefinition<NodeWithAllTypesOfPorts>();
            }

            ReducingConnectedArrayPort_ThrowsException<NodeWithAllTypesOfPorts>(
                (InputPortID)NodeWithAllTypesOfPorts.SimulationPorts.MessageArrayIn, (OutputPortID)NodeWithAllTypesOfPorts.SimulationPorts.MessageOut);
            ReducingConnectedArrayPort_ThrowsException<NodeWithAllTypesOfPorts>(
                (InputPortID)NodeWithAllTypesOfPorts.KernelPorts.InputArrayScalar, (OutputPortID)NodeWithAllTypesOfPorts.KernelPorts.OutputScalar);
        }
    }
}
