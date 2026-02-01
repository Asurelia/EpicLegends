using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.AI;
using UnityEngine.AI;

/// <summary>
/// Utilitaire pour creer et configurer la scene de test.
/// Menu: EpicLegends > Setup > Create Test Scene
/// </summary>
public class SceneSetup : EditorWindow
{
    [MenuItem("EpicLegends/Setup/Create Test Scene")]
    public static void CreateTestScene()
    {
        // Creer nouvelle scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Configurer la lumiere directionnelle
        var sunLight = GameObject.Find("Directional Light");
        if (sunLight != null)
        {
            sunLight.name = "Sun";
            var light = sunLight.GetComponent<Light>();
            light.color = new Color(1f, 0.95f, 0.8f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Creer le sol
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        ground.isStatic = true;

        // Marquer le sol comme statique pour le NavMesh (utilise ContributeGI comme alternative)
        GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);

        // Creer le GameManager
        var gameManagerGO = new GameObject("GameManager");
        gameManagerGO.AddComponent<GameManager>();

        // Creer le Player
        CreatePlayer();

        // Creer quelques ennemis de test
        CreateTestEnemy(new Vector3(5f, 0f, 5f), "TestEnemy_1");
        CreateTestEnemy(new Vector3(-5f, 0f, 5f), "TestEnemy_2");
        CreateTestEnemy(new Vector3(0f, 0f, 8f), "TestEnemy_3");

        // Creer des objets de decoration
        CreateDecoration();

        // Sauvegarder la scene
        string scenePath = "Assets/Scenes/GameplayTest.unity";
        EnsureDirectoryExists(scenePath);
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log($"[SceneSetup] Scene de test creee: {scenePath}");
        Debug.Log("[SceneSetup] Pour le NavMesh: Window > AI > Navigation > Bake");
    }

    private static void CreatePlayer()
    {
        // Creer le joueur
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Default");
        player.transform.position = new Vector3(0f, 1f, 0f);

        // Supprimer le collider par defaut et ajouter un CharacterController ou Rigidbody
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

        var rb = player.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.mass = 70f;
        rb.linearDamping = 5f;

        var col = player.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.5f;
        col.center = Vector3.zero;

        // Ajouter les composants du joueur
        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerStats>();
        var combat = player.AddComponent<CombatController>();
        player.AddComponent<Inventory>();

        // Creer le visuel de l'arme
        var weaponHolder = new GameObject("WeaponHolder");
        weaponHolder.transform.SetParent(player.transform);
        weaponHolder.transform.localPosition = new Vector3(0.5f, 0.5f, 0.5f);

        var weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        weapon.name = "Sword";
        weapon.transform.SetParent(weaponHolder.transform);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localScale = new Vector3(0.1f, 0.1f, 1f);
        Object.DestroyImmediate(weapon.GetComponent<BoxCollider>());

        // Creer la Hitbox
        var hitboxGO = new GameObject("Hitbox");
        hitboxGO.transform.SetParent(weapon.transform);
        hitboxGO.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        var hitboxCol = hitboxGO.AddComponent<BoxCollider>();
        hitboxCol.isTrigger = true;
        hitboxCol.size = new Vector3(1f, 1f, 2f);
        var hitbox = hitboxGO.AddComponent<Hitbox>();
        hitboxGO.SetActive(false); // Desactive par defaut

        // Creer la Hurtbox du joueur
        var hurtboxGO = new GameObject("Hurtbox");
        hurtboxGO.transform.SetParent(player.transform);
        hurtboxGO.transform.localPosition = Vector3.zero;
        var hurtboxCol = hurtboxGO.AddComponent<CapsuleCollider>();
        hurtboxCol.isTrigger = true;
        hurtboxCol.height = 2f;
        hurtboxCol.radius = 0.6f;
        hurtboxGO.AddComponent<Hurtbox>();

        // Ajouter une camera qui suit le joueur
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(0f, 5f, -8f);
            mainCamera.transform.LookAt(player.transform);

            var cameraController = mainCamera.gameObject.AddComponent<CameraController>();
        }

        Debug.Log("[SceneSetup] Player cree avec tous les composants");
    }

    private static void CreateTestEnemy(Vector3 position, string name)
    {
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = name;
        enemy.tag = "Enemy";
        enemy.transform.position = position + Vector3.up;

        // Couleur rouge pour les ennemis
        var renderer = enemy.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.red;
        renderer.material = mat;

        // Ajouter les composants
        var agent = enemy.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.speed = 3f;
        agent.stoppingDistance = 2f;

        enemy.AddComponent<Health>();
        enemy.AddComponent<EnemyAI>();

        // Hurtbox
        var hurtboxGO = new GameObject("Hurtbox");
        hurtboxGO.transform.SetParent(enemy.transform);
        hurtboxGO.transform.localPosition = Vector3.zero;
        var hurtboxCol = hurtboxGO.AddComponent<CapsuleCollider>();
        hurtboxCol.isTrigger = true;
        hurtboxCol.height = 2f;
        hurtboxCol.radius = 0.6f;
        hurtboxGO.AddComponent<Hurtbox>();
    }

    private static void CreateDecoration()
    {
        // Quelques cubes comme obstacles
        for (int i = 0; i < 5; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Obstacle_{i}";
            cube.isStatic = true;

            // Marquer comme statique
            GameObjectUtility.SetStaticEditorFlags(cube, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);

            float x = Random.Range(-15f, 15f);
            float z = Random.Range(-15f, 15f);
            cube.transform.position = new Vector3(x, 0.5f, z);
            cube.transform.localScale = new Vector3(
                Random.Range(1f, 3f),
                Random.Range(1f, 3f),
                Random.Range(1f, 3f)
            );

            // Couleur aleatoire
            var renderer = cube.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(
                Random.Range(0.3f, 0.7f),
                Random.Range(0.3f, 0.7f),
                Random.Range(0.3f, 0.7f)
            );
            renderer.material = mat;
        }

        // Quelques spheres comme collectibles
        for (int i = 0; i < 3; i++)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Collectible_{i}";

            float x = Random.Range(-10f, 10f);
            float z = Random.Range(-10f, 10f);
            sphere.transform.position = new Vector3(x, 1f, z);
            sphere.transform.localScale = Vector3.one * 0.5f;

            // Couleur doree
            var renderer = sphere.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(1f, 0.8f, 0f);
            mat.SetFloat("_Metallic", 0.8f);
            mat.SetFloat("_Smoothness", 0.9f);
            renderer.material = mat;

            // Faire flotter
            sphere.GetComponent<Collider>().isTrigger = true;
        }
    }

    [MenuItem("EpicLegends/Setup/Create All Prefabs")]
    public static void CreateAllPrefabs()
    {
        CreatePlayerPrefab();
        CreateEnemyPrefab();
        CreateProjectilePrefab();
        CreateVFXPrefabs();

        AssetDatabase.Refresh();
        Debug.Log("[SceneSetup] Tous les prefabs ont ete crees!");
    }

    private static void CreatePlayerPrefab()
    {
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";

        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

        var rb = player.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.mass = 70f;

        var col = player.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.5f;

        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerStats>();
        player.AddComponent<CombatController>();
        player.AddComponent<Inventory>();

        // Sauvegarder comme prefab
        string path = "Assets/Prefabs/Player/Player.prefab";
        EnsureDirectoryExists(path);
        PrefabUtility.SaveAsPrefabAsset(player, path);
        Object.DestroyImmediate(player);

        Debug.Log($"[SceneSetup] Prefab cree: {path}");
    }

    private static void CreateEnemyPrefab()
    {
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = "Enemy_Base";
        enemy.tag = "Enemy";

        var renderer = enemy.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.red;
        renderer.material = mat;

        enemy.AddComponent<UnityEngine.AI.NavMeshAgent>();
        enemy.AddComponent<Health>();
        enemy.AddComponent<EnemyAI>();

        var hurtboxGO = new GameObject("Hurtbox");
        hurtboxGO.transform.SetParent(enemy.transform);
        var hurtboxCol = hurtboxGO.AddComponent<CapsuleCollider>();
        hurtboxCol.isTrigger = true;
        hurtboxCol.height = 2f;
        hurtboxGO.AddComponent<Hurtbox>();

        string path = "Assets/Prefabs/Enemies/Enemy_Base.prefab";
        EnsureDirectoryExists(path);
        PrefabUtility.SaveAsPrefabAsset(enemy, path);
        Object.DestroyImmediate(enemy);

        Debug.Log($"[SceneSetup] Prefab cree: {path}");
    }

    private static void CreateProjectilePrefab()
    {
        var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Projectile_Base";
        projectile.transform.localScale = Vector3.one * 0.3f;

        var renderer = projectile.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.cyan;
        mat.SetFloat("_Metallic", 0.5f);
        renderer.material = mat;

        var rb = projectile.AddComponent<Rigidbody>();
        rb.useGravity = false;

        var col = projectile.GetComponent<SphereCollider>();
        col.isTrigger = true;

        string path = "Assets/Prefabs/VFX/Projectile_Base.prefab";
        EnsureDirectoryExists(path);
        PrefabUtility.SaveAsPrefabAsset(projectile, path);
        Object.DestroyImmediate(projectile);

        Debug.Log($"[SceneSetup] Prefab cree: {path}");
    }

    private static void CreateVFXPrefabs()
    {
        // Impact effect placeholder
        var impact = new GameObject("VFX_Impact");
        var ps = impact.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.startLifetime = 0.3f;
        main.startSpeed = 5f;
        main.startSize = 0.2f;
        main.startColor = Color.yellow;
        main.maxParticles = 20;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        string path = "Assets/Prefabs/VFX/VFX_Impact.prefab";
        EnsureDirectoryExists(path);
        PrefabUtility.SaveAsPrefabAsset(impact, path);
        Object.DestroyImmediate(impact);

        Debug.Log($"[SceneSetup] Prefab cree: {path}");
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        string directory = System.IO.Path.GetDirectoryName(filePath);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
    }

    [MenuItem("EpicLegends/Setup/Bake NavMesh")]
    public static void BakeNavMesh()
    {
        // Chercher le type NavMeshSurface par reflexion (package Unity.AI.Navigation)
        var navMeshSurfaceType = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");

        if (navMeshSurfaceType != null)
        {
            // Nouveau systeme avec NavMeshSurface
            var surfaces = Object.FindObjectsByType(navMeshSurfaceType, FindObjectsSortMode.None);

            if (surfaces.Length > 0)
            {
                var buildMethod = navMeshSurfaceType.GetMethod("BuildNavMesh");
                foreach (var surface in surfaces)
                {
                    var go = ((Component)surface).gameObject;
                    Debug.Log($"[SceneSetup] Baking NavMesh sur '{go.name}'...");
                    buildMethod.Invoke(surface, null);
                }

                // Sauvegarder la scene pour persister les donnees NavMesh
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"[SceneSetup] NavMesh bake termine! ({surfaces.Length} surfaces)");
                return;
            }
        }

        // Fallback vers l'ancien systeme
        #pragma warning disable CS0618
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        #pragma warning restore CS0618
        Debug.Log("[SceneSetup] NavMesh bake termine (ancien systeme)! (Window > AI > Navigation pour voir)");
    }
}
