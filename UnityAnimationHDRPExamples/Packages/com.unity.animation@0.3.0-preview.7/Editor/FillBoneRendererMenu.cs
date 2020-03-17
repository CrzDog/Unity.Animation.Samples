using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Animation.Hybrid;

public class FillBoneRendererMenu : MonoBehaviour {
    [MenuItem("Animation/Rig/Fill BoneRendererComponent")]
    public static void FillBoneRendererComponent() {
        var go = Selection.activeGameObject;
        if (go == null) {
            EditorUtility.DisplayDialog("Error", "No GameObject Selected.", "OK");
            return;
        }

        var boneRenderer = go.GetComponent<BoneRendererComponent>();
        if (boneRenderer == null) {
            EditorUtility.DisplayDialog("Error", "Not BoneRendererComponent Attached GameObject.", "OK");
            return;
        }
        string rootBoneName = boneRenderer.RootBoneName;
        Transform[] children = go.GetComponentsInChildren<Transform>();
        Transform root = null;
        foreach (var child in children) {
            if (child.name == rootBoneName) {
                root = child;
            }
        }
        if (root == null) {
            EditorUtility.DisplayDialog("Error", "Root Bone Not Found.", "OK");
            return;
        } else {
            var bones = new List<Transform>();
            Transform[] boneChildren = root.GetComponentsInChildren<Transform>();
            foreach (var bone in boneChildren) {
                bones.Add(bone);
            }
            boneRenderer.Transforms = bones.ToArray();
        }

        EditorUtility.SetDirty(go);
    }
}
