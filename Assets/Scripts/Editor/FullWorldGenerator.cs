using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Generateur de monde complet pour tester EpicLegends.
/// Menu: EpicLegends > World > Generate Full Test World
/// </summary>
public class FullWorldGenerator : EditorWindow
{
    private static Material _groundMat;
    private static Material _stoneMat;
    private static Material _waterMat;
    private static Material _dungeonMat;
    private static Material _treeMat;

    [MenuItem("EpicLegends/World/Generate Full Test World")]
    public static void GenerateWorld()
    {
        // Creer nouvelle scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Initialiser les materiaux
        CreateMaterials();

        // Configurer l'environnement
        SetupEnvironment();

        // Creer le terrain principal
        CreateMainTerrain();

        // Creer le village
        CreateVillage();

        // Creer le donjon
        CreateDungeon();

        // Creer la foret
        CreateForest();

        // Creer le lac
        CreateLake();

        // Creer le GameManager et systemes
        CreateGameSystems();

        // Creer le joueur
        CreatePlayer();

        // Creer les ennemis
        CreateEnemies();

        // Creer les PNJ
        CreateNPCs();

        // Creer une monture
        CreateMount();

        // Creer les collectibles
        CreateCollectibles();

        // Creer l'UI complete
        CreateFullUI();

        // Marquer les objets statiques pour NavMesh
        MarkStaticObjects();

        // Sauvegarder la scene
        string scenePath = "Assets/Scenes/FullTestWorld.unity";
        EnsureDirectoryExists(scenePath);
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log("[WorldGenerator] Monde complet genere!");
        Debug.Log("[WorldGenerator] N'oubliez pas: Window > AI > Navigation > Bake");
        Debug.Log("[WorldGenerator] Puis appuyez sur Play pour tester!");
    }

    private static void CreateMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");

        _groundMat = new Material(shader);
        _groundMat.color = new Color(0.4f, 0.6f, 0.3f); // Herbe

        _stoneMat = new Material(shader);
        _stoneMat.color = new Color(0.5f, 0.5f, 0.5f); // Pierre

        _waterMat = new Material(shader);
        _waterMat.color = new Color(0.2f, 0.5f, 0.8f, 0.7f); // Eau

        _dungeonMat = new Material(shader);
        _dungeonMat.color = new Color(0.3f, 0.25f, 0.2f); // Donjon sombre

        _treeMat = new Material(shader);
        _treeMat.color = new Color(0.2f, 0.5f, 0.2f); // Feuillage
    }

    private static void SetupEnvironment()
    {
        // Configurer le soleil
        var sunLight = GameObject.Find("Directional Light");
        if (sunLight != null)
        {
            sunLight.name = "Sun";
            var light = sunLight.GetComponent<Light>();
            light.color = new Color(1f, 0.95f, 0.8f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Configurer le ciel (Skybox)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.6f, 0.5f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.2f);

        // Brouillard leger
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.9f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 50f;
        RenderSettings.fogEndDistance = 200f;
    }

    private static void CreateMainTerrain()
    {
        var terrainParent = new GameObject("=== TERRAIN ===");

        // Sol principal (200x200)
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "MainGround";
        ground.transform.SetParent(terrainParent.transform);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(20f, 1f, 20f);
        ground.isStatic = true;
        ground.GetComponent<MeshRenderer>().material = _groundMat;
        ground.layer = LayerMask.NameToLayer("Default");

        // Chemin principal (route en pierre)
        CreatePath(terrainParent.transform, new Vector3(0, 0.01f, -50f), new Vector3(0, 0.01f, 50f), 3f);
        CreatePath(terrainParent.transform, new Vector3(-50f, 0.01f, 0), new Vector3(50f, 0.01f, 0), 3f);
    }

    private static void CreatePath(Transform parent, Vector3 start, Vector3 end, float width)
    {
        var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name = "Path";
        path.transform.SetParent(parent);

        Vector3 direction = end - start;
        float length = direction.magnitude;
        Vector3 center = (start + end) / 2f;

        path.transform.position = center;
        path.transform.localScale = new Vector3(width, 0.05f, length);
        path.transform.rotation = Quaternion.LookRotation(direction);
        path.isStatic = true;

        var pathMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        pathMat.color = new Color(0.6f, 0.55f, 0.4f);
        path.GetComponent<MeshRenderer>().material = pathMat;
    }

    private static void CreateVillage()
    {
        var villageParent = new GameObject("=== VILLAGE ===");
        villageParent.transform.position = new Vector3(0, 0, -30f);

        // Maisons
        CreateHouse(villageParent.transform, new Vector3(-15f, 0, 0), "Maison_Forgeron");
        CreateHouse(villageParent.transform, new Vector3(-5f, 0, 5f), "Maison_Marchand");
        CreateHouse(villageParent.transform, new Vector3(5f, 0, 5f), "Taverne");
        CreateHouse(villageParent.transform, new Vector3(15f, 0, 0), "Maison_Mage");

        // Grande maison du chef
        CreateHouse(villageParent.transform, new Vector3(0, 0, -10f), "Maison_Chef", 1.5f);

        // Fontaine centrale
        CreateFountain(villageParent.transform, Vector3.zero);

        // Puits
        CreateWell(villageParent.transform, new Vector3(10f, 0, -5f));

        // Lampadaires
        CreateLamppost(villageParent.transform, new Vector3(-8f, 0, 0));
        CreateLamppost(villageParent.transform, new Vector3(8f, 0, 0));
    }

    private static void CreateHouse(Transform parent, Vector3 position, string name, float scale = 1f)
    {
        var house = new GameObject(name);
        house.transform.SetParent(parent);
        house.transform.position = parent.position + position;

        // Corps de la maison
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(house.transform);
        body.transform.localPosition = new Vector3(0, 2f * scale, 0);
        body.transform.localScale = new Vector3(4f * scale, 4f * scale, 5f * scale);
        body.isStatic = true;

        var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        wallMat.color = new Color(0.8f, 0.75f, 0.6f);
        body.GetComponent<MeshRenderer>().material = wallMat;

        // Toit
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(house.transform);
        roof.transform.localPosition = new Vector3(0, 4.5f * scale, 0);
        roof.transform.localScale = new Vector3(5f * scale, 1f * scale, 6f * scale);
        roof.transform.localRotation = Quaternion.Euler(0, 0, 0);
        roof.isStatic = true;

        var roofMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        roofMat.color = new Color(0.5f, 0.25f, 0.1f);
        roof.GetComponent<MeshRenderer>().material = roofMat;

        // Porte
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.transform.SetParent(house.transform);
        door.transform.localPosition = new Vector3(0, 1f * scale, 2.51f * scale);
        door.transform.localScale = new Vector3(1f * scale, 2f * scale, 0.1f);

        var doorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        doorMat.color = new Color(0.4f, 0.25f, 0.1f);
        door.GetComponent<MeshRenderer>().material = doorMat;
    }

    private static void CreateFountain(Transform parent, Vector3 position)
    {
        var fountain = new GameObject("Fontaine");
        fountain.transform.SetParent(parent);
        fountain.transform.position = parent.position + position;

        // Base
        var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.name = "Base";
        baseObj.transform.SetParent(fountain.transform);
        baseObj.transform.localPosition = new Vector3(0, 0.3f, 0);
        baseObj.transform.localScale = new Vector3(4f, 0.3f, 4f);
        baseObj.isStatic = true;
        baseObj.GetComponent<MeshRenderer>().material = _stoneMat;

        // Eau
        var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        water.name = "Water";
        water.transform.SetParent(fountain.transform);
        water.transform.localPosition = new Vector3(0, 0.4f, 0);
        water.transform.localScale = new Vector3(3.5f, 0.1f, 3.5f);
        water.GetComponent<MeshRenderer>().material = _waterMat;
        Object.DestroyImmediate(water.GetComponent<Collider>());

        // Pilier central
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Pillar";
        pillar.transform.SetParent(fountain.transform);
        pillar.transform.localPosition = new Vector3(0, 1f, 0);
        pillar.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
        pillar.isStatic = true;
        pillar.GetComponent<MeshRenderer>().material = _stoneMat;
    }

    private static void CreateWell(Transform parent, Vector3 position)
    {
        var well = new GameObject("Puits");
        well.transform.SetParent(parent);
        well.transform.position = parent.position + position;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(well.transform);
        body.transform.localPosition = new Vector3(0, 0.5f, 0);
        body.transform.localScale = new Vector3(1.5f, 0.5f, 1.5f);
        body.isStatic = true;
        body.GetComponent<MeshRenderer>().material = _stoneMat;
    }

    private static void CreateLamppost(Transform parent, Vector3 position)
    {
        var lamp = new GameObject("Lampadaire");
        lamp.transform.SetParent(parent);
        lamp.transform.position = parent.position + position;

        // Poteau
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(lamp.transform);
        pole.transform.localPosition = new Vector3(0, 2f, 0);
        pole.transform.localScale = new Vector3(0.1f, 2f, 0.1f);
        pole.isStatic = true;

        var poleMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        poleMat.color = new Color(0.2f, 0.2f, 0.2f);
        pole.GetComponent<MeshRenderer>().material = poleMat;

        // Lumiere
        var lightObj = new GameObject("Light");
        lightObj.transform.SetParent(lamp.transform);
        lightObj.transform.localPosition = new Vector3(0, 4f, 0);

        var pointLight = lightObj.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.color = new Color(1f, 0.9f, 0.7f);
        pointLight.intensity = 2f;
        pointLight.range = 10f;
    }

    private static void CreateDungeon()
    {
        var dungeonParent = new GameObject("=== DONJON ===");
        dungeonParent.transform.position = new Vector3(60f, 0, 0);

        // Entree du donjon (grande arche)
        CreateDungeonEntrance(dungeonParent.transform, Vector3.zero);

        // Couloir principal
        CreateDungeonCorridor(dungeonParent.transform, new Vector3(10f, 0, 0), 20f);

        // Salle du boss
        CreateBossRoom(dungeonParent.transform, new Vector3(35f, 0, 0));

        // Torches
        CreateTorch(dungeonParent.transform, new Vector3(5f, 2f, 3f));
        CreateTorch(dungeonParent.transform, new Vector3(5f, 2f, -3f));
        CreateTorch(dungeonParent.transform, new Vector3(15f, 2f, 3f));
        CreateTorch(dungeonParent.transform, new Vector3(15f, 2f, -3f));
        CreateTorch(dungeonParent.transform, new Vector3(25f, 2f, 3f));
        CreateTorch(dungeonParent.transform, new Vector3(25f, 2f, -3f));
    }

    private static void CreateDungeonEntrance(Transform parent, Vector3 position)
    {
        var entrance = new GameObject("Entree");
        entrance.transform.SetParent(parent);
        entrance.transform.position = parent.position + position;

        // Mur gauche
        var wallL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallL.name = "WallLeft";
        wallL.transform.SetParent(entrance.transform);
        wallL.transform.localPosition = new Vector3(0, 3f, 4f);
        wallL.transform.localScale = new Vector3(2f, 6f, 2f);
        wallL.isStatic = true;
        wallL.GetComponent<MeshRenderer>().material = _dungeonMat;

        // Mur droit
        var wallR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallR.name = "WallRight";
        wallR.transform.SetParent(entrance.transform);
        wallR.transform.localPosition = new Vector3(0, 3f, -4f);
        wallR.transform.localScale = new Vector3(2f, 6f, 2f);
        wallR.isStatic = true;
        wallR.GetComponent<MeshRenderer>().material = _dungeonMat;

        // Arche superieure
        var arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arch.name = "Arch";
        arch.transform.SetParent(entrance.transform);
        arch.transform.localPosition = new Vector3(0, 6.5f, 0);
        arch.transform.localScale = new Vector3(2f, 1f, 10f);
        arch.isStatic = true;
        arch.GetComponent<MeshRenderer>().material = _dungeonMat;
    }

    private static void CreateDungeonCorridor(Transform parent, Vector3 position, float length)
    {
        var corridor = new GameObject("Couloir");
        corridor.transform.SetParent(parent);
        corridor.transform.position = parent.position + position;

        // Sol
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(corridor.transform);
        floor.transform.localPosition = new Vector3(length / 2f, -0.25f, 0);
        floor.transform.localScale = new Vector3(length, 0.5f, 8f);
        floor.isStatic = true;
        floor.GetComponent<MeshRenderer>().material = _dungeonMat;

        // Mur gauche
        var wallL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallL.name = "WallLeft";
        wallL.transform.SetParent(corridor.transform);
        wallL.transform.localPosition = new Vector3(length / 2f, 2.5f, 4.5f);
        wallL.transform.localScale = new Vector3(length, 5f, 1f);
        wallL.isStatic = true;
        wallL.GetComponent<MeshRenderer>().material = _dungeonMat;

        // Mur droit
        var wallR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallR.name = "WallRight";
        wallR.transform.SetParent(corridor.transform);
        wallR.transform.localPosition = new Vector3(length / 2f, 2.5f, -4.5f);
        wallR.transform.localScale = new Vector3(length, 5f, 1f);
        wallR.isStatic = true;
        wallR.GetComponent<MeshRenderer>().material = _dungeonMat;

        // Plafond
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(corridor.transform);
        ceiling.transform.localPosition = new Vector3(length / 2f, 5.25f, 0);
        ceiling.transform.localScale = new Vector3(length, 0.5f, 10f);
        ceiling.isStatic = true;
        ceiling.GetComponent<MeshRenderer>().material = _dungeonMat;
    }

    private static void CreateBossRoom(Transform parent, Vector3 position)
    {
        var room = new GameObject("SalleDuBoss");
        room.transform.SetParent(parent);
        room.transform.position = parent.position + position;

        // Sol
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(room.transform);
        floor.transform.localPosition = new Vector3(10f, -0.25f, 0);
        floor.transform.localScale = new Vector3(20f, 0.5f, 20f);
        floor.isStatic = true;
        floor.GetComponent<MeshRenderer>().material = _dungeonMat;

        // Murs
        CreateWall(room.transform, new Vector3(20.5f, 2.5f, 0), new Vector3(1f, 5f, 20f));
        CreateWall(room.transform, new Vector3(10f, 2.5f, 10.5f), new Vector3(20f, 5f, 1f));
        CreateWall(room.transform, new Vector3(10f, 2.5f, -10.5f), new Vector3(20f, 5f, 1f));

        // Piliers
        CreatePillar(room.transform, new Vector3(5f, 0, 5f));
        CreatePillar(room.transform, new Vector3(5f, 0, -5f));
        CreatePillar(room.transform, new Vector3(15f, 0, 5f));
        CreatePillar(room.transform, new Vector3(15f, 0, -5f));

        // Trone
        CreateThrone(room.transform, new Vector3(17f, 0, 0));

        // Coffre au tresor
        CreateTreasureChest(room.transform, new Vector3(18f, 0, 5f));
    }

    private static void CreateWall(Transform parent, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        wall.isStatic = true;
        wall.GetComponent<MeshRenderer>().material = _dungeonMat;
    }

    private static void CreatePillar(Transform parent, Vector3 position)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Pillar";
        pillar.transform.SetParent(parent);
        pillar.transform.localPosition = position + new Vector3(0, 2.5f, 0);
        pillar.transform.localScale = new Vector3(1f, 2.5f, 1f);
        pillar.isStatic = true;
        pillar.GetComponent<MeshRenderer>().material = _stoneMat;
    }

    private static void CreateThrone(Transform parent, Vector3 position)
    {
        var throne = new GameObject("Trone");
        throne.transform.SetParent(parent);
        throne.transform.localPosition = position;

        // Base
        var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seat.name = "Seat";
        seat.transform.SetParent(throne.transform);
        seat.transform.localPosition = new Vector3(0, 0.5f, 0);
        seat.transform.localScale = new Vector3(2f, 1f, 2f);
        seat.isStatic = true;

        var throneMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        throneMat.color = new Color(0.6f, 0.5f, 0.1f);
        seat.GetComponent<MeshRenderer>().material = throneMat;

        // Dossier
        var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "Back";
        back.transform.SetParent(throne.transform);
        back.transform.localPosition = new Vector3(-0.75f, 2f, 0);
        back.transform.localScale = new Vector3(0.5f, 3f, 2f);
        back.isStatic = true;
        back.GetComponent<MeshRenderer>().material = throneMat;
    }

    private static void CreateTreasureChest(Transform parent, Vector3 position)
    {
        var chest = new GameObject("Coffre");
        chest.transform.SetParent(parent);
        chest.transform.localPosition = position;
        chest.tag = "Interactable";

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(chest.transform);
        body.transform.localPosition = new Vector3(0, 0.4f, 0);
        body.transform.localScale = new Vector3(1.2f, 0.8f, 0.8f);

        var chestMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        chestMat.color = new Color(0.5f, 0.35f, 0.1f);
        body.GetComponent<MeshRenderer>().material = chestMat;

        // Lid
        var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lid.name = "Lid";
        lid.transform.SetParent(chest.transform);
        lid.transform.localPosition = new Vector3(0, 0.9f, 0);
        lid.transform.localScale = new Vector3(1.3f, 0.2f, 0.9f);
        lid.GetComponent<MeshRenderer>().material = chestMat;
    }

    private static void CreateTorch(Transform parent, Vector3 position)
    {
        var torch = new GameObject("Torch");
        torch.transform.SetParent(parent);
        torch.transform.localPosition = position;

        var holder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        holder.name = "Holder";
        holder.transform.SetParent(torch.transform);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localScale = new Vector3(0.1f, 0.3f, 0.1f);

        var holderMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        holderMat.color = new Color(0.3f, 0.2f, 0.1f);
        holder.GetComponent<MeshRenderer>().material = holderMat;

        // Lumiere
        var lightGO = new GameObject("TorchLight");
        lightGO.transform.SetParent(torch.transform);
        lightGO.transform.localPosition = new Vector3(0, 0.4f, 0);

        var torchLight = lightGO.AddComponent<Light>();
        torchLight.type = LightType.Point;
        torchLight.color = new Color(1f, 0.6f, 0.2f);
        torchLight.intensity = 3f;
        torchLight.range = 8f;
    }

    private static void CreateForest()
    {
        var forestParent = new GameObject("=== FORET ===");
        forestParent.transform.position = new Vector3(-50f, 0, 30f);

        // Creer des arbres
        for (int i = 0; i < 30; i++)
        {
            float x = Random.Range(-20f, 20f);
            float z = Random.Range(-20f, 20f);
            CreateTree(forestParent.transform, new Vector3(x, 0, z));
        }

        // Rochers
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(-25f, 25f);
            float z = Random.Range(-25f, 25f);
            CreateRock(forestParent.transform, new Vector3(x, 0, z));
        }

        // Champignons (collectibles)
        for (int i = 0; i < 5; i++)
        {
            float x = Random.Range(-15f, 15f);
            float z = Random.Range(-15f, 15f);
            CreateMushroom(forestParent.transform, new Vector3(x, 0, z), i);
        }
    }

    private static void CreateTree(Transform parent, Vector3 position)
    {
        var tree = new GameObject("Tree");
        tree.transform.SetParent(parent);
        tree.transform.localPosition = position;

        float height = Random.Range(4f, 8f);
        float trunkRadius = Random.Range(0.2f, 0.4f);

        // Tronc
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(tree.transform);
        trunk.transform.localPosition = new Vector3(0, height / 2f, 0);
        trunk.transform.localScale = new Vector3(trunkRadius, height / 2f, trunkRadius);
        trunk.isStatic = true;

        var trunkMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        trunkMat.color = new Color(0.4f, 0.25f, 0.1f);
        trunk.GetComponent<MeshRenderer>().material = trunkMat;

        // Feuillage
        var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.name = "Leaves";
        leaves.transform.SetParent(tree.transform);
        leaves.transform.localPosition = new Vector3(0, height + 1f, 0);
        leaves.transform.localScale = new Vector3(3f, 4f, 3f) * (height / 6f);
        leaves.isStatic = true;
        leaves.GetComponent<MeshRenderer>().material = _treeMat;
    }

    private static void CreateRock(Transform parent, Vector3 position)
    {
        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Rock";
        rock.transform.SetParent(parent);
        rock.transform.localPosition = position + new Vector3(0, 0.3f, 0);

        float scale = Random.Range(0.5f, 1.5f);
        rock.transform.localScale = new Vector3(scale * 1.5f, scale, scale);
        rock.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        rock.isStatic = true;
        rock.GetComponent<MeshRenderer>().material = _stoneMat;
    }

    private static void CreateMushroom(Transform parent, Vector3 position, int index)
    {
        var mushroom = new GameObject($"Mushroom_{index}");
        mushroom.transform.SetParent(parent);
        mushroom.transform.localPosition = position;
        mushroom.tag = "Collectible";

        // Pied
        var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(mushroom.transform);
        stem.transform.localPosition = new Vector3(0, 0.15f, 0);
        stem.transform.localScale = new Vector3(0.1f, 0.15f, 0.1f);

        var stemMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        stemMat.color = Color.white;
        stem.GetComponent<MeshRenderer>().material = stemMat;

        // Chapeau
        var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = "Cap";
        cap.transform.SetParent(mushroom.transform);
        cap.transform.localPosition = new Vector3(0, 0.35f, 0);
        cap.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f);

        var capMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        capMat.color = new Color(1f, 0.2f, 0.2f);
        cap.GetComponent<MeshRenderer>().material = capMat;

        // Collider pour pickup
        var col = mushroom.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;
    }

    private static void CreateLake()
    {
        var lakeParent = new GameObject("=== LAC ===");
        lakeParent.transform.position = new Vector3(50f, 0, -50f);

        // Eau
        var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        water.name = "Water";
        water.transform.SetParent(lakeParent.transform);
        water.transform.localPosition = new Vector3(0, -0.1f, 0);
        water.transform.localScale = new Vector3(15f, 0.1f, 15f);
        water.GetComponent<MeshRenderer>().material = _waterMat;

        // Le collider d'eau peut ralentir le joueur
        water.tag = "Water";

        // Bord du lac (pierres)
        for (int i = 0; i < 20; i++)
        {
            float angle = i * (360f / 20f) * Mathf.Deg2Rad;
            float radius = 7.5f + Random.Range(-0.5f, 0.5f);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            CreateRock(lakeParent.transform, pos);
        }

        // Quai
        CreateDock(lakeParent.transform, new Vector3(8f, 0, 0));
    }

    private static void CreateDock(Transform parent, Vector3 position)
    {
        var dock = new GameObject("Quai");
        dock.transform.SetParent(parent);
        dock.transform.localPosition = position;

        var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.name = "Plank";
        plank.transform.SetParent(dock.transform);
        plank.transform.localPosition = new Vector3(3f, 0.2f, 0);
        plank.transform.localScale = new Vector3(8f, 0.2f, 2f);
        plank.isStatic = true;

        var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        woodMat.color = new Color(0.5f, 0.35f, 0.2f);
        plank.GetComponent<MeshRenderer>().material = woodMat;
    }

    private static void CreateGameSystems()
    {
        var systemsParent = new GameObject("=== GAME SYSTEMS ===");

        // GameManager
        var gmGO = new GameObject("GameManager");
        gmGO.transform.SetParent(systemsParent.transform);
        gmGO.AddComponent<GameManager>();

        // AudioManager
        var audioGO = new GameObject("AudioManager");
        audioGO.transform.SetParent(systemsParent.transform);
        audioGO.AddComponent<AudioSource>();

        // EventSystem pour UI
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(systemsParent.transform);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    private static void CreatePlayer()
    {
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Default");
        player.transform.position = new Vector3(0f, 1f, -25f);

        // Materiau bleu pour le joueur
        var playerMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        playerMat.color = new Color(0.2f, 0.4f, 0.8f);
        player.GetComponent<MeshRenderer>().material = playerMat;

        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

        var rb = player.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.mass = 70f;
        rb.linearDamping = 5f;

        var col = player.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.5f;
        col.center = Vector3.zero;

        // Composants du joueur
        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerStats>();
        player.AddComponent<CombatController>();
        player.AddComponent<Inventory>();

        // Arme
        var weaponHolder = new GameObject("WeaponHolder");
        weaponHolder.transform.SetParent(player.transform);
        weaponHolder.transform.localPosition = new Vector3(0.7f, 0.3f, 0.3f);

        var sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sword.name = "Sword";
        sword.transform.SetParent(weaponHolder.transform);
        sword.transform.localPosition = Vector3.zero;
        sword.transform.localScale = new Vector3(0.1f, 0.1f, 1.2f);
        sword.transform.localRotation = Quaternion.Euler(0, 0, -15f);
        Object.DestroyImmediate(sword.GetComponent<BoxCollider>());

        var swordMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        swordMat.color = new Color(0.8f, 0.8f, 0.9f);
        swordMat.SetFloat("_Metallic", 0.9f);
        swordMat.SetFloat("_Smoothness", 0.8f);
        sword.GetComponent<MeshRenderer>().material = swordMat;

        // Hitbox
        var hitboxGO = new GameObject("Hitbox");
        hitboxGO.transform.SetParent(sword.transform);
        hitboxGO.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        var hitboxCol = hitboxGO.AddComponent<BoxCollider>();
        hitboxCol.isTrigger = true;
        hitboxCol.size = new Vector3(1f, 1f, 2f);
        hitboxGO.AddComponent<Hitbox>();
        hitboxGO.SetActive(false);

        // Hurtbox
        var hurtboxGO = new GameObject("Hurtbox");
        hurtboxGO.transform.SetParent(player.transform);
        hurtboxGO.transform.localPosition = Vector3.zero;
        var hurtboxCol = hurtboxGO.AddComponent<CapsuleCollider>();
        hurtboxCol.isTrigger = true;
        hurtboxCol.height = 2f;
        hurtboxCol.radius = 0.6f;
        hurtboxGO.AddComponent<Hurtbox>();

        // Camera
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = player.transform.position + new Vector3(0f, 8f, -10f);
            mainCamera.transform.LookAt(player.transform);
            mainCamera.gameObject.AddComponent<CameraController>();
        }

        Debug.Log("[WorldGenerator] Player cree avec composants complets");
    }

    private static void CreateEnemies()
    {
        var enemiesParent = new GameObject("=== ENEMIES ===");

        // Ennemis dans la foret
        CreateEnemy(enemiesParent.transform, new Vector3(-45f, 1f, 25f), "Goblin_1", Color.green, 50f);
        CreateEnemy(enemiesParent.transform, new Vector3(-55f, 1f, 35f), "Goblin_2", Color.green, 50f);
        CreateEnemy(enemiesParent.transform, new Vector3(-50f, 1f, 40f), "Wolf", new Color(0.5f, 0.4f, 0.3f), 40f);

        // Ennemis pres du donjon
        CreateEnemy(enemiesParent.transform, new Vector3(55f, 1f, 5f), "Skeleton_1", Color.white, 80f);
        CreateEnemy(enemiesParent.transform, new Vector3(55f, 1f, -5f), "Skeleton_2", Color.white, 80f);
        CreateEnemy(enemiesParent.transform, new Vector3(65f, 1f, 0f), "DarkKnight", new Color(0.2f, 0.2f, 0.3f), 200f);

        // Boss dans le donjon
        CreateBoss(enemiesParent.transform, new Vector3(90f, 1f, 0f), "Boss_DragonKnight");

        Debug.Log("[WorldGenerator] Ennemis crees");
    }

    private static void CreateEnemy(Transform parent, Vector3 position, string name, Color color, float health)
    {
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = name;
        enemy.tag = "Enemy";
        enemy.transform.SetParent(parent);
        enemy.transform.position = position;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        enemy.GetComponent<MeshRenderer>().material = mat;

        var agent = enemy.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.speed = 3f;
        agent.stoppingDistance = 2f;

        var healthComp = enemy.AddComponent<Health>();
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

        // Barre de vie au dessus
        CreateWorldSpaceHealthBar(enemy.transform);
    }

    private static void CreateBoss(Transform parent, Vector3 position, string name)
    {
        var boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        boss.name = name;
        boss.tag = "Enemy";
        boss.transform.SetParent(parent);
        boss.transform.position = position;
        boss.transform.localScale = new Vector3(2f, 2f, 2f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.5f, 0.1f, 0.1f);
        boss.GetComponent<MeshRenderer>().material = mat;

        var agent = boss.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.speed = 2f;
        agent.stoppingDistance = 3f;

        boss.AddComponent<Health>();
        boss.AddComponent<EnemyAI>();

        // Hurtbox
        var hurtboxGO = new GameObject("Hurtbox");
        hurtboxGO.transform.SetParent(boss.transform);
        hurtboxGO.transform.localPosition = Vector3.zero;
        var hurtboxCol = hurtboxGO.AddComponent<CapsuleCollider>();
        hurtboxCol.isTrigger = true;
        hurtboxCol.height = 2f;
        hurtboxCol.radius = 0.8f;
        hurtboxGO.AddComponent<Hurtbox>();

        // Grande barre de vie
        CreateWorldSpaceHealthBar(boss.transform, 3f);
    }

    private static void CreateWorldSpaceHealthBar(Transform parent, float yOffset = 2.5f)
    {
        var healthBarGO = new GameObject("HealthBar");
        healthBarGO.transform.SetParent(parent);
        healthBarGO.transform.localPosition = new Vector3(0, yOffset, 0);

        // Background
        var bgGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bgGO.name = "Background";
        bgGO.transform.SetParent(healthBarGO.transform);
        bgGO.transform.localPosition = Vector3.zero;
        bgGO.transform.localScale = new Vector3(1f, 0.1f, 0.05f);
        Object.DestroyImmediate(bgGO.GetComponent<BoxCollider>());

        var bgMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        bgMat.color = Color.black;
        bgGO.GetComponent<MeshRenderer>().material = bgMat;

        // Fill
        var fillGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fillGO.name = "Fill";
        fillGO.transform.SetParent(healthBarGO.transform);
        fillGO.transform.localPosition = new Vector3(0, 0, -0.03f);
        fillGO.transform.localScale = new Vector3(0.95f, 0.08f, 0.05f);
        Object.DestroyImmediate(fillGO.GetComponent<BoxCollider>());

        var fillMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        fillMat.color = Color.red;
        fillGO.GetComponent<MeshRenderer>().material = fillMat;
    }

    private static void CreateNPCs()
    {
        var npcsParent = new GameObject("=== NPCs ===");

        // Marchand
        CreateNPC(npcsParent.transform, new Vector3(-5f, 1f, -25f), "Marchand", new Color(0.8f, 0.6f, 0.2f));

        // Forgeron
        CreateNPC(npcsParent.transform, new Vector3(-15f, 1f, -30f), "Forgeron", new Color(0.6f, 0.3f, 0.1f));

        // Mage
        CreateNPC(npcsParent.transform, new Vector3(15f, 1f, -30f), "Mage", new Color(0.5f, 0.2f, 0.8f));

        // Chef du village
        CreateNPC(npcsParent.transform, new Vector3(0f, 1f, -40f), "Chef", new Color(0.9f, 0.8f, 0.1f));

        Debug.Log("[WorldGenerator] NPCs crees");
    }

    private static void CreateNPC(Transform parent, Vector3 position, string name, Color color)
    {
        var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = $"NPC_{name}";
        npc.tag = "NPC";
        npc.transform.SetParent(parent);
        npc.transform.position = position;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        npc.GetComponent<MeshRenderer>().material = mat;

        // Indicateur d'interaction (point d'exclamation)
        var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicator.name = "QuestIndicator";
        indicator.transform.SetParent(npc.transform);
        indicator.transform.localPosition = new Vector3(0, 2.5f, 0);
        indicator.transform.localScale = Vector3.one * 0.3f;
        Object.DestroyImmediate(indicator.GetComponent<Collider>());

        var indicatorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        indicatorMat.color = Color.yellow;
        indicatorMat.SetFloat("_Metallic", 0f);
        indicator.GetComponent<MeshRenderer>().material = indicatorMat;

        // Zone d'interaction
        var interactZone = new GameObject("InteractZone");
        interactZone.transform.SetParent(npc.transform);
        interactZone.transform.localPosition = Vector3.zero;
        var interactCol = interactZone.AddComponent<SphereCollider>();
        interactCol.isTrigger = true;
        interactCol.radius = 3f;
    }

    private static void CreateMount()
    {
        var mountsParent = new GameObject("=== MOUNTS ===");

        // Cheval pres de l'ecurie (village)
        var horse = new GameObject("Mount_Horse");
        horse.transform.SetParent(mountsParent.transform);
        horse.transform.position = new Vector3(20f, 0, -25f);
        horse.tag = "Mount";

        // Corps
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(horse.transform);
        body.transform.localPosition = new Vector3(0, 1f, 0);
        body.transform.localScale = new Vector3(1f, 0.8f, 2f);
        body.transform.localRotation = Quaternion.Euler(90f, 0, 0);

        var horseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        horseMat.color = new Color(0.5f, 0.35f, 0.2f);
        body.GetComponent<MeshRenderer>().material = horseMat;

        // Tete
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(horse.transform);
        head.transform.localPosition = new Vector3(0, 1.5f, 1.2f);
        head.transform.localScale = new Vector3(0.5f, 0.5f, 0.7f);
        head.GetComponent<MeshRenderer>().material = horseMat;
        Object.DestroyImmediate(head.GetComponent<Collider>());

        // Jambes
        CreateHorseLeg(horse.transform, new Vector3(-0.3f, 0, 0.5f), horseMat);
        CreateHorseLeg(horse.transform, new Vector3(0.3f, 0, 0.5f), horseMat);
        CreateHorseLeg(horse.transform, new Vector3(-0.3f, 0, -0.5f), horseMat);
        CreateHorseLeg(horse.transform, new Vector3(0.3f, 0, -0.5f), horseMat);

        // Zone de montage
        var mountZone = new GameObject("MountZone");
        mountZone.transform.SetParent(horse.transform);
        var mountCol = mountZone.AddComponent<SphereCollider>();
        mountCol.isTrigger = true;
        mountCol.radius = 2f;

        Debug.Log("[WorldGenerator] Monture creee");
    }

    private static void CreateHorseLeg(Transform parent, Vector3 position, Material mat)
    {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leg.name = "Leg";
        leg.transform.SetParent(parent);
        leg.transform.localPosition = position + new Vector3(0, 0.4f, 0);
        leg.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
        leg.GetComponent<MeshRenderer>().material = mat;
        Object.DestroyImmediate(leg.GetComponent<Collider>());
    }

    private static void CreateCollectibles()
    {
        var collectiblesParent = new GameObject("=== COLLECTIBLES ===");

        // Potions de soin
        CreatePotion(collectiblesParent.transform, new Vector3(10f, 0.5f, -20f), "HealthPotion", Color.red);
        CreatePotion(collectiblesParent.transform, new Vector3(-10f, 0.5f, -20f), "HealthPotion", Color.red);
        CreatePotion(collectiblesParent.transform, new Vector3(5f, 0.5f, 10f), "HealthPotion", Color.red);

        // Potions de mana
        CreatePotion(collectiblesParent.transform, new Vector3(12f, 0.5f, -22f), "ManaPotion", Color.blue);
        CreatePotion(collectiblesParent.transform, new Vector3(-8f, 0.5f, 15f), "ManaPotion", Color.blue);

        // Pieces d'or
        for (int i = 0; i < 15; i++)
        {
            float x = Random.Range(-30f, 30f);
            float z = Random.Range(-30f, 30f);
            CreateCoin(collectiblesParent.transform, new Vector3(x, 0.3f, z), i);
        }

        // Orbes d'experience
        CreateExpOrb(collectiblesParent.transform, new Vector3(30f, 0.5f, 20f));
        CreateExpOrb(collectiblesParent.transform, new Vector3(-30f, 0.5f, -10f));
        CreateExpOrb(collectiblesParent.transform, new Vector3(0f, 0.5f, 40f));

        Debug.Log("[WorldGenerator] Collectibles crees");
    }

    private static void CreatePotion(Transform parent, Vector3 position, string type, Color color)
    {
        var potion = new GameObject($"{type}");
        potion.transform.SetParent(parent);
        potion.transform.position = position;
        potion.tag = "Collectible";

        // Flacon
        var flask = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        flask.name = "Flask";
        flask.transform.SetParent(potion.transform);
        flask.transform.localPosition = Vector3.zero;
        flask.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        flask.GetComponent<MeshRenderer>().material = mat;
        Object.DestroyImmediate(flask.GetComponent<Collider>());

        var col = potion.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;
    }

    private static void CreateCoin(Transform parent, Vector3 position, int index)
    {
        var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coin.name = $"Coin_{index}";
        coin.transform.SetParent(parent);
        coin.transform.position = position;
        coin.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
        coin.tag = "Collectible";

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.85f, 0f);
        mat.SetFloat("_Metallic", 1f);
        mat.SetFloat("_Smoothness", 0.9f);
        coin.GetComponent<MeshRenderer>().material = mat;

        var col = coin.GetComponent<CapsuleCollider>();
        if (col != null) Object.DestroyImmediate(col);

        var sphereCol = coin.AddComponent<SphereCollider>();
        sphereCol.isTrigger = true;
        sphereCol.radius = 0.5f;
    }

    private static void CreateExpOrb(Transform parent, Vector3 position)
    {
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "ExpOrb";
        orb.transform.SetParent(parent);
        orb.transform.position = position;
        orb.transform.localScale = Vector3.one * 0.5f;
        orb.tag = "Collectible";

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.5f, 1f, 0.5f);
        mat.SetFloat("_Metallic", 0.3f);
        orb.GetComponent<MeshRenderer>().material = mat;

        orb.GetComponent<SphereCollider>().isTrigger = true;

        // Lumiere
        var light = orb.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.5f, 1f, 0.5f);
        light.intensity = 1f;
        light.range = 3f;
    }

    private static void CreateFullUI()
    {
        // Canvas principal
        var canvasGO = new GameObject("=== UI CANVAS ===");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // HUD
        CreateHUD(canvasGO.transform);

        // Menu de pause (desactive)
        CreatePauseMenu(canvasGO.transform);

        // Inventaire (desactive)
        CreateInventoryUI(canvasGO.transform);

        // Dialogue (desactive)
        CreateDialogueUI(canvasGO.transform);

        // Minimap
        CreateMinimap(canvasGO.transform);

        Debug.Log("[WorldGenerator] UI complete creee");
    }

    private static void CreateHUD(Transform parent)
    {
        var hud = new GameObject("HUD");
        hud.transform.SetParent(parent);

        var rectTransform = hud.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        // Barre de vie
        CreateStatusBar(hud.transform, "HealthBar", new Vector2(-350, -30), Color.red, "HP: 100/100");

        // Barre de mana
        CreateStatusBar(hud.transform, "ManaBar", new Vector2(-350, -60), Color.blue, "MP: 50/50");

        // Barre de stamina
        CreateStatusBar(hud.transform, "StaminaBar", new Vector2(-350, -90), Color.yellow, "SP: 100/100");

        // Barre d'experience
        CreateExpBar(hud.transform);

        // Niveau
        CreateLevelDisplay(hud.transform);

        // Gold
        CreateGoldDisplay(hud.transform);

        // Hotbar des competences
        CreateSkillHotbar(hud.transform);

        // Indicateur de quete
        CreateQuestTracker(hud.transform);
    }

    private static void CreateStatusBar(Transform parent, string name, Vector2 position, Color color, string text)
    {
        var bar = new GameObject(name);
        bar.transform.SetParent(parent);

        var rect = bar.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(20, position.y);
        rect.sizeDelta = new Vector2(200, 25);

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(bar.transform);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Fill
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(bar.transform);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = color;

        // Text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(bar.transform);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmpText = textGO.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = 14;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
    }

    private static void CreateExpBar(Transform parent)
    {
        var expBar = new GameObject("ExpBar");
        expBar.transform.SetParent(parent);

        var rect = expBar.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 10);
        rect.sizeDelta = new Vector2(-40, 15);

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(expBar.transform);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // Fill
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(expBar.transform);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0.3f, 1f); // 30% d'experience
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.5f, 0.8f, 1f);

        // Text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(expBar.transform);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmpText = textGO.AddComponent<TextMeshProUGUI>();
        tmpText.text = "EXP: 300/1000";
        tmpText.fontSize = 10;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
    }

    private static void CreateLevelDisplay(Transform parent)
    {
        var levelGO = new GameObject("LevelDisplay");
        levelGO.transform.SetParent(parent);

        var rect = levelGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(230, -30);
        rect.sizeDelta = new Vector2(60, 60);

        var bg = levelGO.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(levelGO.transform);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmpText = textGO.AddComponent<TextMeshProUGUI>();
        tmpText.text = "LV\n5";
        tmpText.fontSize = 18;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
    }

    private static void CreateGoldDisplay(Transform parent)
    {
        var goldGO = new GameObject("GoldDisplay");
        goldGO.transform.SetParent(parent);

        var rect = goldGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -20);
        rect.sizeDelta = new Vector2(150, 40);

        var bg = goldGO.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(goldGO.transform);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 0);
        textRect.offsetMax = new Vector2(-10, 0);

        var tmpText = textGO.AddComponent<TextMeshProUGUI>();
        tmpText.text = "1,250 Gold";
        tmpText.fontSize = 18;
        tmpText.alignment = TextAlignmentOptions.MidlineRight;
        tmpText.color = new Color(1f, 0.85f, 0f);
    }

    private static void CreateSkillHotbar(Transform parent)
    {
        var hotbar = new GameObject("SkillHotbar");
        hotbar.transform.SetParent(parent);

        var rect = hotbar.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 35);
        rect.sizeDelta = new Vector2(320, 60);

        // 5 slots de competences
        string[] skillNames = { "1:Slash", "2:Fireball", "3:Heal", "4:Shield", "5:Dash" };
        Color[] skillColors = { Color.white, Color.red, Color.green, Color.cyan, Color.yellow };

        for (int i = 0; i < 5; i++)
        {
            CreateSkillSlot(hotbar.transform, i, skillNames[i], skillColors[i]);
        }
    }

    private static void CreateSkillSlot(Transform parent, int index, string skillName, Color color)
    {
        var slot = new GameObject($"Skill_{index + 1}");
        slot.transform.SetParent(parent);

        var rect = slot.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = new Vector2(10 + index * 62, 0);
        rect.sizeDelta = new Vector2(55, 55);

        var bg = slot.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        // Icone
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(slot.transform);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.2f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color = color;

        // Keybind
        var keyGO = new GameObject("Key");
        keyGO.transform.SetParent(slot.transform);
        var keyRect = keyGO.AddComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0, 0);
        keyRect.anchorMax = new Vector2(1, 0.25f);
        keyRect.offsetMin = Vector2.zero;
        keyRect.offsetMax = Vector2.zero;

        var keyText = keyGO.AddComponent<TextMeshProUGUI>();
        keyText.text = skillName;
        keyText.fontSize = 8;
        keyText.alignment = TextAlignmentOptions.Center;
        keyText.color = Color.white;
    }

    private static void CreateQuestTracker(Transform parent)
    {
        var tracker = new GameObject("QuestTracker");
        tracker.transform.SetParent(parent);

        var rect = tracker.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -70);
        rect.sizeDelta = new Vector2(250, 100);

        var bg = tracker.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);

        // Titre
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(tracker.transform);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.7f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = new Vector2(10, 0);
        titleRect.offsetMax = new Vector2(-10, -5);

        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "Quete: Eliminer les Gobelins";
        titleText.fontSize = 12;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(1f, 0.8f, 0f);

        // Objectif
        var objGO = new GameObject("Objective");
        objGO.transform.SetParent(tracker.transform);
        var objRect = objGO.AddComponent<RectTransform>();
        objRect.anchorMin = new Vector2(0, 0);
        objRect.anchorMax = new Vector2(1, 0.7f);
        objRect.offsetMin = new Vector2(10, 5);
        objRect.offsetMax = new Vector2(-10, 0);

        var objText = objGO.AddComponent<TextMeshProUGUI>();
        objText.text = "- Gobelins tues: 0/3\n- Retourner au village";
        objText.fontSize = 11;
        objText.color = Color.white;
    }

    private static void CreatePauseMenu(Transform parent)
    {
        var pauseMenu = new GameObject("PauseMenu");
        pauseMenu.transform.SetParent(parent);
        pauseMenu.SetActive(false);

        var rect = pauseMenu.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Fond sombre
        var bg = pauseMenu.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.8f);

        // Titre
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(pauseMenu.transform);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.8f);
        titleRect.anchorMax = new Vector2(0.5f, 0.8f);
        titleRect.sizeDelta = new Vector2(300, 50);

        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "PAUSE";
        titleText.fontSize = 36;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Boutons
        CreateMenuButton(pauseMenu.transform, "Resume", new Vector2(0, 50), "Reprendre");
        CreateMenuButton(pauseMenu.transform, "Options", new Vector2(0, -20), "Options");
        CreateMenuButton(pauseMenu.transform, "Quit", new Vector2(0, -90), "Quitter");
    }

    private static void CreateMenuButton(Transform parent, string name, Vector2 position, string text)
    {
        var buttonGO = new GameObject($"Button_{name}");
        buttonGO.transform.SetParent(parent);

        var rect = buttonGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(200, 50);

        var image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f);

        var button = buttonGO.AddComponent<Button>();
        button.targetGraphic = image;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmpText = textGO.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = 20;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
    }

    private static void CreateInventoryUI(Transform parent)
    {
        var inventory = new GameObject("InventoryPanel");
        inventory.transform.SetParent(parent);
        inventory.SetActive(false);

        var rect = inventory.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400, 500);

        var bg = inventory.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // Titre
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(inventory.transform);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(0, 40);

        var titleBg = titleGO.AddComponent<Image>();
        titleBg.color = new Color(0.25f, 0.25f, 0.25f);

        var titleTextGO = new GameObject("Text");
        titleTextGO.transform.SetParent(titleGO.transform);
        var titleTextRect = titleTextGO.AddComponent<RectTransform>();
        titleTextRect.anchorMin = Vector2.zero;
        titleTextRect.anchorMax = Vector2.one;
        titleTextRect.offsetMin = Vector2.zero;
        titleTextRect.offsetMax = Vector2.zero;

        var titleText = titleTextGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "INVENTAIRE";
        titleText.fontSize = 20;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Grille d'inventaire (6x5 = 30 slots)
        var grid = new GameObject("Grid");
        grid.transform.SetParent(inventory.transform);
        var gridRect = grid.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0, 0);
        gridRect.anchorMax = new Vector2(1, 1);
        gridRect.offsetMin = new Vector2(20, 60);
        gridRect.offsetMax = new Vector2(-20, -50);

        var gridLayout = grid.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(55, 55);
        gridLayout.spacing = new Vector2(5, 5);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;

        // Creer 30 slots
        for (int i = 0; i < 30; i++)
        {
            CreateInventorySlot(grid.transform, i);
        }
    }

    private static void CreateInventorySlot(Transform parent, int index)
    {
        var slot = new GameObject($"Slot_{index}");
        slot.transform.SetParent(parent);

        var rect = slot.AddComponent<RectTransform>();
        var image = slot.AddComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        // Quelques items exemple
        if (index == 0 || index == 1 || index == 5)
        {
            var itemGO = new GameObject("Item");
            itemGO.transform.SetParent(slot.transform);
            var itemRect = itemGO.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0.1f, 0.1f);
            itemRect.anchorMax = new Vector2(0.9f, 0.9f);
            itemRect.offsetMin = Vector2.zero;
            itemRect.offsetMax = Vector2.zero;

            var itemImg = itemGO.AddComponent<Image>();
            itemImg.color = index == 0 ? Color.red : (index == 1 ? Color.blue : Color.gray);
        }
    }

    private static void CreateDialogueUI(Transform parent)
    {
        var dialogue = new GameObject("DialoguePanel");
        dialogue.transform.SetParent(parent);
        dialogue.SetActive(false);

        var rect = dialogue.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.05f);
        rect.anchorMax = new Vector2(0.9f, 0.3f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var bg = dialogue.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Nom du PNJ
        var nameGO = new GameObject("NPCName");
        nameGO.transform.SetParent(dialogue.transform);
        var nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(0.3f, 1);
        nameRect.pivot = new Vector2(0, 1);
        nameRect.anchoredPosition = new Vector2(10, 10);
        nameRect.sizeDelta = new Vector2(0, 30);

        var nameBg = nameGO.AddComponent<Image>();
        nameBg.color = new Color(0.2f, 0.2f, 0.3f);

        var nameTextGO = new GameObject("Text");
        nameTextGO.transform.SetParent(nameGO.transform);
        var nameTextRect = nameTextGO.AddComponent<RectTransform>();
        nameTextRect.anchorMin = Vector2.zero;
        nameTextRect.anchorMax = Vector2.one;
        nameTextRect.offsetMin = new Vector2(10, 0);
        nameTextRect.offsetMax = new Vector2(-10, 0);

        var nameText = nameTextGO.AddComponent<TextMeshProUGUI>();
        nameText.text = "Marchand";
        nameText.fontSize = 16;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.color = Color.white;

        // Texte du dialogue
        var textGO = new GameObject("DialogueText");
        textGO.transform.SetParent(dialogue.transform);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(20, 20);
        textRect.offsetMax = new Vector2(-20, -50);

        var dialogueText = textGO.AddComponent<TextMeshProUGUI>();
        dialogueText.text = "Bienvenue voyageur! J'ai des potions de soin et des equipements a vendre. Voulez-vous voir mes marchandises?";
        dialogueText.fontSize = 16;
        dialogueText.color = Color.white;

        // Indicateur "Appuyez pour continuer"
        var continueGO = new GameObject("ContinueText");
        continueGO.transform.SetParent(dialogue.transform);
        var continueRect = continueGO.AddComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(1, 0);
        continueRect.anchorMax = new Vector2(1, 0);
        continueRect.pivot = new Vector2(1, 0);
        continueRect.anchoredPosition = new Vector2(-10, 10);
        continueRect.sizeDelta = new Vector2(200, 20);

        var continueText = continueGO.AddComponent<TextMeshProUGUI>();
        continueText.text = "[Espace] Continuer";
        continueText.fontSize = 12;
        continueText.alignment = TextAlignmentOptions.MidlineRight;
        continueText.color = new Color(0.7f, 0.7f, 0.7f);
    }

    private static void CreateMinimap(Transform parent)
    {
        var minimap = new GameObject("Minimap");
        minimap.transform.SetParent(parent);

        var rect = minimap.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -180);
        rect.sizeDelta = new Vector2(150, 150);

        // Fond
        var bg = minimap.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.15f, 0.1f, 0.9f);

        // Bordure
        var border = new GameObject("Border");
        border.transform.SetParent(minimap.transform);
        var borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-3, -3);
        borderRect.offsetMax = new Vector2(3, 3);
        var borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(0.4f, 0.35f, 0.2f);
        border.transform.SetAsFirstSibling();

        // Indicateur joueur
        var playerMarker = new GameObject("PlayerMarker");
        playerMarker.transform.SetParent(minimap.transform);
        var markerRect = playerMarker.AddComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(10, 10);

        var markerImg = playerMarker.AddComponent<Image>();
        markerImg.color = Color.blue;

        // Texte "N" pour le nord
        var northGO = new GameObject("North");
        northGO.transform.SetParent(minimap.transform);
        var northRect = northGO.AddComponent<RectTransform>();
        northRect.anchorMin = new Vector2(0.5f, 1);
        northRect.anchorMax = new Vector2(0.5f, 1);
        northRect.pivot = new Vector2(0.5f, 1);
        northRect.anchoredPosition = new Vector2(0, -5);
        northRect.sizeDelta = new Vector2(20, 15);

        var northText = northGO.AddComponent<TextMeshProUGUI>();
        northText.text = "N";
        northText.fontSize = 12;
        northText.alignment = TextAlignmentOptions.Center;
        northText.color = Color.white;
    }

    private static void MarkStaticObjects()
    {
        // Trouver tous les objets marques static et les configurer pour NavMesh
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var obj in allObjects)
        {
            if (obj.isStatic && obj.GetComponent<MeshRenderer>() != null)
            {
                GameObjectUtility.SetStaticEditorFlags(obj,
                    StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccludeeStatic);
                count++;
            }
        }

        Debug.Log($"[WorldGenerator] {count} objets marques pour NavMesh");
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        string directory = System.IO.Path.GetDirectoryName(filePath);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
    }
}
