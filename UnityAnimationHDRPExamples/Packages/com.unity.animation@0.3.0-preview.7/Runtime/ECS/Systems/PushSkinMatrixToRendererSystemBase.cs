using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Animation
{
    internal struct ComputeBufferIndex : ISystemStateComponentData
    {
        public int Value;
    }

    public abstract class PrepareSkinMatrixToRendererSystemBase : JobComponentSystem
    {
        static readonly ProfilerMarker k_Marker = new ProfilerMarker("PrepareSkinMatrixToRendererSystemBase");

        const string k_ShaderPropertyName = "_SkinMatrices";
        const int    k_ChunkSize = 2048;

        internal class PrepareSkinMatrixToRendererSystemMainThread : ComponentSystem
        {
            static readonly ProfilerMarker k_Marker = new ProfilerMarker("PrepareSkinMatrixToRendererSystem");

            public PrepareSkinMatrixToRendererSystemBase Parent;

            protected override void OnUpdate()
            {
                k_Marker.Begin();

                bool doResize = false;

                Entities
                    .WithNone<SkinMatrix, BoneIndexOffset>()
                    .WithAll<ComputeBufferIndex>()
                    .ForEach((Entity e) =>
                    {
                        EntityManager.RemoveComponent<ComputeBufferIndex>(e);
                        doResize = true;
                    });

                if (doResize)
                {
                    Parent.m_Offset = 0;
                    Entities
                        .WithAll<SkinMatrix, BoneIndexOffset, ComputeBufferIndex>()
                        .ForEach((Entity e, DynamicBuffer<SkinMatrix> skinMatrices) =>
                        {
                            PostUpdateCommands.SetComponent(e, new ComputeBufferIndex { Value = Parent.m_Offset });
                            Parent.m_Offset += skinMatrices.Length;
                        });
                }

                Entities
                    .WithAll<SkinMatrix, BoneIndexOffset>()
                    .WithNone<ComputeBufferIndex>()
                    .ForEach(
                    (Entity e, DynamicBuffer<SkinMatrix> skinMatrices, ref BoneIndexOffset boneIndexOffset) =>
                    {
                        PostUpdateCommands.AddComponent(e, new ComputeBufferIndex { Value = Parent.m_Offset });
                        Parent.m_Offset += skinMatrices.Length;
                    });

                var size = Parent.m_Buffer.count;
                if ((size < Parent.m_Offset) || (size - Parent.m_Offset > k_ChunkSize))
                {
                    Parent.m_Buffer.Dispose();
                    Parent.m_Buffer = new ComputeBuffer(
                        ((Parent.m_Offset / k_ChunkSize) + 1) * k_ChunkSize,
                        UnsafeUtility.SizeOf<float3x4>(),
                        ComputeBufferType.Default
                        );
                }

                k_Marker.End();
            }
        }

        PrepareSkinMatrixToRendererSystemMainThread m_PrepareSkinMatrixToRendererSystemMainThread;

        EntityQuery m_Query;
        ComputeBuffer m_Buffer;
        NativeArray<float3x4> m_AllMatrices;
        int m_Offset;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_Query = GetEntityQuery(
                ComponentType.ReadOnly<SkinMatrix>(),
                ComponentType.ReadWrite<BoneIndexOffset>()
                );

            m_Buffer = new ComputeBuffer(k_ChunkSize, UnsafeUtility.SizeOf<float3x4>(), ComputeBufferType.Default);
            m_Offset = 0;

            m_PrepareSkinMatrixToRendererSystemMainThread = World.GetOrCreateSystem<PrepareSkinMatrixToRendererSystemMainThread>();
            m_PrepareSkinMatrixToRendererSystemMainThread.Parent = this;
        }

        protected override void OnDestroy()
        {
            m_Buffer.Dispose();
            if (m_AllMatrices.IsCreated)
                m_AllMatrices.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            k_Marker.Begin();

            m_PrepareSkinMatrixToRendererSystemMainThread.Update();
            if (m_Offset == 0)
            {
                k_Marker.End();
                return inputDeps;
            }

            if (m_AllMatrices.Length != m_Buffer.count)
            {
                if (m_AllMatrices.IsCreated)
                    m_AllMatrices.Dispose();

                m_AllMatrices = new NativeArray<float3x4>(m_Buffer.count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
            inputDeps = new FlattenSkinMatricesJob { SkinMatricesBuffer = m_AllMatrices }.Schedule(this, inputDeps);

            k_Marker.End();
            return inputDeps;
        }

        internal void AssignGlobalBufferToShader()
        {
            if (m_Offset == 0)
                return;

            m_Buffer.SetData(m_AllMatrices, 0, 0, m_AllMatrices.Length);
            Shader.SetGlobalBuffer(k_ShaderPropertyName, m_Buffer);
        }

        [BurstCompile]
        unsafe struct FlattenSkinMatricesJob : IJobForEachWithEntity_EBCC<SkinMatrix, BoneIndexOffset, ComputeBufferIndex>
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float3x4> SkinMatricesBuffer;

            public void Execute(Entity entity, int index, DynamicBuffer<SkinMatrix> skinMatrices, ref BoneIndexOffset boneIndexOffset, ref ComputeBufferIndex computeBufferIndex)
            {
                UnsafeUtility.MemCpy(
                    (float3x4*)SkinMatricesBuffer.GetUnsafePtr() + computeBufferIndex.Value,
                    skinMatrices.GetUnsafePtr(),
                    skinMatrices.Length * UnsafeUtility.SizeOf<float3x4>()
                    );

                boneIndexOffset = new BoneIndexOffset { Value = computeBufferIndex.Value };
            }
        }
    }

    public abstract class FinalizePushSkinMatrixToRendererSystemBase : ComponentSystem
    {
        EntityQuery m_Query;

        protected override void OnCreate()
        {
            m_Query = GetEntityQuery(
                ComponentType.ReadWrite<SkinMatrix>(),
                ComponentType.ReadWrite<BoneIndexOffset>()
                );
        }

        protected abstract PrepareSkinMatrixToRendererSystemBase PrepareSkinMatrixToRenderSystem { get; }

        protected override void OnUpdate()
        {
            if (PrepareSkinMatrixToRenderSystem != null)
                PrepareSkinMatrixToRenderSystem.AssignGlobalBufferToShader();
        }
    }
}
