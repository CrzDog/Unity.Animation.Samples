﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DataFlowGraph.Tests
{
    class RenderGraphIntegrityTests
    {
        // TODO tests:
        // * Check free inputs have allocated things
        // * Check number of live kernel nodes matches # of simulation nodes that are of kernel type, and that this matches k_AliveNodes
        // * Check that all input ports after value chunk has been built have valid pointers
        // * Check that input ports that are not connected have correspondingly allocated structures in place
        // * Check world synchronization, including that kernel nodes are mirrored (also all other stuff like data transports)
        // * Check issuing multiple buffer resizes to the same port does not result in errors
        // * Check Unity can survive render error

        // * Check RootFence includes ExternalDependencies, regardless of graph size (including all render models)
        // * Check RootFence m_ComputedVersion always matches render version
        // * Check RenderVersion behaves as expected.. many things depend on it

        public struct Node : INodeData
        {
#pragma warning disable 649  // never assigned
            public int Contents;
#pragma warning restore 649
        }

        public struct Data : IKernelData
        {
#pragma warning disable 649  // never assigned
            public int Contents;
#pragma warning restore 649
        }

        class KernelNode : NodeDefinition<Node, Data, KernelNode.KernelDefs, KernelNode.Kernel>
        {
            public struct KernelDefs : IKernelPortDefinition
            {
                public DataInput<KernelNode, int> Input1, Input2;
                public PortArray<DataInput<KernelNode, int>> InputArray;
                public DataInput<KernelNode, int> Input3;
                public DataOutput<KernelNode, int> Output1, Output2, Output3;
            }

            [BurstCompile(CompileSynchronously = true)]
            public struct Kernel : IGraphKernel<Data, KernelDefs>
            {
                public void Execute(RenderContext ctx, Data data, ref KernelDefs ports)
                {
                    ctx.Resolve(ref ports.Output1) = ctx.Resolve(ports.Input1);
                    ctx.Resolve(ref ports.Output2) = ctx.Resolve(ports.Input2);
                    ctx.Resolve(ref ports.Output3) = ctx.Resolve(ports.Input3);
                }
            }
        }

        class SimpleNode : NodeDefinition<EmptyPorts> { }

        [Test]
        unsafe public void OnlyKernelNodes_AreMirroredIntoRenderGraph()
        {
            using (var set = new NodeSet())
            {
                NodeHandle
                    a = set.Create<KernelNode>(),
                    b = set.Create<KernelNode>(),
                    c = set.Create<KernelNode>(),
                    d = set.Create<SimpleNode>(),
                    e = set.Create<SimpleNode>();

                set.Update();

                var graph = set.DataGraph;

                graph.SyncAnyRendering();

                var inodes = set.GetInternalData();
                var knodes = set.DataGraph.GetInternalData();

                Assert.GreaterOrEqual(knodes.Count, 3);
                Assert.AreEqual(3, graph.m_ExistingNodes);

                ref var ak = ref knodes[a.VHandle.Index];
                ref var bk = ref knodes[b.VHandle.Index];
                ref var ck = ref knodes[c.VHandle.Index];

                Assert.IsTrue(ak.AliveInRenderer);
                Assert.IsTrue(bk.AliveInRenderer);
                Assert.IsTrue(ck.AliveInRenderer);

                // Not guaranteed to exist
                // Assert.IsFalse(knodes[d.VHandle.Index].AliveInRenderer);
                // Assert.IsFalse(knodes[e.VHandle.Index].AliveInRenderer);

                Assert.IsTrue(UnsafeUtility.AddressOf(ref ak.TraitsHandle.Resolve()) == UnsafeUtility.AddressOf(ref Unsafe.AsRef(set.GetNodeTraits(a))));
                Assert.IsTrue(UnsafeUtility.AddressOf(ref bk.TraitsHandle.Resolve()) == UnsafeUtility.AddressOf(ref Unsafe.AsRef(set.GetNodeTraits(b))));
                Assert.IsTrue(UnsafeUtility.AddressOf(ref ck.TraitsHandle.Resolve()) == UnsafeUtility.AddressOf(ref Unsafe.AsRef(set.GetNodeTraits(b))));

                Assert.AreEqual(ak.Handle.ToPublicHandle(), a);
                Assert.AreEqual(bk.Handle.ToPublicHandle(), b);
                Assert.AreEqual(ck.Handle.ToPublicHandle(), c);

                set.Destroy(a, b, c);
                set.Update();

                knodes = set.DataGraph.GetInternalData();

                for (int i = 0; i < knodes.Count; ++i)
                    Assert.IsFalse(knodes[i].AliveInRenderer);

                set.Destroy(d, e);
            }
        }

        [TestCase(1), TestCase(2), TestCase(50)]
        public unsafe void NonConnectedInputs_HaveValidPointers(int numNodes)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle>();

                for (int i = 0; i < numNodes; ++i)
                {
                    var node = set.Create<KernelNode>();
                    nodes.Add(node);
                    set.SetPortArraySize(node, KernelNode.KernelPorts.InputArray, 2);
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();
                var blank = set.DataGraph.m_SharedData.BlankPage;

                for (int i = 0; i < numNodes; ++i)
                {
                    ref var node = ref knodes[nodes[i].VHandle.Index];
                    var ports = Unsafe.AsRef<KernelNode.KernelDefs>(node.Instance.Ports);

                    Assert.IsTrue(ports.Input1.Ptr != null);
                    Assert.IsTrue(ports.Input2.Ptr != null);
                    Assert.IsTrue(ports.Input3.Ptr != null);

                    Assert.IsTrue(ports.Input1.Ptr == blank);
                    Assert.IsTrue(ports.Input2.Ptr == blank);
                    Assert.IsTrue(ports.Input3.Ptr == blank);

                    Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input1.Ptr));
                    Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input2.Ptr));
                    Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input3.Ptr));

                    Assert.IsTrue(ports.InputArray.Ptr != null);
                    Assert.IsTrue(ports.InputArray.Ptr != blank);
                    Assert.IsTrue(ports.InputArray[0].Ptr == blank);
                    Assert.IsTrue(ports.InputArray[1].Ptr == blank);
                    fixed (void** p = &ports.InputArray[0].Ptr)
                        Assert.IsFalse(DataInputUtility.PortOwnsMemory(p));
                    fixed (void** p = &ports.InputArray[1].Ptr)
                        Assert.IsFalse(DataInputUtility.PortOwnsMemory(p));
                }

                nodes.ForEach(n => set.Destroy(n));
            }
        }

        class KernelBufferIONode : NodeDefinition<Node, Data, KernelBufferIONode.KernelDefs, KernelBufferIONode.Kernel>
        {
            public struct KernelDefs : IKernelPortDefinition
            {
                public DataInput<KernelBufferIONode, Buffer<int>> Input;
                public DataOutput<KernelBufferIONode, Buffer<int>> Output;
                public DataOutput<KernelBufferIONode, int> Sum;
            }

            [BurstCompile(CompileSynchronously = true)]
            public struct Kernel : IGraphKernel<Data, KernelDefs>
            {
                public void Execute(RenderContext ctx, Data data, ref KernelDefs ports)
                {
                    var input = ctx.Resolve(ports.Input);
                    var output = ctx.Resolve(ref ports.Output);
                    ref var sum = ref ctx.Resolve(ref ports.Sum);
                    sum = 0;
                    for (int i = 0; i < input.Length; ++i)
                        sum += input[i];
                    for (int i = 0; i < output.Length; ++i)
                        output[i] = i + sum;
                }
            }
        }

        [Test]
        public void DataInputs_CannotReceiveDataBuffers()
        {
            using (var set = new NodeSet())
            {
                var a = set.Create<KernelBufferIONode>();

                Assert.Throws<InvalidOperationException>(() => set.SetData(a, KernelBufferIONode.KernelPorts.Input, new Buffer<int>()));
                Assert.Throws<InvalidOperationException>(() => set.SetData(a, KernelBufferIONode.KernelPorts.Input, Buffer<int>.SizeRequest(0)));
                Assert.Throws<InvalidOperationException>(() => set.SetData(a, KernelBufferIONode.KernelPorts.Input, Buffer<int>.SizeRequest(10)));

                Assert.Throws<InvalidOperationException>(() => set.SetData((NodeHandle)a, (InputPortID)KernelBufferIONode.KernelPorts.Input, new Buffer<int>()));
                Assert.Throws<InvalidOperationException>(() => set.SetData((NodeHandle)a, (InputPortID)KernelBufferIONode.KernelPorts.Input, Buffer<int>.SizeRequest(0)));
                Assert.Throws<InvalidOperationException>(() => set.SetData((NodeHandle)a, (InputPortID)KernelBufferIONode.KernelPorts.Input, Buffer<int>.SizeRequest(10)));

                set.Destroy(a);
            }
        }

        [TestCase(1, 3), TestCase(2, 6), TestCase(50, 10)]
        public unsafe void NonConnectedInputs_CanReceiveData_AndHaveValidPointers(int numNodes, int updateCycles)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle<KernelNode>>();
                for (int i = 0; i < numNodes; ++i)
                    nodes.Add(set.Create<KernelNode>());
                set.Update();

                for (int u = 0; u < updateCycles; ++u)
                {
                    if (u % 2 == 0)
                        for (int i = 0; i < numNodes; ++i)
                            set.SetData(nodes[i], i % 3 == 0 ? KernelNode.KernelPorts.Input1 : i % 3 == 1 ? KernelNode.KernelPorts.Input2 : KernelNode.KernelPorts.Input3, i + u * 100);

                    set.Update();
                    set.DataGraph.SyncAnyRendering();

                    var knodes = set.DataGraph.GetInternalData();
                    var blank = set.DataGraph.m_SharedData.BlankPage;

                    for (int i = 0; i < numNodes; ++i)
                    {
                        ref var node = ref knodes[((NodeHandle)(nodes[i])).VHandle.Index];
                        var ports = Unsafe.AsRef<KernelNode.KernelDefs>(node.Instance.Ports);

                        Assert.IsTrue(ports.Input1.Ptr != null);
                        Assert.IsTrue(ports.Input2.Ptr != null);
                        Assert.IsTrue(ports.Input3.Ptr != null);

                        Assert.IsTrue(i % 3 == 0 ? ports.Input1.Ptr != blank : ports.Input1.Ptr == blank);
                        Assert.IsTrue(i % 3 == 1 ? ports.Input2.Ptr != blank : ports.Input2.Ptr == blank);
                        Assert.IsTrue(i % 3 == 2 ? ports.Input3.Ptr != blank : ports.Input3.Ptr == blank);

                        Assert.IsTrue(*(int*)ports.Input1.Ptr == (i % 3 == 0 ? i + u / 2 * 200 : 0));
                        Assert.IsTrue(*(int*)ports.Input2.Ptr == (i % 3 == 1 ? i + u / 2 * 200 : 0));
                        Assert.IsTrue(*(int*)ports.Input3.Ptr == (i % 3 == 2 ? i + u / 2 * 200 : 0));

                        Assert.IsTrue((i % 3 == 0) == DataInputUtility.PortOwnsMemory(&ports.Input1.Ptr));
                        Assert.IsTrue((i % 3 == 1) == DataInputUtility.PortOwnsMemory(&ports.Input2.Ptr));
                        Assert.IsTrue((i % 3 == 2) == DataInputUtility.PortOwnsMemory(&ports.Input3.Ptr));
                    }
                }

                nodes.ForEach(n => set.Destroy(n));
            }
        }

        [TestCase(1, 3, NodeSet.ConnectionType.Normal), TestCase(2, 6, NodeSet.ConnectionType.Normal), TestCase(50, 10, NodeSet.ConnectionType.Normal),
         TestCase(1, 3, NodeSet.ConnectionType.Feedback), TestCase(2, 6, NodeSet.ConnectionType.Feedback), TestCase(50, 10, NodeSet.ConnectionType.Feedback)]
        public unsafe void ConnectingInputs_AfterReceivingData_KeepsPointersValid(int numNodes, int updateCycles, NodeSet.ConnectionType connectionType)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle<KernelNode>>();
                for (int i = 0; i < numNodes; ++i)
                    nodes.Add(set.Create<KernelNode>());
                set.Update();

                int[] sourceDataValues = { 11, 22, 33 };
                for (int i = 0; i < 3; ++i)
                    set.SetData(nodes[0], i % 3 == 0 ? KernelNode.KernelPorts.Input1 : i % 3 == 1 ? KernelNode.KernelPorts.Input2 : KernelNode.KernelPorts.Input3, sourceDataValues[i]);

                for (int u = 0; u < updateCycles; ++u)
                {
                    for (int i = 1; i < numNodes; ++i)
                        set.SetData(nodes[i], i % 3 == 0 ? KernelNode.KernelPorts.Input1 : i % 3 == 1 ? KernelNode.KernelPorts.Input2 : KernelNode.KernelPorts.Input3, i + u * 100);
                    set.Update();

                    for (int i = 1; i < numNodes; ++i)
                    {
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output1, nodes[i], KernelNode.KernelPorts.Input1, connectionType);
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output2, nodes[i], KernelNode.KernelPorts.Input2, connectionType);
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output3, nodes[i], KernelNode.KernelPorts.Input3, connectionType);
                    }

                    set.Update();
                    set.DataGraph.SyncAnyRendering();
                    var knodes = set.DataGraph.GetInternalData();

                    for (int i = 1; i < numNodes; ++i)
                    {
                        // All nodes other than the source node in the chain should be connected and
                        // be getting their data from their parent (ultimately from the source node).
                        ref var node = ref knodes[((NodeHandle)(nodes[i])).VHandle.Index];
                        var ports = Unsafe.AsRef<KernelNode.KernelDefs>(node.Instance.Ports);
                        ref var parentnode = ref knodes[((NodeHandle)(nodes[i - 1])).VHandle.Index];
                        ref var parentports = ref Unsafe.AsRef<KernelNode.KernelDefs>(parentnode.Instance.Ports);
                        Assert.IsTrue(ports.Input1.Ptr == Unsafe.AsPointer(ref parentports.Output1.m_Value));
                        Assert.IsTrue(ports.Input2.Ptr == Unsafe.AsPointer(ref parentports.Output2.m_Value));
                        Assert.IsTrue(ports.Input3.Ptr == Unsafe.AsPointer(ref parentports.Output3.m_Value));
                        Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input1.Ptr));
                        Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input2.Ptr));
                        Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input3.Ptr));
                        if (connectionType == NodeSet.ConnectionType.Normal)
                        {
                            Assert.IsTrue(*(int*)ports.Input1.Ptr == sourceDataValues[0]);
                            Assert.IsTrue(*(int*)ports.Input2.Ptr == sourceDataValues[1]);
                            Assert.IsTrue(*(int*)ports.Input3.Ptr == sourceDataValues[2]);
                        }
                    }

                    for (int i = 1; i < numNodes; ++i)
                        set.DisconnectAll(nodes[i]);
                    set.Update();
                }

                nodes.ForEach(n => set.Destroy(n));
            }
        }

        [TestCase(1, 3, NodeSet.ConnectionType.Normal), TestCase(2, 6, NodeSet.ConnectionType.Normal), TestCase(50, 10, NodeSet.ConnectionType.Normal),
         TestCase(1, 3, NodeSet.ConnectionType.Feedback), TestCase(2, 6, NodeSet.ConnectionType.Feedback), TestCase(50, 10, NodeSet.ConnectionType.Feedback)]
        public unsafe void ConnectingAndDisconnectingInputs_ResetsReceivedData_AndKeepsPointersValid(int numNodes, int updateCycles, NodeSet.ConnectionType connectionType)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle<KernelNode>>();
                for (int i = 0; i < numNodes; ++i)
                    nodes.Add(set.Create<KernelNode>());

                for (int u = 0; u < updateCycles; ++u)
                {
                    set.Update();
                    set.DataGraph.SyncAnyRendering();

                    var knodes = set.DataGraph.GetInternalData();
                    var blank = set.DataGraph.m_SharedData.BlankPage;

                    for (int i = 0; i < numNodes; ++i)
                    {
                        ref var node = ref knodes[((NodeHandle)(nodes[i])).VHandle.Index];
                        var ports = Unsafe.AsRef<KernelNode.KernelDefs>(node.Instance.Ports);

                        Assert.IsTrue(ports.Input1.Ptr == blank);
                        Assert.IsTrue(ports.Input2.Ptr == blank);
                        Assert.IsTrue(ports.Input3.Ptr == blank);
                        Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input1.Ptr));
                        Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input2.Ptr));
                        Assert.IsFalse(DataInputUtility.PortOwnsMemory(&ports.Input3.Ptr));
                    }

                    for (int i = 1; i < numNodes; ++i)
                        set.SetData(nodes[i], i % 3 == 0 ? KernelNode.KernelPorts.Input1 : i % 3 == 1 ? KernelNode.KernelPorts.Input2 : KernelNode.KernelPorts.Input3, i + u * 100);
                    set.Update();

                    for (int i = 1; i < numNodes; ++i)
                    {
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output1, nodes[i], KernelNode.KernelPorts.Input1, connectionType);
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output2, nodes[i], KernelNode.KernelPorts.Input2, connectionType);
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output3, nodes[i], KernelNode.KernelPorts.Input3, connectionType);
                    }
                    set.Update();

                    for (int i = 1; i < numNodes; ++i)
                        set.DisconnectAll(nodes[i - 1]);
                }

                nodes.ForEach(n => set.Destroy(n));
            }
        }

        [TestCase(1, 3, NodeSet.ConnectionType.Normal), TestCase(2, 6, NodeSet.ConnectionType.Normal), TestCase(50, 10, NodeSet.ConnectionType.Normal),
         TestCase(1, 3, NodeSet.ConnectionType.Feedback), TestCase(2, 6, NodeSet.ConnectionType.Feedback), TestCase(50, 10, NodeSet.ConnectionType.Feedback)]
        public unsafe void ConnectingAndDisconnectingInputs_CanRetainConnectedData_AndKeepsPointersValid(int numNodes, int updateCycles, NodeSet.ConnectionType connectionType)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle<KernelNode>>();
                for (int i = 0; i < numNodes; ++i)
                    nodes.Add(set.Create<KernelNode>());
                set.Update();

                int[] sourceDataValues = { 11, 22, 33 };
                for (int i = 0; i < 3; ++i)
                    set.SetData(nodes[0], i % 3 == 0 ? KernelNode.KernelPorts.Input1 : i % 3 == 1 ? KernelNode.KernelPorts.Input2 : KernelNode.KernelPorts.Input3, sourceDataValues[i]);
                set.Update();

                for (int u = 0; u < updateCycles; ++u)
                {
                    for (int i = 1; i < numNodes; ++i)
                    {
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output1, nodes[i], KernelNode.KernelPorts.Input1, connectionType);
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output2, nodes[i], KernelNode.KernelPorts.Input2, connectionType);
                        set.Connect(nodes[i - 1], KernelNode.KernelPorts.Output3, nodes[i], KernelNode.KernelPorts.Input3, connectionType);
                    }
                    set.Update();

                    for (int i = 1; i < numNodes; ++i)
                    {
                        set.DisconnectAndRetainValue(nodes[i - 1], KernelNode.KernelPorts.Output1, nodes[i], KernelNode.KernelPorts.Input1);
                        set.DisconnectAndRetainValue(nodes[i - 1], KernelNode.KernelPorts.Output2, nodes[i], KernelNode.KernelPorts.Input2);
                        set.DisconnectAndRetainValue(nodes[i - 1], KernelNode.KernelPorts.Output3, nodes[i], KernelNode.KernelPorts.Input3);
                    }
                    set.Update();
                    set.DataGraph.SyncAnyRendering();

                    var knodes = set.DataGraph.GetInternalData();

                    for (int i = 0; i < numNodes; ++i)
                    {
                        ref var node = ref knodes[((NodeHandle)(nodes[i])).VHandle.Index];
                        var ports = Unsafe.AsRef<KernelNode.KernelDefs>(node.Instance.Ports);
                        if (i > 0)
                        {
                            ref var parentnode = ref knodes[((NodeHandle)(nodes[i - 1])).VHandle.Index];
                            ref var parentports = ref Unsafe.AsRef<KernelNode.KernelDefs>(parentnode.Instance.Ports);
                            Assert.IsFalse(ports.Input1.Ptr == Unsafe.AsPointer(ref parentports.Output1.m_Value));
                            Assert.IsFalse(ports.Input2.Ptr == Unsafe.AsPointer(ref parentports.Output2.m_Value));
                            Assert.IsFalse(ports.Input3.Ptr == Unsafe.AsPointer(ref parentports.Output3.m_Value));
                        }
                        Assert.IsTrue(DataInputUtility.PortOwnsMemory(&ports.Input1.Ptr));
                        Assert.IsTrue(DataInputUtility.PortOwnsMemory(&ports.Input2.Ptr));
                        Assert.IsTrue(DataInputUtility.PortOwnsMemory(&ports.Input3.Ptr));
                        if (connectionType == NodeSet.ConnectionType.Normal)
                        {
                            Assert.IsTrue(*(int*)ports.Input1.Ptr == sourceDataValues[0]);
                            Assert.IsTrue(*(int*)ports.Input2.Ptr == sourceDataValues[1]);
                            Assert.IsTrue(*(int*)ports.Input3.Ptr == sourceDataValues[2]);
                        }
                    }
                }

                nodes.ForEach(n => set.Destroy(n));
            }
        }

        [Test]
        public unsafe void ConnectingAndDisconnectingBufferInputs_CannotRetainConnectedData([Values]NodeSet.ConnectionType connectionType)
        {
            using (var set = new NodeSet())
            {
                var a = set.Create<KernelBufferIONode>();
                var b = set.Create<KernelBufferIONode>();

                set.Connect(a, KernelBufferIONode.KernelPorts.Output, b, KernelBufferIONode.KernelPorts.Input, connectionType);
                set.SetBufferSize(a, KernelBufferIONode.KernelPorts.Output, Buffer<int>.SizeRequest(10));

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();
                ref var aNode = ref knodes[((NodeHandle)a).VHandle.Index];
                ref var bNode = ref knodes[((NodeHandle)b).VHandle.Index];
                ref var aPorts = ref Unsafe.AsRef<KernelBufferIONode.KernelDefs>(aNode.Instance.Ports);
                var bPorts = Unsafe.AsRef<KernelBufferIONode.KernelDefs>(bNode.Instance.Ports);

                Assert.IsFalse(DataInputUtility.PortOwnsMemory(&bPorts.Input.Ptr));
                Assert.IsTrue(Unsafe.AsPointer(ref aPorts.Output.m_Value) == bPorts.Input.Ptr);

                Assert.Throws<InvalidOperationException>(() => set.DisconnectAndRetainValue(a, KernelBufferIONode.KernelPorts.Output, b, KernelBufferIONode.KernelPorts.Input));

                set.Update();
                set.DataGraph.SyncAnyRendering();

                bPorts = Unsafe.AsRef<KernelBufferIONode.KernelDefs>(bNode.Instance.Ports);

                Assert.IsFalse(DataInputUtility.PortOwnsMemory(&bPorts.Input.Ptr));
                Assert.IsTrue(Unsafe.AsPointer(ref aPorts.Output.m_Value) == bPorts.Input.Ptr);

                set.Disconnect(a, KernelBufferIONode.KernelPorts.Output, b, KernelBufferIONode.KernelPorts.Input);

                set.Update();
                set.DataGraph.SyncAnyRendering();

                bPorts = Unsafe.AsRef<KernelBufferIONode.KernelDefs>(bNode.Instance.Ports);

                Assert.IsFalse(DataInputUtility.PortOwnsMemory(&bPorts.Input.Ptr));
                Assert.IsTrue(Unsafe.AsPointer(ref aPorts.Output.m_Value) != bPorts.Input.Ptr);

                set.Destroy(a, b);
            }
        }

        [Test]
        public unsafe void ConnectedInputs_AllFromSameOutput_FromSameParent_HaveValidPointers_ThatReferToParent_Values([Values(1,2,50)] int numNodes, [Values]NodeSet.ConnectionType connectionType)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle<KernelNode>>();
                var parents = new List<NodeHandle<KernelNode>>();
                for (int i = 0; i < numNodes; ++i)
                {
                    var child = set.Create<KernelNode>();
                    var parent = set.Create<KernelNode>();

                    set.Connect(parent, KernelNode.KernelPorts.Output3, child, KernelNode.KernelPorts.Input1, connectionType);
                    set.Connect(parent, KernelNode.KernelPorts.Output3, child, KernelNode.KernelPorts.Input2, connectionType);
                    set.Connect(parent, KernelNode.KernelPorts.Output3, child, KernelNode.KernelPorts.Input3, connectionType);

                    set.SetPortArraySize(child, KernelNode.KernelPorts.InputArray, 2);
                    set.Connect(parent, KernelNode.KernelPorts.Output3, child, KernelNode.KernelPorts.InputArray, 0, connectionType);
                    set.Connect(parent, KernelNode.KernelPorts.Output3, child, KernelNode.KernelPorts.InputArray, 1, connectionType);

                    parents.Add(parent);
                    nodes.Add(child);
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                // Assert child nodes are hooked up to parent
                for (int i = 0; i < numNodes; ++i)
                {
                    ref var childPorts = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)nodes[i]).VHandle.Index].Instance.Ports);
                    ref var parentPorts = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)parents[i]).VHandle.Index].Instance.Ports);

                    Assert.IsTrue(childPorts.Input1.Ptr == Unsafe.AsPointer(ref parentPorts.Output3.m_Value));
                    Assert.IsTrue(childPorts.Input2.Ptr == Unsafe.AsPointer(ref parentPorts.Output3.m_Value));
                    Assert.IsTrue(childPorts.Input3.Ptr == Unsafe.AsPointer(ref parentPorts.Output3.m_Value));

                    Assert.IsTrue(childPorts.InputArray[0].Ptr == Unsafe.AsPointer(ref parentPorts.Output3.m_Value));
                    Assert.IsTrue(childPorts.InputArray[1].Ptr == Unsafe.AsPointer(ref parentPorts.Output3.m_Value));
                }

                nodes.ForEach(n => set.Destroy(n));
                parents.ForEach(n => set.Destroy(n));
            }
        }


        [Test]
        public unsafe void ConnectedInputs_FromDifferentOutputs_FromSameParent_HaveValidPointers_ThatReferToParent_Values([Values(1,2,50)] int numNodes, [Values]NodeSet.ConnectionType connectionType)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle<KernelNode>>();
                var parents = new List<NodeHandle<KernelNode>>();
                for (int i = 0; i < numNodes; ++i)
                {
                    var child = set.Create<KernelNode>();
                    var parent = set.Create<KernelNode>();

                    set.Connect(parent, KernelNode.KernelPorts.Output1, child, KernelNode.KernelPorts.Input1, connectionType);
                    set.Connect(parent, KernelNode.KernelPorts.Output2, child, KernelNode.KernelPorts.Input2, connectionType);
                    set.Connect(parent, KernelNode.KernelPorts.Output3, child, KernelNode.KernelPorts.Input3, connectionType);

                    set.SetPortArraySize(child, KernelNode.KernelPorts.InputArray, 2);
                    set.Connect(parent, KernelNode.KernelPorts.Output1, child, KernelNode.KernelPorts.InputArray, 0, connectionType);
                    set.Connect(parent, KernelNode.KernelPorts.Output2, child, KernelNode.KernelPorts.InputArray, 1, connectionType);

                    parents.Add(parent);
                    nodes.Add(child);
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                // Assert child nodes are hooked up to parent
                for (int i = 0; i < numNodes; ++i)
                {
                    ref var childPorts = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)nodes[i]).VHandle.Index].Instance.Ports);
                    ref var parentPorts = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)parents[i]).VHandle.Index].Instance.Ports);

                    Assert.IsTrue(childPorts.Input1.Ptr == Unsafe.AsPointer(ref parentPorts.Output1.m_Value));
                    Assert.IsTrue(childPorts.Input2.Ptr == Unsafe.AsPointer(ref parentPorts.Output2.m_Value));
                    Assert.IsTrue(childPorts.Input3.Ptr == Unsafe.AsPointer(ref parentPorts.Output3.m_Value));

                    Assert.IsTrue(childPorts.InputArray[0].Ptr == UnsafeUtility.AddressOf(ref parentPorts.Output1.m_Value));
                    Assert.IsTrue(childPorts.InputArray[1].Ptr == UnsafeUtility.AddressOf(ref parentPorts.Output2.m_Value));
                }

                nodes.ForEach(n => set.Destroy(n));
                parents.ForEach(n => set.Destroy(n));
            }
        }

        [Test]
        public unsafe void ConnectedInputs_FromDifferentOutputs_FromDifferentParents_HaveValidPointers_ThatReferToParent_Values([Values(1,2,50)] int numNodes, [Values]NodeSet.ConnectionType connectionType)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle<KernelNode>>();
                var parents = new List<NodeHandle<KernelNode>>();
                for (int i = 0; i < numNodes; ++i)
                {
                    var child = set.Create<KernelNode>();

                    NodeHandle<KernelNode>
                        parent1 = set.Create<KernelNode>(),
                        parent2 = set.Create<KernelNode>(),
                        parent3 = set.Create<KernelNode>(),
                        parent4 = set.Create<KernelNode>(),
                        parent5 = set.Create<KernelNode>();

                    set.Connect(parent1, KernelNode.KernelPorts.Output1, child, KernelNode.KernelPorts.Input1, connectionType);
                    set.Connect(parent2, KernelNode.KernelPorts.Output2, child, KernelNode.KernelPorts.Input2, connectionType);
                    set.Connect(parent3, KernelNode.KernelPorts.Output3, child, KernelNode.KernelPorts.Input3, connectionType);

                    set.SetPortArraySize(child, KernelNode.KernelPorts.InputArray, 2);
                    set.Connect(parent4, KernelNode.KernelPorts.Output1, child, KernelNode.KernelPorts.InputArray, 0, connectionType);
                    set.Connect(parent5, KernelNode.KernelPorts.Output2, child, KernelNode.KernelPorts.InputArray, 1, connectionType);

                    parents.Add(parent1);
                    parents.Add(parent2);
                    parents.Add(parent3);
                    parents.Add(parent4);
                    parents.Add(parent5);

                    nodes.Add(child);
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                // Assert child nodes are hooked up to parent
                for (int i = 0; i < numNodes; ++i)
                {
                    ref var childPorts = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)nodes[i]).VHandle.Index].Instance.Ports);

                    ref var parent1Ports = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)parents[i * 5 + 0]).VHandle.Index].Instance.Ports);
                    ref var parent2Ports = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)parents[i * 5 + 1]).VHandle.Index].Instance.Ports);
                    ref var parent3Ports = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)parents[i * 5 + 2]).VHandle.Index].Instance.Ports);
                    ref var parent4Ports = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)parents[i * 5 + 3]).VHandle.Index].Instance.Ports);
                    ref var parent5Ports = ref Unsafe.AsRef<KernelNode.KernelDefs>(knodes[((NodeHandle)parents[i * 5 + 4]).VHandle.Index].Instance.Ports);

                    Assert.IsTrue(childPorts.Input1.Ptr == Unsafe.AsPointer(ref parent1Ports.Output1.m_Value));
                    Assert.IsTrue(childPorts.Input2.Ptr == Unsafe.AsPointer(ref parent2Ports.Output2.m_Value));
                    Assert.IsTrue(childPorts.Input3.Ptr == Unsafe.AsPointer(ref parent3Ports.Output3.m_Value));

                    Assert.IsTrue(childPorts.InputArray[0].Ptr == UnsafeUtility.AddressOf(ref parent4Ports.Output1.m_Value));
                    Assert.IsTrue(childPorts.InputArray[1].Ptr == UnsafeUtility.AddressOf(ref parent5Ports.Output2.m_Value));
                }

                nodes.ForEach(n => set.Destroy(n));
                parents.ForEach(n => set.Destroy(n));
            }
        }

        public struct Aggregate
        {
            public Buffer<int> SubBuffer1;
            public Buffer<int> SubBuffer2;
        }

        class KernelBufferOutputNode : NodeDefinition<Node, Data, KernelBufferOutputNode.KernelDefs, KernelBufferOutputNode.Kernel>
        {
            public struct KernelDefs : IKernelPortDefinition
            {
                public DataOutput<KernelBufferOutputNode, Buffer<int>> Output1, Output2;
                public DataOutput<KernelBufferOutputNode, Aggregate> Output3;
            }

            [BurstCompile(CompileSynchronously = true)]
            public struct Kernel : IGraphKernel<Data, KernelDefs>
            {
                public void Execute(RenderContext ctx, Data data, ref KernelDefs ports)
                {
                }
            }
        }

        [TestCase(1), TestCase(2), TestCase(50)]
        public unsafe void KernelDataOutputBuffers_InitiallyHaveNullArrays_WithZeroSize(int numNodes)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle>();

                for (int i = 0; i < numNodes; ++i)
                {
                    nodes.Add(set.Create<KernelBufferOutputNode>());
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                for (int i = 0; i < numNodes; ++i)
                {
                    ref var node = ref knodes[nodes[i].VHandle.Index];
                    ref var ports = ref Unsafe.AsRef<KernelBufferOutputNode.KernelDefs>(node.Instance.Ports);

                    Assert.IsTrue(ports.Output1.m_Value.Ptr == null);
                    Assert.IsTrue(ports.Output2.m_Value.Ptr == null);
                    Assert.IsTrue(ports.Output3.m_Value.SubBuffer1.Ptr == null);
                    Assert.IsTrue(ports.Output3.m_Value.SubBuffer2.Ptr == null);

                    Assert.Zero(ports.Output1.m_Value.Size);
                    Assert.Zero(ports.Output2.m_Value.Size);
                    Assert.Zero(ports.Output3.m_Value.SubBuffer1.Size);
                    Assert.Zero(ports.Output3.m_Value.SubBuffer2.Size);
                }

                nodes.ForEach(n => set.Destroy(n));
            }
        }

        [Test]
        public unsafe void KernelDataOutputBuffers_HaveSelfOwnerID()
        {
            using (var set = new NodeSet())
            {
                NodeHandle node = set.Create<KernelBufferOutputNode>();

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                ref var knode = ref knodes[((NodeHandle)node).VHandle.Index];
                ref var ports = ref Unsafe.AsRef<KernelBufferOutputNode.KernelDefs>(knode.Instance.Ports);

                Assert.AreEqual(node, ports.Output1.m_Value.OwnerNode.ToPublicHandle());
                Assert.AreEqual(node, ports.Output2.m_Value.OwnerNode.ToPublicHandle());
                Assert.AreEqual(node, ports.Output3.m_Value.SubBuffer1.OwnerNode.ToPublicHandle());
                Assert.AreEqual(node, ports.Output3.m_Value.SubBuffer2.OwnerNode.ToPublicHandle());

                set.Destroy(node);
            }
        }

        [Test]
        public unsafe void KernelDataOutputBuffers_AreUpdated_WhenIssuingSimulation_Resizes()
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<KernelBufferOutputNode>();

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                ref var knode = ref knodes[((NodeHandle)node).VHandle.Index];
                ref var ports = ref Unsafe.AsRef<KernelBufferOutputNode.KernelDefs>(knode.Instance.Ports);

                var oldPort1Pointer = ports.Output1.m_Value.Ptr;
                var oldPort2Pointer = ports.Output2.m_Value.Ptr;
                var oldPort3Pointer = ports.Output3.m_Value.SubBuffer1.Ptr;
                var oldPort4Pointer = ports.Output3.m_Value.SubBuffer2.Ptr;


                //--------------------------------------

                set.SetBufferSize(node, KernelBufferOutputNode.KernelPorts.Output1, Buffer<int>.SizeRequest(15));

                set.Update();
                set.DataGraph.SyncAnyRendering();

                Assert.IsTrue(ports.Output1.m_Value.Ptr != oldPort1Pointer && ports.Output1.m_Value.Ptr != null);
                Assert.IsTrue(ports.Output2.m_Value.Ptr == null);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer1.Ptr == null);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer2.Ptr == null);

                Assert.AreEqual(15, ports.Output1.m_Value.Size);
                Assert.Zero(ports.Output2.m_Value.Size);
                Assert.Zero(ports.Output3.m_Value.SubBuffer1.Size);
                Assert.Zero(ports.Output3.m_Value.SubBuffer2.Size);

                oldPort1Pointer = ports.Output1.m_Value.Ptr;

                //--------------------------------------

                set.SetBufferSize(node, KernelBufferOutputNode.KernelPorts.Output2, Buffer<int>.SizeRequest(17));

                set.Update();
                set.DataGraph.SyncAnyRendering();

                Assert.IsTrue(ports.Output1.m_Value.Ptr == oldPort1Pointer);
                Assert.IsTrue(ports.Output2.m_Value.Ptr != oldPort2Pointer && ports.Output2.m_Value.Ptr != null);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer1.Ptr == null);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer2.Ptr == null);

                Assert.AreEqual(15, ports.Output1.m_Value.Size);
                Assert.AreEqual(17, ports.Output2.m_Value.Size);
                Assert.Zero(ports.Output3.m_Value.SubBuffer1.Size);
                Assert.Zero(ports.Output3.m_Value.SubBuffer2.Size);

                oldPort2Pointer = ports.Output2.m_Value.Ptr;

                //--------------------------------------

                set.SetBufferSize(node, KernelBufferOutputNode.KernelPorts.Output3, new Aggregate { SubBuffer1 = Buffer<int>.SizeRequest(19) });

                set.Update();
                set.DataGraph.SyncAnyRendering();

                Assert.IsTrue(ports.Output1.m_Value.Ptr == oldPort1Pointer);
                Assert.IsTrue(ports.Output2.m_Value.Ptr == oldPort2Pointer);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer1.Ptr != oldPort3Pointer && ports.Output3.m_Value.SubBuffer1.Ptr != null);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer2.Ptr == null);

                Assert.AreEqual(15, ports.Output1.m_Value.Size);
                Assert.AreEqual(17, ports.Output2.m_Value.Size);
                Assert.AreEqual(19, ports.Output3.m_Value.SubBuffer1.Size);
                Assert.Zero(ports.Output3.m_Value.SubBuffer2.Size);

                oldPort3Pointer = ports.Output3.m_Value.SubBuffer1.Ptr;

                //--------------------------------------

                set.SetBufferSize(node, KernelBufferOutputNode.KernelPorts.Output3, new Aggregate { SubBuffer2 = Buffer<int>.SizeRequest(21) });

                set.Update();
                set.DataGraph.SyncAnyRendering();

                Assert.IsTrue(ports.Output1.m_Value.Ptr == oldPort1Pointer);
                Assert.IsTrue(ports.Output2.m_Value.Ptr == oldPort2Pointer);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer1.Ptr == oldPort3Pointer);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer2.Ptr != oldPort4Pointer && ports.Output3.m_Value.SubBuffer2.Ptr != null);

                Assert.AreEqual(15, ports.Output1.m_Value.Size);
                Assert.AreEqual(17, ports.Output2.m_Value.Size);
                Assert.AreEqual(19, ports.Output3.m_Value.SubBuffer1.Size);
                Assert.AreEqual(21, ports.Output3.m_Value.SubBuffer2.Size);

                oldPort4Pointer = ports.Output3.m_Value.SubBuffer2.Ptr;

                //--------------------------------------

                set.SetBufferSize(node, KernelBufferOutputNode.KernelPorts.Output1, Buffer<int>.SizeRequest(2));

                set.Update();
                set.DataGraph.SyncAnyRendering();

                Assert.IsTrue(ports.Output1.m_Value.Ptr != oldPort1Pointer && ports.Output1.m_Value.Ptr != null);
                Assert.IsTrue(ports.Output2.m_Value.Ptr == oldPort2Pointer);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer1.Ptr == oldPort3Pointer);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer2.Ptr == oldPort4Pointer);

                Assert.AreEqual(2, ports.Output1.m_Value.Size);
                Assert.AreEqual(17, ports.Output2.m_Value.Size);
                Assert.AreEqual(19, ports.Output3.m_Value.SubBuffer1.Size);
                Assert.AreEqual(21, ports.Output3.m_Value.SubBuffer2.Size);

                oldPort1Pointer = ports.Output1.m_Value.Ptr;

                //--------------------------------------

                set.SetBufferSize(node, KernelBufferOutputNode.KernelPorts.Output2, Buffer<int>.SizeRequest(0));

                set.Update();
                set.DataGraph.SyncAnyRendering();

                Assert.IsTrue(ports.Output1.m_Value.Ptr == oldPort1Pointer);
                Assert.IsTrue(ports.Output2.m_Value.Ptr == null);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer1.Ptr == oldPort3Pointer);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer2.Ptr == oldPort4Pointer);

                Assert.AreEqual(2, ports.Output1.m_Value.Size);
                Assert.Zero(ports.Output2.m_Value.Size);
                Assert.AreEqual(19, ports.Output3.m_Value.SubBuffer1.Size);
                Assert.AreEqual(21, ports.Output3.m_Value.SubBuffer2.Size);

                //--------------------------------------

                set.SetBufferSize(node, KernelBufferOutputNode.KernelPorts.Output3, new Aggregate { SubBuffer2 = Buffer<int>.SizeRequest(0) });

                set.Update();
                set.DataGraph.SyncAnyRendering();

                Assert.IsTrue(ports.Output1.m_Value.Ptr == oldPort1Pointer);
                Assert.IsTrue(ports.Output2.m_Value.Ptr == null);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer1.Ptr == oldPort3Pointer);
                Assert.IsTrue(ports.Output3.m_Value.SubBuffer2.Ptr == null);

                Assert.AreEqual(2, ports.Output1.m_Value.Size);
                Assert.Zero(ports.Output2.m_Value.Size);
                Assert.AreEqual(19, ports.Output3.m_Value.SubBuffer1.Size);
                Assert.Zero(ports.Output3.m_Value.SubBuffer2.Size);


                set.Destroy(node);

            }
        }

        [Test]
        public unsafe void KernelDataOutputBuffers_AreUpdatedImmediately_InSameCreationFrame()
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<KernelBufferOutputNode>();
                set.SetBufferSize(node, KernelBufferOutputNode.KernelPorts.Output1, Buffer<int>.SizeRequest(15));

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                ref var knode = ref knodes[((NodeHandle)node).VHandle.Index];
                ref var ports = ref Unsafe.AsRef<KernelBufferOutputNode.KernelDefs>(knode.Instance.Ports);

                Assert.IsTrue(ports.Output1.m_Value.Ptr != null);

                Assert.AreEqual(15, ports.Output1.m_Value.Size);

                set.Destroy(node);

            }
        }


        class KernelBufferInputNode : NodeDefinition<Node, Data, KernelBufferInputNode.KernelDefs, KernelBufferInputNode.Kernel>
        {
            public struct KernelDefs : IKernelPortDefinition
            {
                public DataInput<KernelBufferInputNode, Buffer<int>> Input1, Input2;
                public DataInput<KernelBufferInputNode, Aggregate> Input3;
            }

            [BurstCompile(CompileSynchronously = true)]
            public struct Kernel : IGraphKernel<Data, KernelDefs>
            {
                public void Execute(RenderContext ctx, Data data, ref KernelDefs ports)
                {
                }
            }
        }

        [TestCase(1), TestCase(2), TestCase(50)]
        public unsafe void DanglingKernelDataInputBuffers_AlwaysHaveAssignedArrays_ToBlank_WithZeroSize(int numNodes)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle>();

                for (int i = 0; i < numNodes; ++i)
                {
                    nodes.Add(set.Create<KernelBufferInputNode>());
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();
                var blank = set.DataGraph.m_SharedData.BlankPage;

                var knodes = set.DataGraph.GetInternalData();

                for (int i = 0; i < numNodes; ++i)
                {
                    ref var node = ref knodes[nodes[i].VHandle.Index];
                    ref var ports = ref Unsafe.AsRef<KernelBufferInputNode.KernelDefs>(node.Instance.Ports);

                    Assert.IsTrue(ports.Input1.Ptr == blank);
                    Assert.IsTrue(ports.Input2.Ptr == blank);
                    Assert.IsTrue(ports.Input3.Ptr == blank);

                    Assert.Zero(Unsafe.AsRef<Buffer<int>>(ports.Input1.Ptr).Size);
                    Assert.Zero(Unsafe.AsRef<Buffer<int>>(ports.Input2.Ptr).Size);
                    Assert.Zero(Unsafe.AsRef<Buffer<int>>(ports.Input3.Ptr).Size);
                }

                nodes.ForEach(n => set.Destroy(n));
            }
        }

        [TestCase(1), TestCase(2), TestCase(50)]
        public unsafe void DanglingKernelDataInputBuffers_AlwaysHaveAssignedNullArrays(int numNodes)
        {
            using (var set = new NodeSet())
            {
                var nodes = new List<NodeHandle>();

                for (int i = 0; i < numNodes; ++i)
                {
                    nodes.Add(set.Create<KernelBufferInputNode>());
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                for (int i = 0; i < numNodes; ++i)
                {
                    ref var node = ref knodes[nodes[i].VHandle.Index];
                    ref var ports = ref Unsafe.AsRef<KernelBufferInputNode.KernelDefs>(node.Instance.Ports);

                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(ports.Input1.Ptr).Ptr == null);
                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(ports.Input2.Ptr).Ptr == null);
                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(ports.Input3.Ptr).Ptr == null);

                }

                nodes.ForEach(n => set.Destroy(n));
            }
        }

        [Test]
        public unsafe void ConnectingDataBuffers_PatchesInputPorts_ToParentOutputs([Values(1,2,50)] int numNodes, [Values]NodeSet.ConnectionType connectionType)
        {
            using (var set = new NodeSet())
            {
                var outputs = new List<NodeHandle<KernelBufferOutputNode>>();
                var inputs = new List<NodeHandle>();

                for (int i = 0; i < numNodes; ++i)
                {
                    var input = set.Create<KernelBufferInputNode>();
                    var output = set.Create<KernelBufferOutputNode>();

                    set.Connect(output, KernelBufferOutputNode.KernelPorts.Output1, input, KernelBufferInputNode.KernelPorts.Input1, connectionType);
                    set.Connect(output, KernelBufferOutputNode.KernelPorts.Output2, input, KernelBufferInputNode.KernelPorts.Input2, connectionType);
                    set.Connect(output, KernelBufferOutputNode.KernelPorts.Output3, input, KernelBufferInputNode.KernelPorts.Input3, connectionType);

                    inputs.Add(input);
                    outputs.Add(output);
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                for (int i = 0; i < numNodes; ++i)
                {
                    ref var outNode = ref knodes[((NodeHandle)outputs[i]).VHandle.Index];
                    ref var inNode = ref knodes[inputs[i].VHandle.Index];

                    ref var outPorts = ref Unsafe.AsRef<KernelBufferOutputNode.KernelDefs>(outNode.Instance.Ports);
                    ref var inPorts = ref Unsafe.AsRef<KernelBufferInputNode.KernelDefs>(inNode.Instance.Ports);

                    var outArray1 = Unsafe.AsPointer(ref outPorts.Output1.m_Value);
                    var outArray2 = Unsafe.AsPointer(ref outPorts.Output2.m_Value);
                    var outArray3 = Unsafe.AsPointer(ref outPorts.Output3.m_Value);

                    Assert.IsTrue(outArray1 == inPorts.Input1.Ptr);
                    Assert.IsTrue(outArray2 == inPorts.Input2.Ptr);
                    Assert.IsTrue(outArray3 == inPorts.Input3.Ptr);

                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(inPorts.Input1.Ptr).Ptr == null);
                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(inPorts.Input2.Ptr).Ptr == null);
                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(inPorts.Input3.Ptr).Ptr == null);

                    Assert.Zero(Unsafe.AsRef<Buffer<int>>(inPorts.Input1.Ptr).Size);
                    Assert.Zero(Unsafe.AsRef<Buffer<int>>(inPorts.Input2.Ptr).Size);
                    Assert.Zero(Unsafe.AsRef<Buffer<int>>(inPorts.Input3.Ptr).Size);
                }

                for (int i = 0; i < numNodes; ++i)
                {
                    set.SetBufferSize(outputs[i], KernelBufferOutputNode.KernelPorts.Output1, Buffer<int>.SizeRequest(15));
                    set.SetBufferSize(outputs[i], KernelBufferOutputNode.KernelPorts.Output2, Buffer<int>.SizeRequest(17));
                    var aggregateSizes = new Aggregate
                    { SubBuffer1 = Buffer<int>.SizeRequest(19), SubBuffer2 = Buffer<int>.SizeRequest(21) };
                    set.SetBufferSize(outputs[i], KernelBufferOutputNode.KernelPorts.Output3, aggregateSizes);
                }

                set.Update();
                set.DataGraph.SyncAnyRendering();

                knodes = set.DataGraph.GetInternalData();

                for (int i = 0; i < numNodes; ++i)
                {
                    ref var outNode = ref knodes[((NodeHandle)outputs[i]).VHandle.Index];
                    ref var inNode = ref knodes[inputs[i].VHandle.Index];

                    ref var outPorts = ref Unsafe.AsRef<KernelBufferOutputNode.KernelDefs>(outNode.Instance.Ports);
                    ref var inPorts = ref Unsafe.AsRef<KernelBufferInputNode.KernelDefs>(inNode.Instance.Ports);

                    var outArray1 = Unsafe.AsPointer(ref outPorts.Output1.m_Value);
                    var outArray2 = Unsafe.AsPointer(ref outPorts.Output2.m_Value);
                    var outArray3 = Unsafe.AsPointer(ref outPorts.Output3.m_Value);

                    Assert.IsTrue(outArray1 == inPorts.Input1.Ptr);
                    Assert.IsTrue(outArray2 == inPorts.Input2.Ptr);
                    Assert.IsTrue(outArray3 == inPorts.Input3.Ptr);

                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(inPorts.Input1.Ptr).Ptr != null);
                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(inPorts.Input2.Ptr).Ptr != null);
                    Assert.IsTrue(Unsafe.AsRef<Buffer<int>>(inPorts.Input3.Ptr).Ptr != null);

                    Assert.AreEqual(15, Unsafe.AsRef<Buffer<int>>(inPorts.Input1.Ptr).Size);
                    Assert.AreEqual(17, Unsafe.AsRef<Buffer<int>>(inPorts.Input2.Ptr).Size);
                    Assert.AreEqual(19, Unsafe.AsRef<Buffer<int>>(inPorts.Input3.Ptr).Size);
                }

                outputs.ForEach(n => set.Destroy(n));
                inputs.ForEach(n => set.Destroy(n));

            }
        }

        [Test]
        public unsafe void KernelDataPortArrays_AreInitiallyEmpty()
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<NodeWithAllTypesOfPorts>();

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                ref var knode = ref knodes[((NodeHandle)node).VHandle.Index];
                ref var ports = ref Unsafe.AsRef<NodeWithAllTypesOfPorts.KernelDefs>(knode.Instance.Ports);

                Assert.Zero(ports.InputArrayScalar.Size);
                Assert.IsTrue(ports.InputArrayScalar.Ptr == null);

                Assert.Zero(ports.InputArrayBuffer.Size);
                Assert.IsTrue(ports.InputArrayBuffer.Ptr == null);

                set.Destroy(node);
            }
        }

        [Test]
        public unsafe void KernelDataPortArrays_CanSetSize([Values(0, 10)] int size)
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<NodeWithAllTypesOfPorts>();
                set.SetPortArraySize(node, NodeWithAllTypesOfPorts.KernelPorts.InputArrayScalar, (ushort)size);
                set.SetPortArraySize(node, NodeWithAllTypesOfPorts.KernelPorts.InputArrayBuffer, (ushort)size);

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                ref var knode = ref knodes[((NodeHandle)node).VHandle.Index];
                ref var ports = ref Unsafe.AsRef<NodeWithAllTypesOfPorts.KernelDefs>(knode.Instance.Ports);

                Assert.AreEqual(ports.InputArrayScalar.Size, (ushort)size);
                Assert.AreEqual(ports.InputArrayScalar.Ptr == null, size == 0);
                Assert.AreEqual(ports.InputArrayBuffer.Size, (ushort)size);
                Assert.AreEqual(ports.InputArrayBuffer.Ptr == null, size == 0);

                set.Destroy(node);
            }
        }

        [Test]
        public unsafe void KernelDataPortArrays_CanBeResized([Values(0, 10, 20)] int initialSize, [Values(0, 10)] int finalSize)
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<NodeWithAllTypesOfPorts>();
                set.SetPortArraySize(node, NodeWithAllTypesOfPorts.KernelPorts.InputArrayScalar, (ushort)initialSize);
                set.SetPortArraySize(node, NodeWithAllTypesOfPorts.KernelPorts.InputArrayBuffer, (ushort)initialSize);

                set.Update();

                set.SetPortArraySize(node, NodeWithAllTypesOfPorts.KernelPorts.InputArrayScalar, (ushort)finalSize);
                set.SetPortArraySize(node, NodeWithAllTypesOfPorts.KernelPorts.InputArrayBuffer, (ushort)finalSize);

                set.Update();
                set.DataGraph.SyncAnyRendering();

                var knodes = set.DataGraph.GetInternalData();

                ref var knode = ref knodes[((NodeHandle)node).VHandle.Index];
                ref var ports = ref Unsafe.AsRef<NodeWithAllTypesOfPorts.KernelDefs>(knode.Instance.Ports);

                Assert.AreEqual(ports.InputArrayScalar.Size, (ushort)finalSize);
                Assert.AreEqual(ports.InputArrayScalar.Ptr == null, finalSize == 0);
                Assert.AreEqual(ports.InputArrayBuffer.Size, (ushort)finalSize);
                Assert.AreEqual(ports.InputArrayBuffer.Ptr == null, finalSize == 0);

                set.Destroy(node);
            }
        }
    }

}
