using Unity.Animation;
using Unity.Animation.Hybrid;
using Unity.DataFlowGraph;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

#if UNITY_EDITOR
using UnityEngine;

public class UnityChanControllerGraph : AnimationGraphBase {
    public AnimationClip WalkLeftClip;
    public AnimationClip WalkForwardClip;
    public AnimationClip WalkRightClip;

    public AnimationClip RunLeftClip;
    public AnimationClip RunForwardClip;
    public AnimationClip RunRightClip;

    public string MotionName;
    public bool Bake;
    public float SampleRate = 60.0f;
    public bool LoopValues;
    public bool BankPivot;

    private StringHash motionId;

    public override void PreProcessData<T>(T data) {
        if (data is RigComponent) {
            var rig = data as RigComponent;

            for (var boneIter = 0; boneIter < rig.Bones.Length; ++boneIter) {
                if (MotionName == rig.Bones[boneIter].name) {
                    motionId = RigGenerator.ComputeRelativePath(rig.Bones[boneIter], rig.transform);
                }
            }
        }
    }

    public override void AddGraphSetupComponent(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
        var walkLeftDenseClip = ClipBuilder.AnimationClipToDenseClip(WalkLeftClip);
        var walkForwardDenseClip = ClipBuilder.AnimationClipToDenseClip(WalkForwardClip);
        var walkRightDenseClip = ClipBuilder.AnimationClipToDenseClip(WalkRightClip);
        var runLeftDenseClip = ClipBuilder.AnimationClipToDenseClip(RunLeftClip);
        var runForwardDenseClip = ClipBuilder.AnimationClipToDenseClip(RunForwardClip);
        var runRightDenseClip = ClipBuilder.AnimationClipToDenseClip(RunRightClip);
        var clipConfiguration = new ClipConfiguration();
        clipConfiguration.Mask = ClipConfigurationMask.LoopTime | ClipConfigurationMask.CycleRootMotion | ClipConfigurationMask.DeltaRootMotion;
        clipConfiguration.Mask |= LoopValues ? ClipConfigurationMask.LoopValues : 0;
        clipConfiguration.Mask |= BankPivot ? ClipConfigurationMask.BankPivot : 0;
        clipConfiguration.MotionID = motionId;
 
        var graphSetup = new UnityChanControllerSetup {
            WalkLeftClip = walkLeftDenseClip,
            WalkForwardClip = walkForwardDenseClip,
            WalkRightClip = walkRightDenseClip,
            RunLeftClip = runLeftDenseClip,
            RunForwardClip = runForwardDenseClip,
            RunRightClip = runRightDenseClip,
        };
        
        if (Bake) {
            var rigDefinition = dstManager.GetComponentData<Rig>(entity);
            graphSetup.WalkLeftClip = UberClipNode.Bake(rigDefinition.Value, walkLeftDenseClip, clipConfiguration, SampleRate);
            graphSetup.WalkForwardClip = UberClipNode.Bake(rigDefinition.Value, walkForwardDenseClip, clipConfiguration, SampleRate);
            graphSetup.WalkRightClip = UberClipNode.Bake(rigDefinition.Value, walkRightDenseClip, clipConfiguration, SampleRate);
            graphSetup.RunLeftClip = UberClipNode.Bake(rigDefinition.Value, runLeftDenseClip, clipConfiguration, SampleRate);
            graphSetup.RunForwardClip = UberClipNode.Bake(rigDefinition.Value, runForwardDenseClip, clipConfiguration, SampleRate);
            graphSetup.RunRightClip = UberClipNode.Bake(rigDefinition.Value, runRightDenseClip, clipConfiguration, SampleRate);
            clipConfiguration.Mask = ClipConfigurationMask.NormalizedTime | ClipConfigurationMask.LoopTime | ClipConfigurationMask.RootMotionFromVelocity;
            clipConfiguration.MotionID = 0;
        } else {
            clipConfiguration.Mask |= ClipConfigurationMask.NormalizedTime;
        }

        graphSetup.Configuration = clipConfiguration;
        dstManager.AddComponentData(entity, graphSetup);
    }
}
#endif

public struct UnityChanControllerSetup : ISampleSetup {
    public BlobAssetReference<Clip> WalkLeftClip;
    public BlobAssetReference<Clip> WalkForwardClip;
    public BlobAssetReference<Clip> WalkRightClip;

    public BlobAssetReference<Clip> RunLeftClip;
    public BlobAssetReference<Clip> RunForwardClip;
    public BlobAssetReference<Clip> RunRightClip;

    public ClipConfiguration Configuration;
}

public struct UnityChanControllerData : ISampleData
{
    public NodeHandle<ComponentNode>                              EntityNode;
    public NodeHandle<DeltaTimeNode>                              DeltaTimeNode;
    public NodeHandle<TimeCounterNode>                            TimeCounterNode;
    public NodeHandle<DirectionMixerNode>                         MixerWalkNode;
    public NodeHandle<DirectionMixerNode>                         MixerRunNode;
    public NodeHandle<MixerNode>                                  MixerSpeedNode;
    public NodeHandle<RootMotionNode>                             RootMotionNode;
    public NodeHandle<UnityChanControllerDataInputNode>           ControllerDataInputNode;
    public NodeHandle<ConvertLocalToWorldComponentToFloat4x4Node> LocalToWorldToFloat4x4Node;
    public NodeHandle<ConvertFloat4x4ToLocalToWorldComponentNode> Float4x4ToLocalToWorldNode;

    public RigidTransform FollowX;

    public float Direction;
    public float DirectionDamped;
    public float Speed;
    public float SpeedDamped;

    public int Player;
}

public struct UnityChanControllerDataInput : IComponentData
{
    public float MixerWalkJobBlend;
    public float TimeCounterSpeed;
    public float MixerSpeedBlend;
}

[UpdateBefore(typeof(PreAnimationSystemGroup))]
public class UnityChanControllerSystem : SampleSystemBase<
    UnityChanControllerSetup,
    UnityChanControllerData,
    PreAnimationGraphTag,
    PreAnimationGraphSystem
    >
{
    protected override UnityChanControllerData CreateGraph(Entity entity, ref Rig rig, PreAnimationGraphSystem graphSystem, ref UnityChanControllerSetup setup)
    {
        var set = graphSystem.Set;
        var data = new UnityChanControllerData();

        data.EntityNode = set.CreateComponentNode(entity);
        data.DeltaTimeNode = set.Create<DeltaTimeNode>();
        data.TimeCounterNode = set.Create<TimeCounterNode>();
        data.MixerWalkNode = set.Create<DirectionMixerNode>();
        data.MixerRunNode = set.Create<DirectionMixerNode>();
        data.MixerSpeedNode = set.Create<MixerNode>();
        data.RootMotionNode = set.Create<RootMotionNode>();
        data.ControllerDataInputNode = set.Create<UnityChanControllerDataInputNode>();
        data.LocalToWorldToFloat4x4Node = set.Create<ConvertLocalToWorldComponentToFloat4x4Node>();
        data.Float4x4ToLocalToWorldNode = set.Create<ConvertFloat4x4ToLocalToWorldComponentNode>();

        data.Direction = 2.0f;
        data.Speed = 0.0f;

        set.Connect(data.DeltaTimeNode, DeltaTimeNode.KernelPorts.DeltaTime, data.TimeCounterNode, TimeCounterNode.KernelPorts.DeltaTime);
        set.Connect(data.TimeCounterNode, TimeCounterNode.KernelPorts.OutputDeltaTime, data.MixerWalkNode, DirectionMixerNode.KernelPorts.DeltaTime);
        set.Connect(data.TimeCounterNode, TimeCounterNode.KernelPorts.Time, data.MixerWalkNode, DirectionMixerNode.KernelPorts.Time);
        set.Connect(data.TimeCounterNode, TimeCounterNode.KernelPorts.OutputDeltaTime, data.MixerRunNode, DirectionMixerNode.KernelPorts.DeltaTime);
        set.Connect(data.TimeCounterNode, TimeCounterNode.KernelPorts.Time, data.MixerRunNode, DirectionMixerNode.KernelPorts.Time);

        set.Connect(data.MixerWalkNode, DirectionMixerNode.KernelPorts.Output, data.MixerSpeedNode, MixerNode.KernelPorts.Input0);
        set.Connect(data.MixerRunNode, DirectionMixerNode.KernelPorts.Output, data.MixerSpeedNode, MixerNode.KernelPorts.Input1);
        set.Connect(data.MixerSpeedNode, MixerNode.KernelPorts.Output, data.RootMotionNode, RootMotionNode.KernelPorts.Input);
        set.Connect(data.RootMotionNode, RootMotionNode.KernelPorts.Output, data.EntityNode);

        set.Connect(data.EntityNode, data.LocalToWorldToFloat4x4Node, ConvertLocalToWorldComponentToFloat4x4Node.KernelPorts.Input, NodeSet.ConnectionType.Feedback);
        set.Connect(data.LocalToWorldToFloat4x4Node, ConvertLocalToWorldComponentToFloat4x4Node.KernelPorts.Output, data.RootMotionNode, RootMotionNode.KernelPorts.PrevRootX);
        set.Connect(data.EntityNode, data.ControllerDataInputNode, UnityChanControllerDataInputNode.KernelPorts.Input, NodeSet.ConnectionType.Feedback);
        set.Connect(data.RootMotionNode, RootMotionNode.KernelPorts.RootX, data.Float4x4ToLocalToWorldNode, ConvertFloat4x4ToLocalToWorldComponentNode.KernelPorts.Input);
        set.Connect(data.Float4x4ToLocalToWorldNode, ConvertFloat4x4ToLocalToWorldComponentNode.KernelPorts.Output, data.EntityNode);

        set.Connect(data.ControllerDataInputNode, UnityChanControllerDataInputNode.KernelPorts.MixerWalkJobBlend, data.MixerWalkNode, DirectionMixerNode.KernelPorts.Weight);
        set.Connect(data.ControllerDataInputNode, UnityChanControllerDataInputNode.KernelPorts.MixerWalkJobBlend, data.MixerRunNode, DirectionMixerNode.KernelPorts.Weight);
        set.Connect(data.ControllerDataInputNode, UnityChanControllerDataInputNode.KernelPorts.TimeCounterSpeed, data.TimeCounterNode, TimeCounterNode.KernelPorts.Speed);
        set.Connect(data.ControllerDataInputNode, UnityChanControllerDataInputNode.KernelPorts.MixerSpeedBlend, data.MixerSpeedNode, MixerNode.KernelPorts.Weight);

        set.SendMessage(data.MixerWalkNode, DirectionMixerNode.SimulationPorts.Rig, rig);
        set.SendMessage(data.MixerWalkNode, DirectionMixerNode.SimulationPorts.ClipConfiguration, setup.Configuration);
        set.SendMessage(data.MixerWalkNode, DirectionMixerNode.SimulationPorts.Clip0, setup.WalkLeftClip);
        set.SendMessage(data.MixerWalkNode, DirectionMixerNode.SimulationPorts.Clip1, setup.WalkForwardClip);
        set.SendMessage(data.MixerWalkNode, DirectionMixerNode.SimulationPorts.Clip2, setup.WalkRightClip);
        set.SendMessage(data.MixerRunNode, DirectionMixerNode.SimulationPorts.Rig, rig);
        set.SendMessage(data.MixerRunNode, DirectionMixerNode.SimulationPorts.ClipConfiguration, setup.Configuration);
        set.SendMessage(data.MixerRunNode, DirectionMixerNode.SimulationPorts.Clip0, setup.RunLeftClip);
        set.SendMessage(data.MixerRunNode, DirectionMixerNode.SimulationPorts.Clip1, setup.RunForwardClip);
        set.SendMessage(data.MixerRunNode, DirectionMixerNode.SimulationPorts.Clip2, setup.RunRightClip);

        set.SendMessage(data.MixerSpeedNode, MixerNode.SimulationPorts.Rig, rig);
        set.SendMessage(data.RootMotionNode, RootMotionNode.SimulationPorts.Rig, rig);

        PostUpdateCommands.AddComponent<UnityChanControllerDataInput>(entity);
        PostUpdateCommands.AddComponent(entity, graphSystem.Tag);

        return data;
    }

    protected override void DestroyGraph(Entity entity, PreAnimationGraphSystem graphSystem, ref UnityChanControllerData data)
    {
        var set = graphSystem.Set;
        set.Destroy(data.DeltaTimeNode);
        set.Destroy(data.TimeCounterNode);
        set.Destroy(data.MixerWalkNode);
        set.Destroy(data.MixerRunNode);
        set.Destroy(data.MixerSpeedNode);
        set.Destroy(data.RootMotionNode);
        set.Destroy(data.EntityNode);
        set.Destroy(data.ControllerDataInputNode);
        set.Destroy(data.LocalToWorldToFloat4x4Node);
        set.Destroy(data.Float4x4ToLocalToWorldNode);
    }
}

[UpdateBefore(typeof(PreAnimationSystemGroup))]
public class UnityChanControllerApplyState : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDep)
    {
        var dampWeight = Time.DeltaTime / 0.5f;
        var time = Time.ElapsedTime;

        return Entities
            .ForEach((Entity entity, ref LocalToWorld localToWorld, ref UnityChanControllerData data, ref UnityChanControllerDataInput input) =>
            {
                var rootX = new RigidTransform(localToWorld.Value);

                if (data.Player == 0)
                {
                    var rand = new Unity.Mathematics.Random((uint)entity.Index + (uint)math.fmod(time * 1000, 1000));

                    data.Direction += rand.NextBool() ? -0.1f : 0.1f;
                    data.Direction = math.clamp(data.Direction, 0, 4);

                    data.Speed += rand.NextBool() ? -0.1f : 0.1f;
                    data.Speed = math.clamp(data.Speed, 0, 1);
                }

                data.Player = 0;

                data.DirectionDamped = math.lerp(data.DirectionDamped, data.Direction, dampWeight);
                input.MixerWalkJobBlend = data.DirectionDamped;

                data.SpeedDamped = math.lerp(data.SpeedDamped, data.Speed, dampWeight);
                input.TimeCounterSpeed = 1.0f + 0.5f * data.SpeedDamped;
                input.MixerSpeedBlend = data.SpeedDamped;

                data.FollowX.pos = math.lerp(data.FollowX.pos, rootX.pos, dampWeight);
                data.FollowX.rot = mathex.lerp(math.normalizesafe(data.FollowX.rot), rootX.rot, dampWeight);
            }).Schedule(inputDep);
    }
}

public class UnityChanControllerDataInputNode
    : NodeDefinition<UnityChanControllerDataInputNode.Data, UnityChanControllerDataInputNode.SimPorts, UnityChanControllerDataInputNode.KernelData, UnityChanControllerDataInputNode.KernelDefs, UnityChanControllerDataInputNode.Kernel>
{
    public struct SimPorts : ISimulationPortDefinition { }

    public struct KernelDefs : IKernelPortDefinition
    {
        public DataInput<UnityChanControllerDataInputNode, UnityChanControllerDataInput> Input;

        public DataOutput<UnityChanControllerDataInputNode, float> MixerWalkJobBlend;
        public DataOutput<UnityChanControllerDataInputNode, float> TimeCounterSpeed;
        public DataOutput<UnityChanControllerDataInputNode, float> MixerSpeedBlend;
    }

    public struct Data : INodeData { }

    public struct KernelData : IKernelData { }

    [BurstCompile]
    public struct Kernel : IGraphKernel<KernelData, KernelDefs>
    {
        public void Execute(RenderContext ctx, KernelData data, ref KernelDefs ports)
        {
            var input = ctx.Resolve(ports.Input);
            ctx.Resolve(ref ports.MixerWalkJobBlend) = input.MixerWalkJobBlend;
            ctx.Resolve(ref ports.TimeCounterSpeed)  = input.TimeCounterSpeed;
            ctx.Resolve(ref ports.MixerSpeedBlend)   = input.MixerSpeedBlend;
        }
    }
}