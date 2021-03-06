using System;

namespace Unity.DataFlowGraph
{
    public partial class NodeSet
    {
        /// <summary>
        /// See <see cref="Connect(NodeHandle, OutputPortID, NodeHandle, InputPortID, ConnectionType)"/>
        /// </summary>
        public void Connect<TMsg, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            MessageOutput<TSource, TMsg> sourcePort,
            NodeHandle<TDestination> destHandle,
            MessageInput<TDestination, TMsg> destPort
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition, IMsgHandler<TMsg>
        {
            Connect((PortDescription.Category.Message, typeof(TMsg), ConnectionType.Normal), new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPort.Port)));
        }

        /// <summary>
        /// Overload of <see cref="Connect(NodeHandle, OutputPortID, NodeHandle, InputPortID, ConnectionType)"/>
        /// targeting a destination port array with an index parameter.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range with respect to the port array.</exception>
        public void Connect<TMsg, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            MessageOutput<TSource, TMsg> sourcePort,
            NodeHandle<TDestination> destHandle,
            PortArray<MessageInput<TDestination, TMsg>> destPortArray,
            int index
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition, IMsgHandler<TMsg>
        {
            Connect((PortDescription.Category.Message, typeof(TMsg), ConnectionType.Normal), new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPortArray.Port, index)));
        }

        /// <summary>
        /// See <see cref="Connect(NodeHandle, OutputPortID, NodeHandle, InputPortID, ConnectionType)"/>
        /// </summary>
        public void Connect<TDSLHandler, TDSL, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DSLOutput<TSource, TDSLHandler, TDSL> sourcePort,
            NodeHandle<TDestination> destHandle,
            DSLInput<TDestination, TDSLHandler, TDSL> destPort
        )
            where TSource : NodeDefinition, TDSL
            where TDestination : NodeDefinition, TDSL
            where TDSLHandler : DSLHandler<TDSL>, new()
            where TDSL : class
        {
            Connect((PortDescription.Category.DomainSpecific, typeof(TDSLHandler), ConnectionType.Normal), new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPort.Port)));
        }

        /// <summary>
        /// See <see cref="Connect(NodeHandle, OutputPortID, NodeHandle, InputPortID, ConnectionType)"/>
        /// </summary>
        public void Connect<TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeHandle<TDestination> destHandle,
            DataInput<TDestination, TType> destPort,
            ConnectionType connectionType = ConnectionType.Normal
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition
            where TType : struct
        {
            Connect((PortDescription.Category.Data, typeof(TType), connectionType), new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPort.Port)));
        }

        /// <summary>
        /// Overload of <see cref="Connect(NodeHandle, OutputPortID, NodeHandle, InputPortID, ConnectionType)"/>
        /// targeting a destination port array with an index parameter.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range with respect to the port array.</exception>
        public void Connect<TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeHandle<TDestination> destHandle,
            PortArray<DataInput<TDestination, TType>> destPortArray,
            int index,
            ConnectionType connectionType = ConnectionType.Normal
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition
            where TType : struct
        {
            Connect((PortDescription.Category.Data, typeof(TType), connectionType), new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPortArray.Port, index)));
        }

        /// <summary>
        /// See <see cref="Disconnect(NodeHandle, OutputPortID, NodeHandle, InputPortID)"/>
        /// </summary>
        public void Disconnect<TMsg, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            MessageOutput<TSource, TMsg> sourcePort,
            NodeHandle<TDestination> destHandle,
            MessageInput<TDestination, TMsg> destPort
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition, IMsgHandler<TMsg>
        {
            Disconnect(new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPort.Port)));
        }

        /// <summary>
        /// Overload of <see cref="Disconnect(NodeHandle, OutputPortID, NodeHandle, InputPortID)"/>
        /// targeting a destination port array with an index parameter.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range with respect to the port array.</exception>
        public void Disconnect<TMsg, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            MessageOutput<TSource, TMsg> sourcePort,
            NodeHandle<TDestination> destHandle,
            PortArray<MessageInput<TDestination, TMsg>> destPortArray,
            int index
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition, IMsgHandler<TMsg>
        {
            Disconnect(new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPortArray.Port, index)));
        }

        /// <summary>
        /// See <see cref="Disconnect(NodeHandle, OutputPortID, NodeHandle, InputPortID)"/>
        /// </summary>
        public void Disconnect<TDSLHandler, TDSL, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DSLOutput<TSource, TDSLHandler, TDSL> sourcePort,
            NodeHandle<TDestination> destHandle,
            DSLInput<TDestination, TDSLHandler, TDSL> destPort
        )
            where TSource : NodeDefinition, TDSL
            where TDestination : NodeDefinition, TDSL
            where TDSLHandler : DSLHandler<TDSL>, new()
            where TDSL : class
        {
            Disconnect(new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPort.Port)));
        }

        /// <summary>
        /// See <see cref="Disconnect(NodeHandle, OutputPortID, NodeHandle, InputPortID)"/>
        /// </summary>
        public void Disconnect<TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeHandle<TDestination> destHandle,
            DataInput<TDestination, TType> destPort
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition
            where TType : struct
        {
            Disconnect(new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPort.Port)));
        }

        /// <summary>
        /// Overload of <see cref="Disconnect(NodeHandle, OutputPortID, NodeHandle, InputPortID)"/>
        /// targeting a destination port array with an index parameter.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range with respect to the port array.</exception>
        public void Disconnect<TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeHandle<TDestination> destHandle,
            PortArray<DataInput<TDestination, TType>> destPortArray,
            int index
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition
            where TType : struct
        {
            Disconnect(new OutputPair(this, sourceHandle, sourcePort.Port), new InputPair(this, destHandle, new InputPortArrayID(destPortArray.Port, index)));
        }

        /// <summary>
        /// See <see cref="DisconnectAndRetainValue(NodeHandle,vOutputPortID, NodeHandle, InputPortID)"/>
        /// </summary>
        public void DisconnectAndRetainValue<TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeHandle<TDestination> destHandle,
            DataInput<TDestination, TType> destPort
        )
            where TSource : NodeDefinition, new()
            where TDestination : NodeDefinition, new()
            where TType : struct
        {
            DisconnectAndRetainValue(sourceHandle, sourcePort, destHandle, new InputPortArrayID(destPort.Port));
        }

        /// <summary>
        /// Overload of <see cref="DisconnectAndRetainValue(NodeHandle, OutputPortID, NodeHandle, InputPortID)"/>
        /// targeting a port array with an index parameter.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range with respect to the port array.</exception>
        public void DisconnectAndRetainValue<TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeHandle<TDestination> destHandle,
            PortArray<DataInput<TDestination, TType>> destPortArray,
            int index
        )
            where TSource : NodeDefinition, new()
            where TDestination : NodeDefinition, new()
            where TType : struct
        {
            DisconnectAndRetainValue(sourceHandle, sourcePort, destHandle, new InputPortArrayID(destPortArray.Port, index));
        }

        public void Connect<TTask, TDSLHandler, TDSL, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DSLOutput<TSource, TDSLHandler, TDSL> sourcePort,
            NodeInterfaceLink<TTask, TDestination> destHandle
        )
            where TSource : NodeDefinition, TDSL
            where TDestination : NodeDefinition, TTask, new()
            where TTask : ITaskPort<TTask>
            where TDSLHandler : DSLHandler<TDSL>, new()
            where TDSL : class
        {
            Connect(sourceHandle, sourcePort.Port, destHandle, GetDefinition(destHandle.TypedHandle).GetPort(destHandle));
        }

        public void Connect<TTask, TMsg, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            MessageOutput<TSource, TMsg> sourcePort,
            NodeInterfaceLink<TTask, TDestination> destHandle
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition, TTask, IMsgHandler<TMsg>, new()
            where TTask : ITaskPort<TTask>
        {
            Connect(sourceHandle, sourcePort.Port, destHandle, GetDefinition(destHandle.TypedHandle).GetPort(destHandle));
        }

        public void Connect<TTask>(
            NodeHandle sourceHandle,
            OutputPortID sourcePort,
            NodeInterfaceLink<TTask> destHandle
        )
            where TTask : ITaskPort<TTask>
        {
            var f = GetDefinition(destHandle);
            if (f is TTask task)
            {
                Connect(sourceHandle, sourcePort, destHandle, task.GetPort(destHandle));
            }
            else
                throw new InvalidOperationException(
                    $"Cannot connect source to destination. Destination not of type {typeof(TTask).Name}");
        }

        public void Connect<TTask, TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeInterfaceLink<TTask, TDestination> destHandle
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition, TTask, new()
            where TTask : ITaskPort<TTask>
            where TType : struct
        {
            Connect(sourceHandle, sourcePort.Port, destHandle, GetDefinition(destHandle.TypedHandle).GetPort(destHandle));
        }

        public void Disconnect<TTask, TDSLHandler, TDSL, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DSLOutput<TSource, TDSLHandler, TDSL> sourcePort,
            NodeInterfaceLink<TTask, TDestination> destHandle
        )
            where TSource : NodeDefinition, TDSL
            where TDestination : NodeDefinition, TDSL, TTask, new()
            where TDSLHandler : DSLHandler<TDSL>, new()
            where TDSL : class
            where TTask : ITaskPort<TTask>
        {
            Disconnect(sourceHandle, sourcePort.Port, destHandle, GetDefinition(destHandle.TypedHandle).GetPort(destHandle));
        }

        public void Disconnect<TTask, TMsg, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            MessageOutput<TSource, TMsg> sourcePort,
            NodeInterfaceLink<TTask, TDestination> destHandle
        )
            where TSource : NodeDefinition
            where TTask : ITaskPort<TTask>
            where TDestination : NodeDefinition, TTask, IMsgHandler<TMsg>, new()
        {
            Disconnect(sourceHandle, sourcePort.Port, destHandle, GetDefinition(destHandle.TypedHandle).GetPort(destHandle));
        }

        public void Disconnect<TTask, TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeInterfaceLink<TTask, TDestination> destHandle
        )
            where TSource : NodeDefinition
            where TDestination : NodeDefinition, TTask, new()
            where TType : struct
            where TTask : ITaskPort<TTask>
        {
            Disconnect(sourceHandle, sourcePort.Port, destHandle, GetDefinition(destHandle.TypedHandle).GetPort(destHandle));
        }

        public void Disconnect<TTask>(
            NodeHandle sourceHandle,
            OutputPortID sourcePort,
            NodeInterfaceLink<TTask> destHandle
        )
            where TTask : ITaskPort<TTask>
        {
            var f = GetDefinition(destHandle);
            if (f is TTask task)
            {
                Disconnect(sourceHandle, sourcePort, destHandle, task.GetPort(destHandle));
            }
            else
                throw new InvalidOperationException(
                    $"Cannot disconnect source from destination. Destination not of type {typeof(TTask).Name}");
        }

        /// <summary>
        /// See <see cref="DisconnectAndRetainValue(NodeHandle, OutputPortID, NodeHandle, InputPortID)"/>
        /// </summary>
        void DisconnectAndRetainValue<TType, TSource, TDestination>(
            NodeHandle<TSource> sourceHandle,
            DataOutput<TSource, TType> sourcePort,
            NodeHandle<TDestination> destHandle,
            InputPortArrayID destPort
        )
            where TSource : NodeDefinition, new()
            where TDestination : NodeDefinition, new()
            where TType : struct
        {
            var source = new OutputPair(this, sourceHandle, sourcePort.Port);
            var dest = new InputPair(this, destHandle, destPort);

            var portDef = GetFormalPort(dest);

            if (portDef.HasBuffers)
                throw new InvalidOperationException($"Cannot retain data on a data port which includes buffers");

            Disconnect(source, dest);
            m_Diff.RetainData(dest);
        }
    }
}
