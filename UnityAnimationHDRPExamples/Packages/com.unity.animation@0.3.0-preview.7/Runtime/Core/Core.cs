using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine.Assertions;

namespace Unity.Animation
{
    static public partial class Core
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void SetDataInSample<T>(ref BlobArray<float> samples, int offset, T data)
            where T : unmanaged
        {
            *(T*)((float*)samples.GetUnsafePtr() + offset) = data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ref T GetDataInSample<T>(ref BlobArray<float> samples, int offset)
            where T : unmanaged
        {
            return ref *(T*)((float*)samples.GetUnsafePtr() + offset);
        }

        /// Find a binding in a binding array.
        public static int FindBindingIndex(ref BlobArray<StringHash> bindings, StringHash searchedBinding)
        {
            // TODO : Optimize
            var i = 0;
            while (i < bindings.Length && !bindings[i].Equals(searchedBinding))
                ++i;
            return i < bindings.Length ? i : -1;
        }

        unsafe static public void Blend(
            ref AnimationStream output,
            ref AnimationStream input1,
            ref AnimationStream input2,
            float weight,
            NativeArray<WeightData> weightMasks
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input1.Rig.Value.GetHashCode());
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input2.Rig.Value.GetHashCode());
            Assert.AreEqual(WeightDataSize(output.Rig), weightMasks.Length);

            int wIdx = 0;
            float4* wMask = (float4*)weightMasks.GetUnsafeReadOnlyPtr();

            // Blend 4-wide lerp non rotation data
            float4* input1Data = input1.GetDataChunkUnsafePtr();
            float4* input2Data = input2.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i, ++wIdx)
            {
                outputData[i] = math.lerp(input1Data[i], input2Data[i], wMask[wIdx] * weight);
            }

            // Blend 4-wide rotations
            quaternion4* input1Rot = input1.GetRotationChunkUnsafePtr();
            quaternion4* input2Rot = input2.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i, ++wIdx)
            {
                outputRot[i] = mathex.lerp(input1Rot[i], input2Rot[i], wMask[wIdx] * weight);
            }
        }

        unsafe static public void Blend(
            ref AnimationStream output,
            ref AnimationStream input1,
            ref AnimationStream input2,
            float weight
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input1.Rig.Value.GetHashCode());
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input2.Rig.Value.GetHashCode());

            // Blend 4 wide lerp non rotation data
            float4* input1Data = input1.GetDataChunkUnsafePtr();
            float4* input2Data = input2.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i)
            {
                outputData[i] = math.lerp(input1Data[i], input2Data[i], weight);
            }

            // Blend 4-wide rotations
            quaternion4* inputRot1 = input1.GetRotationChunkUnsafePtr();
            quaternion4* inputRot2 = input2.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i)
            {
                outputRot[i] = mathex.lerp(inputRot1[i], inputRot2[i], weight);
            }
        }

        static unsafe public void BlendOverrideLayer(
            ref AnimationStream output,
            ref AnimationStream input,
            float weight,
            NativeArray<WeightData> weightMasks
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input.Rig.Value.GetHashCode());
            Assert.AreEqual(WeightDataSize(output.Rig), weightMasks.Length);

            int wIdx = 0;
            float4* wMask = (float4*)weightMasks.GetUnsafeReadOnlyPtr();

            // Blend 4-wide lerp non rotation data
            float4* inputData  = input.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i, ++wIdx)
            {
                outputData[i] = math.lerp(outputData[i], inputData[i], wMask[wIdx] * weight);
            }

            // Blend 4-wide rotations
            quaternion4* inputRot  = input.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i, ++wIdx)
            {
                outputRot[i] = mathex.lerp(outputRot[i], inputRot[i], wMask[wIdx] * weight);
            }
        }

        static unsafe public void BlendOverrideLayer(
            ref AnimationStream output,
            ref AnimationStream input,
            float weight
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input.Rig.Value.GetHashCode());

            // Blend 4-wide lerp non rotation data
            float4* inputData  = input.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i)
            {
                outputData[i] = math.lerp(outputData[i], inputData[i], weight);
            }

            // Blend 4-wide rotations
            quaternion4* inputRot  = input.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i)
            {
                outputRot[i] = mathex.lerp(outputRot[i], inputRot[i], weight);
            }
        }

        static unsafe public void BlendAdditiveLayer(
            ref AnimationStream output,
            ref AnimationStream input,
            float weight,
            NativeArray<WeightData> weightMasks
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input.Rig.Value.GetHashCode());
            Assert.AreEqual(WeightDataSize(output.Rig), weightMasks.Length);

            int wIdx = 0;
            float4* wMask = (float4*)weightMasks.GetUnsafeReadOnlyPtr();

            // Blend 4-wide lerp non rotation data
            float4* inputData  = input.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i, ++wIdx)
            {
                outputData[i] = math.mad(inputData[i], wMask[wIdx] * weight, outputData[i]);
            }

            // Blend 4-wide rotations
            quaternion4* inputRot  = input.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i, ++wIdx)
            {
                outputRot[i] = mathex.mul(outputRot[i], mathex.quatWeight(inputRot[i], wMask[wIdx] * weight));
            }
        }

        static unsafe public void BlendAdditiveLayer(
            ref AnimationStream output,
            ref AnimationStream input,
            float weight
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input.Rig.Value.GetHashCode());

            ref var bindings = ref output.Rig.Value.Bindings;

            // Blend 4-wide lerp non rotation data
            float4* inputData  = input.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i)
            {
                outputData[i] = math.mad(inputData[i], weight, outputData[i]);
            }

            // Blend 4-wide rotations
            quaternion4* inputRot  = input.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i)
            {
                outputRot[i] = mathex.mul(outputRot[i], mathex.quatWeight(inputRot[i], weight));
            }
        }

        static public void RigRemapper(
            BlobAssetReference<RigRemapTable> remapTable,
            ref AnimationStream destinationStream,
            ref AnimationStream sourceStream
            )
        {
            ref var remap = ref remapTable.Value;

            for (int i=0;i<remap.TranslationMappings.Length;i++)
            {
                ref var mapping = ref remap.TranslationMappings[i];
                var value = sourceStream.GetLocalToParentTranslation(mapping.SourceIndex);

                var offsetIndex = remapTable.Value.TranslationMappings[i].OffsetIndex;

                if (offsetIndex > 0)
                {
                    var translationOffset = remapTable.Value.TranslationOffsets[offsetIndex];
                    value = math.mul(translationOffset.Rotation, value * translationOffset.Scale);
                }

                destinationStream.SetLocalToParentTranslation(mapping.DestinationIndex, value);
            }

            for (int i=0;i<remap.RotationMappings.Length;i++)
            {
                ref var mapping = ref remap.RotationMappings[i];
                var value = sourceStream.GetLocalToParentRotation(mapping.SourceIndex);

                var offsetIndex = remapTable.Value.RotationMappings[i].OffsetIndex;

                if (offsetIndex > 0)
                {
                    var rotationOffset = remapTable.Value.RotationOffsets[offsetIndex];
                    value = math.mul(math.mul(rotationOffset.PreRotation, value), rotationOffset.PostRotation);
                }

                destinationStream.SetLocalToParentRotation(mapping.DestinationIndex, value);
            }

            for (int i=0;i<remap.ScaleMappings.Length;i++)
            {
                ref var mapping = ref remap.ScaleMappings[i];
                var value = sourceStream.GetLocalToParentScale(mapping.SourceIndex);
                destinationStream.SetLocalToParentScale(mapping.DestinationIndex, value);
            }

            for (int i=0;i<remap.FloatMappings.Length;i++)
            {
                ref var mapping = ref remap.FloatMappings[i];
                var value = sourceStream.GetFloat(mapping.SourceIndex);
                destinationStream.SetFloat(mapping.DestinationIndex, value);
            }

            for (int i=0;i<remap.IntMappings.Length;i++)
            {
                ref var mapping = ref remap.IntMappings[i];
                var value = sourceStream.GetInt(mapping.SourceIndex);
                destinationStream.SetInt(mapping.DestinationIndex, value);
            }
        }

        static private float WeightForIndex(ref BlobArray<float> thresholdArray, int length, int index, float blend)
        {
            if (blend >= thresholdArray[index])
            {
                if (index + 1 == length)
                {
                    return 1.0f;
                }
                else if (thresholdArray[index + 1] < blend)
                {
                    return 0.0f;
                }
                else
                {
                    if (thresholdArray[index] - thresholdArray[index + 1] != 0)
                    {
                        return (blend - thresholdArray[index + 1]) / (thresholdArray[index] - thresholdArray[index + 1]);
                    }
                    else
                    {
                        return 1.0f;
                    }
                }
            }
            else
            {
                if (index == 0)
                {
                    return 1.0f;
                }
                else if (thresholdArray[index - 1] > blend)
                {
                    return 0.0f;
                }
                else
                {
                    if ((thresholdArray[index] - thresholdArray[index - 1]) != 0)
                    {
                        return (blend - thresholdArray[index - 1]) / (thresholdArray[index] - thresholdArray[index - 1]);
                    }
                    else
                    {
                        return 1.0f;
                    }
                }
            }
        }

        static public void ComputeBlendTree1DWeights(BlobAssetReference<BlendTree1D> blendTree, float blendParameter, ref NativeArray<float> outWeights)
        {
            var length = blendTree.Value.MotionThresholds.Length;

            var blend = math.clamp(blendParameter, blendTree.Value.MotionThresholds[0], blendTree.Value.MotionThresholds[length - 1]);
            for(int i=0;i<length;i++)
            {
                outWeights[i] = WeightForIndex(ref blendTree.Value.MotionThresholds, length, i, blend);
            }
        }

        static public float ComputeBlendTree1DDuration(BlobAssetReference<BlendTree1D> blendTree, ref NativeArray<float> weights)
        {
            var length = blendTree.Value.MotionThresholds.Length;

            var duration = 0.0f;
            for(int i=0;i<length;i++)
            {
                duration += weights[i] * blendTree.Value.Motions[i].Clip.Value.Duration / blendTree.Value.MotionSpeeds[i];
            }

            return duration;
        }

        static public unsafe void ComputeBlendTree2DSimpleDirectionalWeights(BlobAssetReference<BlendTree2DSimpleDirectional> blendTree, float2 blendParameter, ref NativeArray<float> outWeights)
        {
            var length = blendTree.Value.Motions.Length;

            UnsafeUtility.MemClear(outWeights.GetUnsafePtr(), UnsafeUtility.SizeOf<float>() * outWeights.Length);

            // Handle fallback
            if (length < 2)
            {
                if (length == 1)
                    outWeights[0] = 1.0F;
                return;
            }

            // Handle special case when sampled exactly in the middle
            if (math.all(blendParameter == float2.zero))
            {
                // If we have a center motion, give that one all the weight
                for (int i = 0; i < length; i++)
                {
                    if (math.all(blendTree.Value.MotionPositions[i] == float2.zero))
                    {
                        outWeights[i] = 1;
                        return;
                    }
                }

                // Otherwise divide weight evenly
                float sharedWeight = 1.0f / length;
                for (int i = 0; i < length; i++)
                    outWeights[i] = sharedWeight;
                return;
            }

            int indexA = -1;
            int indexB = -1;
            int indexCenter = -1;
            float maxDotForNegCross = -100000.0f;
            float maxDotForPosCross = -100000.0f;
            for (int i = 0; i < length; i++)
            {
                if (math.all(blendTree.Value.MotionPositions[i] == float2.zero))
                {
                    if (indexCenter >= 0)
                        return;
                    indexCenter = i;
                    continue;
                }
                var posNormalized = math.normalize(blendTree.Value.MotionPositions[i]);
                float dot = math.dot(posNormalized, blendParameter);
                float det = posNormalized.x * blendParameter.y - posNormalized.y * blendParameter.x;
                if (det > 0)
                {
                    if (dot > maxDotForPosCross)
                    {
                        maxDotForPosCross = dot;
                        indexA = i;
                    }
                }
                else
                {
                    if (dot > maxDotForNegCross)
                    {
                        maxDotForNegCross = dot;
                        indexB = i;
                    }
                }
            }

            float centerWeight = 0.0F;

            if (indexA < 0 || indexB < 0)
            {
                // Fallback if sampling point is not inside a triangle
                centerWeight = 1;
            }
            else
            {
                var a = blendTree.Value.MotionPositions[indexA];
                var b = blendTree.Value.MotionPositions[indexB];

                // Calculate weights using barycentric coordinates
                // (formulas from http://en.wikipedia.org/wiki/Barycentric_coordinate_system_%28mathematics%29 )
                float det = b.y * a.x - b.x * a.y;        // Simplified from: (b.y-0)*(a.x-0) + (0-b.x)*(a.y-0);
                float wA = (b.y * blendParameter.x - b.x * blendParameter.y) / det; // Simplified from: ((b.y-0)*(l.x-0) + (0-b.x)*(l.y-0)) / det;
                float wB = (a.x * blendParameter.y - a.y * blendParameter.x) / det; // Simplified from: ((0-a.y)*(l.x-0) + (a.x-0)*(l.y-0)) / det;
                centerWeight = 1 - wA - wB;

                // Clamp to be inside triangle
                if (centerWeight < 0)
                {
                    centerWeight = 0;
                    float sum = wA + wB;
                    wA /= sum;
                    wB /= sum;
                }
                else if (centerWeight > 1)
                {
                    centerWeight = 1;
                    wA = 0;
                    wB = 0;
                }

                // Give weight to the two vertices on the periphery that are closest
                outWeights[indexA] = wA;
                outWeights[indexB] = wB;
            }

            if (indexCenter >= 0)
            {
                outWeights[indexCenter] = centerWeight;
            }
            else
            {
                // Give weight to all children when input is in the center
                float sharedWeight = 1.0f / length;
                for (int i = 0; i < length; i++)
                    outWeights[i] += sharedWeight * centerWeight;
            }
        }

        static public float ComputeBlendTree2DSimpleDirectionalDuration(BlobAssetReference<BlendTree2DSimpleDirectional> blendTree, ref NativeArray<float> weights)
        {
            var length = blendTree.Value.MotionPositions.Length;

            var duration = 0.0f;
            for(int i=0;i<length;i++)
            {
                duration += weights[i] * blendTree.Value.Motions[i].Clip.Value.Duration / blendTree.Value.MotionSpeeds[i];
            }

            return duration;
        }

        public struct ClipKeyframe
        {
            public int Left;
            public int Right;
            public float Weight;

            static public ClipKeyframe Create(ref Clip clip, float time)
            {
                var curveCount = clip.Bindings.CurveCount;
                var sampleIndex = math.clamp(time, 0, clip.Duration) * clip.SampleRate;

                return new ClipKeyframe {
                    Left = (int)math.floor(sampleIndex) * curveCount,
                    Right = (int)math.ceil(sampleIndex) * curveCount,
                    Weight = sampleIndex - math.floor(sampleIndex)
                };
            }
        }

        static unsafe public void EvaluateClip(BlobAssetReference<ClipInstance> clipInstance, float time, ref AnimationStream stream, int additive)
        {
            Assert.AreEqual(clipInstance.Value.RigDefinition.Value.GetHashCode(), stream.Rig.Value.GetHashCode());

            if(additive != 0)
                AnimationStreamUtils.MemClear(ref stream);
            else
                AnimationStreamUtils.SetDefaultValues(ref stream);

            ref var clip = ref clipInstance.Value.Clip;
            ref var bindings = ref clip.Bindings;
            var keyframe = ClipKeyframe.Create(ref clip, time);

            float* scratchPtr = stackalloc float[16];
            float* samplesPtr = (float*)clip.Samples.GetUnsafePtr();

            // Rotations
            {
                int i = 0;
                int count = bindings.RotationBindings.Length;
                int curveIndex = bindings.RotationSamplesOffset;
                for (; i + 4 < count; i += 4, curveIndex += BindingSet.RotationKeyFloatCount * 4)
                {
                    var leftKey  = (quaternion*)(samplesPtr + curveIndex + keyframe.Left);
                    var rightKey = (quaternion*)(samplesPtr + curveIndex + keyframe.Right);
                    var result   = (quaternion*)scratchPtr;

                    for (int j = 0; j < 4; ++j)
                    {
                        result[j] = mathex.lerp(leftKey[j], rightKey[j], keyframe.Weight);
                    }

                    var i0 = clipInstance.Value.RotationBindingMap[i + 0];
                    var i1 = clipInstance.Value.RotationBindingMap[i + 1];
                    var i2 = clipInstance.Value.RotationBindingMap[i + 2];
                    var i3 = clipInstance.Value.RotationBindingMap[i + 3];

                    Assert.IsTrue(i0 != -1);
                    Assert.IsTrue(i1 != -1);
                    Assert.IsTrue(i2 != -1);
                    Assert.IsTrue(i3 != -1);

                    stream.SetLocalToParentRotation(i0, result[0]);
                    stream.SetLocalToParentRotation(i1, result[1]);
                    stream.SetLocalToParentRotation(i2, result[2]);
                    stream.SetLocalToParentRotation(i3, result[3]);
                }

                for (; i < count; ++i, curveIndex += BindingSet.RotationKeyFloatCount)
                {
                    var index = clipInstance.Value.RotationBindingMap[i];
                    Assert.IsTrue(index != -1);

                    ref var leftKey = ref GetDataInSample<quaternion>(ref clip.Samples, curveIndex + keyframe.Left);
                    ref var rightKey = ref GetDataInSample<quaternion>(ref clip.Samples, curveIndex + keyframe.Right);
                    stream.SetLocalToParentRotation(index, mathex.lerp(leftKey, rightKey, keyframe.Weight));
                }
            }

            // Translations
            {
                int i = 0;
                int count = bindings.TranslationBindings.Length;
                int curveIndex = bindings.TranslationSamplesOffset;
                for (; i + 5 < count; i += 5, curveIndex += BindingSet.TranslationKeyFloatCount * 5)
                {
                    var leftKey  = (float4*)(samplesPtr + curveIndex + keyframe.Left);
                    var rightKey = (float4*)(samplesPtr + curveIndex + keyframe.Right);
                    var result   = (float4*)scratchPtr;

                    for (int j = 0; j < 4; ++j)
                    {
                        result[j] = math.lerp(leftKey[j], rightKey[j], keyframe.Weight);
                    }

                    var i0 = clipInstance.Value.TranslationBindingMap[i + 0];
                    var i1 = clipInstance.Value.TranslationBindingMap[i + 1];
                    var i2 = clipInstance.Value.TranslationBindingMap[i + 2];
                    var i3 = clipInstance.Value.TranslationBindingMap[i + 3];
                    var i4 = clipInstance.Value.TranslationBindingMap[i + 4];

                    Assert.IsTrue(i0 != -1);
                    Assert.IsTrue(i1 != -1);
                    Assert.IsTrue(i2 != -1);
                    Assert.IsTrue(i3 != -1);
                    Assert.IsTrue(i4 != -1);

                    var float3Data = (float3*)scratchPtr;
                    stream.SetLocalToParentTranslation(i0, float3Data[0]);
                    stream.SetLocalToParentTranslation(i1, float3Data[1]);
                    stream.SetLocalToParentTranslation(i2, float3Data[2]);
                    stream.SetLocalToParentTranslation(i3, float3Data[3]);
                    stream.SetLocalToParentTranslation(i4, float3Data[4]);
                }

                for (; i < count; ++i, curveIndex += BindingSet.TranslationKeyFloatCount)
                {
                    var index = clipInstance.Value.TranslationBindingMap[i];
                    Assert.IsTrue(index != -1);

                    ref var leftKey = ref GetDataInSample<float3>(ref clip.Samples, curveIndex + keyframe.Left);
                    ref var rightKey = ref GetDataInSample<float3>(ref clip.Samples, curveIndex + keyframe.Right);
                    stream.SetLocalToParentTranslation(index, math.lerp(leftKey, rightKey, keyframe.Weight));
                }
            }

            // Scales
            {
                int i = 0;
                int count = bindings.ScaleBindings.Length;
                int curveIndex = bindings.ScaleSamplesOffset;
                for (; i + 5 < count; i += 5, curveIndex += BindingSet.ScaleKeyFloatCount * 5)
                {
                    var leftKey  = (float4*)(samplesPtr + curveIndex + keyframe.Left);
                    var rightKey = (float4*)(samplesPtr + curveIndex + keyframe.Right);
                    var result   = (float4*)scratchPtr;

                    for (int j = 0; j < 4; ++j)
                    {
                        result[j] = math.lerp(leftKey[j], rightKey[j], keyframe.Weight);
                    }

                    var i0 = clipInstance.Value.ScaleBindingMap[i + 0];
                    var i1 = clipInstance.Value.ScaleBindingMap[i + 1];
                    var i2 = clipInstance.Value.ScaleBindingMap[i + 2];
                    var i3 = clipInstance.Value.ScaleBindingMap[i + 3];
                    var i4 = clipInstance.Value.ScaleBindingMap[i + 4];

                    Assert.IsTrue(i0 != -1);
                    Assert.IsTrue(i1 != -1);
                    Assert.IsTrue(i2 != -1);
                    Assert.IsTrue(i3 != -1);
                    Assert.IsTrue(i4 != -1);

                    var float3Data = (float3*)scratchPtr;
                    stream.SetLocalToParentScale(i0, float3Data[0]);
                    stream.SetLocalToParentScale(i1, float3Data[1]);
                    stream.SetLocalToParentScale(i2, float3Data[2]);
                    stream.SetLocalToParentScale(i3, float3Data[3]);
                    stream.SetLocalToParentScale(i4, float3Data[4]);
                }

                for (; i < count; ++i, curveIndex += BindingSet.ScaleKeyFloatCount)
                {
                    var index = clipInstance.Value.ScaleBindingMap[i];
                    Assert.IsTrue(index != -1);

                    ref var leftKey = ref GetDataInSample<float3>(ref clip.Samples, curveIndex + keyframe.Left);
                    ref var rightKey = ref GetDataInSample<float3>(ref clip.Samples, curveIndex + keyframe.Right);
                    stream.SetLocalToParentScale(index, math.lerp(leftKey, rightKey, keyframe.Weight));
                }
            }

            // Floats
            {
                int i = 0;
                int count = bindings.FloatBindings.Length;
                int curveIndex = bindings.FloatSamplesOffset;
                for (; i + 4 < count; i += 4, curveIndex += BindingSet.FloatKeyFloatCount * 4)
                {
                    var leftKey  = (float4*)(samplesPtr + curveIndex + keyframe.Left);
                    var rightKey = (float4*)(samplesPtr + curveIndex + keyframe.Right);
                    var result   = (float4*)scratchPtr;

                    *result = math.lerp(*leftKey, *rightKey, keyframe.Weight);

                    var i0 = clipInstance.Value.FloatBindingMap[i + 0];
                    var i1 = clipInstance.Value.FloatBindingMap[i + 1];
                    var i2 = clipInstance.Value.FloatBindingMap[i + 2];
                    var i3 = clipInstance.Value.FloatBindingMap[i + 3];

                    Assert.IsTrue(i0 != -1);
                    Assert.IsTrue(i1 != -1);
                    Assert.IsTrue(i2 != -1);
                    Assert.IsTrue(i3 != -1);

                    stream.SetFloat(i0, scratchPtr[0]);
                    stream.SetFloat(i1, scratchPtr[1]);
                    stream.SetFloat(i2, scratchPtr[2]);
                    stream.SetFloat(i3, scratchPtr[3]);
                }

                for (; i < count; ++i, curveIndex += BindingSet.FloatKeyFloatCount)
                {
                    var index = clipInstance.Value.FloatBindingMap[i];
                    Assert.IsTrue(index != -1);

                    var leftKey = clip.Samples[curveIndex + keyframe.Left];
                    var rightKey = clip.Samples[curveIndex + keyframe.Right];
                    stream.SetFloat(index, math.lerp(leftKey, rightKey, keyframe.Weight));
                }
            }

            // Ints
            {
                int i = 0;
                int count = count = bindings.IntBindings.Length;
                int curveIndex = bindings.IntSamplesOffset;

                // TODO: Find the algorithm we want. Right now take the left most key.
                for (; i + 4 < count; i += 4, curveIndex += BindingSet.IntKeyFloatCount * 4)
                {
                    var leftKey = *(int4*)(samplesPtr + curveIndex + keyframe.Left);

                    var i0 = clipInstance.Value.IntBindingMap[i + 0];
                    var i1 = clipInstance.Value.IntBindingMap[i + 1];
                    var i2 = clipInstance.Value.IntBindingMap[i + 2];
                    var i3 = clipInstance.Value.IntBindingMap[i + 3];

                    Assert.IsTrue(i0 != -1);
                    Assert.IsTrue(i1 != -1);
                    Assert.IsTrue(i2 != -1);
                    Assert.IsTrue(i3 != -1);

                    stream.SetInt(i0, leftKey.x);
                    stream.SetInt(i0, leftKey.y);
                    stream.SetInt(i0, leftKey.z);
                    stream.SetInt(i0, leftKey.w);
                }

                for (; i < count; ++i, curveIndex += BindingSet.IntKeyFloatCount)
                {
                    var index = clipInstance.Value.IntBindingMap[i];
                    Assert.IsTrue(index != -1);

                    var leftKey = clip.Samples[curveIndex + keyframe.Left];
                    stream.SetInt(index, (int)leftKey);
                }
            }
        }

        internal static float AdjustLastFrameValue(float beforeLastValue, float atDurationValue, float lastFrameError)
        {
            return lastFrameError < 1.0f ? math.lerp(beforeLastValue, atDurationValue, 1.0f/(1.0f-lastFrameError)) : atDurationValue;
        }

        internal static float3 AdjustLastFrameValue(float3 beforeLastValue, float3 atDurationValue, float lastFrameError)
        {
            return lastFrameError < 1.0f ? math.lerp(beforeLastValue, atDurationValue, 1.0f/(1.0f-lastFrameError)) : atDurationValue;
        }

        internal static float4 AdjustLastFrameValue(float4 beforeLastValue, float4 atDurationValue, float lastFrameError)
        {
            return lastFrameError < 1.0f ? math.lerp(beforeLastValue, atDurationValue, 1.0f/(1.0f-lastFrameError)) : atDurationValue;
        }
        static public void MixerBegin(ref AnimationStream output)
        {
            AnimationStreamUtils.MemClear(ref output);
        }

        static unsafe public void MixerEnd(
            ref AnimationStream output,
            ref AnimationStream input,
            ref AnimationStream defaultPoseInput,
            float sumWeight
            )
        {
            if(sumWeight < 1.0F)
            {
                if(defaultPoseInput.IsNull)
                {
                    var defaultPoseStream = AnimationStream.FromDefaultValues(output.Rig);
                    MixerAdd(ref output, ref input, ref defaultPoseStream, 1.0F - sumWeight, 0);
                }
                else
                    MixerAdd(ref output, ref input, ref defaultPoseInput, 1.0F - sumWeight, 0);
            }
            else
                AnimationStreamUtils.MemCpy(ref output, ref input);

            // Normalize 4-wide rotations
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i)
            {
                outputRot[i] = mathex.normalizesafe(outputRot[i]);
            }
        }

        static unsafe public float MixerAdd(
            ref AnimationStream output,
            ref AnimationStream input,
            ref AnimationStream add,
            float weight,
            float sumWeight
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.IsFalse(input.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input.Rig.Value.GetHashCode());

            if (weight > 0.0f && !add.IsNull)
            {
                // Add 4-wide non rotational data
                float4* inputData  = input.GetDataChunkUnsafePtr();
                float4* addData    = add.GetDataChunkUnsafePtr();
                float4* outputData = output.GetDataChunkUnsafePtr();
                for (int i = 0, count = output.DataChunkCount; i < count; ++i)
                {
                    outputData[i] = math.mad(addData[i], weight, inputData[i]);
                }

                // Add 4-wide rotations
                quaternion4* inputRot  = input.GetRotationChunkUnsafePtr();
                quaternion4* addRot    = add.GetRotationChunkUnsafePtr();
                quaternion4* outputRot = output.GetRotationChunkUnsafePtr();

                for (int i = 0, count = output.RotationChunkCount; i < count; ++i)
                {
                    outputRot[i] = mathex.add(inputRot[i], addRot[i] * weight);
                }
                sumWeight += weight;
            }
            else
            {
                AnimationStreamUtils.MemCpy(ref output, ref input);
            }

            return sumWeight;
        }

        static unsafe public void AddPose(
            ref AnimationStream output,
            ref AnimationStream inputA,
            ref AnimationStream inputB
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.IsFalse(inputA.IsNull);
            Assert.IsFalse(inputB.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), inputA.Rig.Value.GetHashCode());
            Assert.AreEqual(output.Rig.Value.GetHashCode(), inputB.Rig.Value.GetHashCode());

            float4* inputAData = inputA.GetDataChunkUnsafePtr();
            float4* inputBData = inputB.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i)
            {
                outputData[i] = inputAData[i] + inputBData[i];
            }

            // 4-wide add
            quaternion4* inputARot = inputA.GetRotationChunkUnsafePtr();
            quaternion4* inputBRot = inputB.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i)
            {
                outputRot[i] = mathex.mul(inputARot[i], inputBRot[i]);
            }
        }

        static unsafe public void InversePose(
            ref AnimationStream output,
            ref AnimationStream input
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.IsFalse(input.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input.Rig.Value.GetHashCode());

            // 4-wide inverse
            float4* inputData  = input.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i)
            {
                outputData[i] = inputData[i] * -1f;
            }

            // Inverse 4-wide rotations
            quaternion4* inputRot  = input.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i)
            {
                outputRot[i] = mathex.conjugate(inputRot[i]);
            }
        }

        static unsafe public void WeightPose(
            ref AnimationStream output,
            ref AnimationStream input,
            NativeArray<WeightData> weights
            )
        {
            Assert.IsFalse(output.IsNull);
            Assert.IsFalse(input.IsNull);
            Assert.AreEqual(output.Rig.Value.GetHashCode(), input.Rig.Value.GetHashCode());
            Assert.AreEqual(WeightDataSize(output.Rig), weights.Length);

            int wIdx = 0;
            float4* weight = (float4*)weights.GetUnsafeReadOnlyPtr();

            // Blend 4-wide lerp non rotation data
            float4* inputData  = input.GetDataChunkUnsafePtr();
            float4* outputData = output.GetDataChunkUnsafePtr();
            for (int i = 0, count = output.DataChunkCount; i < count; ++i, ++wIdx)
            {
                outputData[i] = inputData[i] * weight[wIdx];
            }

            // Blend 4-wide rotations
            quaternion4* inputRot  = input.GetRotationChunkUnsafePtr();
            quaternion4* outputRot = output.GetRotationChunkUnsafePtr();
            for (int i = 0, count = output.RotationChunkCount; i < count; ++i, ++wIdx)
            {
                outputRot[i] = mathex.quatWeight(inputRot[i], weight[wIdx]);
            }
        }

        static public void ComputeLocalToWorld(
            float4x4 localToWorld,
            ref AnimationStream stream,
            NativeArray<float4x4> outLocalToWorlds
            )
        {
            if (stream.IsNull)
                return;

            int count = stream.Rig.Value.Skeleton.BoneCount;
            Assert.AreEqual(outLocalToWorlds.Length, count);

            // Compute world space transforms
            outLocalToWorlds[0] = math.mul(localToWorld, stream.GetLocalToParentMatrix(0));
            for (int i = 1; i != count; ++i)
            {
                var pIdx = stream.Rig.Value.Skeleton.ParentIndexes[i];
                outLocalToWorlds[i] = math.mul(outLocalToWorlds[pIdx], stream.GetLocalToParentMatrix(i));
            }
        }

        static public void ComputeLocalToRoot(
            ref AnimationStream stream,
            NativeArray<float4x4> outLocalToRoots
            )
        {
            if (stream.IsNull)
                return;

            int count = stream.Rig.Value.Skeleton.BoneCount;
            Assert.AreEqual(outLocalToRoots.Length, count);

            // Compute object space transforms
            outLocalToRoots[0] = stream.GetLocalToParentMatrix(0);
            for (int i = 1; i != count; ++i)
            {
                var pIdx = stream.Rig.Value.Skeleton.ParentIndexes[i];
                outLocalToRoots[i] = math.mul(outLocalToRoots[pIdx], stream.GetLocalToParentMatrix(i));
            }
        }

        static public void ComputeLocalToWorldAndRoot(
            float4x4 localToWorld,
            ref AnimationStream stream,
            NativeArray<float4x4> outLocalToWorlds,
            NativeArray<float4x4> outLocalToRoots
            )
        {
            if (stream.IsNull)
                return;

            int count = stream.Rig.Value.Skeleton.BoneCount;
            Assert.AreEqual(outLocalToWorlds.Length, count);
            Assert.AreEqual(outLocalToRoots.Length, count);

            var mat = stream.GetLocalToParentMatrix(0);
            outLocalToWorlds[0] = math.mul(localToWorld, mat);
            outLocalToRoots[0] = mat;

            // Compute object and world transforms
            for (int i = 1; i != count; ++i)
            {
                var pIdx = stream.Rig.Value.Skeleton.ParentIndexes[i];
                mat = stream.GetLocalToParentMatrix(i);

                outLocalToWorlds[i] = math.mul(outLocalToWorlds[pIdx], mat);
                outLocalToRoots[i] = math.mul(outLocalToRoots[pIdx], mat);
            }
        }
    }
}

