using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmadGH.MapEditor
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "HasanKachal.Core.World", "HasanKachal.Core")]
    [ExecuteAlways]
    public sealed class ClotheslineGenerator : MonoBehaviour
    {
        [Serializable]
        public sealed class AttachmentRule
        {
            public GameObject prefab;
            [Min(0)] public int wireIndex;
            [Min(1)] public int count = 5;
            [Range(0f, 1f)] public float start = 0.1f;
            [Range(0f, 1f)] public float end = 0.9f;
            [Min(0f)] public float hangingOffset;
            public bool alignToWire = true;
        }

        [Header("Supports")]
        [SerializeField] private GameObject polePrefab;
        [SerializeField] private List<GameObject> polePrefabs = new List<GameObject>();
        [SerializeField, Min(2)] private int poleCount = 2;
        [SerializeField, Min(0.1f)] private float poleSpacing = 4f;
        [SerializeField, Min(0.1f)] private float poleHeight = 3f;

        [Header("Wires")]
        [SerializeField, Min(1)] private int wireCount = 1;
        [SerializeField, Min(0f)] private float wireDropFromPoleTop = 0.1f;
        [SerializeField, Min(0f)] private float wireSpacing = 0.25f;
        [SerializeField, Min(0f)] private float sagAmount = 0.35f;
        [SerializeField, Min(2)] private int segmentsPerSpan = 12;
        [SerializeField, Min(0.001f)] private float wireThickness = 0.025f;
        [SerializeField] private Material wireMaterial;
        [SerializeField] private bool useManualWirePoints;
        [SerializeField] private bool updateInPlayMode = true;

        [Header("Generated Data")]
        [SerializeField] private List<Transform> polePoints = new List<Transform>();
        [SerializeField] private List<Transform> wireControlPoints = new List<Transform>();
        [SerializeField] private List<LineRenderer> wireRenderers = new List<LineRenderer>();

        [Header("Hanging Objects")]
        [SerializeField] private List<GameObject> hangingPrefabs = new List<GameObject>();
        [SerializeField, Min(0)] private int hangingObjectCount = 6;
        [SerializeField, Min(0)] private int hangingWireIndex;
        [SerializeField, Range(0f, 1f)] private float hangingStart = 0.1f;
        [SerializeField, Range(0f, 1f)] private float hangingEnd = 0.9f;
        [SerializeField, Min(0f)] private float hangingOffset;
        [SerializeField] private bool alignHangingObjectsToWire = true;
        [SerializeField] private bool randomizeHangingPrefabs;
        [SerializeField] private int hangingRandomSeed;

        [Header("Advanced Attachments")]
        [SerializeField] private List<AttachmentRule> attachmentRules = new List<AttachmentRule>();

        [SerializeField] private Transform polesRoot;
        [SerializeField] private Transform wireControlsRoot;
        [SerializeField] private Transform wiresRoot;
        [SerializeField] private Transform attachmentsRoot;

        public IReadOnlyList<Transform> PolePoints => polePoints;
        public IReadOnlyList<Transform> WireControlPoints => wireControlPoints;
        public IReadOnlyList<GameObject> PolePrefabs => polePrefabs;
        public IReadOnlyList<GameObject> HangingPrefabs => hangingPrefabs;
        public IReadOnlyList<AttachmentRule> AttachmentRules => attachmentRules;
        public int SpanCount => Mathf.Max(0, polePoints.Count - 1);
        public int WireCount => wireCount;
        public bool UseManualWirePoints
        {
            get => useManualWirePoints;
            set => useManualWirePoints = value;
        }

        private void OnEnable()
        {
            if (Application.isPlaying && polePoints.Count >= 2)
            {
                RebuildWireGeometry();
            }
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && updateInPlayMode && polePoints.Count >= 2)
            {
                RebuildWireGeometry();
            }
        }

        public void ConfigureSettings(
            GameObject newPolePrefab,
            int newPoleCount,
            float newPoleSpacing,
            float newPoleHeight,
            int newWireCount,
            float newWireDropFromPoleTop,
            float newWireSpacing,
            float newSagAmount)
        {
            polePrefab = newPolePrefab;
            poleCount = Mathf.Max(2, newPoleCount);
            poleSpacing = Mathf.Max(0.1f, newPoleSpacing);
            poleHeight = Mathf.Max(0.1f, newPoleHeight);
            wireCount = Mathf.Max(1, newWireCount);
            wireDropFromPoleTop = Mathf.Max(0f, newWireDropFromPoleTop);
            wireSpacing = Mathf.Max(0f, newWireSpacing);
            sagAmount = Mathf.Max(0f, newSagAmount);
        }

        public void ConfigurePolePrefabs(IReadOnlyList<GameObject> prefabs)
        {
            polePrefabs.Clear();
            if (prefabs != null)
            {
                for (int index = 0; index < prefabs.Count; index++)
                {
                    GameObject prefab = prefabs[index];
                    if (prefab != null && !polePrefabs.Contains(prefab))
                    {
                        polePrefabs.Add(prefab);
                    }
                }
            }

            polePrefab = polePrefabs.Count > 0 ? polePrefabs[0] : null;
        }

        public void ConfigureWireStyle(Material material, float thickness)
        {
            wireMaterial = material;
            wireThickness = Mathf.Max(0.001f, thickness);
        }

        public void ConfigureHangingObjects(
            IReadOnlyList<GameObject> prefabs,
            int count,
            int wireIndex,
            float start,
            float end,
            float offset,
            bool alignToWire,
            bool randomize,
            int randomSeed)
        {
            hangingPrefabs.Clear();
            if (prefabs != null)
            {
                for (int index = 0; index < prefabs.Count; index++)
                {
                    GameObject prefab = prefabs[index];
                    if (prefab != null && !hangingPrefabs.Contains(prefab))
                    {
                        hangingPrefabs.Add(prefab);
                    }
                }
            }

            hangingObjectCount = Mathf.Max(0, count);
            hangingWireIndex = Mathf.Max(0, wireIndex);
            hangingStart = Mathf.Clamp01(start);
            hangingEnd = Mathf.Clamp01(end);
            hangingOffset = Mathf.Max(0f, offset);
            alignHangingObjectsToWire = alignToWire;
            randomizeHangingPrefabs = randomize;
            hangingRandomSeed = randomSeed;
        }

        public void GenerateFromSettings()
        {
            EnsureGeneratedRoots();
            EnsurePolePoints();

            if (polePoints.Count < 2)
            {
                return;
            }

            EnsureWireControlPoints();
            EnsureWireRenderers();
            RebuildWireGeometry();
        }

        public void GenerateFromWorldPoints(IReadOnlyList<Vector3> worldPoints, bool spawnPolePrefabs)
        {
            if (worldPoints == null || worldPoints.Count < 2)
            {
                return;
            }

            EnsureGeneratedRoots();
            ClearGeneratedStructure();

            poleCount = worldPoints.Count;
            if (!spawnPolePrefabs)
            {
                polePrefab = null;
                polePrefabs.Clear();
            }

            for (int index = 0; index < worldPoints.Count; index++)
            {
                GameObject pointObject = new GameObject(spawnPolePrefabs ? $"Pole_{index + 1:00}" : $"Anchor_{index + 1:00}");
                pointObject.transform.SetParent(polesRoot, true);
                pointObject.transform.position = worldPoints[index];
                pointObject.transform.rotation = Quaternion.identity;

                GameObject selectedPolePrefab = spawnPolePrefabs ? GetPolePrefab(index) : null;
                if (selectedPolePrefab != null)
                {
                    GameObject poleInstance = Instantiate(selectedPolePrefab, pointObject.transform);
                    poleInstance.name = "Pole Mesh";
                    poleInstance.transform.localPosition = Vector3.zero;
                    poleInstance.transform.localRotation = Quaternion.identity;
                }

                polePoints.Add(pointObject.transform);
            }

            EnsureWireControlPoints();
            EnsureWireRenderers();
            RebuildWireGeometry();
        }

        public void RebuildGeometry()
        {
            if (polePoints.Count < 2)
            {
                return;
            }

            EnsureGeneratedRoots();
            EnsureWireControlPoints();
            EnsureWireRenderers();
            RebuildWireGeometry();
        }

        public void RebuildWireGeometry()
        {
            if (polePoints == null || polePoints.Count < 2 || wireCount < 1)
            {
                ClearWireRenderers();
                return;
            }

            EnsureWireControlPoints();
            EnsureWireRenderers();

            int spanCount = polePoints.Count - 1;
            int segmentCount = Mathf.Max(2, segmentsPerSpan);

            for (int wireIndex = 0; wireIndex < wireCount; wireIndex++)
            {
                LineRenderer lineRenderer = wireRenderers[wireIndex];
                lineRenderer.positionCount = spanCount * segmentCount + 1;
                lineRenderer.widthMultiplier = Mathf.Max(0.001f, wireThickness);
                lineRenderer.useWorldSpace = true;
                lineRenderer.sharedMaterial = wireMaterial != null
                    ? wireMaterial
                    : GetOrCreateDefaultWireMaterial(lineRenderer);

                int pointIndex = 0;
                for (int spanIndex = 0; spanIndex < spanCount; spanIndex++)
                {
                    Vector3 start = GetWireAnchorPosition(spanIndex, wireIndex);
                    Vector3 end = GetWireAnchorPosition(spanIndex + 1, wireIndex);
                    Transform controlPoint = GetWireControlPoint(wireIndex, spanIndex);

                    if (!useManualWirePoints && controlPoint != null)
                    {
                        controlPoint.position = CalculateAutomaticControlPoint(start, end);
                    }

                    Vector3 control = controlPoint != null
                        ? controlPoint.position
                        : CalculateAutomaticControlPoint(start, end);

                    for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                    {
                        float t = segmentIndex / (float)segmentCount;
                        lineRenderer.SetPosition(pointIndex++, EvaluateQuadraticBezier(start, control, end, t));
                    }
                }

                lineRenderer.SetPosition(pointIndex, GetWireAnchorPosition(spanCount, wireIndex));
            }
        }

        public void ApplySagToWirePoints()
        {
            useManualWirePoints = false;
            RebuildWireGeometry();
        }

        public void RegenerateAttachments()
        {
            EnsureGeneratedRoots();
            ClearChildren(attachmentsRoot);

            if (polePoints.Count < 2)
            {
                return;
            }

            GenerateHangingObjects();

            if (attachmentRules == null || attachmentRules.Count == 0)
            {
                return;
            }

            for (int ruleIndex = 0; ruleIndex < attachmentRules.Count; ruleIndex++)
            {
                AttachmentRule rule = attachmentRules[ruleIndex];
                if (rule == null || rule.prefab == null || rule.count < 1)
                {
                    continue;
                }

                int safeWireIndex = Mathf.Clamp(rule.wireIndex, 0, Mathf.Max(0, wireCount - 1));
                float start = Mathf.Clamp01(rule.start);
                float end = Mathf.Clamp01(rule.end);
                if (end < start)
                {
                    (start, end) = (end, start);
                }

                for (int itemIndex = 0; itemIndex < rule.count; itemIndex++)
                {
                    float normalizedPosition = rule.count == 1
                        ? (start + end) * 0.5f
                        : Mathf.Lerp(start, end, itemIndex / (float)(rule.count - 1));

                    if (!TryEvaluateWire(safeWireIndex, normalizedPosition, out Vector3 position, out Vector3 tangent))
                    {
                        continue;
                    }

                    CreateAttachmentInstance(
                        rule.prefab,
                        $"Attachment_{ruleIndex + 1:00}_{itemIndex + 1:00}",
                        position,
                        tangent,
                        rule.hangingOffset,
                        rule.alignToWire);
                }
            }
        }

        private void GenerateHangingObjects()
        {
            if (hangingPrefabs == null || hangingPrefabs.Count == 0 || hangingObjectCount < 1)
            {
                return;
            }

            int safeWireIndex = Mathf.Clamp(hangingWireIndex, 0, Mathf.Max(0, wireCount - 1));
            float start = Mathf.Clamp01(hangingStart);
            float end = Mathf.Clamp01(hangingEnd);
            if (end < start)
            {
                (start, end) = (end, start);
            }

            int spanCount = polePoints.Count - 1;
            var random = new System.Random(hangingRandomSeed);
            for (int spanIndex = 0; spanIndex < spanCount; spanIndex++)
            {
                for (int itemIndex = 0; itemIndex < hangingObjectCount; itemIndex++)
                {
                    float spanPosition = hangingObjectCount == 1
                        ? (start + end) * 0.5f
                        : Mathf.Lerp(start, end, itemIndex / (float)(hangingObjectCount - 1));
                    float normalizedPosition = (spanIndex + spanPosition) / spanCount;

                    if (!TryEvaluateWire(safeWireIndex, normalizedPosition, out Vector3 position, out Vector3 tangent))
                    {
                        continue;
                    }

                    int globalItemIndex = spanIndex * hangingObjectCount + itemIndex;
                    int prefabIndex = randomizeHangingPrefabs
                        ? random.Next(hangingPrefabs.Count)
                        : globalItemIndex % hangingPrefabs.Count;
                    GameObject prefab = hangingPrefabs[prefabIndex];
                    if (prefab == null)
                    {
                        continue;
                    }

                    CreateAttachmentInstance(
                        prefab,
                        $"Hanging_Span_{spanIndex + 1:00}_{itemIndex + 1:00}_{prefab.name}",
                        position,
                        tangent,
                        hangingOffset,
                        alignHangingObjectsToWire);
                }
            }
        }

        private void CreateAttachmentInstance(
            GameObject prefab,
            string instanceName,
            Vector3 position,
            Vector3 tangent,
            float offset,
            bool alignToWire)
        {
            GameObject instance = Instantiate(prefab, attachmentsRoot);
            instance.name = instanceName;
            instance.transform.position = position + Vector3.down * Mathf.Max(0f, offset);
            if (alignToWire && tangent.sqrMagnitude > 0.0001f)
            {
                instance.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            }
        }

        public Transform GetWireControlPoint(int wireIndex, int spanIndex)
        {
            int spanCount = Mathf.Max(0, polePoints.Count - 1);
            int flattenedIndex = wireIndex * spanCount + spanIndex;
            if (flattenedIndex < 0 || flattenedIndex >= wireControlPoints.Count)
            {
                return null;
            }

            return wireControlPoints[flattenedIndex];
        }

        public bool TryEvaluateWire(int wireIndex, float normalizedPosition, out Vector3 position, out Vector3 tangent)
        {
            position = default;
            tangent = Vector3.forward;

            int spanCount = polePoints.Count - 1;
            if (wireIndex < 0 || wireIndex >= wireCount || spanCount < 1)
            {
                return false;
            }

            float clampedPosition = Mathf.Clamp01(normalizedPosition);
            float scaledPosition = clampedPosition * spanCount;
            int spanIndex = Mathf.Min(Mathf.FloorToInt(scaledPosition), spanCount - 1);
            float spanT = spanIndex == spanCount - 1 && clampedPosition >= 1f
                ? 1f
                : scaledPosition - spanIndex;

            Vector3 start = GetWireAnchorPosition(spanIndex, wireIndex);
            Vector3 end = GetWireAnchorPosition(spanIndex + 1, wireIndex);
            Transform controlPoint = GetWireControlPoint(wireIndex, spanIndex);
            Vector3 control = controlPoint != null
                ? controlPoint.position
                : CalculateAutomaticControlPoint(start, end);

            position = EvaluateQuadraticBezier(start, control, end, spanT);
            tangent = EvaluateQuadraticTangent(start, control, end, spanT);
            return true;
        }

        private void EnsureGeneratedRoots()
        {
            polesRoot = EnsureChildRoot(polesRoot, "Poles");
            wireControlsRoot = EnsureChildRoot(wireControlsRoot, "Wire Control Points");
            wiresRoot = EnsureChildRoot(wiresRoot, "Wires");
            attachmentsRoot = EnsureChildRoot(attachmentsRoot, "Attachments");
        }

        private void EnsurePolePoints()
        {
            poleCount = Mathf.Max(2, poleCount);

            for (int index = polePoints.Count - 1; index >= poleCount; index--)
            {
                if (polePoints[index] != null)
                {
                    DestroyGeneratedObject(polePoints[index].gameObject);
                }

                polePoints.RemoveAt(index);
            }

            for (int index = 0; index < poleCount; index++)
            {
                Transform polePoint = index < polePoints.Count ? polePoints[index] : null;
                if (polePoint == null)
                {
                    GameObject selectedPolePrefab = GetPolePrefab(index);
                    GameObject pointObject = selectedPolePrefab != null
                        ? Instantiate(selectedPolePrefab, polesRoot)
                        : new GameObject($"Anchor_{index + 1:00}");
                    pointObject.name = selectedPolePrefab != null
                        ? $"Pole_{index + 1:00}"
                        : $"Anchor_{index + 1:00}";
                    if (selectedPolePrefab == null)
                    {
                        pointObject.transform.SetParent(polesRoot, false);
                    }

                    polePoint = pointObject.transform;
                    polePoint.localPosition = Vector3.right * (index * poleSpacing);
                    polePoint.localRotation = Quaternion.identity;

                    if (index < polePoints.Count)
                    {
                        polePoints[index] = polePoint;
                    }
                    else
                    {
                        polePoints.Add(polePoint);
                    }
                }
            }
        }

        private GameObject GetPolePrefab(int pointIndex)
        {
            if (polePrefabs != null && polePrefabs.Count > 0)
            {
                return polePrefabs[Mathf.Abs(pointIndex) % polePrefabs.Count];
            }

            return polePrefab;
        }

        private void EnsureWireControlPoints()
        {
            if (wireControlsRoot == null || polePoints.Count < 2)
            {
                return;
            }

            int spanCount = polePoints.Count - 1;
            int requiredCount = wireCount * spanCount;

            // A topology change (wire or support count) changes the flattened
            // wire/span mapping, so rebuild the controls instead of reusing
            // points that may now belong to another wire or span.
            if (wireControlPoints.Count != requiredCount)
            {
                ClearChildren(wireControlsRoot);
                wireControlPoints.Clear();
                useManualWirePoints = false;
            }

            for (int index = wireControlPoints.Count - 1; index >= requiredCount; index--)
            {
                if (wireControlPoints[index] != null)
                {
                    DestroyGeneratedObject(wireControlPoints[index].gameObject);
                }

                wireControlPoints.RemoveAt(index);
            }

            for (int wireIndex = 0; wireIndex < wireCount; wireIndex++)
            {
                for (int spanIndex = 0; spanIndex < spanCount; spanIndex++)
                {
                    int flattenedIndex = wireIndex * spanCount + spanIndex;
                    Transform controlPoint = flattenedIndex < wireControlPoints.Count
                        ? wireControlPoints[flattenedIndex]
                        : null;
                    if (controlPoint != null)
                    {
                        continue;
                    }

                    GameObject controlObject = new GameObject($"Wire_{wireIndex + 1:00}_Span_{spanIndex + 1:00}_Control");
                    controlObject.transform.SetParent(wireControlsRoot, true);
                    controlObject.transform.position = CalculateAutomaticControlPoint(
                        GetWireAnchorPosition(spanIndex, wireIndex),
                        GetWireAnchorPosition(spanIndex + 1, wireIndex));

                    if (flattenedIndex < wireControlPoints.Count)
                    {
                        wireControlPoints[flattenedIndex] = controlObject.transform;
                    }
                    else
                    {
                        wireControlPoints.Add(controlObject.transform);
                    }
                }
            }
        }

        private void EnsureWireRenderers()
        {
            if (wiresRoot == null)
            {
                return;
            }

            for (int index = wireRenderers.Count - 1; index >= wireCount; index--)
            {
                if (wireRenderers[index] != null)
                {
                    DestroyGeneratedObject(wireRenderers[index].gameObject);
                }

                wireRenderers.RemoveAt(index);
            }

            while (wireRenderers.Count < wireCount)
            {
                int wireIndex = wireRenderers.Count;
                GameObject wireObject = new GameObject($"Wire_{wireIndex + 1:00}");
                wireObject.transform.SetParent(wiresRoot, false);
                LineRenderer lineRenderer = wireObject.AddComponent<LineRenderer>();
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
                lineRenderer.textureMode = LineTextureMode.Tile;
                wireRenderers.Add(lineRenderer);
            }
        }

        private Vector3 GetWireAnchorPosition(int poleIndex, int wireIndex)
        {
            Transform polePoint = polePoints[poleIndex];
            float height = Mathf.Max(0f, poleHeight - wireDropFromPoleTop - wireIndex * wireSpacing);
            return polePoint.position + polePoint.up * height;
        }

        private Vector3 CalculateAutomaticControlPoint(Vector3 start, Vector3 end)
        {
            return Vector3.Lerp(start, end, 0.5f) + Vector3.down * sagAmount;
        }

        private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float inverseT = 1f - t;
            return inverseT * inverseT * start + 2f * inverseT * t * control + t * t * end;
        }

        private static Vector3 EvaluateQuadraticTangent(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            return 2f * (1f - t) * (control - start) + 2f * t * (end - control);
        }

        private Transform EnsureChildRoot(Transform root, string rootName)
        {
            if (root != null)
            {
                return root;
            }

            GameObject rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(transform, false);
            return rootObject.transform;
        }

        private void ClearGeneratedStructure()
        {
            ClearChildren(polesRoot);
            ClearChildren(wireControlsRoot);
            ClearChildren(wiresRoot);
            ClearChildren(attachmentsRoot);
            polePoints.Clear();
            wireControlPoints.Clear();
            wireRenderers.Clear();
        }

        private void ClearWireRenderers()
        {
            for (int index = 0; index < wireRenderers.Count; index++)
            {
                if (wireRenderers[index] != null)
                {
                    wireRenderers[index].positionCount = 0;
                }
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                DestroyGeneratedObject(parent.GetChild(index).gameObject);
            }
        }

        private static Material GetOrCreateDefaultWireMaterial(LineRenderer lineRenderer)
        {
            if (lineRenderer.sharedMaterial != null)
            {
                return lineRenderer.sharedMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = "Clothesline Default Wire Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.color = new Color(0.08f, 0.06f, 0.04f, 1f);
            return material;
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
