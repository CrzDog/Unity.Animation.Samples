using System.Linq;
using NUnit.Framework;

namespace Unity.DataFlowGraph.Tests
{
    using Topology = TopologyAPI<ValidatedHandle, InputPortArrayID, OutputPortID>;

    public class TopologyIntegrityTests
    {
        // TODO: Check linked list integrity of connection table

        public enum ConnectionAPI
        {
            StronglyTyped,
            WeaklyTyped
        }

        public struct Node : INodeData
        {
            public int Contents;
        }


        public struct Data : IKernelData { }

        [Test]
        public void TopologyVersionIncreases_OnCreatingNodes()
        {
            using (var set = new NodeSet())
            {
                var version = set.TopologyVersion.Version;
                var node = set.Create<NodeWithAllTypesOfPorts>();
                Assert.Greater(set.TopologyVersion.Version, version);
                set.Destroy(node);
            }
        }

        [Test]
        public void TopologyVersionIncreases_OnDestroyingNodes()
        {
            using (var set = new NodeSet())
            {
                var node = set.Create<NodeWithAllTypesOfPorts>();
                var version = set.TopologyVersion.Version;
                set.Destroy(node);
                Assert.Greater(set.TopologyVersion.Version, version);
            }
        }

        [Test]
        public void TopologyVersionIncreases_OnCreatingConnections()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<NodeWithAllTypesOfPorts>
                    a = set.Create<NodeWithAllTypesOfPorts>(),
                    b = set.Create<NodeWithAllTypesOfPorts>();

                var version = set.TopologyVersion.Version;
                set.Connect(a, NodeWithAllTypesOfPorts.SimulationPorts.MessageOut, b, NodeWithAllTypesOfPorts.SimulationPorts.MessageIn);

                Assert.Greater(set.TopologyVersion.Version, version);

                set.Destroy(a, b);
            }
        }

        [Test]
        public void TopologyVersionIncreases_OnDestroyingConnections()
        {
            using (var set = new NodeSet())
            {
                NodeHandle<NodeWithAllTypesOfPorts>
                    a = set.Create<NodeWithAllTypesOfPorts>(),
                    b = set.Create<NodeWithAllTypesOfPorts>();

                set.Connect(a, NodeWithAllTypesOfPorts.SimulationPorts.MessageOut, b, NodeWithAllTypesOfPorts.SimulationPorts.MessageIn);
                var version = set.TopologyVersion.Version;
                set.Disconnect(a, NodeWithAllTypesOfPorts.SimulationPorts.MessageOut, b, NodeWithAllTypesOfPorts.SimulationPorts.MessageIn);

                Assert.Greater(set.TopologyVersion.Version, version);

                set.Destroy(a, b);
            }
        }

        [TestCase(ConnectionAPI.StronglyTyped)]
        [TestCase(ConnectionAPI.WeaklyTyped)]
        public void MessageConnectionsMade_CausesConnectionTable_ToBePopulatedCorrectly_AndSubsequentlyRemoved(ConnectionAPI meansOfConnection)
        {
            using (var set = new NodeSet())
            {
                NodeHandle<NodeWithAllTypesOfPorts>
                    a = set.Create<NodeWithAllTypesOfPorts>(),
                    b = set.Create<NodeWithAllTypesOfPorts>();

                NodeHandle untypedA = a, untypedB = b;

                Assert.AreEqual(0, set.GetTopologyDatabase().CountEstablishedConnections(), "There are valid connections in a new set with zero connections");

                if (meansOfConnection == ConnectionAPI.StronglyTyped)
                    set.Connect(a, NodeWithAllTypesOfPorts.SimulationPorts.MessageOut, b, NodeWithAllTypesOfPorts.SimulationPorts.MessageIn);
                else
                    set.Connect(a, set.GetDefinition(a).GetPortDescription(a).Outputs[0], b, set.GetDefinition(b).GetPortDescription(b).Inputs[0]);

                Assert.AreEqual(1, set.GetTopologyDatabase().CountEstablishedConnections(), "There isn't exactly one valid edge in a new set with one connection");

                var madeConnection = new Topology.Connection();

                Assert.IsFalse(madeConnection.Valid, "Default constructed connection is valid");

                int indexHandleCounter = 0, foundIndexHandle = 0;
                for(int i = 0; i < set.GetTopologyDatabase().TotalConnections; ++i)
                {
                    if (set.GetTopologyDatabase()[i].Valid)
                    {
                        madeConnection = set.GetTopologyDatabase()[i];
                        foundIndexHandle = indexHandleCounter;
                    }
                    indexHandleCounter++;
                }

                Assert.IsTrue(madeConnection.Valid, "Could not find the made connection");
                Assert.NotZero(foundIndexHandle, "Found connection cannot be the invalid slot");

                // check the connection is as it should be
                Assert.AreEqual((uint)PortDescription.Category.Message, madeConnection.TraversalFlags);
                Assert.AreEqual(untypedB, madeConnection.Destination.ToPublicHandle());
                Assert.AreEqual(untypedA, madeConnection.Source.ToPublicHandle());
                Assert.AreEqual(NodeWithAllTypesOfPorts.SimulationPorts.MessageOut.Port, madeConnection.SourceOutputPort);
                Assert.AreEqual(NodeWithAllTypesOfPorts.SimulationPorts.MessageIn.Port, madeConnection.DestinationInputPort.PortID);
                Assert.AreEqual(foundIndexHandle, madeConnection.HandleToSelf.Index);

                if (meansOfConnection == ConnectionAPI.StronglyTyped)
                    set.Disconnect(a, NodeWithAllTypesOfPorts.SimulationPorts.MessageOut, b, NodeWithAllTypesOfPorts.SimulationPorts.MessageIn);
                else
                    set.Disconnect(a, set.GetDefinition(a).GetPortDescription(a).Outputs[0], b, set.GetDefinition(b).GetPortDescription(b).Inputs[0]);

                Assert.AreEqual(0, set.GetTopologyDatabase().CountEstablishedConnections(), "There are valid connections in a new set with zero connections");

                set.Destroy(a, b);
            }
        }

        [TestCase(ConnectionAPI.StronglyTyped)]
        [TestCase(ConnectionAPI.WeaklyTyped)]
        public void DSLConnectionsMade_CausesConnectionTable_ToBePopulatedCorrectly_AndSubsequentlyRemoved(ConnectionAPI meansOfConnection)
        {
            using (var set = new NodeSet())
            {
                NodeHandle<NodeWithAllTypesOfPorts>
                    a = set.Create<NodeWithAllTypesOfPorts>(),
                    b = set.Create<NodeWithAllTypesOfPorts>();

                NodeHandle untypedA = a, untypedB = b;

                Assert.AreEqual(0, set.GetTopologyDatabase().CountEstablishedConnections(), "There are valid connections in a new set with zero connections");

                if (meansOfConnection == ConnectionAPI.StronglyTyped)
                    set.Connect(a, NodeWithAllTypesOfPorts.SimulationPorts.DSLOut, b, NodeWithAllTypesOfPorts.SimulationPorts.DSLIn);
                else
                    set.Connect(a, set.GetDefinition(a).GetPortDescription(a).Outputs[1], b, set.GetDefinition(b).GetPortDescription(b).Inputs[2]);

                Assert.AreEqual(1, set.GetTopologyDatabase().CountEstablishedConnections(), "There isn't exactly one valid edge in a new set with one connection");

                var madeConnection = new Topology.Connection();

                Assert.IsFalse(madeConnection.Valid, "Default constructed connection is valid");

                int indexHandleCounter = 0, foundIndexHandle = 0;
                for (int i = 0; i < set.GetTopologyDatabase().TotalConnections; ++i)
                {
                    if (set.GetTopologyDatabase()[i].Valid)
                    {
                        madeConnection = set.GetTopologyDatabase()[i];
                        foundIndexHandle = indexHandleCounter;
                    }
                    indexHandleCounter++;
                }

                Assert.IsTrue(madeConnection.Valid, "Could not find the made connection");
                Assert.NotZero(foundIndexHandle, "Found connection cannot be the invalid slot");

                // check the connection is as it should be
                Assert.AreEqual((uint)PortDescription.Category.DomainSpecific, madeConnection.TraversalFlags);
                Assert.AreEqual(untypedB, madeConnection.Destination.ToPublicHandle());
                Assert.AreEqual(untypedA, madeConnection.Source.ToPublicHandle());
                Assert.AreEqual(NodeWithAllTypesOfPorts.SimulationPorts.DSLOut.Port, madeConnection.SourceOutputPort);
                Assert.AreEqual(NodeWithAllTypesOfPorts.SimulationPorts.DSLIn.Port, madeConnection.DestinationInputPort.PortID);
                Assert.AreEqual(foundIndexHandle, madeConnection.HandleToSelf.Index);

                if (meansOfConnection == ConnectionAPI.StronglyTyped)
                {
                    set.Disconnect(a, NodeWithAllTypesOfPorts.SimulationPorts.DSLOut, b, NodeWithAllTypesOfPorts.SimulationPorts.DSLIn);
                }
                else
                    set.Disconnect(a, set.GetDefinition(a).GetPortDescription(a).Outputs[1], b, set.GetDefinition(b).GetPortDescription(b).Inputs[2]);

                Assert.AreEqual(0, set.GetTopologyDatabase().CountEstablishedConnections(), "There are valid connections in a new set with zero connections");

                set.Destroy(a, b);
            }
        }

        [TestCase(ConnectionAPI.StronglyTyped)]
        [TestCase(ConnectionAPI.WeaklyTyped)]
        public void DataConnectionsMade_CausesConnectionTable_ToBePopulatedCorrectly_AndSubsequentlyRemoved(ConnectionAPI meansOfConnection)
        {
            using (var set = new NodeSet())
            {
                NodeHandle<NodeWithAllTypesOfPorts>
                    a = set.Create<NodeWithAllTypesOfPorts>(),
                    b = set.Create<NodeWithAllTypesOfPorts>();

                NodeHandle untypedA = a, untypedB = b;

                Assert.AreEqual(0, set.GetTopologyDatabase().CountEstablishedConnections(), "There are valid connections in a new set with zero connections");

                if (meansOfConnection == ConnectionAPI.StronglyTyped)
                    set.Connect(a, NodeWithAllTypesOfPorts.KernelPorts.OutputScalar, b, NodeWithAllTypesOfPorts.KernelPorts.InputScalar);
                else
                    set.Connect(a, set.GetDefinition(a).GetPortDescription(a).Outputs[3], b, set.GetDefinition(b).GetPortDescription(b).Inputs[5]);

                Assert.AreEqual(1, set.GetTopologyDatabase().CountEstablishedConnections(), "There isn't exactly one valid edge in a new set with one connection");

                var madeConnection = new Topology.Connection();

                Assert.IsFalse(madeConnection.Valid, "Default constructed connection is valid");

                int indexHandleCounter = 0, foundIndexHandle = 0;
                for (int i = 0; i < set.GetTopologyDatabase().TotalConnections; ++i)
                {
                    if (set.GetTopologyDatabase()[i].Valid)
                    {
                        madeConnection = set.GetTopologyDatabase()[i];
                        foundIndexHandle = indexHandleCounter;
                    }
                    indexHandleCounter++;
                }

                Assert.IsTrue(madeConnection.Valid, "Could not find the made connection");
                Assert.NotZero(foundIndexHandle, "Found connection cannot be the invalid slot");

                // check the connection is as it should be
                Assert.AreEqual((uint)PortDescription.Category.Data, madeConnection.TraversalFlags);
                Assert.AreEqual(untypedB, madeConnection.Destination.ToPublicHandle());
                Assert.AreEqual(untypedA, madeConnection.Source.ToPublicHandle());
                // Fails for the same reason as MixedPortDeclarations_AreConsecutivelyNumbered_AndRespectsDeclarationOrder
                Assert.AreEqual(NodeWithAllTypesOfPorts.KernelPorts.InputScalar.Port, madeConnection.DestinationInputPort.PortID);
                Assert.AreEqual(NodeWithAllTypesOfPorts.KernelPorts.OutputScalar.Port, madeConnection.SourceOutputPort);
                Assert.AreEqual(foundIndexHandle, madeConnection.HandleToSelf.Index);

                if (meansOfConnection == ConnectionAPI.StronglyTyped)
                {
                    set.Disconnect(a, NodeWithAllTypesOfPorts.KernelPorts.OutputScalar, b, NodeWithAllTypesOfPorts.KernelPorts.InputScalar);
                }
                else
                    set.Disconnect(a, set.GetDefinition(a).GetPortDescription(a).Outputs[3], b, set.GetDefinition(b).GetPortDescription(b).Inputs[5]);

                Assert.AreEqual(0, set.GetTopologyDatabase().CountEstablishedConnections(), "There are valid connections in a new set with zero connections");

                set.Destroy(a, b);
            }
        }

    }

}
