using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Animation.Hybrid;

namespace Unity.Animation.Editor {
    public static class AddSkinnedMeshMenu {
        [MenuItem("Animation/Rig/Add SkinnedMesh")]
        public static void AddSkinnedMesh() {
            var go = Selection.activeGameObject;
            if (go == null) {
                EditorUtility.DisplayDialog("Error", "No GameObject selected.", "OK");
                return;
            }

            var rigComponent = go.GetComponent<RigComponent>();
            if (rigComponent == null) {
                EditorUtility.DisplayDialog("Error", "Not RigComponent Attached GameObject.", "OK");
            }

            SkinnedMeshRenderer[] children =  go.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var child in children) {
                if (child.GetComponent<SkinnedMesh>() == null) {
                    var skinnedMesh = child.gameObject.AddComponent<SkinnedMesh>();
                    skinnedMesh.Rig = rigComponent;
                    skinnedMesh.SkinnedMeshRenderer = child;
                }
            }
        }
    }
}

