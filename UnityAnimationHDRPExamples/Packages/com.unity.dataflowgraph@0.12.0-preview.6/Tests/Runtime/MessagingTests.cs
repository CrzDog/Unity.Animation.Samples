using System;
using NUnit.Framework;
using Unity.Burst;

namespace Unity.DataFlowGraph.Tests
{
    public class MessagingTests
    {
        public struct Node : INodeData
        {
            public int Contents;
            public float OtherContents;
        }

        public class SimpleMessageNode : NodeDefinition<Node, SimpleMessageNode.SimPorts>, IMsgHandler<Message>
        {
            public struct SimPorts : ISimulationPortDefinition
            {
                public MessageInput<SimpleMessageNode, Message> Input;
                public MessageOutput<SimpleMessageNode, Message> Output;
            }

            public void HandleMessage(in MessageContext ctx, in Message msg)
            {
                ref var data = ref GetNodeData(ctx.Handle);

                Assert.That(ctx.Port == SimulationPorts.Input);
                data.Contents = msg.Contents;
                ctx.EmitMessage(SimulationPorts.Output, new Message(data.Contents + 20));
            }

            protected internal override void OnUpdate(in UpdateContext ctx)
            {
                ref var data = ref GetNodeData(ctx.Handle);
                data.Contents += 1;
                ctx.EmitMessage(SimulationPorts.Output, new Message(data.Contents + 20));
            }
        }

        [Test]
        public void TestSimpleMessageSending()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<SimpleMessageNode>
                    a = set.Create<SimpleMessageNode>(),
                    b = set.Create<SimpleMessageNode>();

                set.Connect(a, SimpleMessageNode.SimulationPorts.Output, b, SimpleMessageNode.SimulationPorts.Input);
                set.SendMessage(a, SimpleMessageNode.SimulationPorts.Input, new Message(10));

                Assert.AreEqual(10, set.GetNodeData<Node>(a).Contents);
                Assert.AreEqual(30, set.GetNodeData<Node>(b).Contents);

                set.Destroy(a, b);
            }
        }

        [Test]
        public void TestSimpleMessageEmitting_OnUpdate()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<SimpleMessageNode>
                    a = set.Create<SimpleMessageNode>(),
                    b = set.Create<SimpleMessageNode>();

                set.Connect(a, SimpleMessageNode.SimulationPorts.Output, b, SimpleMessageNode.SimulationPorts.Input);
                set.SendMessage(a, SimpleMessageNode.SimulationPorts.Input, new Message(10));

                for (var i = 1; i < 10; ++i)
                {
                    set.Update();
                    Assert.AreEqual(10 + i, set.GetNodeData<Node>(a).Contents);
                    Assert.AreEqual(30 + i + 1, set.GetNodeData<Node>(b).Contents);
                }

                set.Destroy(a, b);
            }
        }

        [Test]
        public void EmitMessage_OnHandleMessage_AfterDestroy_Throws()
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<DelegateMessageIONode>(
                    (in MessageContext ctx, in Message msg) =>
                    {
                        set.Destroy(ctx.Handle);
                        ctx.EmitMessage(DelegateMessageIONode.SimulationPorts.Output, msg);
                    }
                );
                Assert.Throws<InvalidOperationException>(
                    () => set.SendMessage(node, DelegateMessageIONode.SimulationPorts.Input, new Message()));
            }
        }

        [Test]
        public void EmitMessage_OnUpdate_AfterDestroy_Throws()
        {
            using (var set = new NodeSet())
            {
                set.Create<DelegateMessageIONode>(
                    (in UpdateContext ctx) =>
                    {
                        set.Destroy(ctx.Handle);
                        ctx.EmitMessage(DelegateMessageIONode.SimulationPorts.Output, new Message());
                    }
                );
                Assert.Throws<InvalidOperationException>(() => set.Update());
            }
        }

        class SimpleMessageArrayNode : NodeDefinition<Node, SimpleMessageArrayNode.SimPorts>, IMsgHandler<int>
        {
            public struct SimPorts : ISimulationPortDefinition
            {
#pragma warning disable 649  // Assigned through internal DataFlowGraph reflection
                public PortArray<MessageInput<SimpleMessageArrayNode, int>> Inputs;
                public MessageOutput<SimpleMessageArrayNode, int> Output;
#pragma warning restore 649
            }

            public void HandleMessage(in MessageContext ctx, in int msg)
            {
                ref var data = ref GetNodeData(ctx.Handle);

                Assert.That(ctx.Port == SimulationPorts.Inputs);
                ushort index = ctx.ArrayIndex;
                data.Contents = msg + index;
                ctx.EmitMessage(SimulationPorts.Output, index + 30);
            }
        }

        [Test]
        public void TestSimpleMessageArraySending()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<SimpleMessageArrayNode>
                    a = set.Create<SimpleMessageArrayNode>(),
                    b = set.Create<SimpleMessageArrayNode>();

                set.SetPortArraySize(a, SimpleMessageArrayNode.SimulationPorts.Inputs, 2);
                set.SetPortArraySize(b, SimpleMessageArrayNode.SimulationPorts.Inputs, 3);

                set.Connect(a, SimpleMessageArrayNode.SimulationPorts.Output, b, SimpleMessageArrayNode.SimulationPorts.Inputs, 2);
                set.SendMessage(a, SimpleMessageArrayNode.SimulationPorts.Inputs, 1, 10);

                Assert.AreEqual(11, set.GetNodeData<Node>(a).Contents);
                Assert.AreEqual(33, set.GetNodeData<Node>(b).Contents);

                set.Destroy(a, b);
            }
        }

        [Test]
        public void CanEnqueueMultipleMessagesAndConsumeInSteps()
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<DelegateMessageIONode<Node>, Node>(
                    (in MessageContext ctx, in Message msg) => set.GetNodeData<Node>(ctx.Handle).Contents += msg.Contents);

                Assert.AreEqual(0, set.GetNodeData<Node>(node).Contents);

                for (int mc = 0; mc < 10; ++mc)
                {
                    for (int i = 0; i < 10; ++i)
                    {
                        set.SendMessage(node, DelegateMessageIONode<Node>.SimulationPorts.Input, new Message(10));
                    }

                    var contents = set.GetNodeData<Node>(node).Contents;
                    Assert.AreEqual((mc + 1) * 10 * 10, contents);
                }

                set.Destroy(node);
            }
        }

        public class MulticastTestNode : NodeDefinition<Node, MulticastTestNode.SimPorts>, IMsgHandler<Message>
        {
            public struct SimPorts : ISimulationPortDefinition
            {
                public MessageInput<MulticastTestNode, Message> Input1, Input2;
                public MessageOutput<MulticastTestNode, Message> Output1, Output2;
            }

            public void HandleMessage(in MessageContext ctx, in Message msg)
            {
                ref var data = ref GetNodeData(ctx.Handle);

                Assert.That(ctx.Port == SimulationPorts.Input1 || ctx.Port == SimulationPorts.Input2);
                data.Contents += msg.Contents;
                ctx.EmitMessage(SimulationPorts.Output1, new Message(data.Contents + 1));
            }
        }

        [Test]
        public void TestDiamondMessageSending()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<MulticastTestNode>
                    a = set.Create<MulticastTestNode>(),
                    b = set.Create<MulticastTestNode>(),
                    c = set.Create<MulticastTestNode>(),
                    d = set.Create<MulticastTestNode>();

                set.Connect(a, MulticastTestNode.SimulationPorts.Output1, b, MulticastTestNode.SimulationPorts.Input1);
                set.Connect(a, MulticastTestNode.SimulationPorts.Output1, c, MulticastTestNode.SimulationPorts.Input1);
                set.Connect(b, MulticastTestNode.SimulationPorts.Output1, d, MulticastTestNode.SimulationPorts.Input1);
                set.Connect(c, MulticastTestNode.SimulationPorts.Output1, d, MulticastTestNode.SimulationPorts.Input1);

                set.SendMessage(a, MulticastTestNode.SimulationPorts.Input1, new Message(10));

                Assert.AreEqual(10, set.GetNodeData<Node>(a).Contents);
                Assert.AreEqual(11, set.GetNodeData<Node>(b).Contents);
                Assert.AreEqual(11, set.GetNodeData<Node>(c).Contents);
                Assert.AreEqual(24, set.GetNodeData<Node>(d).Contents);

                set.Destroy(a, b, c, d);
            }
        }

        [Test]
        public void TestPortMessageIsolation()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<MulticastTestNode>
                    a = set.Create<MulticastTestNode>(),
                    b = set.Create<MulticastTestNode>(),
                    c = set.Create<MulticastTestNode>(),
                    d = set.Create<MulticastTestNode>();

                set.Connect(a, MulticastTestNode.SimulationPorts.Output1, b, MulticastTestNode.SimulationPorts.Input1);
                set.Connect(a, MulticastTestNode.SimulationPorts.Output2, c, MulticastTestNode.SimulationPorts.Input1);
                set.Connect(b, MulticastTestNode.SimulationPorts.Output1, d, MulticastTestNode.SimulationPorts.Input1);
                set.Connect(c, MulticastTestNode.SimulationPorts.Output1, d, MulticastTestNode.SimulationPorts.Input1);

                set.SendMessage(a, MulticastTestNode.SimulationPorts.Input1, new Message(10));

                Assert.AreEqual(10, set.GetNodeData<Node>(a).Contents);
                Assert.AreEqual(11, set.GetNodeData<Node>(b).Contents);
                // multicaster sends message on port 0, but c is connected through port 1, so it never updates its data.
                Assert.AreEqual(0, set.GetNodeData<Node>(c).Contents);
                Assert.AreEqual(12, set.GetNodeData<Node>(d).Contents);

                set.Destroy(a, b, c, d);
            }
        }

        [Test]
        public void TestMulticastMessageSending()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<MulticastTestNode>
                    a = set.Create<MulticastTestNode>(),
                    b = set.Create<MulticastTestNode>(),
                    c = set.Create<MulticastTestNode>();

                set.Connect(a, MulticastTestNode.SimulationPorts.Output1, b, MulticastTestNode.SimulationPorts.Input1);
                set.Connect(a, MulticastTestNode.SimulationPorts.Output1, c, MulticastTestNode.SimulationPorts.Input1);

                set.SendMessage(a, MulticastTestNode.SimulationPorts.Input1, new Message(10));

                Assert.AreEqual(10, set.GetNodeData<Node>(a).Contents);
                Assert.AreEqual(11, set.GetNodeData<Node>(b).Contents);
                Assert.AreEqual(11, set.GetNodeData<Node>(c).Contents);

                set.Destroy(a, b, c);
            }
        }

        public class PassMessageThroughNextPort : NodeDefinition<Node, PassMessageThroughNextPort.SimPorts>, IMsgHandler<Message>
        {
            public struct SimPorts : ISimulationPortDefinition
            {
                public MessageInput<PassMessageThroughNextPort, Message> Input1, Input2, Input3, Input4, Input5, Input6, Input7, Input8;
                public MessageOutput<PassMessageThroughNextPort, Message> Output1, Output2, Output3, Output4, Output5, Output6, Output7, Output8;
            }

            public void HandleMessage(in MessageContext ctx, in Message msg)
            {
                ref var data = ref GetNodeData(ctx.Handle);

                data.Contents =
                    msg.Contents +
                    (ctx.Port == SimulationPorts.Input1 ? 0 :
                     ctx.Port == SimulationPorts.Input2 ? 1 :
                     ctx.Port == SimulationPorts.Input3 ? 2 :
                     ctx.Port == SimulationPorts.Input4 ? 3 :
                     ctx.Port == SimulationPorts.Input5 ? 4 :
                     ctx.Port == SimulationPorts.Input6 ? 5 :
                     ctx.Port == SimulationPorts.Input7 ? 6 : 7);
                ctx.EmitMessage(
                    ctx.Port == SimulationPorts.Input1 ? SimulationPorts.Output2 :
                    ctx.Port == SimulationPorts.Input2 ? SimulationPorts.Output3 :
                    ctx.Port == SimulationPorts.Input3 ? SimulationPorts.Output4 :
                    ctx.Port == SimulationPorts.Input4 ? SimulationPorts.Output5 :
                    ctx.Port == SimulationPorts.Input5 ? SimulationPorts.Output6 :
                    ctx.Port == SimulationPorts.Input6 ? SimulationPorts.Output7 :
                        SimulationPorts.Output8,
                    new Message(msg.Contents + 1)
                );
            }
        }

        [Test]
        public void TestMessagePortCascading()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<PassMessageThroughNextPort>
                    a = set.Create<PassMessageThroughNextPort>(),
                    b = set.Create<PassMessageThroughNextPort>(),
                    c = set.Create<PassMessageThroughNextPort>(),
                    d = set.Create<PassMessageThroughNextPort>();

                set.Connect(a, PassMessageThroughNextPort.SimulationPorts.Output2, b, PassMessageThroughNextPort.SimulationPorts.Input3);
                set.Connect(b, PassMessageThroughNextPort.SimulationPorts.Output4, c, PassMessageThroughNextPort.SimulationPorts.Input5);
                set.Connect(c, PassMessageThroughNextPort.SimulationPorts.Output6, d, PassMessageThroughNextPort.SimulationPorts.Input7);

                set.SendMessage(a, PassMessageThroughNextPort.SimulationPorts.Input1, new Message(10));

                Assert.AreEqual(10, set.GetNodeData<Node>(a).Contents);
                Assert.AreEqual(13, set.GetNodeData<Node>(b).Contents);
                Assert.AreEqual(16, set.GetNodeData<Node>(c).Contents);
                Assert.AreEqual(19, set.GetNodeData<Node>(d).Contents);

                set.Destroy(a, b, c, d);
            }
        }

        public class DifferentHandlers
            : NodeDefinition<Node, DifferentHandlers.SimPorts>
            , IMsgHandler<int>
            , IMsgHandler<float>
        {
            public struct SimPorts : ISimulationPortDefinition
            {
                public MessageInput<DifferentHandlers, int> Input1;
                public MessageInput<DifferentHandlers, float> Input2;
                public MessageOutput<DifferentHandlers, Message> Output1, Output2;
            }

            public void HandleMessage(in MessageContext ctx, in int msg)
            {
                ref var data = ref GetNodeData(ctx.Handle);
                data.Contents = msg + 1 + (ctx.Port == SimulationPorts.Input1 ? 0 : 1);
            }

            public void HandleMessage(in MessageContext ctx, in float msg)
            {
                ref var data = ref GetNodeData(ctx.Handle);
                data.OtherContents = msg + 2 + (ctx.Port == SimulationPorts.Input1 ? 0 : 1);
            }
        }


        [Test]
        public void TestDifferentHandlers()
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<DifferentHandlers>();

                set.SendMessage(node, DifferentHandlers.SimulationPorts.Input1, 5);
                set.SendMessage(node, DifferentHandlers.SimulationPorts.Input2, 5f);

                var data = set.GetNodeData<Node>(node);

                Assert.AreEqual(6, data.Contents);
                Assert.AreEqual(8, data.OtherContents);

                set.Destroy(node);
            }
        }

        public class KernelNode : NodeDefinition<KernelNode.Node, KernelNode.Data, KernelNode.KernelDefs, KernelNode.Kernel>
        {
            public struct Node : INodeData { }

            public struct Data : IKernelData { }

            public struct KernelDefs : IKernelPortDefinition
            {
                public DataInput<KernelNode, int> Input1, Input2;
                public DataOutput<KernelNode, int> Output;
            }

            [BurstCompile(CompileSynchronously = true)]
            public struct Kernel : IGraphKernel<Data, KernelDefs>
            {
                public void Execute(RenderContext ctx, Data data, ref KernelDefs ports)
                {
                }
            }
        }

        [Test]
        public void CannotSetData_OnConnectedDataInput()
        {
            using (var set = new NodeSet())
            {
                var node1 = set.Create<KernelNode>();
                var node2 = set.Create<KernelNode>();

                set.Connect(node1, KernelNode.KernelPorts.Output, node2, KernelNode.KernelPorts.Input1);

                set.SetData(node1, KernelNode.KernelPorts.Input1, 5);
                set.SetData(node1, KernelNode.KernelPorts.Input2, 10);

                Assert.Throws<InvalidOperationException>(() => set.SetData(node2, KernelNode.KernelPorts.Input1, 15));
                set.SetData(node2, KernelNode.KernelPorts.Input2, 20);

                set.Disconnect(node1, KernelNode.KernelPorts.Output, node2, KernelNode.KernelPorts.Input1);
                set.Connect(node1, KernelNode.KernelPorts.Output, node2, KernelNode.KernelPorts.Input2);

                set.SetData(node2, KernelNode.KernelPorts.Input1, 25);
                Assert.Throws<InvalidOperationException>(() => set.SetData(node2, KernelNode.KernelPorts.Input2, 30));

                set.Destroy(node1);
                set.Destroy(node2);
            }
        }
    }
}
