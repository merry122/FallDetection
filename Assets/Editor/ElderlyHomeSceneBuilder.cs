using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class ElderlyHomeSceneBuilder
{
    const string RootName = "Synthetic Elderly Home";
    const string NavigationSurfaceName = "Navigation - NavMesh Surface";
    const string OldManPath = "Assets/assets/Old Man Idle.fbx";
    const string ControllerPath = "Assets/Scripts/ElderlySimulation.controller";
    const string SolidColorShaderName = "FallDetection/SyntheticSolidColor";
    const string BedPrefabPath = "Assets/Furniture Mega Pack/Prefabs/Beds/Bed01.prefab";
    const string ChairPrefabPath = "Assets/Furniture Mega Pack/Prefabs/Chairs/Chair01.prefab";
    const string SofaPrefabPath = "Assets/Furniture Mega Pack/Prefabs/Sofas/Sofa01.prefab";
    const string CoffeeTablePrefabPath = "Assets/Furniture Mega Pack/Prefabs/Tables/Table01.prefab";
    const string DiningTablePrefabPath = "Assets/Furniture Mega Pack/Prefabs/Tables/Table06.prefab";
    const string DrawerPrefabPath = "Assets/Furniture Mega Pack/Prefabs/Drawers/Drawer01.prefab";
    const string HumanIdleClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
    const string HumanWalkClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
    const string HumanFallClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Fall01.fbx";
    const string NavMeshDataPath = "Assets/Scenes/SampleScene_NavMesh.asset";

    [MenuItem("Fall Detection/Build Elderly Home Scene")]
    public static void BuildScene()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject(RootName);

        Material floorMaterial = CreateMaterial("Synthetic Floor Material", new Color(0.55f, 0.50f, 0.43f));
        Material wallMaterial = CreateMaterial("Synthetic Wall Material", Color.white);
        Material woodMaterial = CreateMaterial("Synthetic Wood Material", new Color(0.42f, 0.28f, 0.16f));
        Material fabricMaterial = CreateMaterial("Synthetic Fabric Material", new Color(0.27f, 0.38f, 0.45f));
        Material obstacleMaterial = CreateMaterial("Synthetic Obstacle Material", new Color(0.45f, 0.18f, 0.14f));
        Material darkMaterial = CreateMaterial("Synthetic Dark Detail Material", new Color(0.05f, 0.05f, 0.05f));
        Material paperMaterial = CreateMaterial("Synthetic Paper Material", new Color(0.9f, 0.88f, 0.78f));
        Material plantMaterial = CreateMaterial("Synthetic Plant Material", new Color(0.1f, 0.35f, 0.16f));

        if (!SceneAlreadyHasRoom())
        {
            BuildOptionalRoomShell(root.transform, floorMaterial, wallMaterial);
        }

        Transform chairSeat = BuildFurniture(root.transform, woodMaterial, fabricMaterial, obstacleMaterial, darkMaterial, paperMaterial, plantMaterial, wallMaterial);
        Transform[] waypoints = BuildWaypoints(root.transform);
        bool navMeshReady = BuildNavMeshSurface(root);
        GameObject elderly = BuildElderlySubject(root.transform, waypoints, chairSeat, navMeshReady);
        BuildSurveillanceCamera(root.transform, elderly.transform);
        BuildScenarioManager(root.transform, elderly);
        BuildSimulationLighting(root.transform);
        FixSceneMaterials();

        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Built synthetic elderly home scene with a generated NavMeshSurface. If movement still fails, open the Synthetic Elderly Home object and press Bake on its NavMeshSurface.");
    }

    [MenuItem("Fall Detection/Rebuild Scene NavMesh")]
    public static void RebuildSceneNavMeshMenu()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            Debug.LogError("Could not find Synthetic Elderly Home. Run Fall Detection > Build Elderly Home Scene first.");
            return;
        }

        bool navMeshReady = BuildNavMeshSurface(root);
        PlaceAgentsOnNavMesh();

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        AssetDatabase.SaveAssets();

        if (navMeshReady)
        {
            Debug.Log("Rebuilt and saved the scene NavMesh. The elderly subject should now initialize on the baked floor.");
        }
        else
        {
            Debug.LogError("NavMesh rebuild finished with no walkable triangles. Check that Room Floor/Floor has a collider or renderer and is not marked Not Walkable.");
        }
    }

    [MenuItem("Fall Detection/Fix Pink Materials")]
    public static void FixPinkMaterialsMenu()
    {
        FixSceneMaterials();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("Reassigned synthetic scene materials. Use Assets > Refresh if Unity still shows old pink previews.");
    }

    static void BuildOptionalRoomShell(Transform root, Material floorMaterial, Material wallMaterial)
    {
        GameObject shell = new GameObject("Generated Room Bounds");
        shell.transform.SetParent(root);

        CreateCube("Floor", shell.transform, new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 6f), floorMaterial);
        CreateCube("Back Wall", shell.transform, new Vector3(0f, 1.5f, 3f), new Vector3(8f, 3f, 0.16f), wallMaterial);
        CreateCube("Left Wall", shell.transform, new Vector3(-4f, 1.5f, 0f), new Vector3(0.16f, 3f, 6f), wallMaterial);
        CreateCube("Right Wall", shell.transform, new Vector3(4f, 1.5f, 0f), new Vector3(0.16f, 3f, 6f), wallMaterial);
        CreateCube("Front Wall Left", shell.transform, new Vector3(-3f, 1.5f, -3f), new Vector3(2f, 3f, 0.16f), wallMaterial);
        CreateCube("Front Wall Right", shell.transform, new Vector3(3f, 1.5f, -3f), new Vector3(2f, 3f, 0.16f), wallMaterial);
        CreateCube("Door Header", shell.transform, new Vector3(0f, 2.65f, -3f), new Vector3(2f, 0.7f, 0.16f), wallMaterial);
    }

    static bool SceneAlreadyHasRoom()
    {
        return GameObject.Find("Room Floor") != null
            || GameObject.Find("Floor") != null
            || GameObject.Find("Back Wall") != null
            || GameObject.Find("Left Wall") != null
            || GameObject.Find("Right Wall") != null;
    }

    static Transform BuildFurniture(
        Transform root,
        Material woodMaterial,
        Material fabricMaterial,
        Material obstacleMaterial,
        Material darkMaterial,
        Material paperMaterial,
        Material plantMaterial,
        Material wallMaterial)
    {
        GameObject furniture = new GameObject("Elderly Home Furniture");
        furniture.transform.SetParent(root);

        GameObject bedArea = CreateEmpty("Bed Area", furniture.transform);
        GameObject livingArea = CreateEmpty("Living Area", furniture.transform);
        GameObject tableArea = CreateEmpty("Table Area", furniture.transform);
        GameObject riskArea = CreateEmpty("Fall Risk Objects", furniture.transform);
        GameObject decorArea = CreateEmpty("Decor", furniture.transform);

        CreatePrefabOrCube("Bed - Furniture Mega Pack", bedArea.transform, BedPrefabPath, new Vector3(-2.55f, 0f, 1.45f), new Vector3(0f, 90f, 0f), Vector3.one, new Vector3(2.35f, 0.65f, 1.55f), woodMaterial);
        CreatePrefabOrCube("Bedside Drawer - Furniture Mega Pack", bedArea.transform, DrawerPrefabPath, new Vector3(-3.25f, 0f, 0.05f), Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), new Vector3(0.5f, 0.6f, 0.5f), woodMaterial);
        CreateCube("Lamp", bedArea.transform, new Vector3(-3.25f, 0.78f, 0.05f), new Vector3(0.2f, 0.3f, 0.2f), obstacleMaterial);
        CreateCube("Medicine Box", bedArea.transform, new Vector3(-3.05f, 0.63f, 0.04f), new Vector3(0.18f, 0.08f, 0.12f), paperMaterial);
        CreateCylinder("Water Glass", bedArea.transform, new Vector3(-3.35f, 0.66f, -0.04f), Vector3.zero, new Vector3(0.08f, 0.12f, 0.08f), paperMaterial);

        CreatePrefabOrCube("Sofa - Furniture Mega Pack", livingArea.transform, SofaPrefabPath, new Vector3(2.45f, 0f, 1.65f), new Vector3(0f, 180f, 0f), new Vector3(0.9f, 0.9f, 0.9f), new Vector3(1.7f, 0.8f, 0.75f), fabricMaterial);
        CreatePrefabOrCube("Coffee Table - Furniture Mega Pack", livingArea.transform, CoffeeTablePrefabPath, new Vector3(1.75f, 0f, 0.25f), Vector3.zero, new Vector3(0.85f, 0.85f, 0.85f), new Vector3(1.05f, 0.32f, 0.6f), woodMaterial);
        CreateCube("Remote Control", livingArea.transform, new Vector3(1.55f, 0.42f, 0.28f), new Vector3(0.12f, 0.04f, 0.28f), darkMaterial);
        CreateCube("Book On Coffee Table", livingArea.transform, new Vector3(2.0f, 0.42f, 0.18f), new Vector3(0.3f, 0.05f, 0.22f), paperMaterial);

        CreatePrefabOrCube("Dining Table - Furniture Mega Pack", tableArea.transform, DiningTablePrefabPath, new Vector3(-0.45f, 0f, -1.55f), Vector3.zero, new Vector3(0.85f, 0.85f, 0.85f), new Vector3(1.0f, 0.6f, 0.75f), woodMaterial);
        CreateCube("Plate", tableArea.transform, new Vector3(-0.45f, 0.56f, -1.55f), new Vector3(0.28f, 0.03f, 0.28f), paperMaterial);
        CreateCube("Phone", tableArea.transform, new Vector3(-0.1f, 0.57f, -1.75f), new Vector3(0.16f, 0.035f, 0.28f), darkMaterial);

        CreatePrefabOrCube("Chair Near Bed - Furniture Mega Pack", tableArea.transform, ChairPrefabPath, new Vector3(-1.75f, 0f, -1.75f), new Vector3(0f, 90f, 0f), new Vector3(0.85f, 0.85f, 0.85f), new Vector3(0.5f, 0.95f, 0.5f), fabricMaterial);
        Transform chairSeat = new GameObject("Chair Sit Target").transform;
        chairSeat.SetParent(tableArea.transform);
        chairSeat.position = new Vector3(-1.75f, 0.75f, -1.75f);
        chairSeat.rotation = Quaternion.Euler(0f, 90f, 0f);

        CreateCube("Rug Low Contrast", riskArea.transform, new Vector3(0.65f, 0.01f, -0.2f), new Vector3(2.25f, 0.03f, 1.35f), fabricMaterial);
        CreateCube("Slippers Trip Object", riskArea.transform, new Vector3(-0.35f, 0.08f, -0.3f), new Vector3(0.32f, 0.08f, 0.18f), obstacleMaterial);
        CreateCube("Small Box Obstacle", riskArea.transform, new Vector3(2.85f, 0.18f, -1.35f), new Vector3(0.36f, 0.36f, 0.36f), obstacleMaterial);
        CreateCube("Laundry Basket", riskArea.transform, new Vector3(3.05f, 0.32f, 2.15f), new Vector3(0.45f, 0.64f, 0.45f), obstacleMaterial);
        CreateCylinder("Walking Cane", riskArea.transform, new Vector3(-3.25f, 0.52f, -0.9f), new Vector3(8f, 0f, 20f), new Vector3(0.05f, 0.52f, 0.05f), obstacleMaterial);
        CreateCube("Walker Frame Front", riskArea.transform, new Vector3(0.05f, 0.52f, 2.25f), new Vector3(0.65f, 0.08f, 0.08f), obstacleMaterial);
        CreateCube("Walker Left Leg", riskArea.transform, new Vector3(-0.25f, 0.28f, 2.0f), new Vector3(0.06f, 0.56f, 0.06f), obstacleMaterial);
        CreateCube("Walker Right Leg", riskArea.transform, new Vector3(0.35f, 0.28f, 2.0f), new Vector3(0.06f, 0.56f, 0.06f), obstacleMaterial);
        CreateCube("Walker Back Bar", riskArea.transform, new Vector3(0.05f, 0.52f, 1.8f), new Vector3(0.65f, 0.08f, 0.08f), obstacleMaterial);
        CreateCube("Wall Picture", decorArea.transform, new Vector3(-1.0f, 1.8f, 2.91f), new Vector3(1.0f, 0.65f, 0.04f), paperMaterial);
        CreateCube("Wall Picture Frame", decorArea.transform, new Vector3(-1.0f, 1.8f, 2.88f), new Vector3(1.12f, 0.75f, 0.035f), woodMaterial);
        CreateCube("White Baseboard Back", decorArea.transform, new Vector3(0f, 0.12f, 2.88f), new Vector3(7.5f, 0.12f, 0.06f), wallMaterial);
        CreateCube("Curtain Left", decorArea.transform, new Vector3(3.55f, 1.55f, 0.8f), new Vector3(0.08f, 1.5f, 0.7f), fabricMaterial);
        CreateCube("Curtain Right", decorArea.transform, new Vector3(3.55f, 1.55f, 1.7f), new Vector3(0.08f, 1.5f, 0.7f), fabricMaterial);
        CreateCylinder("Plant Pot", decorArea.transform, new Vector3(3.2f, 0.2f, -2.15f), Vector3.zero, new Vector3(0.22f, 0.2f, 0.22f), obstacleMaterial);
        CreateCube("Plant Leaves", decorArea.transform, new Vector3(3.2f, 0.55f, -2.15f), new Vector3(0.48f, 0.48f, 0.48f), plantMaterial);

        return chairSeat;
    }

    static Transform[] BuildWaypoints(Transform root)
    {
        GameObject waypointRoot = new GameObject("Daily Activity Waypoints");
        waypointRoot.transform.SetParent(root);

        Vector3[] positions =
        {
            new Vector3(-2.35f, 0f, 0.15f),
            new Vector3(-0.65f, 0f, -0.65f),
            new Vector3(1.2f, 0f, -0.85f),
            new Vector3(2.45f, 0f, 0.55f),
            new Vector3(-1.25f, 0f, 0.85f)
        };

        Transform[] waypoints = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Transform waypoint = new GameObject("Waypoint " + (i + 1)).transform;
            waypoint.SetParent(waypointRoot.transform);
            waypoint.position = positions[i];
            waypoints[i] = waypoint;
        }

        return waypoints;
    }

    static GameObject BuildElderlySubject(Transform root, Transform[] waypoints, Transform chairSeat, bool useNavMeshAgent)
    {
        GameObject subjectRoot = new GameObject("Elderly Subject");
        subjectRoot.transform.SetParent(root);
        subjectRoot.transform.position = new Vector3(-2.35f, 0f, 0.15f);
        subjectRoot.SetActive(false);

        GameObject oldManPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OldManPath);
        GameObject visual;

        if (oldManPrefab != null)
        {
            visual = (GameObject)PrefabUtility.InstantiatePrefab(oldManPrefab);
            visual.name = "Old Man Visual";
            visual.transform.SetParent(subjectRoot.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Elderly Placeholder Capsule";
            visual.transform.SetParent(subjectRoot.transform);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
        }

        Animator animator = visual.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = visual.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = CreateAnimatorController();

        NavMeshAgent agent = null;
        if (useNavMeshAgent && NavMesh.SamplePosition(subjectRoot.transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            subjectRoot.transform.position = hit.position;
            agent = subjectRoot.AddComponent<NavMeshAgent>();
            agent.speed = 0.7f;
            agent.angularSpeed = 120f;
            agent.acceleration = 2f;
            agent.radius = 0.34f;
            agent.height = 1.7f;
            agent.baseOffset = 0f;
            agent.stoppingDistance = 0.12f;
            agent.autoBraking = true;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }
        else
        {
            Debug.LogWarning("No valid NavMesh was available for the elderly subject. The scene will use transform-based fallback movement and will not create a NavMeshAgent.");
        }

        ElderlyActivityAgent controller = subjectRoot.AddComponent<ElderlyActivityAgent>();
        controller.animator = animator;
        controller.navMeshAgent = agent;
        controller.waypoints = waypoints;
        controller.chairSeat = chairSeat;

        subjectRoot.SetActive(true);
        return subjectRoot;
    }

    static void BuildSurveillanceCamera(Transform root, Transform subject)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Surveillance Camera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        camera.name = "Surveillance Camera";
        camera.transform.SetParent(root);

        SurveillanceCameraRig rig = camera.GetComponent<SurveillanceCameraRig>();
        if (rig == null)
        {
            rig = camera.gameObject.AddComponent<SurveillanceCameraRig>();
        }

        rig.lookAtTarget = subject;
        rig.fixedPosition = new Vector3(3.65f, 2.75f, -2.85f);
        rig.fieldOfView = 72f;
        rig.ApplySetup();
    }

    static void BuildScenarioManager(Transform root, GameObject elderly)
    {
        GameObject scenario = new GameObject("Fall Scenario Manager");
        scenario.transform.SetParent(root);

        FallScenarioManager manager = scenario.AddComponent<FallScenarioManager>();
        manager.elderlyAgent = elderly.GetComponent<ElderlyActivityAgent>();
        manager.subjectRoot = elderly.transform;
        manager.runScenarioOnStart = false;
        manager.scenarioType = FallScenarioType.None;
    }

    static void BuildSimulationLighting(Transform root)
    {
        GameObject ceilingLight = new GameObject("Soft Ceiling Light");
        ceilingLight.transform.SetParent(root);
        ceilingLight.transform.position = new Vector3(0f, 2.8f, -0.2f);

        Light light = ceilingLight.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 7f;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);

        RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.35f);
    }

    static bool BuildNavMeshSurface(GameObject root)
    {
        ConfigureNavigationObjects(root);

        NavMeshSurface surface = GetOrCreateNavMeshSurface(root);

        surface.collectObjects = CollectObjects.All;
        surface.layerMask = ~0;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.defaultArea = 0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.center = new Vector3(0f, 1f, 0f);
        surface.size = new Vector3(9f, 3f, 7f);

        Debug.Log("Starting NavMesh bake on " + surface.name + ". Geometry: Physics Colliders, bounds: " + surface.size + ", center: " + surface.center + ".");
        surface.BuildNavMesh();
        SaveNavMeshDataAsset(surface);

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        int vertexCount = triangulation.vertices != null ? triangulation.vertices.Length : 0;
        Debug.Log("NavMesh bake finished. Triangle vertex count: " + vertexCount + ". NavMesh data asset: " + (surface.navMeshData != null ? NavMeshDataPath : "none") + ".");
        return vertexCount > 0;
    }

    static NavMeshSurface GetOrCreateNavMeshSurface(GameObject root)
    {
        NavMeshSurface rootSurface = root.GetComponent<NavMeshSurface>();
        Transform surfaceTransform = root.transform.Find(NavigationSurfaceName);
        GameObject surfaceObject = surfaceTransform != null ? surfaceTransform.gameObject : new GameObject(NavigationSurfaceName);
        surfaceObject.transform.SetParent(root.transform);
        surfaceObject.transform.localPosition = Vector3.zero;
        surfaceObject.transform.localRotation = Quaternion.identity;
        surfaceObject.transform.localScale = Vector3.one;

        NavMeshSurface surface = surfaceObject.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = surfaceObject.AddComponent<NavMeshSurface>();
        }

        if (rootSurface != null)
        {
            Object.DestroyImmediate(rootSurface);
        }

        return surface;
    }

    static void ConfigureNavigationObjects(GameObject root)
    {
        foreach (GameObject floor in FindObjectsByNames("Room Floor", "Floor"))
        {
            MarkNavigationStatic(floor, true);
            SetNavMeshArea(floor, 0);
        }

        foreach (GameObject wall in FindObjectsByNames("Back Wall", "Left Wall", "Right Wall", "Front Wall Left", "Front Wall Right", "Door Header", "Ceiling"))
        {
            MarkNavigationStatic(wall, false);
            SetNavMeshArea(wall, 1);
        }

        Transform furnitureRoot = root.transform.Find("Elderly Home Furniture");
        if (furnitureRoot == null)
        {
            return;
        }

        foreach (Collider collider in furnitureRoot.GetComponentsInChildren<Collider>())
        {
            GameObject obstacle = collider.gameObject;
            string lowerName = obstacle.name.ToLowerInvariant();

            if (lowerName.Contains("rug"))
            {
                MarkNavigationStatic(obstacle, true);
                SetNavMeshArea(obstacle, 0);
                continue;
            }

            MarkNavigationStatic(obstacle, false);
            SetNavMeshArea(obstacle, 1);
            EnsureNavMeshObstacle(obstacle, collider);
        }
    }

    static GameObject[] FindObjectsByNames(params string[] names)
    {
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (string name in names)
        {
            GameObject gameObject = GameObject.Find(name);
            if (gameObject != null)
            {
                found.Add(gameObject);
            }
        }

        return found.ToArray();
    }

    static void MarkNavigationStatic(GameObject gameObject, bool navigationStatic)
    {
        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(gameObject);
        flags = navigationStatic
            ? flags | StaticEditorFlags.NavigationStatic
            : flags & ~StaticEditorFlags.NavigationStatic;
        GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
    }

    static void SetNavMeshArea(GameObject gameObject, int area)
    {
        NavMeshModifier modifier = gameObject.GetComponent<NavMeshModifier>();
        if (modifier == null)
        {
            modifier = gameObject.AddComponent<NavMeshModifier>();
        }

        modifier.overrideArea = true;
        modifier.area = area;
        EditorUtility.SetDirty(gameObject);
    }

    static void EnsureNavMeshObstacle(GameObject gameObject, Collider sourceCollider)
    {
        NavMeshObstacle obstacle = gameObject.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = gameObject.AddComponent<NavMeshObstacle>();
        }

        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;

        if (sourceCollider is BoxCollider boxCollider)
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = boxCollider.center;
            obstacle.size = boxCollider.size;
        }
        else
        {
            Bounds bounds = sourceCollider.bounds;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = gameObject.transform.InverseTransformPoint(bounds.center);
            Vector3 lossyScale = gameObject.transform.lossyScale;
            obstacle.size = new Vector3(
                SafeDivide(bounds.size.x, Mathf.Abs(lossyScale.x)),
                SafeDivide(bounds.size.y, Mathf.Abs(lossyScale.y)),
                SafeDivide(bounds.size.z, Mathf.Abs(lossyScale.z)));
        }

        EditorUtility.SetDirty(obstacle);
    }

    static void SaveNavMeshDataAsset(NavMeshSurface surface)
    {
        if (surface.navMeshData == null)
        {
            return;
        }

        NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);
        }
        else if (surface.navMeshData != existing)
        {
            EditorUtility.CopySerialized(surface.navMeshData, existing);
            surface.navMeshData = existing;
        }

        EditorUtility.SetDirty(surface);
    }

    static void PlaceAgentsOnNavMesh()
    {
        foreach (NavMeshAgent agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.transform.position = hit.position;
                agent.enabled = true;
            }
            else
            {
                agent.enabled = false;
                Debug.LogWarning(agent.name + " is not close enough to the baked NavMesh, so its NavMeshAgent was disabled.");
            }

            EditorUtility.SetDirty(agent);
        }
    }

    static RuntimeAnimatorController CreateAnimatorController()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            AssignHumanBasicMotions(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsSitting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Fall", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Activity", AnimatorControllerParameterType.Int);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idle = stateMachine.AddState("Idle Standing");
        AnimatorState walking = stateMachine.AddState("Slow Walk");
        AnimatorState sitting = stateMachine.AddState("Sitting");
        AnimatorState falling = stateMachine.AddState("Falling");
        idle.motion = LoadAnimationClip(HumanIdleClipPath);
        walking.motion = LoadAnimationClip(HumanWalkClipPath);
        sitting.motion = LoadAnimationClip(HumanIdleClipPath);
        falling.motion = LoadAnimationClip(HumanFallClipPath);

        stateMachine.defaultState = idle;

        AddBoolTransition(idle, walking, "Speed", 0.1f, true);
        AddBoolTransition(walking, idle, "Speed", 0.1f, false);
        AddSittingTransition(idle, sitting);
        AddSittingTransition(walking, sitting);
        AddNotSittingTransition(sitting, idle);
        AddFallTransition(idle, falling);
        AddFallTransition(walking, falling);
        AddFallTransition(sitting, falling);

        AssetDatabase.SaveAssets();
        return controller;
    }

    static void AssignHumanBasicMotions(AnimatorController controller)
    {
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimationClip idleClip = LoadAnimationClip(HumanIdleClipPath);
        AnimationClip walkClip = LoadAnimationClip(HumanWalkClipPath);
        AnimationClip fallClip = LoadAnimationClip(HumanFallClipPath);

        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            switch (childState.state.name)
            {
                case "Idle Standing":
                    childState.state.motion = idleClip;
                    break;
                case "Slow Walk":
                    childState.state.motion = walkClip;
                    break;
                case "Sitting":
                    childState.state.motion = idleClip;
                    break;
                case "Falling":
                    childState.state.motion = fallClip;
                    break;
            }
        }
    }

    static AnimationClip LoadAnimationClip(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }

        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, float threshold, bool greater)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.15f;
        transition.AddCondition(greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, threshold, parameter);
    }

    static void AddSittingTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.2f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "IsSitting");
    }

    static void AddNotSittingTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.2f;
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSitting");
    }

    static void AddFallTransition(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Fall");
    }

    static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        ApplyMaterial(cube, material);
        return cube;
    }

    static GameObject CreateEmpty(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent);
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        return gameObject;
    }

    static GameObject CreatePrefabOrCube(
        string name,
        Transform parent,
        string prefabPath,
        Vector3 position,
        Vector3 rotation,
        Vector3 scale,
        Vector3 fallbackCubeScale,
        Material fallbackMaterial)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return CreateCube(name + " Placeholder", parent, position + Vector3.up * (fallbackCubeScale.y * 0.5f), fallbackCubeScale, fallbackMaterial);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent);
        instance.transform.position = position;
        instance.transform.eulerAngles = rotation;
        instance.transform.localScale = scale;
        SetStaticRecursively(instance);
        EnsureSimpleCollider(instance);
        return instance;
    }

    static void SetStaticRecursively(GameObject gameObject)
    {
        gameObject.isStatic = true;
        foreach (Transform child in gameObject.transform)
        {
            SetStaticRecursively(child.gameObject);
        }
    }

    static void EnsureSimpleCollider(GameObject gameObject)
    {
        if (gameObject.GetComponentsInChildren<Collider>().Length > 0)
        {
            return;
        }

        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.center = gameObject.transform.InverseTransformPoint(bounds.center);
        Vector3 lossyScale = gameObject.transform.lossyScale;
        collider.size = new Vector3(
            SafeDivide(bounds.size.x, Mathf.Abs(lossyScale.x)),
            SafeDivide(bounds.size.y, Mathf.Abs(lossyScale.y)),
            SafeDivide(bounds.size.z, Mathf.Abs(lossyScale.z)));
    }

    static float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }

    static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 rotation, Vector3 scale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent);
        cylinder.transform.position = position;
        cylinder.transform.eulerAngles = rotation;
        cylinder.transform.localScale = scale;
        ApplyMaterial(cylinder, material);
        return cylinder;
    }

    static Material CreateMaterial(string name, Color color)
    {
        string folder = "Assets/Scripts/GeneratedMaterials";
        Directory.CreateDirectory(folder);
        string path = folder + "/" + name + ".mat";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(FindSimulationShader());
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = FindSimulationShader();
        material.color = color;
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void FixSceneMaterials()
    {
        Material wallMaterial = CreateMaterial("Synthetic Wall Material", Color.white);
        Material floorMaterial = CreateMaterial("Synthetic Floor Material", new Color(0.55f, 0.50f, 0.43f));
        Material woodMaterial = CreateMaterial("Synthetic Wood Material", new Color(0.42f, 0.28f, 0.16f));
        Material fabricMaterial = CreateMaterial("Synthetic Fabric Material", new Color(0.27f, 0.38f, 0.45f));
        Material obstacleMaterial = CreateMaterial("Synthetic Obstacle Material", new Color(0.45f, 0.18f, 0.14f));

        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            string objectName = renderer.gameObject.name.ToLowerInvariant();

            if (objectName.Contains("wall") || objectName.Contains("ceiling") || objectName.Contains("baseboard"))
            {
                renderer.sharedMaterial = wallMaterial;
            }
            else if (objectName.Contains("floor") || objectName.Contains("rug"))
            {
                renderer.sharedMaterial = floorMaterial;
            }
            else if (objectName.Contains("bed") || objectName.Contains("table") || objectName.Contains("frame"))
            {
                renderer.sharedMaterial = woodMaterial;
            }
            else if (objectName.Contains("sofa") || objectName.Contains("chair") || objectName.Contains("mattress") || objectName.Contains("pillow") || objectName.Contains("curtain"))
            {
                renderer.sharedMaterial = fabricMaterial;
            }
            else if (renderer.sharedMaterial == null || renderer.sharedMaterial.name.Contains("Synthetic"))
            {
                renderer.sharedMaterial = obstacleMaterial;
            }
        }
    }

    static Shader FindSimulationShader()
    {
        Shader shader = Shader.Find(SolidColorShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        return shader;
    }

    static void ApplyMaterial(GameObject gameObject, Material material)
    {
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
