using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweatingBullets.EditorTools
{
    public class ScaleSceneAroundCentroid : EditorWindow
    {
        private float _scaleFactor = 0.01f;
        private bool _includeUI = false;
        private bool _recenterAtOrigin = true;

        [MenuItem("Tools/Scale Scene Around Centroid...")]
        private static void Open()
        {
            GetWindow<ScaleSceneAroundCentroid>("Scale Scene");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Uniformly scales all root objects in the active scene around the centroid of their renderer bounds. " +
                "Use this to shrink a scene whose objects are too far from the camera for URP's shadow distance.\n\n" +
                "Skips Canvas roots and the EventSystem by default.",
                MessageType.Info);

            _scaleFactor = EditorGUILayout.FloatField("Scale Factor", _scaleFactor);
            _recenterAtOrigin = EditorGUILayout.Toggle("Recenter At Origin", _recenterAtOrigin);
            _includeUI = EditorGUILayout.Toggle("Include UI Canvases", _includeUI);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_scaleFactor <= 0f))
            {
                if (GUILayout.Button("Apply To Active Scene"))
                {
                    Apply();
                }
            }
        }

        private void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Scale Scene", "No active scene.", "OK");
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            // Decide which roots are eligible
            var eligible = new System.Collections.Generic.List<Transform>();
            foreach (GameObject root in roots)
            {
                if (!_includeUI)
                {
                    if (root.GetComponent<Canvas>() != null) continue;
                    if (root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) continue;
                }
                eligible.Add(root.transform);
            }

            if (eligible.Count == 0)
            {
                EditorUtility.DisplayDialog("Scale Scene", "No eligible root objects found.", "OK");
                return;
            }

            // Compute centroid from renderer bounds across all eligible roots
            Bounds? combined = null;
            foreach (Transform t in eligible)
            {
                Renderer[] rends = t.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in rends)
                {
                    if (combined.HasValue)
                    {
                        Bounds b = combined.Value;
                        b.Encapsulate(r.bounds);
                        combined = b;
                    }
                    else
                    {
                        combined = r.bounds;
                    }
                }
            }

            Vector3 centroid;
            if (combined.HasValue)
            {
                centroid = combined.Value.center;
            }
            else
            {
                // Fallback: average of root positions
                Vector3 sum = Vector3.zero;
                foreach (Transform t in eligible) sum += t.position;
                centroid = sum / eligible.Count;
            }

            string summary = $"Scale {eligible.Count} root(s) by {_scaleFactor:0.######} around centroid {centroid}.\n" +
                             (_recenterAtOrigin ? "Centroid will be moved to origin." : "Centroid stays in place.");
            if (!EditorUtility.DisplayDialog("Scale Scene", summary, "Apply", "Cancel"))
            {
                return;
            }

            Undo.SetCurrentGroupName("Scale Scene Around Centroid");
            int undoGroup = Undo.GetCurrentGroup();

            Vector3 newCenter = _recenterAtOrigin ? Vector3.zero : centroid;

            foreach (Transform t in eligible)
            {
                Undo.RecordObject(t, "Scale Scene");
                Vector3 newPos = (t.position - centroid) * _scaleFactor + newCenter;
                Vector3 newScale = t.localScale * _scaleFactor;
                t.position = newPos;
                t.localScale = newScale;
                EditorUtility.SetDirty(t);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[ScaleSceneAroundCentroid] Scaled {eligible.Count} root(s) by {_scaleFactor} around {centroid}.");
        }
    }
}
