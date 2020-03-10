using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Assertions;
using System.Runtime.CompilerServices;

namespace Unity.Animation
{
    public static partial class Core
    {
        const float k_Epsilon = 1e-5f;
        const float k_SqEpsilon = 1e-8f;

        public struct TwoBoneIKData
        {
            public RigidTransform Target;
            public RigidTransform TargetOffset;

            public float3 Hint;
            public float2 LimbLengths;

            public int RootIndex;
            public int MidIndex;
            public int TipIndex;

            public float TargetPositionWeight; // [0, 1]
            public float TargetRotationWeight; // [0, 1]
            public float HintWeight;           // [0, 1]

            public static TwoBoneIKData Default() => new TwoBoneIKData
            {
                Target               = RigidTransform.identity,
                TargetOffset         = RigidTransform.identity,
                Hint                 = float3.zero,
                LimbLengths          = float2.zero,
                RootIndex            = -1,
                MidIndex             = -1,
                TipIndex             = -1,
                TargetPositionWeight = 1f,
                TargetRotationWeight = 1f,
                HintWeight           = 0f
            };
        }

        public static void SolveTwoBoneIK(ref AnimationStream stream, in TwoBoneIKData data, float weight)
        {
            Assert.IsTrue(data.RootIndex > -1);
            Assert.IsTrue(data.MidIndex  > -1);
            Assert.IsTrue(data.TipIndex  > -1);

            weight = math.saturate(weight);
            if (!(weight > 0f))
                return;

            stream.GetLocalToRootTR(data.RootIndex, out float3 rootPos, out quaternion rootRot);
            stream.GetLocalToRootTR(data.MidIndex , out float3 midPos , out quaternion midRot);
            stream.GetLocalToRootTR(data.TipIndex , out float3 tipPos , out quaternion tipRot);

            float3 tPos = math.lerp(tipPos, data.Target.pos + data.TargetOffset.pos, math.saturate(data.TargetPositionWeight) * weight);
            quaternion tRot = mathex.lerp(tipRot, mathex.mul(data.Target.rot, data.TargetOffset.rot), math.saturate(data.TargetRotationWeight) * weight);
            float hintWeight = math.saturate(data.HintWeight) * weight;
            bool hasHint = hintWeight > 0f;

            float3 ab = midPos - rootPos;
            float3 bc = tipPos - midPos;
            float3 ac = tipPos - rootPos;
            float3 at = tPos   - rootPos;

            float prevAngle = TriangleAngle(math.length(ac), data.LimbLengths);
            float newAngle  = TriangleAngle(math.length(at), data.LimbLengths);

            // Bend normal strategy is to take whatever has been provided in the animation
            // stream to minimize configuration changes, however if this is collinear
            // try computing a bend normal given a hint (when provided) or otherwise the desired target position.
            // If all fails, math.up() is used.
            float3 axis = math.cross(ab, bc);
            if (math.lengthsq(axis) < k_SqEpsilon)
            {
                axis = math.select(float3.zero, math.cross(data.Hint - rootPos, bc), hasHint);
                if (math.lengthsq(axis) < k_SqEpsilon)
                    axis = math.cross(at, bc);
                if (math.lengthsq(axis) < k_SqEpsilon)
                    axis = math.up();
            }
            axis = math.normalize(axis);

            math.sincos(0.5f * (prevAngle - newAngle), out float sin, out float cos);
            quaternion deltaRot = new float4(axis * sin, cos);
            stream.SetLocalToRootRotation(data.MidIndex, mathex.mul(deltaRot, midRot));

            tipPos = stream.GetLocalToRootTranslation(data.TipIndex);
            ac = tipPos - rootPos;
            stream.SetLocalToRootRotation(data.RootIndex, mathex.mul(mathex.fromTo(ac, at), rootRot));

            if (hasHint)
            {
                float acLengthSq = math.lengthsq(ac);
                if (acLengthSq > 0f)
                {
                    midPos = stream.GetLocalToRootTranslation(data.MidIndex);
                    tipPos = stream.GetLocalToRootTranslation(data.TipIndex);
                    ab = midPos - rootPos;
                    ac = tipPos - rootPos;

                    float3 acNorm = ac * math.rsqrt(acLengthSq);
                    float3 ah = data.Hint - rootPos;
                    float3 abProj = ab - acNorm * math.dot(ab, acNorm);
                    float3 ahProj = ah - acNorm * math.dot(ah, acNorm);

                    float maxReach = data.LimbLengths.x + data.LimbLengths.y;
                    if (math.lengthsq(abProj) > (maxReach * maxReach * 0.001f) && math.lengthsq(ahProj) > 0f)
                    {
                        quaternion hintRot = mathex.fromTo(abProj, ahProj);
                        hintRot.value.xyz *= hintWeight;
                        stream.SetLocalToRootRotation(data.RootIndex, mathex.mul(hintRot, stream.GetLocalToRootRotation(data.RootIndex)));
                    }
                }
            }

            stream.SetLocalToRootRotation(data.TipIndex, tRot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float TriangleAngle(float aLen, float2 limbLengths) =>
            math.acos(math.clamp((math.lengthsq(limbLengths) - aLen * aLen) / (limbLengths.x * limbLengths.y) * 0.5f, -1f, 1f));

        static float Sum(NativeArray<float> array)
        {
            float sum = 0f;
            for (int i = 0; i < array.Length; ++i)
                sum += array[i];

            return sum;
        }

        public struct PositionConstraintData
        {
            public float3 LocalOffset;
            public bool3 LocalAxesMask;
            public int Index;

            public NativeArray<float3> SourcePositions;
            public NativeArray<float3> SourceOffsets;
            public NativeArray<float> SourceWeights;

            public static PositionConstraintData Default() => new PositionConstraintData
            {
                Index = -1,
                LocalAxesMask = new bool3(true),
                LocalOffset = float3.zero
            };
        }

        public static void SolvePositionConstraint(ref AnimationStream stream, in PositionConstraintData data, float weight)
        {
            Assert.IsTrue(data.Index > -1);
            Assert.IsTrue(data.SourcePositions.Length == data.SourceOffsets.Length);
            Assert.IsTrue(data.SourcePositions.Length == data.SourceWeights.Length);

            weight = math.saturate(weight);
            if (!(weight > 0f))
                return;

            float sumWeights = Sum(data.SourceWeights);
            if (sumWeights < k_Epsilon)
                return;

            float weightScale = math.select(1f, math.rcp(sumWeights), sumWeights > 1f);
            float3 currentT = stream.GetLocalToRootTranslation(data.Index);
            float3 accumT = currentT;

            for (int i = 0; i < data.SourcePositions.Length; ++i)
            {
                float normalizedWeight = data.SourceWeights[i] * weightScale;
                if (normalizedWeight < k_Epsilon)
                    continue;

                accumT += (data.SourcePositions[i] + data.SourceOffsets[i] - currentT) * normalizedWeight;
            }

            // Convert accumT to local space
            int parentIdx = stream.Rig.Value.Skeleton.ParentIndexes[data.Index];
            if (parentIdx != -1)
            {
                RigidTransform parentTx;
                stream.GetLocalToRootTR(parentIdx, out parentTx.pos, out parentTx.rot);
                accumT = math.transform(math.inverse(parentTx), accumT);
            }

            float3 currentLocalT = stream.GetLocalToParentTranslation(data.Index);
            if (!math.all(data.LocalAxesMask))
                accumT = math.select(currentLocalT, accumT, data.LocalAxesMask);

            stream.SetLocalToParentTranslation(data.Index, math.lerp(currentLocalT, accumT + data.LocalOffset, weight));
        }

        public struct RotationConstraintData
        {
            public quaternion LocalOffset;
            public bool3 LocalAxesMask;
            public int Index;

            public NativeArray<quaternion> SourceRotations;
            public NativeArray<quaternion> SourceOffsets;
            public NativeArray<float> SourceWeights;

            public static RotationConstraintData Default() => new RotationConstraintData
            {
                Index = -1,
                LocalOffset = quaternion.identity,
                LocalAxesMask = new bool3(true)
            };
        }

        public static void SolveRotationConstraint(ref AnimationStream stream, in RotationConstraintData data, float weight)
        {
            Assert.IsTrue(data.Index > -1);
            Assert.IsTrue(data.SourceRotations.Length == data.SourceOffsets.Length);
            Assert.IsTrue(data.SourceRotations.Length == data.SourceWeights.Length);

            weight = math.saturate(weight);
            if (!(weight > 0f))
                return;

            float sumWeights = Sum(data.SourceWeights);
            if (sumWeights < k_Epsilon)
                return;

            float weightScale = math.select(1f, math.rcp(sumWeights), sumWeights > 1f);
            float accumWeights = 0f;
            quaternion accumR = float4.zero;

            for (int i = 0; i < data.SourceRotations.Length; ++i)
            {
                float normalizedWeight = data.SourceWeights[i] * weightScale;
                if (normalizedWeight < k_Epsilon)
                    continue;

                quaternion addValue = mathex.mul(data.SourceRotations[i], data.SourceOffsets[i]);
                accumR = mathex.add(accumR, addValue.value * normalizedWeight);
                accumWeights += normalizedWeight;
            }

            accumR = math.normalizesafe(accumR);
            if (accumWeights < 1f)
                accumR = mathex.lerp(stream.GetLocalToRootRotation(data.Index), accumR, accumWeights);

            // Convert accumR to local space
            int parentIdx = stream.Rig.Value.Skeleton.ParentIndexes[data.Index];
            if (parentIdx != -1)
                accumR = mathex.mul(math.inverse(stream.GetLocalToRootRotation(parentIdx)), accumR);

            quaternion currentLocalR = stream.GetLocalToParentRotation(data.Index);
            if (!math.all(data.LocalAxesMask))
                accumR = quaternion.Euler(math.select(mathex.toEuler(currentLocalR), mathex.toEuler(accumR), data.LocalAxesMask));

            stream.SetLocalToParentRotation(data.Index, mathex.lerp(currentLocalR, mathex.mul(accumR, data.LocalOffset), weight));
        }

        public struct ParentConstraintData
        {
            public bool3 LocalTranslationAxesMask;
            public bool3 LocalRotationAxesMask;
            public int Index;

            public NativeArray<RigidTransform> SourceTx;
            public NativeArray<RigidTransform> SourceOffsets;
            public NativeArray<float> SourceWeights;

            public static ParentConstraintData Default() => new ParentConstraintData
            {
                Index = -1,
                LocalTranslationAxesMask = new bool3(true),
                LocalRotationAxesMask = new bool3(true)
            };
        }

        public static void SolveParentConstraint(ref AnimationStream stream, in ParentConstraintData data, float weight)
        {
            Assert.IsTrue(data.Index > -1);
            Assert.IsTrue(data.SourceTx.Length == data.SourceOffsets.Length);
            Assert.IsTrue(data.SourceTx.Length == data.SourceWeights.Length);

            weight = math.saturate(weight);
            if (!(weight > 0f))
                return;

            float sumWeights = Sum(data.SourceWeights);
            if (sumWeights < k_Epsilon)
                return;

            float weightScale = math.select(1f, math.rcp(sumWeights), sumWeights > 1f);
            float accumWeights = 0f;
            RigidTransform accumTx = new RigidTransform(float4.zero, float3.zero);

            for (int i = 0; i < data.SourceTx.Length; ++i)
            {
                float normalizedWeight = data.SourceWeights[i] * weightScale;
                if (normalizedWeight < k_Epsilon)
                    continue;

                RigidTransform sourceTx = math.mul(data.SourceTx[i], data.SourceOffsets[i]);
                accumTx.pos += sourceTx.pos * normalizedWeight;
                accumTx.rot = mathex.add(accumTx.rot, sourceTx.rot.value * normalizedWeight);
                accumWeights += normalizedWeight;
            }

            accumTx.rot = math.normalizesafe(accumTx.rot);
            if (accumWeights < 1f)
            {
                stream.GetLocalToRootTR(data.Index, out float3 currentT, out quaternion currentR);
                accumTx.pos += currentT * (1f - accumWeights);
                accumTx.rot = mathex.lerp(currentR, accumTx.rot, accumWeights);
            }

            // Convert accumTx to local space
            int parentIdx = stream.Rig.Value.Skeleton.ParentIndexes[data.Index];
            if (parentIdx != -1)
            {
                RigidTransform parentTx;
                stream.GetLocalToRootTR(parentIdx, out parentTx.pos, out parentTx.rot);
                accumTx = math.mul(math.inverse(parentTx), accumTx);
            }

            stream.GetLocalToParentTRS(data.Index, out float3 currentLocalT, out quaternion currentLocalR, out float3 currentLocalS);
            if (!math.all(data.LocalTranslationAxesMask))
                accumTx.pos = math.select(currentLocalT, accumTx.pos, data.LocalTranslationAxesMask);
            if (!math.all(data.LocalRotationAxesMask))
                accumTx.rot = quaternion.Euler(math.select(mathex.toEuler(currentLocalR), mathex.toEuler(accumTx.rot), data.LocalRotationAxesMask));

            stream.SetLocalToParentTRS(
                data.Index,
                math.lerp(currentLocalT, accumTx.pos, weight),
                mathex.lerp(currentLocalR, accumTx.rot, weight),
                currentLocalS
                );
        }

        public struct AimConstraintData
        {
            public quaternion LocalOffset;
            public float3 LocalAimAxis;
            public bool3 LocalAxesMask;
            public float MinAngleLimit; // radians
            public float MaxAngleLimit; // radians
            public int Index;

            public NativeArray<float3> SourcePositions;
            public NativeArray<quaternion> SourceOffsets;
            public NativeArray<float> SourceWeights;

            public static AimConstraintData Default() => new AimConstraintData
            {
                Index = -1,
                LocalOffset = quaternion.identity,
                LocalAimAxis = new float3(0f, 1f, 0f),
                LocalAxesMask = new bool3(true),
                MinAngleLimit = -math.PI,
                MaxAngleLimit = math.PI
            };
        }

        public static void SolveAimConstraint(
            ref AnimationStream stream,
            in AimConstraintData data,
            in float weight
            )
        {
            Assert.IsTrue(data.Index > -1);
            Assert.IsTrue(data.SourcePositions.Length == data.SourceOffsets.Length);
            Assert.IsTrue(data.SourcePositions.Length == data.SourceWeights.Length);

            if (!(weight > 0f))
                return;

            float sumWeights = Sum(data.SourceWeights);
            if (sumWeights < k_Epsilon)
                return;

            float weightScale = math.select(1f, math.rcp(sumWeights), sumWeights > 1f);
            int parentIdx = stream.Rig.Value.Skeleton.ParentIndexes[data.Index];
            quaternion parentInvR = parentIdx != -1 ? math.inverse(stream.GetLocalToRootRotation(parentIdx)) : quaternion.identity;
            bool hasMasks = !math.all(data.LocalAxesMask);

            float accumWeights = 0f;
            quaternion accumDeltaR = new quaternion(float4.zero);
            float3 currentT = stream.GetLocalToRootTranslation(data.Index);
            for (int i = 0; i < data.SourcePositions.Length; ++i)
            {
                float normalizedWeight = data.SourceWeights[i] * weightScale;
                if (normalizedWeight < k_Epsilon)
                    continue;

                float3 fromDir = data.LocalAimAxis;
                float3 toDir = math.mul(parentInvR, data.SourcePositions[i] - currentT);
                if (math.lengthsq(toDir) < k_Epsilon)
                    continue;

                float3 crossDir = math.cross(fromDir, toDir);
                if (hasMasks)
                {
                    crossDir = math.normalize(math.select(float3.zero, crossDir, data.LocalAxesMask));
                    if (math.lengthsq(crossDir) > k_Epsilon)
                    {
                        toDir = mathex.projectOnPlane(toDir, crossDir);
                    }
                    else
                    {
                        toDir = fromDir;
                    }
                }
                else
                {
                    crossDir = math.normalize(crossDir);
                }

                quaternion rotToSource = quaternion.AxisAngle(
                    crossDir,
                    math.clamp(mathex.angle(data.LocalAimAxis, toDir), data.MinAngleLimit, data.MaxAngleLimit)
                    );

                rotToSource = mathex.mul(data.SourceOffsets[i], rotToSource);
                accumDeltaR = mathex.add(accumDeltaR, rotToSource.value * normalizedWeight);
                accumWeights += normalizedWeight;
            }

            accumDeltaR = math.normalizesafe(accumDeltaR);
            if (accumWeights < 1f)
                accumDeltaR = mathex.lerp(quaternion.identity, accumDeltaR, accumWeights);

            stream.SetLocalToParentRotation(
                data.Index,
                mathex.lerp(stream.GetLocalToParentRotation(data.Index), mathex.mul(accumDeltaR, data.LocalOffset), weight)
                );
        }
    }
}
