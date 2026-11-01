using System;
using System.Collections.Generic;
using EmadGH.MapEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace EmadGH.MapEditor.Editor
{
    public sealed class MapEditorWindow : EditorWindow
    {
        private enum ToolTab
        {
            AddToScene = 0,
            Clotheslines = 1,
            PhysicsSimulation = 2
        }

        private enum PlacementAnchorMode
        {
            ObjectPivot,
            CenterPivot,
            BottomOfBoundingBox
        }

        private enum PlacementMode
        {
            Single,
            Brush
        }

        private enum PrefabSelectionMode
        {
            Manual,
            Random,
            Sequential
        }

        private struct PlacementVariation
        {
            public Vector3 RandomEuler;
            public Vector3 LocalScale;
        }

        private sealed class BrushSpawnPoint
        {
            public GameObject Prefab;
            public Vector3 Position;
            public Vector3 Normal;
            public PlacementVariation Variation;
        }

        private sealed class PhysicsSimulationBodyState
        {
            public Rigidbody Rigidbody;
            public bool IsKinematic;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 LinearVelocity;
            public Vector3 AngularVelocity;
        }

        private const string MenuPath = "Tools/Map Editor";
        private const string UxmlPath = "Packages/com.emadgh.map-editor/Editor/MapEditorWindow.uxml";
        private const float DefaultRotationStep = 15f;
        private const float MinimumBrushSpacing = 0.05f;

        [SerializeField] private ToolTab activeTab;

        [SerializeField] private List<GameObject> sourcePrefabs = new List<GameObject>();
        [SerializeField] private int activePrefabIndex;
        [SerializeField] private bool ignoreAfterSpawn = true;
        [SerializeField] private bool alignToSurface;
        [SerializeField] private bool randomRotationX;
        [SerializeField] private bool randomRotationY;
        [SerializeField] private bool randomRotationZ;
        [SerializeField] private float randomScaleMinimumPercent = 100f;
        [SerializeField] private float randomScaleMaximumPercent = 100f;
        [SerializeField] private PlacementAnchorMode placementAnchorMode;
        [SerializeField] private float rotationStep = DefaultRotationStep;
        [SerializeField] private float verticalOffsetStep = 0.25f;
        [SerializeField] private PlacementMode placementMode;
        [SerializeField] private float brushRadius = 1f;
        [SerializeField] private float brushSpacing = 1f;
        [SerializeField] private int brushObjectsPerStep = 1;
        [SerializeField] private bool brushRandomizePosition = true;
        [SerializeField] private bool brushAlignToSurface;
        [SerializeField] private PrefabSelectionMode prefabSelectionMode;
        [SerializeField] private int sequentialPrefabIndex = -1;
        [SerializeField] private float physicsSimulationDuration = 3f;
        [SerializeField] private bool physicsAddRigidbodyIfMissing = true;
        [SerializeField] private bool physicsAutoSimulateAfterSpawn;

        [SerializeField] private List<GameObject> clotheslinePolePrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> clotheslineHangingPrefabs = new List<GameObject>();
        [SerializeField] private bool clotheslineSpawnPoles = true;
        [SerializeField] private int clotheslineWireCount = 1;
        [SerializeField] private float clotheslinePoleHeight = 3f;
        [SerializeField] private float clotheslineWireDropFromTop = 0.1f;
        [SerializeField] private float clotheslineWireSpacing = 0.25f;
        [SerializeField] private float clotheslineSagAmount = 0.35f;
        [SerializeField] private float clotheslineWireThickness = 0.025f;
        [SerializeField] private Material clotheslineWireMaterial;
        [SerializeField] private int hangingObjectCount = 6;
        [SerializeField] private int hangingWireIndex;
        [SerializeField] private float hangingStart = 0.1f;
        [SerializeField] private float hangingEnd = 0.9f;
        [SerializeField] private float hangingPivotOffset;
        [SerializeField] private bool alignHangingObjectsToWire = true;
        [SerializeField] private bool randomizeHangingObjects;
        [SerializeField] private int hangingRandomSeed;
        [SerializeField] private string sourceCollectionGuid;
        [SerializeField] private string poleCollectionGuid;
        [SerializeField] private string hangingCollectionGuid;

        private readonly HashSet<GameObject> ignoredSpawnedObjects = new HashSet<GameObject>();
        private readonly HashSet<GameObject> placedSceneObjects = new HashSet<GameObject>();
        private readonly List<Vector3> clotheslinePoints = new List<Vector3>();
        private readonly List<PhysicsSimulationBodyState> physicsBodyStates =
            new List<PhysicsSimulationBodyState>();
        private readonly HashSet<Rigidbody> physicsTargetBodies = new HashSet<Rigidbody>();
        private readonly List<Component> physicsAddedComponents = new List<Component>();
        private readonly List<Vector3> brushStrokePath = new List<Vector3>();
        private readonly List<Vector3> brushStampCenters = new List<Vector3>();
        private readonly List<Vector3> brushStampNormals = new List<Vector3>();
        private readonly List<BrushSpawnPoint> brushSpawnPoints = new List<BrushSpawnPoint>();
        private readonly List<GameObject> brushPreviewInstances = new List<GameObject>();
        private readonly List<GameObject> brushPreviewSources = new List<GameObject>();
        private readonly System.Random brushRandom = new System.Random();
        private readonly System.Random placementRandom = new System.Random();
        private GameObject pendingRandomPrefab;
        private int brushSequentialPrefabIndex = -1;
        private int brushPlannedSpawnCount;

        private MapEditorSceneInteraction sceneInteraction;
        private bool toolEnabled;
        private bool hasPointerPosition;
        private Vector3 pointerPosition;
        private Vector3 pointerNormal = Vector3.up;
        private bool brushStrokeActive;
        private bool brushHasLastWorldPosition;
        private Vector2 brushLastMousePosition;
        private Vector3 brushLastWorldPosition;
        private Vector3 brushLastWorldNormal = Vector3.up;
        private float brushDistanceSinceLastSpawn;
        private SceneView lastSceneView;

        private GameObject previewInstance;
        private GameObject previewSource;
        private PlacementVariation previewVariation;
        private bool hasPreviewVariation;
        private float rotationY;
        private float verticalOffset;
        [SerializeField] private GameObject physicsBatchesRoot;
        [SerializeField] private GameObject activePhysicsBatch;
        [SerializeField] private bool physicsBatchSimulationCompleted;
        private bool physicsSimulationRunning;
        private bool physicsSimulationIsActiveBatch;
        private float physicsSimulationElapsed;
        private double physicsLastTickTime;
        private string physicsSimulationLabel = string.Empty;
        private SimulationMode previousPhysicsSimulationMode;

        private Button addToSceneTabButton;
        private Button physicsSimulationTabButton;
        private Button clotheslinesTabButton;
        private VisualElement sharedPlacementPanel;
        private VisualElement addToSceneTab;
        private VisualElement physicsSimulationTab;
        private VisualElement clotheslinesTab;
        private VisualElement assetThumbnailGrid;
        private VisualElement poleThumbnailGrid;
        private VisualElement hangingThumbnailGrid;
        private VisualElement assetCollectionBar;
        private VisualElement poleCollectionBar;
        private VisualElement hangingCollectionBar;
        private Label activeAssetLabel;
        private Label activeToolLabel;
        private Label statusLabel;
        private Button toolActionButton;
        private FloatField physicsDurationField;
        private Button singlePlacementModeButton;
        private Button brushPlacementModeButton;
        private VisualElement brushSettingsPanel;
        private FloatField brushRadiusField;
        private FloatField brushSpacingField;
        private IntegerField brushObjectsPerStepField;
        private Toggle brushRandomizePositionToggle;
        private Toggle brushAlignToSurfaceToggle;
        private Toggle randomRotationXToggle;
        private Toggle randomRotationYToggle;
        private Toggle randomRotationZToggle;
        private FloatField randomScaleMinimumField;
        private FloatField randomScaleMaximumField;
        private Button manualPrefabModeButton;
        private Button randomPrefabModeButton;
        private Button sequentialPrefabModeButton;
        private Toggle physicsAddRigidbodyToggle;
        private Toggle physicsAutoSimulateAfterSpawnToggle;
        private Label physicsBatchLabel;
        private Label physicsSelectionLabel;
        private Button simulateBatchButton;
        private Button simulateSelectionButton;
        private Button confirmBatchButton;
        private Button cancelSimulationButton;
        private bool waitingForThumbnailPreviews;

        [MenuItem(MenuPath, false, 100)]
        private static void ShowWindow()
        {
            MapEditorWindow window = GetWindow<MapEditorWindow>();
            window.titleContent = new GUIContent("Map Editor");
            window.minSize = new Vector2(430f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            sceneInteraction = new MapEditorSceneInteraction();
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            StopThumbnailPreviewPolling();
            StopPhysicsSimulation(true);
            toolEnabled = false;
            clotheslinePoints.Clear();
            ClearBrushStroke();
            DestroyPreview();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (layout == null)
            {
                rootVisualElement.Add(new Label($"UI asset not found: {UxmlPath}"));
                return;
            }

            layout.CloneTree(rootVisualElement);
            CacheUiReferences();
            BindUiEvents();
            BuildClotheslineControls();
            RebuildCollectionControls();
            RebuildAllThumbnailGrids();
            UpdateTabUi();
            UpdatePlacementModeUi();
            UpdatePrefabSelectionModeUi();
            UpdateToolActionUi();
            UpdateActiveAssetLabel();
            UpdatePhysicsUi();
        }

        private void CacheUiReferences()
        {
            addToSceneTabButton = rootVisualElement.Q<Button>("addToSceneTabButton");
            physicsSimulationTabButton = rootVisualElement.Q<Button>("physicsSimulationTabButton");
            clotheslinesTabButton = rootVisualElement.Q<Button>("clotheslinesTabButton");
            sharedPlacementPanel = rootVisualElement.Q<VisualElement>("sharedPlacementPanel");
            addToSceneTab = rootVisualElement.Q<VisualElement>("addToSceneTab");
            physicsSimulationTab = rootVisualElement.Q<VisualElement>("physicsSimulationTab");
            clotheslinesTab = rootVisualElement.Q<VisualElement>("clotheslinesTab");
            assetThumbnailGrid = rootVisualElement.Q<VisualElement>("assetThumbnailGrid");
            poleThumbnailGrid = rootVisualElement.Q<VisualElement>("poleThumbnailGrid");
            hangingThumbnailGrid = rootVisualElement.Q<VisualElement>("hangingThumbnailGrid");
            assetCollectionBar = rootVisualElement.Q<VisualElement>("assetCollectionBar");
            poleCollectionBar = rootVisualElement.Q<VisualElement>("poleCollectionBar");
            hangingCollectionBar = rootVisualElement.Q<VisualElement>("hangingCollectionBar");
            activeAssetLabel = rootVisualElement.Q<Label>("activeAssetLabel");
            activeToolLabel = rootVisualElement.Q<Label>("activeToolLabel");
            statusLabel = rootVisualElement.Q<Label>("statusLabel");
            toolActionButton = rootVisualElement.Q<Button>("toolActionButton");
            singlePlacementModeButton = rootVisualElement.Q<Button>("singlePlacementModeButton");
            brushPlacementModeButton = rootVisualElement.Q<Button>("brushPlacementModeButton");
            brushSettingsPanel = rootVisualElement.Q<VisualElement>("brushSettingsPanel");
            brushRadiusField = rootVisualElement.Q<FloatField>("brushRadiusField");
            brushSpacingField = rootVisualElement.Q<FloatField>("brushSpacingField");
            brushObjectsPerStepField = rootVisualElement.Q<IntegerField>("brushObjectsPerStepField");
            brushRandomizePositionToggle = rootVisualElement.Q<Toggle>("brushRandomizePositionToggle");
            brushAlignToSurfaceToggle = rootVisualElement.Q<Toggle>("brushAlignToSurfaceToggle");
            randomRotationXToggle = rootVisualElement.Q<Toggle>("randomRotationXToggle");
            randomRotationYToggle = rootVisualElement.Q<Toggle>("randomRotationYToggle");
            randomRotationZToggle = rootVisualElement.Q<Toggle>("randomRotationZToggle");
            randomScaleMinimumField = rootVisualElement.Q<FloatField>("randomScaleMinimumField");
            randomScaleMaximumField = rootVisualElement.Q<FloatField>("randomScaleMaximumField");
            manualPrefabModeButton = rootVisualElement.Q<Button>("manualPrefabModeButton");
            randomPrefabModeButton = rootVisualElement.Q<Button>("randomPrefabModeButton");
            sequentialPrefabModeButton = rootVisualElement.Q<Button>("sequentialPrefabModeButton");
            physicsDurationField = rootVisualElement.Q<FloatField>("physicsDurationField");
            physicsAddRigidbodyToggle = rootVisualElement.Q<Toggle>("physicsAddRigidbodyToggle");
            physicsAutoSimulateAfterSpawnToggle =
                rootVisualElement.Q<Toggle>("physicsAutoSimulateAfterSpawnToggle");
            physicsBatchLabel = rootVisualElement.Q<Label>("physicsBatchLabel");
            physicsSelectionLabel = rootVisualElement.Q<Label>("physicsSelectionLabel");
            simulateBatchButton = rootVisualElement.Q<Button>("simulateBatchButton");
            simulateSelectionButton = rootVisualElement.Q<Button>("simulateSelectionButton");
            confirmBatchButton = rootVisualElement.Q<Button>("confirmBatchButton");
            cancelSimulationButton = rootVisualElement.Q<Button>("cancelSimulationButton");
        }

        private void BindUiEvents()
        {
            addToSceneTabButton.clicked += () => SelectTab(ToolTab.AddToScene);
            physicsSimulationTabButton.clicked += () => SelectTab(ToolTab.PhysicsSimulation);
            clotheslinesTabButton.clicked += () => SelectTab(ToolTab.Clotheslines);
            toolActionButton.clicked += OnToolActionClicked;
            singlePlacementModeButton.clicked += () => SetPlacementMode(PlacementMode.Single);
            brushPlacementModeButton.clicked += () => SetPlacementMode(PlacementMode.Brush);
            manualPrefabModeButton.clicked += () => SetPrefabSelectionMode(PrefabSelectionMode.Manual);
            randomPrefabModeButton.clicked += () => SetPrefabSelectionMode(PrefabSelectionMode.Random);
            sequentialPrefabModeButton.clicked += () => SetPrefabSelectionMode(PrefabSelectionMode.Sequential);

            brushRadiusField.SetValueWithoutNotify(Mathf.Max(0.05f, brushRadius));
            brushRadiusField.RegisterValueChangedCallback(change =>
            {
                brushRadius = Mathf.Max(0.05f, change.newValue);
                brushRadiusField.SetValueWithoutNotify(brushRadius);
                RepaintSceneView();
            });

            brushSpacingField.SetValueWithoutNotify(Mathf.Max(MinimumBrushSpacing, brushSpacing));
            brushSpacingField.RegisterValueChangedCallback(change =>
            {
                brushSpacing = Mathf.Max(MinimumBrushSpacing, change.newValue);
                brushSpacingField.SetValueWithoutNotify(brushSpacing);
            });

            brushObjectsPerStepField.SetValueWithoutNotify(Mathf.Max(1, brushObjectsPerStep));
            brushObjectsPerStepField.RegisterValueChangedCallback(change =>
            {
                brushObjectsPerStep = Mathf.Max(1, change.newValue);
                brushObjectsPerStepField.SetValueWithoutNotify(brushObjectsPerStep);
            });

            brushRandomizePositionToggle.SetValueWithoutNotify(brushRandomizePosition);
            brushRandomizePositionToggle.RegisterValueChangedCallback(change =>
            {
                brushRandomizePosition = change.newValue;
            });

            brushAlignToSurfaceToggle.SetValueWithoutNotify(brushAlignToSurface);
            brushAlignToSurfaceToggle.RegisterValueChangedCallback(change =>
            {
                brushAlignToSurface = change.newValue;
                if (brushStrokeActive)
                {
                    RefreshBrushPreviewInstances(0);
                }

                if (toolEnabled && placementMode == PlacementMode.Brush && lastSceneView != null)
                {
                    UpdatePlacementPreview(lastSceneView);
                }
            });

            randomRotationXToggle.SetValueWithoutNotify(randomRotationX);
            randomRotationXToggle.RegisterValueChangedCallback(change =>
            {
                randomRotationX = change.newValue;
                OnPlacementVariationSettingsChanged();
            });

            randomRotationYToggle.SetValueWithoutNotify(randomRotationY);
            randomRotationYToggle.RegisterValueChangedCallback(change =>
            {
                randomRotationY = change.newValue;
                OnPlacementVariationSettingsChanged();
            });

            randomRotationZToggle.SetValueWithoutNotify(randomRotationZ);
            randomRotationZToggle.RegisterValueChangedCallback(change =>
            {
                randomRotationZ = change.newValue;
                OnPlacementVariationSettingsChanged();
            });

            randomScaleMinimumField.SetValueWithoutNotify(Mathf.Max(0f, randomScaleMinimumPercent));
            randomScaleMinimumField.RegisterValueChangedCallback(change =>
            {
                randomScaleMinimumPercent = Mathf.Max(0f, change.newValue);
                randomScaleMinimumField.SetValueWithoutNotify(randomScaleMinimumPercent);
                OnPlacementVariationSettingsChanged();
            });

            randomScaleMaximumField.SetValueWithoutNotify(Mathf.Max(0f, randomScaleMaximumPercent));
            randomScaleMaximumField.RegisterValueChangedCallback(change =>
            {
                randomScaleMaximumPercent = Mathf.Max(0f, change.newValue);
                randomScaleMaximumField.SetValueWithoutNotify(randomScaleMaximumPercent);
                OnPlacementVariationSettingsChanged();
            });

            physicsDurationField.SetValueWithoutNotify(Mathf.Max(0.1f, physicsSimulationDuration));
            physicsDurationField.RegisterValueChangedCallback(change =>
            {
                physicsSimulationDuration = Mathf.Max(0.1f, change.newValue);
                physicsDurationField.SetValueWithoutNotify(physicsSimulationDuration);
            });

            physicsAddRigidbodyToggle.SetValueWithoutNotify(physicsAddRigidbodyIfMissing);
            physicsAddRigidbodyToggle.RegisterValueChangedCallback(change =>
            {
                physicsAddRigidbodyIfMissing = change.newValue;
            });

            physicsAutoSimulateAfterSpawnToggle.SetValueWithoutNotify(physicsAutoSimulateAfterSpawn);
            physicsAutoSimulateAfterSpawnToggle.RegisterValueChangedCallback(change =>
            {
                physicsAutoSimulateAfterSpawn = change.newValue;
                SetStatus(physicsAutoSimulateAfterSpawn
                    ? "Auto Simulate After Spawn enabled. Each new Physics object will settle immediately."
                    : "Auto Simulate After Spawn disabled.");
            });

            simulateBatchButton.clicked += SimulateActivePhysicsBatch;
            simulateSelectionButton.clicked += SimulateSelectedSceneObjects;
            confirmBatchButton.clicked += ConfirmPhysicsBatch;
            cancelSimulationButton.clicked += CancelPhysicsSimulation;

            rootVisualElement.Q<Button>("addSelectionButton").clicked += () =>
                AddProjectSelection(sourcePrefabs, "placeable asset(s)", true);
            rootVisualElement.Q<Button>("clearAssetsButton").clicked += ClearSourcePrefabs;
            rootVisualElement.Q<Button>("addPoleSelectionButton").clicked += () =>
                AddProjectSelection(clotheslinePolePrefabs, "pole prefab(s)", false);
            rootVisualElement.Q<Button>("clearPolePrefabsButton").clicked += () =>
                ClearPrefabList(clotheslinePolePrefabs, "Pole Prefabs cleared.");
            rootVisualElement.Q<Button>("addHangingSelectionButton").clicked += () =>
                AddProjectSelection(clotheslineHangingPrefabs, "hanging prefab(s)", false);
            rootVisualElement.Q<Button>("clearHangingPrefabsButton").clicked += () =>
                ClearPrefabList(clotheslineHangingPrefabs, "Hanging Objects cleared.");

            Toggle ignoreToggle = rootVisualElement.Q<Toggle>("ignoreAfterSpawnToggle");
            ignoreToggle.SetValueWithoutNotify(ignoreAfterSpawn);
            ignoreToggle.RegisterValueChangedCallback(change =>
            {
                ignoreAfterSpawn = change.newValue;
                RemoveDestroyedPlacedObjects();
                if (ignoreAfterSpawn)
                {
                    ignoredSpawnedObjects.Clear();
                    foreach (GameObject placedObject in placedSceneObjects)
                    {
                        ignoredSpawnedObjects.Add(placedObject);
                    }
                }
                else
                {
                    ignoredSpawnedObjects.Clear();
                }

                SetStatus(ignoreAfterSpawn
                    ? "Ignore After Spawn enabled for existing and future placements."
                    : "Ignore After Spawn disabled. Existing spawned objects can receive placement clicks.");
            });

            Toggle alignSurfaceToggle = rootVisualElement.Q<Toggle>("alignToSurfaceToggle");
            alignSurfaceToggle.SetValueWithoutNotify(alignToSurface);
            alignSurfaceToggle.RegisterValueChangedCallback(change =>
            {
                alignToSurface = change.newValue;
                if (toolEnabled && IsPlacementTab() && lastSceneView != null)
                {
                    UpdatePlacementPreview(lastSceneView);
                }

                SetStatus(alignToSurface
                    ? "Surface alignment enabled."
                    : "Surface alignment disabled.");
            });

            VisualElement placementAnchorContainer =
                rootVisualElement.Q<VisualElement>("placementAnchorContainer");
            List<string> placementAnchorLabels = new List<string>
            {
                "Object Pivot",
                "Center Pivot",
                "Bottom of Bounding Box"
            };
            PopupField<string> placementAnchorField = new PopupField<string>(
                "Placement Anchor",
                placementAnchorLabels,
                Mathf.Clamp((int)placementAnchorMode, 0, placementAnchorLabels.Count - 1));
            placementAnchorField.RegisterValueChangedCallback(change =>
            {
                int selectedIndex = placementAnchorLabels.IndexOf(change.newValue);
                if (selectedIndex < 0)
                {
                    return;
                }

                placementAnchorMode = (PlacementAnchorMode)selectedIndex;
                if (brushStrokeActive)
                {
                    RefreshBrushPreviewInstances(0);
                }

                if (toolEnabled && IsPlacementTab() && lastSceneView != null)
                {
                    UpdatePlacementPreview(lastSceneView);
                }
            });
            placementAnchorContainer.Add(placementAnchorField);

            FloatField rotationField = rootVisualElement.Q<FloatField>("rotationStepField");
            rotationField.SetValueWithoutNotify(Mathf.Max(1f, rotationStep));
            rotationField.RegisterValueChangedCallback(change =>
            {
                rotationStep = Mathf.Max(1f, change.newValue);
                rotationField.SetValueWithoutNotify(rotationStep);
            });

            FloatField verticalOffsetField = rootVisualElement.Q<FloatField>("verticalOffsetStepField");
            verticalOffsetField.SetValueWithoutNotify(Mathf.Max(0.001f, verticalOffsetStep));
            verticalOffsetField.RegisterValueChangedCallback(change =>
            {
                verticalOffsetStep = Mathf.Max(0.001f, change.newValue);
                verticalOffsetField.SetValueWithoutNotify(verticalOffsetStep);
            });

            RegisterPrefabDropTarget(assetThumbnailGrid, sourcePrefabs, true);
            RegisterPrefabDropTarget(poleThumbnailGrid, clotheslinePolePrefabs, false);
            RegisterPrefabDropTarget(hangingThumbnailGrid, clotheslineHangingPrefabs, false);
        }

        private void BuildClotheslineControls()
        {
            VisualElement controls = rootVisualElement.Q<VisualElement>("clotheslineControls");
            controls.Clear();

            AddToggleField(controls, "Create Pole Prefabs", clotheslineSpawnPoles, value => clotheslineSpawnPoles = value);
            AddIntegerField(controls, "Wire Count", clotheslineWireCount, 1, value => clotheslineWireCount = value);
            AddFloatField(controls, "Pole Height", clotheslinePoleHeight, 0f, value => clotheslinePoleHeight = value);
            AddFloatField(controls, "Drop From Pole Top", clotheslineWireDropFromTop, 0f, value => clotheslineWireDropFromTop = value);
            AddFloatField(controls, "Wire Spacing", clotheslineWireSpacing, 0f, value => clotheslineWireSpacing = value);
            AddFloatField(controls, "Sag Amount", clotheslineSagAmount, 0f, value => clotheslineSagAmount = value);
            AddFloatField(controls, "Wire Thickness", clotheslineWireThickness, 0.001f, value => clotheslineWireThickness = value);

            ObjectField materialField = new ObjectField("Wire Material")
            {
                objectType = typeof(Material),
                allowSceneObjects = false,
                value = clotheslineWireMaterial
            };
            materialField.RegisterValueChangedCallback(change => clotheslineWireMaterial = change.newValue as Material);
            controls.Add(materialField);

            AddIntegerField(controls, "Hanging Objects Per Span", hangingObjectCount, 0, value => hangingObjectCount = value);
            AddIntegerField(controls, "Hanging Wire Index", hangingWireIndex, 0, value => hangingWireIndex = value);
            AddSliderField(controls, "Hanging Start", hangingStart, value => hangingStart = value);
            AddSliderField(controls, "Hanging End", hangingEnd, value => hangingEnd = value);
            AddFloatField(controls, "Hanging Pivot Offset", hangingPivotOffset, 0f, value => hangingPivotOffset = value);
            AddToggleField(controls, "Align Hanging Objects To Wire", alignHangingObjectsToWire, value => alignHangingObjectsToWire = value);
            AddToggleField(controls, "Randomize Hanging Objects", randomizeHangingObjects, value => randomizeHangingObjects = value);
            AddIntegerField(controls, "Hanging Random Seed", hangingRandomSeed, int.MinValue, value => hangingRandomSeed = value);
        }

        private void RebuildCollectionControls()
        {
            BuildCollectionControl(
                assetCollectionBar,
                sourcePrefabs,
                sourceCollectionGuid,
                value => sourceCollectionGuid = value,
                true);
            BuildCollectionControl(
                poleCollectionBar,
                clotheslinePolePrefabs,
                poleCollectionGuid,
                value => poleCollectionGuid = value,
                false);
            BuildCollectionControl(
                hangingCollectionBar,
                clotheslineHangingPrefabs,
                hangingCollectionGuid,
                value => hangingCollectionGuid = value,
                false);
        }

        private void BuildCollectionControl(
            VisualElement container,
            List<GameObject> target,
            string savedGuid,
            Action<string> saveGuid,
            bool selectFirstAdded)
        {
            container.Clear();
            List<MapEditorPrefabCollection> collections = FindPrefabCollections();
            List<string> labels = new List<string> { "Choose Collection..." };
            List<string> guids = new List<string> { string.Empty };
            HashSet<string> usedLabels = new HashSet<string>();

            for (int index = 0; index < collections.Count; index++)
            {
                MapEditorPrefabCollection collection = collections[index];
                string label = collection.DisplayName;
                if (!usedLabels.Add(label))
                {
                    label = $"{label} ({index + 1})";
                    usedLabels.Add(label);
                }

                labels.Add(label);
                guids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(collection)));
            }

            int selectedIndex = Mathf.Max(0, guids.IndexOf(savedGuid));
            string selectedGuid = guids[selectedIndex];
            PopupField<string> popup = new PopupField<string>("Collection", labels, selectedIndex);
            popup.AddToClassList("collection-popup");
            popup.RegisterValueChangedCallback(change =>
            {
                int choiceIndex = labels.IndexOf(change.newValue);
                selectedGuid = choiceIndex >= 0 ? guids[choiceIndex] : string.Empty;
                saveGuid(selectedGuid);

                if (!string.IsNullOrEmpty(selectedGuid))
                {
                    ReplaceListWithCollection(selectedGuid, target, selectFirstAdded);
                }
            });
            container.Add(popup);

            Button addButton = new Button(() =>
                AddCollectionToList(selectedGuid, target, selectFirstAdded))
            {
                text = "Add Collection"
            };
            addButton.AddToClassList("collection-button");
            container.Add(addButton);

            Button saveButton = new Button(() =>
                SaveListAsCollection(target, saveGuid))
            {
                text = "Save as Collection"
            };
            saveButton.AddToClassList("collection-button");
            saveButton.AddToClassList("collection-button-last");
            container.Add(saveButton);
        }

        private static List<MapEditorPrefabCollection> FindPrefabCollections()
        {
            string[] collectionGuids = AssetDatabase.FindAssets("t:MapEditorPrefabCollection");
            List<MapEditorPrefabCollection> collections = new List<MapEditorPrefabCollection>();
            for (int index = 0; index < collectionGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(collectionGuids[index]);
                MapEditorPrefabCollection collection =
                    AssetDatabase.LoadAssetAtPath<MapEditorPrefabCollection>(path);
                if (collection != null)
                {
                    collections.Add(collection);
                }
            }

            // Editor-only ScriptableObject types are not returned by the type
            // filter in some Unity domain reload states. Fall back to scanning
            // asset files so existing collections remain discoverable.
            if (collections.Count == 0)
            {
                string[] assetPaths = AssetDatabase.GetAllAssetPaths();
                for (int index = 0; index < assetPaths.Length; index++)
                {
                    string path = assetPaths[index];
                    if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    MapEditorPrefabCollection collection =
                        AssetDatabase.LoadAssetAtPath<MapEditorPrefabCollection>(path);
                    if (collection != null)
                    {
                        collections.Add(collection);
                    }
                }
            }

            collections.Sort((left, right) =>
                string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            return collections;
        }

        private void AddCollectionToList(
            string collectionGuid,
            List<GameObject> target,
            bool selectFirstAdded)
        {
            string collectionPath = AssetDatabase.GUIDToAssetPath(collectionGuid);
            MapEditorPrefabCollection collection =
                AssetDatabase.LoadAssetAtPath<MapEditorPrefabCollection>(collectionPath);
            if (collection == null)
            {
                SetStatus("Choose a Prefab Collection first.");
                return;
            }

            int originalCount = target.Count;
            int addedCount = AddPrefabReferencesToList(collection.Prefabs, target);
            if (selectFirstAdded && originalCount == 0 && addedCount > 0)
            {
                activePrefabIndex = 0;
                ResetPrefabSelectionCursor();
            }

            RebuildAllThumbnailGrids();
            UpdateActiveAssetLabel();
            SetStatus(addedCount > 0
                ? $"Added {addedCount} asset(s) from {collection.DisplayName}."
                : $"{collection.DisplayName} contains no new valid assets.");
        }

        private void ReplaceListWithCollection(
            string collectionGuid,
            List<GameObject> target,
            bool selectFirstAdded)
        {
            string collectionPath = AssetDatabase.GUIDToAssetPath(collectionGuid);
            MapEditorPrefabCollection collection =
                AssetDatabase.LoadAssetAtPath<MapEditorPrefabCollection>(collectionPath);
            if (collection == null)
            {
                SetStatus("Choose a Prefab Collection first.");
                return;
            }

            target.Clear();
            int addedCount = AddPrefabReferencesToList(collection.Prefabs, target);
            if (selectFirstAdded)
            {
                activePrefabIndex = 0;
                ResetPrefabSelectionCursor();
                rotationY = 0f;
                DestroyPreview();
            }

            RebuildAllThumbnailGrids();
            UpdateActiveAssetLabel();
            SetStatus(addedCount > 0
                ? $"Loaded {addedCount} asset(s) from {collection.DisplayName}."
                : $"{collection.DisplayName} contains no valid assets.");
        }

        private void SaveListAsCollection(
            IReadOnlyList<GameObject> source,
            Action<string> saveGuid)
        {
            if (source == null || source.Count == 0)
            {
                SetStatus("Add at least one asset before saving a Collection.");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Prefab Collection",
                "New Prefab Collection",
                "asset",
                "Choose a name and location for this reusable Prefab Collection.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = AssetDatabase.GenerateUniqueAssetPath(path);
            MapEditorPrefabCollection collection =
                CreateInstance<MapEditorPrefabCollection>();
            collection.SetPrefabs(source);
            AssetDatabase.CreateAsset(collection, path);
            AssetDatabase.SaveAssets();

            string guid = AssetDatabase.AssetPathToGUID(path);
            saveGuid(guid);
            RebuildCollectionControls();
            EditorGUIUtility.PingObject(collection);
            SetStatus($"Saved Collection: {collection.DisplayName}");
        }

        private static void AddToggleField(VisualElement parent, string label, bool initialValue, Action<bool> onChanged)
        {
            Toggle field = new Toggle(label) { value = initialValue };
            field.RegisterValueChangedCallback(change => onChanged(change.newValue));
            parent.Add(field);
        }

        private static void AddIntegerField(VisualElement parent, string label, int initialValue, int minimum, Action<int> onChanged)
        {
            IntegerField field = new IntegerField(label) { value = initialValue };
            field.RegisterValueChangedCallback(change =>
            {
                int value = Mathf.Max(minimum, change.newValue);
                field.SetValueWithoutNotify(value);
                onChanged(value);
            });
            parent.Add(field);
        }

        private static void AddFloatField(VisualElement parent, string label, float initialValue, float minimum, Action<float> onChanged)
        {
            FloatField field = new FloatField(label) { value = initialValue };
            field.RegisterValueChangedCallback(change =>
            {
                float value = Mathf.Max(minimum, change.newValue);
                field.SetValueWithoutNotify(value);
                onChanged(value);
            });
            parent.Add(field);
        }

        private static void AddSliderField(VisualElement parent, string label, float initialValue, Action<float> onChanged)
        {
            Slider field = new Slider(label, 0f, 1f) { value = Mathf.Clamp01(initialValue) };
            field.RegisterValueChangedCallback(change => onChanged(Mathf.Clamp01(change.newValue)));
            parent.Add(field);
        }

        private void SelectTab(ToolTab tab)
        {
            if (activeTab == tab)
            {
                return;
            }

            if (physicsSimulationRunning)
            {
                StopPhysicsSimulation(true);
            }

            bool reactivate = toolEnabled;
            if (toolEnabled)
            {
                DeactivateTool(true, false);
            }

            activeTab = tab;
            UpdateTabUi();
            if (reactivate)
            {
                TryActivateTool();
            }
            else
            {
                UpdateToolActionUi();
            }
        }

        private void UpdateTabUi()
        {
            SetVisible(sharedPlacementPanel, IsPlacementTab());
            SetVisible(addToSceneTab, activeTab == ToolTab.AddToScene);
            SetVisible(physicsSimulationTab, activeTab == ToolTab.PhysicsSimulation);
            SetVisible(clotheslinesTab, activeTab == ToolTab.Clotheslines);
            SetActiveClass(addToSceneTabButton, activeTab == ToolTab.AddToScene);
            SetActiveClass(physicsSimulationTabButton, activeTab == ToolTab.PhysicsSimulation);
            SetActiveClass(clotheslinesTabButton, activeTab == ToolTab.Clotheslines);
            if (activeToolLabel != null)
            {
                switch (activeTab)
                {
                    case ToolTab.PhysicsSimulation:
                        activeToolLabel.text = "Physics Simulation";
                        break;
                    case ToolTab.Clotheslines:
                        activeToolLabel.text = "Clotheslines";
                        break;
                    default:
                        activeToolLabel.text = "Add to Scene";
                        break;
                }
            }
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (visible)
            {
                element.RemoveFromClassList("hidden");
            }
            else
            {
                element.AddToClassList("hidden");
            }
        }

        private static void SetActiveClass(VisualElement element, bool active)
        {
            if (active)
            {
                element.AddToClassList("active");
            }
            else
            {
                element.RemoveFromClassList("active");
            }
        }

        private void SetPlacementMode(PlacementMode mode)
        {
            if (placementMode == mode)
            {
                return;
            }

            ClearBrushStroke();
            placementMode = mode;
            UpdatePlacementModeUi();
            if (toolEnabled && IsPlacementTab() && lastSceneView != null)
            {
                UpdatePlacementPreview(lastSceneView);
                RepaintSceneView();
            }

            SetStatus(placementMode == PlacementMode.Brush
                ? "Brush mode enabled. Drag in the Scene View to preview and place a brush stroke."
                : "Single mode enabled. Click in the Scene View to place one object.");
        }

        private void UpdatePlacementModeUi()
        {
            SetActiveClass(singlePlacementModeButton, placementMode == PlacementMode.Single);
            SetActiveClass(brushPlacementModeButton, placementMode == PlacementMode.Brush);
            SetVisible(brushSettingsPanel, placementMode == PlacementMode.Brush);
        }

        private void SetPrefabSelectionMode(PrefabSelectionMode mode)
        {
            if (prefabSelectionMode == mode)
            {
                return;
            }

            prefabSelectionMode = mode;
            pendingRandomPrefab = null;
            sequentialPrefabIndex = FindNextValidPrefabIndex(activePrefabIndex);
            if (prefabSelectionMode == PrefabSelectionMode.Random)
            {
                pendingRandomPrefab = GetRandomPlaceablePrefab();
            }

            UpdatePrefabSelectionModeUi();
            UpdateActiveAssetLabel();
            if (toolEnabled && IsPlacementTab() && lastSceneView != null)
            {
                UpdatePlacementPreview(lastSceneView);
            }

            SetStatus(prefabSelectionMode == PrefabSelectionMode.Manual
                ? "Prefab selection: Manual. The active Placeable Asset will be used."
                : prefabSelectionMode == PrefabSelectionMode.Random
                    ? "Prefab selection: Random. Each spawn chooses from Placeable Assets."
                    : "Prefab selection: Sequential. Each spawn advances through Placeable Assets.");
        }

        private void UpdatePrefabSelectionModeUi()
        {
            SetActiveClass(manualPrefabModeButton, prefabSelectionMode == PrefabSelectionMode.Manual);
            SetActiveClass(randomPrefabModeButton, prefabSelectionMode == PrefabSelectionMode.Random);
            SetActiveClass(sequentialPrefabModeButton, prefabSelectionMode == PrefabSelectionMode.Sequential);
        }

        private void ClearBrushStroke()
        {
            DestroyBrushPreviewInstances();
            brushStrokeActive = false;
            brushHasLastWorldPosition = false;
            brushDistanceSinceLastSpawn = 0f;
            brushSequentialPrefabIndex = -1;
            brushPlannedSpawnCount = 0;
            brushStrokePath.Clear();
            brushStampCenters.Clear();
            brushStampNormals.Clear();
            brushSpawnPoints.Clear();
        }

        private void OnToolActionClicked()
        {
            if (!toolEnabled)
            {
                TryActivateTool();
                return;
            }

            if (activeTab == ToolTab.Clotheslines)
            {
                FinishClothesline();
            }
            else
            {
                DeactivateTool(false, true);
            }
        }

        private void TryActivateTool()
        {
            if (IsPlacementTab() && GetNextPrefabForSpawn() == null)
            {
                SetStatus("Add at least one Placeable Asset first.");
                return;
            }

            if (activeTab == ToolTab.Clotheslines && clotheslineSpawnPoles && clotheslinePolePrefabs.Count == 0)
            {
                SetStatus("Add at least one Pole Prefab, or disable Create Pole Prefabs.");
                return;
            }

            toolEnabled = true;
            hasPointerPosition = false;
            if (activeTab == ToolTab.Clotheslines)
            {
                clotheslinePoints.Clear();
                SetStatus("Clotheslines is active. Click any number of support points, then press Enter or Finish & Create.");
            }
            else if (activeTab == ToolTab.PhysicsSimulation)
            {
                SetStatus("Physics Simulation is active. Place objects into a batch, then simulate it from this tab.");
            }
            else
            {
                SetStatus("Add to Scene is active. Move the pointer over the Scene View.");
            }

            UpdateToolActionUi();
            RepaintSceneView();
        }

        private void DeactivateTool(bool cancelled, bool showStatus)
        {
            toolEnabled = false;
            hasPointerPosition = false;
            verticalOffset = 0f;
            clotheslinePoints.Clear();
            ClearBrushStroke();
            if (physicsSimulationRunning)
            {
                StopPhysicsSimulation(true);
            }
            DestroyPreview();
            UpdateToolActionUi();
            if (showStatus)
            {
                SetStatus(cancelled ? "Tool cancelled." : "Tool disabled.");
            }
            RepaintSceneView();
        }

        private void UpdateToolActionUi()
        {
            if (toolActionButton == null)
            {
                return;
            }

            if (!toolEnabled)
            {
                toolActionButton.text = "Enable Tool";
                toolActionButton.RemoveFromClassList("active");
                return;
            }

            toolActionButton.text = activeTab == ToolTab.Clotheslines
                ? "Finish & Create"
                : "Disable Tool";
            toolActionButton.AddToClassList("active");
        }

        private bool IsPlacementTab()
        {
            return activeTab == ToolTab.AddToScene || activeTab == ToolTab.PhysicsSimulation;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!toolEnabled)
            {
                return;
            }

            if (physicsSimulationRunning)
            {
                sceneView.Repaint();
                return;
            }

            sceneView.wantsMouseMove = true;
            lastSceneView = sceneView;
            Event currentEvent = Event.current;

            if (MapEditorSceneInteraction.CaptureSceneInput(currentEvent))
            {
                return;
            }

            if (HandleSharedKeyInput(currentEvent))
            {
                return;
            }

            if (IsPlacementTab() &&
                currentEvent.type == EventType.ScrollWheel &&
                currentEvent.shift &&
                !currentEvent.control)
            {
                float scrollDirection = GetScrollDirection(currentEvent);
                if (Mathf.Abs(scrollDirection) > 0.001f)
                {
                    rotationY = Mathf.Repeat(rotationY + scrollDirection * rotationStep, 360f);
                    if (brushStrokeActive)
                    {
                        RefreshBrushPreviewInstances(0);
                    }
                }

                UpdatePlacementPreview(sceneView);
                currentEvent.Use();
                return;
            }

            if (IsPlacementTab() &&
                currentEvent.type == EventType.ScrollWheel &&
                currentEvent.control &&
                !currentEvent.shift)
            {
                float scrollDirection = GetScrollDirection(currentEvent);
                if (Mathf.Abs(scrollDirection) > 0.001f)
                {
                    // Unity reports the wheel direction opposite to the
                    // expected vertical movement direction.
                    verticalOffset -= scrollDirection * verticalOffsetStep;
                    if (brushStrokeActive)
                    {
                        RefreshBrushPreviewInstances(0);
                    }

                    UpdatePlacementPreview(sceneView);
                    SetStatus($"Vertical offset: {verticalOffset:0.###} units");
                }

                // While the Map Editor is active, Ctrl + Wheel is reserved
                // for vertical placement and must not zoom the Scene View.
                currentEvent.Use();
                return;
            }

            if (MapEditorSceneInteraction.IsPointerEvent(currentEvent))
            {
                RemoveDestroyedIgnoredObjects();
                hasPointerPosition = sceneInteraction.TryGetPlacementPosition(
                    currentEvent.mousePosition,
                    IsIgnoredTransform,
                    out pointerPosition,
                    out pointerNormal);

                if (IsPlacementTab())
                {
                    UpdatePlacementPreview(sceneView);
                }
                else if (currentEvent.type == EventType.MouseMove)
                {
                    sceneView.Repaint();
                }
            }

            if (IsPlacementTab() &&
                placementMode == PlacementMode.Brush &&
                HandleBrushSceneInput(sceneView, currentEvent))
            {
                return;
            }

            if (MapEditorSceneInteraction.IsPrimaryClick(currentEvent))
            {
                if (!hasPointerPosition)
                {
                    SetStatus("No usable surface was found for placement.");
                    return;
                }

                if (IsPlacementTab())
                {
                    SpawnCurrentPrefab(pointerPosition, pointerNormal);
                }
                else
                {
                    clotheslinePoints.Add(pointerPosition);
                    hasPointerPosition = false;
                    SetStatus($"Clotheslines: {clotheslinePoints.Count} point(s) recorded. Press Enter to finish.");
                    sceneView.Repaint();
                }

                currentEvent.Use();
            }

            if (activeTab == ToolTab.Clotheslines && currentEvent.type == EventType.Repaint)
            {
                DrawClotheslinePreview();
            }
            else if (IsPlacementTab() &&
                     placementMode == PlacementMode.Brush &&
                     currentEvent.type == EventType.Repaint)
            {
                DrawBrushPreview();
            }
        }

        private bool HandleBrushSceneInput(SceneView sceneView, Event currentEvent)
        {
            if (MapEditorSceneInteraction.IsPrimaryClick(currentEvent))
            {
                if (!hasPointerPosition)
                {
                    SetStatus("No usable surface was found for the brush stroke.");
                    currentEvent.Use();
                    return true;
                }

                BeginBrushStroke(currentEvent.mousePosition);
                currentEvent.Use();
                sceneView.Repaint();
                return true;
            }

            if (brushStrokeActive &&
                currentEvent.type == EventType.MouseDrag &&
                currentEvent.button == 0 &&
                !currentEvent.alt)
            {
                UpdateBrushStrokePointer(currentEvent.mousePosition);
                currentEvent.Use();
                sceneView.Repaint();
                return true;
            }

            if (brushStrokeActive && currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                UpdateBrushStrokePointer(currentEvent.mousePosition);
                SpawnBrushStroke();
                ClearBrushStroke();
                hasPointerPosition = false;
                if (!physicsSimulationRunning)
                {
                    UpdatePlacementPreview(sceneView);
                }
                currentEvent.Use();
                sceneView.Repaint();
                return true;
            }

            return false;
        }

        private void BeginBrushStroke(Vector2 mousePosition)
        {
            ClearBrushStroke();
            brushStrokeActive = true;
            brushLastMousePosition = mousePosition;
            brushLastWorldPosition = pointerPosition;
            brushLastWorldNormal = pointerNormal;
            brushHasLastWorldPosition = true;
            brushDistanceSinceLastSpawn = 0f;
            brushSequentialPrefabIndex = prefabSelectionMode == PrefabSelectionMode.Sequential
                ? GetNextPrefabSelectionIndex()
                : -1;
            brushStrokePath.Add(pointerPosition);
            AddBrushStamp(pointerPosition, pointerNormal);
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
            }
        }

        private void UpdateBrushStrokePointer(Vector2 mousePosition)
        {
            if ((mousePosition - brushLastMousePosition).sqrMagnitude < 0.01f)
            {
                return;
            }

            brushLastMousePosition = mousePosition;
            if (!hasPointerPosition)
            {
                brushHasLastWorldPosition = false;
                brushDistanceSinceLastSpawn = 0f;
                return;
            }

            if (brushStrokePath.Count == 0 ||
                Vector3.Distance(brushStrokePath[brushStrokePath.Count - 1], pointerPosition) > 0.001f)
            {
                brushStrokePath.Add(pointerPosition);
            }

            if (!brushHasLastWorldPosition)
            {
                brushLastWorldPosition = pointerPosition;
                brushLastWorldNormal = pointerNormal;
                brushHasLastWorldPosition = true;
                return;
            }

            AddBrushSpawnsAlongSegment(
                brushLastWorldPosition,
                brushLastWorldNormal,
                pointerPosition,
                pointerNormal);
            brushLastWorldPosition = pointerPosition;
            brushLastWorldNormal = pointerNormal;
        }

        private void AddBrushSpawnsAlongSegment(
            Vector3 start,
            Vector3 startNormal,
            Vector3 end,
            Vector3 endNormal)
        {
            float segmentLength = Vector3.Distance(start, end);
            if (segmentLength < 0.0001f)
            {
                return;
            }

            float spacing = Mathf.Max(MinimumBrushSpacing, brushSpacing);
            float distanceToNextSpawn = spacing - brushDistanceSinceLastSpawn;
            float distanceAlongSegment = 0f;
            while (distanceAlongSegment + distanceToNextSpawn <= segmentLength + 0.0001f)
            {
                distanceAlongSegment += distanceToNextSpawn;
                float normalizedDistance = Mathf.Clamp01(distanceAlongSegment / segmentLength);
                AddBrushStamp(
                    Vector3.Lerp(start, end, normalizedDistance),
                    Vector3.Slerp(startNormal, endNormal, normalizedDistance));
                brushDistanceSinceLastSpawn = 0f;
                distanceToNextSpawn = spacing;
            }

            brushDistanceSinceLastSpawn += segmentLength - distanceAlongSegment;
        }

        private void AddBrushStamp(Vector3 center, Vector3 normal)
        {
            brushStampCenters.Add(center);
            brushStampNormals.Add(normal);
            int previewStartIndex = brushSpawnPoints.Count;
            int count = Mathf.Max(1, brushObjectsPerStep);
            for (int index = 0; index < count; index++)
            {
                GameObject prefab = GetBrushSpawnPrefab();
                if (prefab == null)
                {
                    continue;
                }

                Vector3 position = brushRandomizePosition
                    ? GetRandomBrushPosition(center, normal)
                    : center;
                brushSpawnPoints.Add(new BrushSpawnPoint
                {
                    Prefab = prefab,
                    Position = position,
                    Normal = normal,
                    Variation = CreatePlacementVariation(prefab)
                });
            }

            RefreshBrushPreviewInstances(previewStartIndex);
        }

        private GameObject GetBrushSpawnPrefab()
        {
            if (prefabSelectionMode == PrefabSelectionMode.Manual)
            {
                return GetCurrentPrefab();
            }

            brushPlannedSpawnCount++;
            if (prefabSelectionMode == PrefabSelectionMode.Random)
            {
                return brushPlannedSpawnCount == 1
                    ? GetNextPrefabForSpawn()
                    : GetRandomPlaceablePrefab();
            }

            if (brushSequentialPrefabIndex < 0)
            {
                brushSequentialPrefabIndex = GetNextPrefabSelectionIndex();
            }

            int selectedIndex = FindNextValidPrefabIndex(brushSequentialPrefabIndex);
            if (selectedIndex < 0)
            {
                return null;
            }

            brushSequentialPrefabIndex = FindNextValidPrefabIndex(selectedIndex + 1);
            return sourcePrefabs[selectedIndex];
        }

        private GameObject GetRandomPlaceablePrefab()
        {
            List<GameObject> validPrefabs = new List<GameObject>();
            for (int index = 0; index < sourcePrefabs.Count; index++)
            {
                GameObject prefab = sourcePrefabs[index];
                if (prefab != null && IsValidSceneAsset(AssetDatabase.GetAssetPath(prefab)))
                {
                    validPrefabs.Add(prefab);
                }
            }

            return validPrefabs.Count > 0
                ? validPrefabs[brushRandom.Next(validPrefabs.Count)]
                : GetCurrentPrefab();
        }

        private bool IsValidPlaceablePrefab(GameObject prefab)
        {
            return prefab != null &&
                   sourcePrefabs.Contains(prefab) &&
                   IsValidSceneAsset(AssetDatabase.GetAssetPath(prefab));
        }

        private GameObject GetNextPrefabForSpawn()
        {
            switch (prefabSelectionMode)
            {
                case PrefabSelectionMode.Random:
                    if (!IsValidPlaceablePrefab(pendingRandomPrefab))
                    {
                        pendingRandomPrefab = GetRandomPlaceablePrefab();
                    }

                    return pendingRandomPrefab;

                case PrefabSelectionMode.Sequential:
                    int nextIndex = GetNextPrefabSelectionIndex();
                    return nextIndex >= 0 ? sourcePrefabs[nextIndex] : null;

                default:
                    return GetCurrentPrefab();
            }
        }

        private int GetNextPrefabSelectionIndex()
        {
            int startIndex = sequentialPrefabIndex >= 0
                ? sequentialPrefabIndex
                : activePrefabIndex;
            return FindNextValidPrefabIndex(startIndex);
        }

        private int FindNextValidPrefabIndex(int startIndex)
        {
            int count = sourcePrefabs.Count;
            if (count == 0)
            {
                return -1;
            }

            int normalizedStart = ((startIndex % count) + count) % count;
            for (int offset = 0; offset < count; offset++)
            {
                int index = (normalizedStart + offset) % count;
                GameObject prefab = sourcePrefabs[index];
                if (prefab != null && IsValidSceneAsset(AssetDatabase.GetAssetPath(prefab)))
                {
                    return index;
                }
            }

            return -1;
        }

        private void AdvancePrefabSelectionAfterSpawn(GameObject spawnedPrefab)
        {
            if (prefabSelectionMode == PrefabSelectionMode.Random)
            {
                pendingRandomPrefab = GetRandomPlaceablePrefab();
            }
            else if (prefabSelectionMode == PrefabSelectionMode.Sequential)
            {
                int usedIndex = sourcePrefabs.IndexOf(spawnedPrefab);
                if (usedIndex < 0)
                {
                    usedIndex = sequentialPrefabIndex;
                }

                sequentialPrefabIndex = FindNextValidPrefabIndex(usedIndex + 1);
            }
        }

        private Vector3 GetRandomBrushPosition(Vector3 center, Vector3 surfaceNormal)
        {
            float radius = Mathf.Max(0f, brushRadius);
            if (radius <= 0.0001f)
            {
                return center;
            }

            Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f
                ? surfaceNormal.normalized
                : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.Cross(normal, Vector3.right);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            float angle = (float)(brushRandom.NextDouble() * Math.PI * 2.0);
            float distance = Mathf.Sqrt((float)brushRandom.NextDouble()) * radius;
            Vector3 offset = tangent * (Mathf.Cos(angle) * distance) +
                             bitangent * (Mathf.Sin(angle) * distance);
            return center + offset;
        }

        private void DrawBrushPreview()
        {
            if (!brushStrokeActive && !hasPointerPosition)
            {
                return;
            }

            Color previousColor = Handles.color;
            float radius = Mathf.Max(0.05f, brushRadius);
            Vector3 cursorPosition = brushStrokeActive && brushHasLastWorldPosition
                ? brushLastWorldPosition
                : pointerPosition;
            Vector3 cursorSurfaceNormal = brushStrokeActive && brushHasLastWorldPosition
                ? brushLastWorldNormal
                : pointerNormal;
            Vector3 cursorNormal = brushAlignToSurface ? cursorSurfaceNormal : Vector3.up;

            Handles.color = new Color(0.2f, 0.75f, 1f, 0.12f);
            if (hasPointerPosition)
            {
                Vector3 pointerDiscNormal = brushAlignToSurface ? pointerNormal : Vector3.up;
                Handles.DrawSolidDisc(pointerPosition, pointerDiscNormal, radius);
            }

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            if (brushStrokeActive)
            {
                int stride = Mathf.Max(1, Mathf.CeilToInt(brushStampCenters.Count / 200f));
                for (int index = 0; index < brushStampCenters.Count; index += stride)
                {
                    Vector3 center = brushStampCenters[index];
                    Vector3 surfaceNormal = brushStampNormals.Count > 0
                        ? brushStampNormals[Mathf.Min(index, brushStampNormals.Count - 1)]
                        : cursorSurfaceNormal;
                    Vector3 normal = brushAlignToSurface ? surfaceNormal : Vector3.up;
                    Handles.DrawWireDisc(center, normal, radius);
                }

                if (brushStrokePath.Count > 1)
                {
                    Handles.color = new Color(1f, 0.8f, 0.15f, 1f);
                    Handles.DrawAAPolyLine(3f, brushStrokePath.ToArray());
                }

                Handles.color = new Color(0.25f, 1f, 0.55f, 1f);
                for (int index = 0; index < brushSpawnPoints.Count; index++)
                {
                    BrushSpawnPoint spawnPoint = brushSpawnPoints[index];
                    Handles.DotHandleCap(
                        0,
                        spawnPoint.Position,
                        Quaternion.identity,
                        HandleUtility.GetHandleSize(spawnPoint.Position) * 0.055f,
                        EventType.Repaint);
                }
            }
            else
            {
                Handles.DrawWireDisc(cursorPosition, cursorNormal, radius);
            }

            Handles.color = previousColor;
        }

        private bool HandleSharedKeyInput(Event currentEvent)
        {
            if (currentEvent.type != EventType.KeyDown)
            {
                return false;
            }

            if (currentEvent.keyCode == KeyCode.Escape)
            {
                DeactivateTool(true, true);
                currentEvent.Use();
                return true;
            }

            if (activeTab == ToolTab.Clotheslines &&
                (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter))
            {
                FinishClothesline();
                currentEvent.Use();
                return true;
            }

            if (IsPlacementTab())
            {
                if (currentEvent.keyCode == KeyCode.LeftBracket || currentEvent.character == '[')
                {
                    CycleActivePrefab(-1);
                    currentEvent.Use();
                    return true;
                }

                if (currentEvent.keyCode == KeyCode.RightBracket || currentEvent.character == ']')
                {
                    CycleActivePrefab(1);
                    currentEvent.Use();
                    return true;
                }
            }

            return false;
        }

        private void FinishClothesline()
        {
            if (clotheslinePoints.Count < 2)
            {
                SetStatus("At least two support points are required. Press Esc to cancel.");
                return;
            }

            List<Vector3> completedPoints = new List<Vector3>(clotheslinePoints);
            toolEnabled = false;
            hasPointerPosition = false;
            clotheslinePoints.Clear();
            UpdateToolActionUi();
            CreateClotheslineObject(completedPoints);
        }

        private void DrawClotheslinePreview()
        {
            List<Vector3> previewPoints = new List<Vector3>(clotheslinePoints);
            if (hasPointerPosition)
            {
                previewPoints.Add(pointerPosition);
            }

            Handles.color = new Color(0.25f, 1f, 0.4f, 1f);
            for (int index = 0; index < previewPoints.Count; index++)
            {
                Vector3 point = previewPoints[index];
                Handles.SphereHandleCap(
                    0,
                    point,
                    Quaternion.identity,
                    HandleUtility.GetHandleSize(point) * 0.11f,
                    EventType.Repaint);
                Handles.Label(point, $"Point {index + 1}");
            }

            if (previewPoints.Count < 2)
            {
                return;
            }

            Handles.color = new Color(0.95f, 0.75f, 0.1f, 1f);
            int previewWireCount = Mathf.Max(1, clotheslineWireCount);
            for (int wireIndex = 0; wireIndex < previewWireCount; wireIndex++)
            {
                for (int spanIndex = 0; spanIndex < previewPoints.Count - 1; spanIndex++)
                {
                    Vector3 start = GetPreviewWirePoint(previewPoints[spanIndex], wireIndex);
                    Vector3 end = GetPreviewWirePoint(previewPoints[spanIndex + 1], wireIndex);
                    Vector3 control = Vector3.Lerp(start, end, 0.5f) + Vector3.down * clotheslineSagAmount;
                    Vector3 previous = start;

                    for (int segmentIndex = 1; segmentIndex <= 12; segmentIndex++)
                    {
                        float t = segmentIndex / 12f;
                        Vector3 next = EvaluateQuadraticBezier(start, control, end, t);
                        Handles.DrawLine(previous, next);
                        previous = next;
                    }
                }
            }

            DrawHangingObjectPreview(previewPoints);
        }

        private void DrawHangingObjectPreview(IReadOnlyList<Vector3> points)
        {
            if (clotheslineHangingPrefabs.Count == 0 || hangingObjectCount < 1)
            {
                return;
            }

            Handles.color = new Color(0.25f, 0.85f, 1f, 1f);
            int wireIndex = Mathf.Clamp(hangingWireIndex, 0, Mathf.Max(0, clotheslineWireCount - 1));
            float start = Mathf.Clamp01(Mathf.Min(hangingStart, hangingEnd));
            float end = Mathf.Clamp01(Mathf.Max(hangingStart, hangingEnd));

            int spanCount = points.Count - 1;
            for (int spanIndex = 0; spanIndex < spanCount; spanIndex++)
            {
                for (int index = 0; index < hangingObjectCount; index++)
                {
                    float spanPosition = hangingObjectCount == 1
                        ? (start + end) * 0.5f
                        : Mathf.Lerp(start, end, index / (float)(hangingObjectCount - 1));
                    float normalized = (spanIndex + spanPosition) / spanCount;
                    if (!TryEvaluatePreviewWire(points, wireIndex, normalized, out Vector3 position))
                    {
                        continue;
                    }

                    Handles.DotHandleCap(
                        0,
                        position + Vector3.down * hangingPivotOffset,
                        Quaternion.identity,
                        HandleUtility.GetHandleSize(position) * 0.06f,
                        EventType.Repaint);
                }
            }
        }

        private bool TryEvaluatePreviewWire(
            IReadOnlyList<Vector3> points,
            int wireIndex,
            float normalizedPosition,
            out Vector3 position)
        {
            position = default;
            int spanCount = points.Count - 1;
            if (spanCount < 1)
            {
                return false;
            }

            float scaled = Mathf.Clamp01(normalizedPosition) * spanCount;
            int spanIndex = Mathf.Min(Mathf.FloorToInt(scaled), spanCount - 1);
            float t = normalizedPosition >= 1f && spanIndex == spanCount - 1
                ? 1f
                : scaled - spanIndex;
            Vector3 start = GetPreviewWirePoint(points[spanIndex], wireIndex);
            Vector3 end = GetPreviewWirePoint(points[spanIndex + 1], wireIndex);
            Vector3 control = Vector3.Lerp(start, end, 0.5f) + Vector3.down * clotheslineSagAmount;
            position = EvaluateQuadraticBezier(start, control, end, t);
            return true;
        }

        private Vector3 GetPreviewWirePoint(Vector3 point, int wireIndex)
        {
            if (!clotheslineSpawnPoles)
            {
                return point + Vector3.down * (wireIndex * clotheslineWireSpacing);
            }

            float height = Mathf.Max(
                0f,
                clotheslinePoleHeight - clotheslineWireDropFromTop - wireIndex * clotheslineWireSpacing);
            return point + Vector3.up * height;
        }

        private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float inverseT = 1f - t;
            return inverseT * inverseT * start + 2f * inverseT * t * control + t * t * end;
        }

        private void CreateClotheslineObject(IReadOnlyList<Vector3> points)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                SetStatus("The active Scene is invalid.");
                return;
            }

            bool spawnPoles = clotheslineSpawnPoles && clotheslinePolePrefabs.Count > 0;
            GameObject root = new GameObject("Clothesline");
            root.transform.position = points[0];

            ClotheslineGenerator generator = root.AddComponent<ClotheslineGenerator>();
            GameObject firstPolePrefab = spawnPoles ? clotheslinePolePrefabs[0] : null;
            generator.ConfigureSettings(
                firstPolePrefab,
                points.Count,
                4f,
                spawnPoles ? clotheslinePoleHeight : 0f,
                clotheslineWireCount,
                spawnPoles ? clotheslineWireDropFromTop : 0f,
                clotheslineWireSpacing,
                clotheslineSagAmount);
            generator.ConfigurePolePrefabs(spawnPoles ? clotheslinePolePrefabs : null);
            generator.ConfigureWireStyle(clotheslineWireMaterial, clotheslineWireThickness);
            generator.ConfigureHangingObjects(
                clotheslineHangingPrefabs,
                hangingObjectCount,
                hangingWireIndex,
                hangingStart,
                hangingEnd,
                hangingPivotOffset,
                alignHangingObjectsToWire,
                randomizeHangingObjects,
                hangingRandomSeed);
            generator.GenerateFromWorldPoints(points, spawnPoles);
            generator.RegenerateAttachments();

            Undo.RegisterCreatedObjectUndo(root, "Create Clothesline");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(activeScene);
            SetStatus(spawnPoles
                ? "Clothesline with poles, wires, and hanging objects created."
                : "Wire-only Clothesline with anchors and hanging objects created.");
            RepaintSceneView();
        }

        private void SpawnCurrentPrefab(Vector3 position, Vector3 surfaceNormal)
        {
            GameObject sourcePrefab = GetNextPrefabForSpawn();
            if (sourcePrefab == null)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                SetStatus("The active Scene is invalid.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            bool physicsPlacement = activeTab == ToolTab.PhysicsSimulation;
            Undo.SetCurrentGroupName(physicsPlacement ? "Map Editor Physics Batch Spawn" : "Map Editor Spawn");

            GameObject physicsBatch = physicsPlacement ? GetOrCreatePhysicsBatch(activeScene) : null;
            if (physicsPlacement && physicsBatch == null)
            {
                SetStatus("Could not create a Physics Batch in the active Scene.");
                return;
            }

            bool previousBatchContentsWereSimulated = physicsBatch != null &&
                (physicsBatch.transform.childCount == 0 || physicsBatchSimulationCompleted);

            GameObject instance = SpawnPrefabInstance(
                sourcePrefab,
                position,
                surfaceNormal,
                activeScene,
                physicsBatch,
                physicsBatch != null,
                null,
                GetPreviewOrCreatePlacementVariation(sourcePrefab));
            if (instance == null)
            {
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            AdvancePrefabSelectionAfterSpawn(sourcePrefab);
            hasPreviewVariation = false;
            UpdateActiveAssetLabel();

            if (physicsBatch != null)
            {
                physicsBatchSimulationCompleted = false;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(activeScene);
            string anchorLabel = GetPlacementAnchorLabel();
            if (physicsPlacement)
            {
                SetStatus($"Added {sourcePrefab.name} to {physicsBatch.name}. {anchorLabel}. Place more objects or simulate the batch.");
            }
            else
            {
                SetStatus(alignToSurface
                    ? $"Spawned {sourcePrefab.name}. {anchorLabel}, surface aligned, rotation: {rotationY:0.#}°"
                    : $"Spawned {sourcePrefab.name}. {anchorLabel}, Y rotation: {rotationY:0.#}°");
            }
            UpdatePhysicsUi();
            if (physicsPlacement && physicsAutoSimulateAfterSpawn)
            {
                List<GameObject> autoSimulationRoots = new List<GameObject> { instance };
                StartPhysicsSimulation(
                    autoSimulationRoots,
                    $"Auto Simulate: {sourcePrefab.name}",
                    previousBatchContentsWereSimulated);
            }

            if (!physicsSimulationRunning && toolEnabled && IsPlacementTab() && lastSceneView != null)
            {
                UpdatePlacementPreview(lastSceneView);
            }
            RepaintSceneView();
        }

        private void SpawnBrushStroke()
        {
            if (brushSpawnPoints.Count == 0)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                SetStatus("The active Scene is invalid.");
                return;
            }

            bool physicsPlacement = activeTab == ToolTab.PhysicsSimulation;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(physicsPlacement
                ? "Map Editor Physics Brush Stroke"
                : "Map Editor Brush Stroke");

            GameObject physicsBatch = physicsPlacement ? GetOrCreatePhysicsBatch(activeScene) : null;
            if (physicsPlacement && physicsBatch == null)
            {
                SetStatus("Could not create a Physics Batch in the active Scene.");
                return;
            }

            bool previousBatchContentsWereSimulated = physicsBatch != null &&
                (physicsBatch.transform.childCount == 0 || physicsBatchSimulationCompleted);
            GameObject strokeParent = physicsBatch != null
                ? physicsBatch
                : CreateBrushStrokeRoot(activeScene);
            List<GameObject> spawnedObjects = new List<GameObject>(brushSpawnPoints.Count);
            for (int index = 0; index < brushSpawnPoints.Count; index++)
            {
                BrushSpawnPoint spawnPoint = brushSpawnPoints[index];
                GameObject instance = SpawnPrefabInstance(
                    spawnPoint.Prefab,
                    spawnPoint.Position,
                    spawnPoint.Normal,
                    activeScene,
                    strokeParent,
                    physicsBatch != null,
                    brushAlignToSurface,
                    spawnPoint.Variation);
                if (instance != null)
                {
                    spawnedObjects.Add(instance);
                    AdvancePrefabSelectionAfterSpawn(spawnPoint.Prefab);
                }
            }

            if (spawnedObjects.Count == 0)
            {
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            if (physicsBatch != null)
            {
                physicsBatchSimulationCompleted = false;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = spawnedObjects[spawnedObjects.Count - 1];
            EditorSceneManager.MarkSceneDirty(activeScene);
            UpdateActiveAssetLabel();
            SetStatus(physicsPlacement
                ? $"Added {spawnedObjects.Count} object(s) to {physicsBatch.name} as one brush stroke."
                : $"Added {spawnedObjects.Count} object(s) to one Brush Stroke group.");
            UpdatePhysicsUi();

            if (physicsPlacement && physicsAutoSimulateAfterSpawn)
            {
                StartPhysicsSimulation(
                    spawnedObjects,
                    $"Auto Simulate Brush: {spawnedObjects.Count} object(s)",
                    previousBatchContentsWereSimulated);
            }

            RepaintSceneView();
        }

        private GameObject SpawnPrefabInstance(
            GameObject sourcePrefab,
            Vector3 position,
            Vector3 surfaceNormal,
            Scene activeScene,
            GameObject parent,
            bool addTemporaryRigidbody = false,
            bool? alignToSurfaceOverride = null,
            PlacementVariation? placementVariation = null)
        {
            if (sourcePrefab == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab, activeScene) as GameObject;
            if (instance == null)
            {
                instance = Instantiate(sourcePrefab);
                SceneManager.MoveGameObjectToScene(instance, activeScene);
            }

            if (instance == null)
            {
                return null;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Map Editor Spawn");
            Undo.RecordObject(instance.transform, "Place Map Editor Object");
            ApplyPlacement(
                instance,
                position,
                surfaceNormal,
                alignToSurfaceOverride,
                true,
                placementVariation,
                sourcePrefab.transform.localRotation);
            if (parent != null)
            {
                Undo.SetTransformParent(instance.transform, parent.transform, "Add Object To Placement Batch");
            }

            if (addTemporaryRigidbody && physicsAddRigidbodyIfMissing)
            {
                EnsureRigidbody(instance);
            }

            if (ignoreAfterSpawn)
            {
                ignoredSpawnedObjects.Add(instance);
            }

            placedSceneObjects.Add(instance);
            return instance;
        }

        private GameObject CreateBrushStrokeRoot(Scene activeScene)
        {
            int strokeIndex = 1;
            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                const string prefix = "Map Editor Brush Stroke ";
                if (roots[index].name.StartsWith(prefix, StringComparison.Ordinal) &&
                    int.TryParse(roots[index].name.Substring(prefix.Length), out int existingIndex))
                {
                    strokeIndex = Mathf.Max(strokeIndex, existingIndex + 1);
                }
            }

            GameObject strokeRoot = new GameObject($"Map Editor Brush Stroke {strokeIndex:00}");
            Undo.RegisterCreatedObjectUndo(strokeRoot, "Create Brush Stroke Group");
            return strokeRoot;
        }

        private GameObject GetOrCreatePhysicsBatch(Scene activeScene)
        {
            if (activePhysicsBatch != null && activePhysicsBatch.scene == activeScene)
            {
                return activePhysicsBatch;
            }

            activePhysicsBatch = null;
            physicsBatchesRoot = null;
            GameObject[] sceneRoots = activeScene.GetRootGameObjects();
            for (int index = 0; index < sceneRoots.Length; index++)
            {
                if (sceneRoots[index].name == "Map Editor Physics Batches")
                {
                    physicsBatchesRoot = sceneRoots[index];
                    break;
                }
            }

            if (physicsBatchesRoot == null)
            {
                physicsBatchesRoot = new GameObject("Map Editor Physics Batches");
                Undo.RegisterCreatedObjectUndo(physicsBatchesRoot, "Create Physics Batches Root");
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            activePhysicsBatch = new GameObject(
                $"Physics Batch {physicsBatchesRoot.transform.childCount + 1:00}");
            physicsBatchSimulationCompleted = false;
            Undo.RegisterCreatedObjectUndo(activePhysicsBatch, "Create Physics Batch");
            Undo.SetTransformParent(
                activePhysicsBatch.transform,
                physicsBatchesRoot.transform,
                "Parent Physics Batch");
            EditorSceneManager.MarkSceneDirty(activeScene);
            UpdatePhysicsUi();
            return activePhysicsBatch;
        }

        private Rigidbody EnsureRigidbody(GameObject target)
        {
            if (target == null || target.GetComponentsInChildren<Rigidbody>(true).Length > 0)
            {
                return target != null ? target.GetComponentInChildren<Rigidbody>(true) : null;
            }

            Rigidbody body = Undo.AddComponent<Rigidbody>(target);
            if (body != null)
            {
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                physicsAddedComponents.Add(body);
            }

            return body;
        }

        private void SimulateActivePhysicsBatch()
        {
            if (physicsSimulationRunning)
            {
                return;
            }

            if (activePhysicsBatch == null || activePhysicsBatch.transform.childCount == 0)
            {
                SetStatus("Place objects in the Physics Simulation tab before simulating a batch.");
                return;
            }

            EnsureBatchRigidbodies(activePhysicsBatch);
            List<GameObject> roots = new List<GameObject> { activePhysicsBatch };
            StartPhysicsSimulation(roots, activePhysicsBatch.name, true);
        }

        private void SimulateSelectedSceneObjects()
        {
            if (physicsSimulationRunning)
            {
                return;
            }

            List<GameObject> selectedRoots = GetSelectedSceneObjects();
            if (selectedRoots.Count == 0)
            {
                SetStatus("Select one or more objects in the active Scene first.");
                return;
            }

            if (physicsAddRigidbodyIfMissing)
            {
                for (int index = 0; index < selectedRoots.Count; index++)
                {
                    EnsureRigidbody(selectedRoots[index]);
                }
            }

            StartPhysicsSimulation(selectedRoots, "Scene Selection", false);
        }

        private void ConfirmPhysicsBatch()
        {
            if (physicsSimulationRunning)
            {
                SetStatus("Finish the current simulation before confirming the batch.");
                return;
            }

            if (activePhysicsBatch == null || activePhysicsBatch.transform.childCount == 0)
            {
                SetStatus("There is no active Physics Batch to confirm.");
                return;
            }

            if (!physicsBatchSimulationCompleted)
            {
                SetStatus("Simulate the active batch first, then confirm its final transforms.");
                return;
            }

            Scene activeScene = activePhysicsBatch.scene;
            EditorSceneManager.MarkSceneDirty(activeScene);
            string batchName = activePhysicsBatch.name;
            activePhysicsBatch = null;
            SetStatus($"{batchName} confirmed. Switch to Physics Simulation to create the next batch.");
            UpdatePhysicsUi();
        }

        private void CancelPhysicsSimulation()
        {
            if (!physicsSimulationRunning)
            {
                SetStatus("No Physics Simulation is running.");
                return;
            }

            StopPhysicsSimulation(true);
            SetStatus("Physics Simulation cancelled. Original transforms were restored.");
            UpdatePhysicsUi();
            RepaintSceneView();
        }

        private void StartPhysicsSimulation(
            IReadOnlyList<GameObject> roots,
            string label,
            bool completesActiveBatch)
        {
            if (physicsSimulationRunning)
            {
                return;
            }

            physicsTargetBodies.Clear();
            for (int index = 0; index < roots.Count; index++)
            {
                GameObject root = roots[index];
                if (root == null)
                {
                    continue;
                }

                Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
                for (int bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
                {
                    Rigidbody body = bodies[bodyIndex];
                    if (body != null && body.gameObject.scene.IsValid())
                    {
                        physicsTargetBodies.Add(body);
                    }
                }
            }

            if (physicsTargetBodies.Count == 0)
            {
                SetStatus("No Rigidbody was found in the selected Physics Batch or Scene objects.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            Rigidbody[] allBodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            physicsBodyStates.Clear();
            previousPhysicsSimulationMode = Physics.simulationMode;
            for (int index = 0; index < allBodies.Length; index++)
            {
                Rigidbody body = allBodies[index];
                if (body == null || body.gameObject.scene != activeScene)
                {
                    continue;
                }

                PhysicsSimulationBodyState state = new PhysicsSimulationBodyState
                {
                    Rigidbody = body,
                    IsKinematic = body.isKinematic,
                    Position = body.transform.position,
                    Rotation = body.transform.rotation,
                    LinearVelocity = body.linearVelocity,
                    AngularVelocity = body.angularVelocity
                };
                physicsBodyStates.Add(state);

                bool simulate = physicsTargetBodies.Contains(body);
                if (simulate)
                {
                    Undo.RecordObject(body.transform, "Simulate Physics Batch");
                    body.isKinematic = false;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                else
                {
                    body.isKinematic = true;
                }
            }

            Physics.SyncTransforms();
            Physics.simulationMode = SimulationMode.Script;
            hasPointerPosition = false;
            DestroyPreview();
            physicsSimulationRunning = true;
            physicsSimulationIsActiveBatch = completesActiveBatch;
            physicsSimulationElapsed = 0f;
            physicsLastTickTime = EditorApplication.timeSinceStartup;
            physicsSimulationLabel = label;
            EditorApplication.update += TickPhysicsSimulation;
            SetStatus($"Simulating {label} for {physicsSimulationDuration:0.##} seconds...");
            UpdatePhysicsUi();
            RepaintSceneView();
        }

        private void TickPhysicsSimulation()
        {
            if (!physicsSimulationRunning)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float step = Mathf.Clamp((float)(now - physicsLastTickTime), 0.005f, 0.05f);
            physicsLastTickTime = now;
            Physics.Simulate(step);
            physicsSimulationElapsed += step;
            RepaintSceneView();

            if (physicsSimulationElapsed >= physicsSimulationDuration)
            {
                CompletePhysicsSimulation();
            }
            else
            {
                UpdatePhysicsUi();
            }
        }

        private void CompletePhysicsSimulation()
        {
            bool simulatedActiveBatch = physicsSimulationIsActiveBatch;
            StopPhysicsSimulation(false);
            if (simulatedActiveBatch && activePhysicsBatch != null)
            {
                physicsBatchSimulationCompleted = true;
            }
            physicsSimulationIsActiveBatch = false;
            SetStatus(simulatedActiveBatch
                ? "Simulation complete. Review the result, then Confirm Batch or Cancel Simulation."
                : "Simulation complete. Review the settled object in the Scene.");
            UpdatePhysicsUi();
            RepaintSceneView();
        }

        private void StopPhysicsSimulation(bool restoreTransforms)
        {
            if (!physicsSimulationRunning && physicsBodyStates.Count == 0)
            {
                return;
            }

            bool wasActiveBatchSimulation = physicsSimulationIsActiveBatch;
            EditorApplication.update -= TickPhysicsSimulation;
            for (int index = 0; index < physicsBodyStates.Count; index++)
            {
                PhysicsSimulationBodyState state = physicsBodyStates[index];
                if (state.Rigidbody == null)
                {
                    continue;
                }

                if (restoreTransforms)
                {
                    state.Rigidbody.transform.SetPositionAndRotation(state.Position, state.Rotation);
                }

                state.Rigidbody.isKinematic = state.IsKinematic;
                state.Rigidbody.linearVelocity = restoreTransforms
                    ? state.LinearVelocity
                    : Vector3.zero;
                state.Rigidbody.angularVelocity = restoreTransforms
                    ? state.AngularVelocity
                    : Vector3.zero;
            }

            RemoveAddedPhysicsComponents();
            Physics.simulationMode = previousPhysicsSimulationMode;
            Physics.SyncTransforms();
            if (!restoreTransforms && physicsBodyStates.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            physicsSimulationRunning = false;
            if (restoreTransforms)
            {
                physicsSimulationIsActiveBatch = false;
                if (wasActiveBatchSimulation)
                {
                    physicsBatchSimulationCompleted = false;
                }
            }
            else
            {
                physicsSimulationIsActiveBatch = false;
            }
            physicsSimulationElapsed = 0f;
            physicsSimulationLabel = string.Empty;
            physicsBodyStates.Clear();
            physicsTargetBodies.Clear();
            UpdatePhysicsUi();
        }

        private void RemoveAddedPhysicsComponents()
        {
            bool removedAny = false;
            for (int index = physicsAddedComponents.Count - 1; index >= 0; index--)
            {
                Component component = physicsAddedComponents[index];
                physicsAddedComponents.RemoveAt(index);
                if (component == null)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(component);
                removedAny = true;
            }

            if (removedAny)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        private void EnsureBatchRigidbodies(GameObject batch)
        {
            if (!physicsAddRigidbodyIfMissing || batch == null)
            {
                return;
            }

            for (int index = 0; index < batch.transform.childCount; index++)
            {
                EnsureRigidbody(batch.transform.GetChild(index).gameObject);
            }
        }

        private static List<GameObject> GetSelectedSceneObjects()
        {
            List<GameObject> selectedObjects = new List<GameObject>();
            GameObject[] selection = Selection.gameObjects;
            Scene activeScene = SceneManager.GetActiveScene();
            for (int index = 0; index < selection.Length; index++)
            {
                GameObject selected = selection[index];
                if (selected != null && selected.scene == activeScene && !selectedObjects.Contains(selected))
                {
                    selectedObjects.Add(selected);
                }
            }

            return selectedObjects;
        }

        private void UpdatePhysicsUi()
        {
            if (physicsBatchLabel != null)
            {
                if (physicsSimulationRunning)
                {
                    physicsBatchLabel.text =
                        $"Simulating {physicsSimulationLabel}: {physicsSimulationElapsed:0.0}/{physicsSimulationDuration:0.0}s";
                }
                else if (activePhysicsBatch != null)
                {
                    physicsBatchLabel.text =
                        $"{activePhysicsBatch.name}: {activePhysicsBatch.transform.childCount} object(s)";
                }
                else
                {
                    physicsBatchLabel.text = "No active batch. Enable the Physics Simulation tool and place objects.";
                }
            }

            if (physicsSelectionLabel != null)
            {
                physicsSelectionLabel.text = $"Scene Selection: {GetSelectedSceneObjects().Count} object(s)";
            }

            if (simulateBatchButton != null)
            {
                simulateBatchButton.SetEnabled(
                    !physicsSimulationRunning &&
                    activePhysicsBatch != null &&
                    activePhysicsBatch.transform.childCount > 0);
            }

            if (simulateSelectionButton != null)
            {
                simulateSelectionButton.SetEnabled(
                    !physicsSimulationRunning && GetSelectedSceneObjects().Count > 0);
            }

            if (confirmBatchButton != null)
            {
                confirmBatchButton.SetEnabled(
                    !physicsSimulationRunning &&
                    activePhysicsBatch != null &&
                    activePhysicsBatch.transform.childCount > 0 &&
                    physicsBatchSimulationCompleted);
            }

            if (cancelSimulationButton != null)
            {
                cancelSimulationButton.SetEnabled(physicsSimulationRunning);
            }
        }

        private void UpdatePlacementPreview(SceneView sceneView)
        {
            if (!toolEnabled || !IsPlacementTab())
            {
                DestroyPreview();
                return;
            }

            GameObject currentPrefab = GetNextPrefabForSpawn();
            if (currentPrefab == null)
            {
                DestroyPreview();
                return;
            }

            EnsurePreview(currentPrefab);
            if (previewInstance == null)
            {
                return;
            }

            if (placementMode == PlacementMode.Brush && brushStrokeActive)
            {
                previewInstance.SetActive(false);
            }
            else if (hasPointerPosition)
            {
                previewInstance.SetActive(true);
                ApplyPlacement(
                    previewInstance,
                    pointerPosition,
                    pointerNormal,
                    placementMode == PlacementMode.Brush ? brushAlignToSurface : (bool?)null,
                    true,
                    previewVariation,
                    currentPrefab.transform.localRotation);
            }
            else
            {
                previewInstance.SetActive(false);
            }

            sceneView.Repaint();
        }

        private static float GetScrollDirection(Event currentEvent)
        {
            if (Mathf.Abs(currentEvent.delta.y) > 0.001f)
            {
                return Mathf.Sign(currentEvent.delta.y);
            }

            if (Mathf.Abs(currentEvent.delta.x) > 0.001f)
            {
                return Mathf.Sign(currentEvent.delta.x);
            }

            return 0f;
        }

        private void ApplyPlacement(
            GameObject instance,
            Vector3 hitPosition,
            Vector3 surfaceNormal,
            bool? alignToSurfaceOverride = null,
            bool syncPhysicsTransforms = true,
            PlacementVariation? placementVariation = null,
            Quaternion? prefabRotationOverride = null)
        {
            bool shouldAlignToSurface = alignToSurfaceOverride ?? alignToSurface;
            if (placementVariation.HasValue)
            {
                instance.transform.localScale = placementVariation.Value.LocalScale;
            }

            Vector3 safeNormal = surfaceNormal.sqrMagnitude > 0.0001f
                ? surfaceNormal.normalized
                : Vector3.up;
            Quaternion prefabRotation = prefabRotationOverride ?? instance.transform.localRotation;
            Quaternion placementRotation = GetPlacementRotation(
                safeNormal,
                shouldAlignToSurface,
                prefabRotation);
            if (placementVariation.HasValue)
            {
                placementRotation *= Quaternion.Euler(placementVariation.Value.RandomEuler);
            }

            instance.transform.SetPositionAndRotation(
                hitPosition,
                placementRotation);
            if (syncPhysicsTransforms)
            {
                Physics.SyncTransforms();
            }

            if (placementAnchorMode != PlacementAnchorMode.ObjectPivot &&
                TryGetWorldBounds(instance, out Bounds bounds))
            {
                if (placementAnchorMode == PlacementAnchorMode.CenterPivot)
                {
                    instance.transform.position += hitPosition - bounds.center;
                }
                else if (placementAnchorMode == PlacementAnchorMode.BottomOfBoundingBox)
                {
                    Vector3 anchorAxis = shouldAlignToSurface ? safeNormal : Vector3.up;
                    float minimumProjection = GetMinimumBoundsProjection(instance, bounds, anchorAxis);
                    float targetProjection = Vector3.Dot(hitPosition, anchorAxis);
                    instance.transform.position += anchorAxis * (targetProjection - minimumProjection);
                }
            }

            Vector3 offsetDirection = shouldAlignToSurface ? safeNormal : Vector3.up;
            instance.transform.position += offsetDirection * verticalOffset;
        }

        private static bool TryGetWorldBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            // Preview instances intentionally disable Colliders. Prefer visual
            // bounds whenever available so Preview and the spawned instance use
            // exactly the same anchor calculation. Colliders are only a
            // fallback for invisible/logic-only prefabs.
            if (hasBounds)
            {
                if (bounds.size.sqrMagnitude > 0.000001f)
                {
                    return true;
                }

                bounds = default;
                hasBounds = false;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
            }

            return hasBounds && bounds.size.sqrMagnitude > 0.000001f;
        }

        private static float GetMinimumBoundsProjection(GameObject instance, Bounds fallbackBounds, Vector3 axis)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool hasRendererBounds = false;
            float minimumProjection = float.PositiveInfinity;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Bounds localBounds = renderer.localBounds;
                Vector3 extents = localBounds.extents;
                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 localCorner = localBounds.center + new Vector3(
                        (cornerIndex & 1) == 0 ? -extents.x : extents.x,
                        (cornerIndex & 2) == 0 ? -extents.y : extents.y,
                        (cornerIndex & 4) == 0 ? -extents.z : extents.z);
                    Vector3 worldCorner = renderer.transform.TransformPoint(localCorner);
                    minimumProjection = Mathf.Min(minimumProjection, Vector3.Dot(worldCorner, axis));
                    hasRendererBounds = true;
                }
            }

            if (hasRendererBounds)
            {
                return minimumProjection;
            }

            Vector3 absoluteAxis = new Vector3(
                Mathf.Abs(axis.x),
                Mathf.Abs(axis.y),
                Mathf.Abs(axis.z));
            return Vector3.Dot(fallbackBounds.center, axis) -
                   Vector3.Dot(fallbackBounds.extents, absoluteAxis);
        }

        private string GetPlacementAnchorLabel()
        {
            switch (placementAnchorMode)
            {
                case PlacementAnchorMode.CenterPivot:
                    return "Center Pivot";
                case PlacementAnchorMode.BottomOfBoundingBox:
                    return "Bottom of Bounding Box";
                default:
                    return "Object Pivot";
            }
        }

        private Quaternion GetPlacementRotation(
            Vector3 surfaceNormal,
            bool shouldAlignToSurface,
            Quaternion prefabRotation)
        {
            Quaternion placementRotation;
            if (!shouldAlignToSurface)
            {
                placementRotation = Quaternion.Euler(0f, rotationY, 0f);
            }
            else
            {
                Vector3 safeNormal = surfaceNormal.sqrMagnitude > 0.0001f
                    ? surfaceNormal.normalized
                    : Vector3.up;
                Quaternion alignRotation = Quaternion.FromToRotation(Vector3.up, safeNormal);
                placementRotation = alignRotation * Quaternion.AngleAxis(rotationY, Vector3.up);
            }

            // Preserve the authored prefab root rotation (for example X=80°).
            return placementRotation * prefabRotation;
        }

        private PlacementVariation GetPreviewOrCreatePlacementVariation(GameObject sourcePrefab)
        {
            return previewSource == sourcePrefab && hasPreviewVariation
                ? previewVariation
                : CreatePlacementVariation(sourcePrefab);
        }

        private PlacementVariation CreatePlacementVariation(GameObject sourcePrefab)
        {
            if (sourcePrefab == null)
            {
                return default;
            }

            float minimumScale = Mathf.Min(randomScaleMinimumPercent, randomScaleMaximumPercent);
            float maximumScale = Mathf.Max(randomScaleMinimumPercent, randomScaleMaximumPercent);
            float scaleMultiplier = RandomRange(minimumScale, maximumScale) / 100f;
            return new PlacementVariation
            {
                RandomEuler = new Vector3(
                    randomRotationX ? RandomRange(0f, 360f) : 0f,
                    randomRotationY ? RandomRange(0f, 360f) : 0f,
                    randomRotationZ ? RandomRange(0f, 360f) : 0f),
                LocalScale = sourcePrefab.transform.localScale * scaleMultiplier
            };
        }

        private float RandomRange(float minimum, float maximum)
        {
            if (Mathf.Abs(maximum - minimum) < 0.0001f)
            {
                return minimum;
            }

            return minimum + (float)placementRandom.NextDouble() * (maximum - minimum);
        }

        private void OnPlacementVariationSettingsChanged()
        {
            hasPreviewVariation = false;
            if (toolEnabled && IsPlacementTab() && lastSceneView != null)
            {
                UpdatePlacementPreview(lastSceneView);
            }
        }

        private void EnsurePreview(GameObject currentPrefab)
        {
            if (previewInstance != null && previewSource == currentPrefab)
            {
                EnsurePreviewVariation(currentPrefab);
                return;
            }

            DestroyPreview();
            previewSource = currentPrefab;
            previewInstance = CreatePreviewInstance(currentPrefab, $"{currentPrefab.name} (Preview)");
            EnsurePreviewVariation(currentPrefab);
        }

        private void EnsurePreviewVariation(GameObject sourcePrefab)
        {
            if (hasPreviewVariation || sourcePrefab == null)
            {
                return;
            }

            previewVariation = CreatePlacementVariation(sourcePrefab);
            hasPreviewVariation = true;
        }

        private GameObject CreatePreviewInstance(GameObject sourcePrefab, string previewName)
        {
            if (sourcePrefab == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (instance == null)
            {
                instance = Instantiate(sourcePrefab);
            }

            if (instance == null)
            {
                return null;
            }

            instance.name = previewName;
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            return instance;
        }

        private void RefreshBrushPreviewInstances(int startIndex)
        {
            if (!brushStrokeActive)
            {
                return;
            }

            int firstIndex = Mathf.Clamp(startIndex, 0, brushSpawnPoints.Count);
            while (brushPreviewInstances.Count < brushSpawnPoints.Count)
            {
                brushPreviewInstances.Add(null);
                brushPreviewSources.Add(null);
            }

            for (int index = firstIndex; index < brushSpawnPoints.Count; index++)
            {
                BrushSpawnPoint spawnPoint = brushSpawnPoints[index];
                if (spawnPoint.Prefab == null)
                {
                    continue;
                }

                if (brushPreviewInstances[index] == null || brushPreviewSources[index] != spawnPoint.Prefab)
                {
                    if (brushPreviewInstances[index] != null)
                    {
                        DestroyImmediate(brushPreviewInstances[index]);
                    }

                    brushPreviewInstances[index] = CreatePreviewInstance(
                        spawnPoint.Prefab,
                        $"{spawnPoint.Prefab.name} (Brush Preview)");
                    brushPreviewSources[index] = spawnPoint.Prefab;
                }

                GameObject preview = brushPreviewInstances[index];
                if (preview == null)
                {
                    continue;
                }

                preview.SetActive(true);
                ApplyPlacement(
                    preview,
                    spawnPoint.Position,
                    spawnPoint.Normal,
                    brushAlignToSurface,
                    false,
                    spawnPoint.Variation,
                    spawnPoint.Prefab.transform.localRotation);
            }
        }

        private void DestroyBrushPreviewInstances()
        {
            for (int index = 0; index < brushPreviewInstances.Count; index++)
            {
                if (brushPreviewInstances[index] != null)
                {
                    DestroyImmediate(brushPreviewInstances[index]);
                }
            }

            brushPreviewInstances.Clear();
            brushPreviewSources.Clear();
        }

        private void DestroyPreview()
        {
            if (previewInstance != null)
            {
                DestroyImmediate(previewInstance);
            }

            previewInstance = null;
            previewSource = null;
            previewVariation = default;
            hasPreviewVariation = false;
        }

        private void CycleActivePrefab(int direction)
        {
            if (sourcePrefabs.Count == 0)
            {
                return;
            }

            int nextIndex = activePrefabIndex + direction;
            if (nextIndex < 0)
            {
                nextIndex = sourcePrefabs.Count - 1;
            }
            else if (nextIndex >= sourcePrefabs.Count)
            {
                nextIndex = 0;
            }

            SetActivePrefab(nextIndex);
        }

        private void SetActivePrefab(int index)
        {
            if (sourcePrefabs.Count == 0)
            {
                activePrefabIndex = 0;
                ResetPrefabSelectionCursor();
                DestroyPreview();
                UpdateActiveAssetLabel();
                return;
            }

            activePrefabIndex = Mathf.Clamp(index, 0, sourcePrefabs.Count - 1);
            if (prefabSelectionMode == PrefabSelectionMode.Sequential)
            {
                sequentialPrefabIndex = FindNextValidPrefabIndex(activePrefabIndex);
            }
            rotationY = 0f;
            verticalOffset = 0f;
            DestroyPreview();
            RebuildAllThumbnailGrids();
            UpdateActiveAssetLabel();
            if (toolEnabled && IsPlacementTab() && hasPointerPosition && lastSceneView != null)
            {
                UpdatePlacementPreview(lastSceneView);
            }

            GameObject currentPrefab = GetNextPrefabForSpawn();
            SetStatus(currentPrefab != null
                ? $"Next Prefab: {currentPrefab.name} ({prefabSelectionMode})"
                : "No valid Placeable Asset is available for the current selection mode.");
        }

        private GameObject GetCurrentPrefab()
        {
            if (activePrefabIndex < 0 || activePrefabIndex >= sourcePrefabs.Count)
            {
                return null;
            }

            GameObject prefab = sourcePrefabs[activePrefabIndex];
            return prefab != null && IsValidSceneAsset(AssetDatabase.GetAssetPath(prefab)) ? prefab : null;
        }

        private void ResetPrefabSelectionCursor()
        {
            pendingRandomPrefab = null;
            sequentialPrefabIndex = FindNextValidPrefabIndex(activePrefabIndex);
            if (prefabSelectionMode == PrefabSelectionMode.Random)
            {
                pendingRandomPrefab = GetRandomPlaceablePrefab();
            }
        }

        private void AddProjectSelection(List<GameObject> target, string label, bool selectFirstAdded)
        {
            int originalCount = target.Count;
            int addedCount = AddAssetsToList(Selection.objects, target);
            if (addedCount == 0)
            {
                SetStatus("Select one or more Prefabs or imported Models in the Project window.");
                return;
            }

            if (selectFirstAdded && originalCount == 0)
            {
                activePrefabIndex = 0;
                ResetPrefabSelectionCursor();
            }

            RebuildAllThumbnailGrids();
            UpdateActiveAssetLabel();
            SetStatus($"Added {addedCount} {label}.");
        }

        private static int AddAssetsToList(IEnumerable<UnityEngine.Object> objects, List<GameObject> target)
        {
            List<GameObject> prefabs = new List<GameObject>();
            foreach (UnityEngine.Object selectedObject in objects)
            {
                string assetPath = AssetDatabase.GetAssetPath(selectedObject);
                if (!IsValidSceneAsset(assetPath))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                }
            }

            return AddPrefabReferencesToList(prefabs, target);
        }

        private static int AddPrefabReferencesToList(
            IEnumerable<GameObject> prefabs,
            List<GameObject> target)
        {
            int addedCount = 0;
            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null ||
                    !IsValidSceneAsset(AssetDatabase.GetAssetPath(prefab)) ||
                    target.Contains(prefab))
                {
                    continue;
                }

                target.Add(prefab);
                addedCount++;
            }

            return addedCount;
        }

        private void ClearSourcePrefabs()
        {
            if (toolEnabled && IsPlacementTab())
            {
                DeactivateTool(true, false);
            }

            sourcePrefabs.Clear();
            activePrefabIndex = 0;
            ResetPrefabSelectionCursor();
            rotationY = 0f;
            RebuildAllThumbnailGrids();
            UpdateActiveAssetLabel();
            SetStatus("Placeable Assets cleared.");
        }

        private void ClearPrefabList(List<GameObject> target, string status)
        {
            target.Clear();
            RebuildAllThumbnailGrids();
            SetStatus(status);
        }

        private void RemovePrefabAt(List<GameObject> target, int index)
        {
            if (index < 0 || index >= target.Count)
            {
                return;
            }

            bool sourceList = ReferenceEquals(target, sourcePrefabs);
            if (sourceList && index == activePrefabIndex)
            {
                DestroyPreview();
            }

            target.RemoveAt(index);
            if (sourceList)
            {
                activePrefabIndex = target.Count == 0 ? 0 : Mathf.Clamp(activePrefabIndex, 0, target.Count - 1);
                ResetPrefabSelectionCursor();
                if (target.Count == 0 && toolEnabled && IsPlacementTab())
                {
                    DeactivateTool(true, false);
                }
                UpdateActiveAssetLabel();
            }

            RebuildAllThumbnailGrids();
        }

        private void RegisterPrefabDropTarget(VisualElement targetElement, List<GameObject> targetList, bool selectFirstAdded)
        {
            targetElement.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (ContainsValidDraggedAsset())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.StopPropagation();
                }
            });

            targetElement.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!ContainsValidDraggedAsset())
                {
                    return;
                }

                int originalCount = targetList.Count;
                DragAndDrop.AcceptDrag();
                int addedCount = AddAssetsToList(DragAndDrop.objectReferences, targetList);
                if (selectFirstAdded && originalCount == 0 && addedCount > 0)
                {
                    activePrefabIndex = 0;
                    ResetPrefabSelectionCursor();
                }

                RebuildAllThumbnailGrids();
                UpdateActiveAssetLabel();
                SetStatus($"Added {addedCount} asset(s) by drag and drop.");
                evt.StopPropagation();
            });
        }

        private static bool ContainsValidDraggedAsset()
        {
            UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences;
            for (int index = 0; index < draggedObjects.Length; index++)
            {
                if (IsValidSceneAsset(AssetDatabase.GetAssetPath(draggedObjects[index])))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildAllThumbnailGrids()
        {
            if (assetThumbnailGrid == null)
            {
                return;
            }

            bool loading = false;
            loading |= BuildThumbnailGrid(
                assetThumbnailGrid,
                sourcePrefabs,
                activePrefabIndex,
                "Drop Prefabs or Models here",
                SetActivePrefab,
                index => RemovePrefabAt(sourcePrefabs, index));
            loading |= BuildThumbnailGrid(
                poleThumbnailGrid,
                clotheslinePolePrefabs,
                -1,
                "Drop Pole Prefabs here",
                null,
                index => RemovePrefabAt(clotheslinePolePrefabs, index));
            loading |= BuildThumbnailGrid(
                hangingThumbnailGrid,
                clotheslineHangingPrefabs,
                -1,
                "Drop Clothes, Flags, or Banners here",
                null,
                index => RemovePrefabAt(clotheslineHangingPrefabs, index));

            if (loading)
            {
                StartThumbnailPreviewPolling();
            }
        }

        private static bool BuildThumbnailGrid(
            VisualElement grid,
            IReadOnlyList<GameObject> items,
            int selectedIndex,
            string emptyText,
            Action<int> onSelect,
            Action<int> onRemove)
        {
            grid.Clear();
            if (items.Count == 0)
            {
                Label emptyLabel = new Label(emptyText);
                emptyLabel.AddToClassList("thumbnail-empty-label");
                grid.Add(emptyLabel);
                return false;
            }

            bool loadingPreview = false;
            for (int index = 0; index < items.Count; index++)
            {
                int itemIndex = index;
                GameObject item = items[itemIndex];
                VisualElement card = new VisualElement();
                card.AddToClassList("thumbnail-card");
                if (itemIndex == selectedIndex)
                {
                    card.AddToClassList("active");
                }

                Button selectButton = new Button(() => onSelect?.Invoke(itemIndex));
                selectButton.AddToClassList("thumbnail-select-button");

                Texture2D preview = item != null ? AssetPreview.GetAssetPreview(item) : null;
                if (preview == null && item != null)
                {
                    preview = AssetPreview.GetMiniThumbnail(item);
                    loadingPreview |= IsLoadingPreview(item);
                }

                Image image = new Image
                {
                    image = preview,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.AddToClassList("thumbnail-image");
                selectButton.Add(image);

                Label nameLabel = new Label(item != null ? item.name : "Missing Asset");
                nameLabel.AddToClassList("thumbnail-name");
                selectButton.Add(nameLabel);
                card.Add(selectButton);

                Button removeButton = new Button(() => onRemove(itemIndex))
                {
                    text = "×",
                    tooltip = item != null ? $"Remove {item.name}" : "Remove missing asset"
                };
                removeButton.AddToClassList("thumbnail-remove-button");
                card.Add(removeButton);
                grid.Add(card);
            }

            return loadingPreview;
        }

        private void StartThumbnailPreviewPolling()
        {
            if (waitingForThumbnailPreviews)
            {
                return;
            }

            waitingForThumbnailPreviews = true;
            EditorApplication.update += PollThumbnailPreviews;
        }

        private void PollThumbnailPreviews()
        {
            if (AnyThumbnailPreviewLoading(sourcePrefabs) ||
                AnyThumbnailPreviewLoading(clotheslinePolePrefabs) ||
                AnyThumbnailPreviewLoading(clotheslineHangingPrefabs))
            {
                return;
            }

            StopThumbnailPreviewPolling();
            RebuildAllThumbnailGrids();
            Repaint();
        }

        private static bool IsLoadingPreview(UnityEngine.Object item)
        {
#if UNITY_6000_7_OR_NEWER
            return AssetPreview.IsLoadingAssetPreview(item.GetEntityId());
#else
            return AssetPreview.IsLoadingAssetPreview(item.GetInstanceID());
#endif
        }

        private static bool AnyThumbnailPreviewLoading(IReadOnlyList<GameObject> items)
        {
            for (int index = 0; index < items.Count; index++)
            {
                GameObject item = items[index];
                if (item != null && IsLoadingPreview(item))
                {
                    return true;
                }
            }

            return false;
        }

        private void StopThumbnailPreviewPolling()
        {
            if (!waitingForThumbnailPreviews)
            {
                return;
            }

            waitingForThumbnailPreviews = false;
            EditorApplication.update -= PollThumbnailPreviews;
        }

        private void UpdateActiveAssetLabel()
        {
            GameObject currentPrefab = GetNextPrefabForSpawn();
            string label;
            if (currentPrefab == null)
            {
                label = $"Prefab Mode: {prefabSelectionMode} — no valid Placeable Asset";
            }
            else if (prefabSelectionMode == PrefabSelectionMode.Manual)
            {
                label = $"Active Asset: {currentPrefab.name} ({activePrefabIndex + 1}/{sourcePrefabs.Count})";
            }
            else if (prefabSelectionMode == PrefabSelectionMode.Sequential)
            {
                int index = GetNextPrefabSelectionIndex();
                label = $"Prefab Mode: Sequential — Next: {currentPrefab.name} ({index + 1}/{sourcePrefabs.Count})";
            }
            else
            {
                label = $"Prefab Mode: Random — Next: {currentPrefab.name}";
            }

            if (activeAssetLabel != null)
            {
                activeAssetLabel.text = label;
            }

        }

        private static bool IsValidSceneAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   !AssetDatabase.IsValidFolder(assetPath) &&
                   AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(GameObject);
        }

        private bool IsIgnoredTransform(Transform target)
        {
            while (target != null)
            {
                if (ignoredSpawnedObjects.Contains(target.gameObject))
                {
                    return true;
                }

                target = target.parent;
            }

            return false;
        }

        private void RemoveDestroyedIgnoredObjects()
        {
            ignoredSpawnedObjects.RemoveWhere(item => item == null);
            RemoveDestroyedPlacedObjects();
        }

        private void RemoveDestroyedPlacedObjects()
        {
            placedSceneObjects.RemoveWhere(item => item == null);
        }

        private void OnSelectionChanged()
        {
            UpdatePhysicsUi();
            if (toolEnabled)
            {
                RepaintSceneView();
            }
        }

        private void OnUndoRedoPerformed()
        {
            RemoveDestroyedIgnoredObjects();
            UpdatePhysicsUi();
            RepaintSceneView();
        }

        private void RepaintSceneView()
        {
            if (lastSceneView != null)
            {
                lastSceneView.Repaint();
            }

            SceneView.RepaintAll();
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }
    }
}
