using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmadGH.MapEditor.Editor
{
    public static class AddAssetToSceneMenu
    {
        private const string MenuPath = "Assets/Add to Scene";
        private const float PlacementDistance = 10f;

        [MenuItem(MenuPath, false, 2000)]
        private static void AddSelectedAssetsToScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog(
                    "Add to Scene",
                    "There is no active scene to add the selected asset to.",
                    "OK");
                return;
            }

            if (!TryGetPlacementPose(out Vector3 position, out Quaternion rotation))
            {
                EditorUtility.DisplayDialog(
                    "Add to Scene",
                    "Open a Scene View before using Add to Scene.",
                    "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Asset to Scene");

            var addedObjects = new List<GameObject>();
            var addedPaths = new HashSet<string>();

            foreach (Object selectedObject in Selection.objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(selectedObject);
                if (!IsSceneAsset(assetPath) || !addedPaths.Add(assetPath))
                {
                    continue;
                }

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null)
                {
                    continue;
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(asset, scene) as GameObject;
                if (instance == null)
                {
                    instance = Object.Instantiate(asset);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                }

                instance.transform.SetPositionAndRotation(position, rotation);
                Undo.RegisterCreatedObjectUndo(instance, "Add Asset to Scene");
                addedObjects.Add(instance);
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (addedObjects.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Add to Scene",
                    "Select a prefab or an imported model asset in the Project window.",
                    "OK");
                return;
            }

            Selection.objects = addedObjects.ToArray();
            Selection.activeObject = addedObjects[0];
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateAddSelectedAssetsToScene()
        {
            if (Selection.objects == null || Selection.objects.Length == 0)
            {
                return false;
            }

            foreach (Object selectedObject in Selection.objects)
            {
                if (IsSceneAsset(AssetDatabase.GetAssetPath(selectedObject)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSceneAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath))
            {
                return false;
            }

            return AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(GameObject);
        }

        private static bool TryGetPlacementPose(out Vector3 position, out Quaternion rotation)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                position = default;
                rotation = Quaternion.identity;
                return false;
            }

            Transform viewTransform = sceneView.camera.transform;
            position = viewTransform.position + viewTransform.forward * PlacementDistance;
            rotation = Quaternion.identity;
            return true;
        }
    }
}
