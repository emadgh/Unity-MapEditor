using EmadGH.MapEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EmadGH.MapEditor.Editor
{
    [CustomEditor(typeof(ClotheslineGenerator))]
    public sealed class ClotheslineGeneratorEditor : UnityEditor.Editor
    {
        private SerializedProperty polePrefabs;
        private SerializedProperty poleCount;
        private SerializedProperty poleSpacing;
        private SerializedProperty poleHeight;
        private SerializedProperty wireCount;
        private SerializedProperty wireDropFromPoleTop;
        private SerializedProperty wireSpacing;
        private SerializedProperty sagAmount;
        private SerializedProperty segmentsPerSpan;
        private SerializedProperty wireThickness;
        private SerializedProperty wireMaterial;
        private SerializedProperty useManualWirePoints;
        private SerializedProperty updateInPlayMode;
        private SerializedProperty hangingPrefabs;
        private SerializedProperty hangingObjectCount;
        private SerializedProperty hangingWireIndex;
        private SerializedProperty hangingStart;
        private SerializedProperty hangingEnd;
        private SerializedProperty hangingOffset;
        private SerializedProperty alignHangingObjectsToWire;
        private SerializedProperty randomizeHangingPrefabs;
        private SerializedProperty hangingRandomSeed;
        private SerializedProperty attachmentRules;

        private void OnEnable()
        {
            polePrefabs = serializedObject.FindProperty("polePrefabs");
            poleCount = serializedObject.FindProperty("poleCount");
            poleSpacing = serializedObject.FindProperty("poleSpacing");
            poleHeight = serializedObject.FindProperty("poleHeight");
            wireCount = serializedObject.FindProperty("wireCount");
            wireDropFromPoleTop = serializedObject.FindProperty("wireDropFromPoleTop");
            wireSpacing = serializedObject.FindProperty("wireSpacing");
            sagAmount = serializedObject.FindProperty("sagAmount");
            segmentsPerSpan = serializedObject.FindProperty("segmentsPerSpan");
            wireThickness = serializedObject.FindProperty("wireThickness");
            wireMaterial = serializedObject.FindProperty("wireMaterial");
            useManualWirePoints = serializedObject.FindProperty("useManualWirePoints");
            updateInPlayMode = serializedObject.FindProperty("updateInPlayMode");
            hangingPrefabs = serializedObject.FindProperty("hangingPrefabs");
            hangingObjectCount = serializedObject.FindProperty("hangingObjectCount");
            hangingWireIndex = serializedObject.FindProperty("hangingWireIndex");
            hangingStart = serializedObject.FindProperty("hangingStart");
            hangingEnd = serializedObject.FindProperty("hangingEnd");
            hangingOffset = serializedObject.FindProperty("hangingOffset");
            alignHangingObjectsToWire = serializedObject.FindProperty("alignHangingObjectsToWire");
            randomizeHangingPrefabs = serializedObject.FindProperty("randomizeHangingPrefabs");
            hangingRandomSeed = serializedObject.FindProperty("hangingRandomSeed");
            attachmentRules = serializedObject.FindProperty("attachmentRules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Clothesline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(polePrefabs, new GUIContent("Pole Prefabs"), true);
            EditorGUILayout.PropertyField(poleCount, new GUIContent("Pole Count"));
            EditorGUILayout.PropertyField(poleSpacing, new GUIContent("Pole Spacing"));
            EditorGUILayout.PropertyField(poleHeight, new GUIContent("Pole Height"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Wire Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(wireCount, new GUIContent("Wire Count"));
            EditorGUILayout.PropertyField(wireDropFromPoleTop, new GUIContent("Drop From Pole Top"));
            EditorGUILayout.PropertyField(wireSpacing, new GUIContent("Wire Spacing"));
            EditorGUILayout.PropertyField(sagAmount, new GUIContent("Sag Amount"));
            EditorGUILayout.PropertyField(segmentsPerSpan, new GUIContent("Segments Per Span"));
            EditorGUILayout.PropertyField(wireThickness, new GUIContent("Wire Thickness"));
            EditorGUILayout.PropertyField(wireMaterial, new GUIContent("Wire Material"));
            EditorGUILayout.PropertyField(useManualWirePoints, new GUIContent("Manual Wire Points"));
            EditorGUILayout.PropertyField(updateInPlayMode, new GUIContent("Update In Play Mode"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Hanging Objects", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hangingPrefabs, new GUIContent("Prefabs"), true);
            EditorGUILayout.PropertyField(hangingObjectCount, new GUIContent("Count Per Span"));
            EditorGUILayout.PropertyField(hangingWireIndex, new GUIContent("Wire Index"));
            EditorGUILayout.PropertyField(hangingStart, new GUIContent("Start"));
            EditorGUILayout.PropertyField(hangingEnd, new GUIContent("End"));
            EditorGUILayout.PropertyField(hangingOffset, new GUIContent("Pivot Offset"));
            EditorGUILayout.PropertyField(alignHangingObjectsToWire, new GUIContent("Align To Wire"));
            EditorGUILayout.PropertyField(randomizeHangingPrefabs, new GUIContent("Randomize Prefabs"));
            if (randomizeHangingPrefabs.boolValue)
            {
                EditorGUILayout.PropertyField(hangingRandomSeed, new GUIContent("Random Seed"));
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Advanced Attachments", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(attachmentRules, new GUIContent("Additional Rules"), true);

            EditorGUILayout.Space(6f);
            ClotheslineGenerator generator = (ClotheslineGenerator)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate / Update"))
                {
                    Generate(generator);
                }

                if (GUILayout.Button("Apply Sag"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Apply Clothesline Sag");
                    generator.ApplySagToWirePoints();
                    generator.RegenerateAttachments();
                    MarkDirty(generator);
                }
            }

            if (GUILayout.Button("Regenerate Attachments"))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Regenerate Clothesline Attachments");
                generator.RegenerateAttachments();
                MarkDirty(generator);
            }

            EditorGUILayout.HelpBox(
                "Green pole/anchor points and yellow wire control points can be moved in the Scene View. Moving a wire point enables Manual Wire Points.",
                MessageType.Info);

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                generator.GenerateFromSettings();
                generator.RegenerateAttachments();
                MarkDirty(generator);
            }
        }

        private static void Generate(ClotheslineGenerator generator)
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Clothesline");
            generator.GenerateFromSettings();
            generator.RegenerateAttachments();
            MarkDirty(generator);
        }

        private static void MarkDirty(ClotheslineGenerator generator)
        {
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            ClotheslineGenerator generator = (ClotheslineGenerator)target;
            if (generator.PolePoints == null || generator.PolePoints.Count == 0)
            {
                return;
            }

            Handles.color = new Color(0.2f, 1f, 0.35f, 1f);
            for (int poleIndex = 0; poleIndex < generator.PolePoints.Count; poleIndex++)
            {
                Transform polePoint = generator.PolePoints[poleIndex];
                if (polePoint == null)
                {
                    continue;
                }

                Handles.SphereHandleCap(
                    0,
                    polePoint.position,
                    Quaternion.identity,
                    HandleUtility.GetHandleSize(polePoint.position) * 0.12f,
                    EventType.Repaint);
                Handles.Label(polePoint.position, $"Pole {poleIndex + 1}");

                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.PositionHandle(polePoint.position, polePoint.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(polePoint, "Move Clothesline Pole");
                    polePoint.position = newPosition;
                    generator.RebuildWireGeometry();
                    generator.RegenerateAttachments();
                    MarkDirty(generator);
                }
            }

            Handles.color = new Color(1f, 0.72f, 0.1f, 1f);
            for (int wireIndex = 0; wireIndex < generator.WireCount; wireIndex++)
            {
                for (int spanIndex = 0; spanIndex < generator.SpanCount; spanIndex++)
                {
                    Transform controlPoint = generator.GetWireControlPoint(wireIndex, spanIndex);
                    if (controlPoint == null)
                    {
                        continue;
                    }

                    Handles.SphereHandleCap(
                        0,
                        controlPoint.position,
                        Quaternion.identity,
                        HandleUtility.GetHandleSize(controlPoint.position) * 0.1f,
                        EventType.Repaint);
                    Handles.Label(controlPoint.position, $"Wire {wireIndex + 1} / Span {spanIndex + 1}");

                    EditorGUI.BeginChangeCheck();
                    Vector3 newPosition = Handles.PositionHandle(controlPoint.position, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(controlPoint, "Move Clothesline Wire Point");
                        Undo.RecordObject(generator, "Edit Clothesline Wire Point");
                        controlPoint.position = newPosition;
                        generator.UseManualWirePoints = true;
                        generator.RebuildWireGeometry();
                        generator.RegenerateAttachments();
                        MarkDirty(generator);
                    }
                }
            }
        }
    }
}
