using UnityEngine;
using UnityEditor;

/// <summary>
/// Cree toutes les donnees de test pour le jeu.
/// Menu: EpicLegends > Data > Create All Test Data
/// </summary>
public class TestDataCreator : EditorWindow
{
    [MenuItem("EpicLegends/Data/Create All Test Data")]
    public static void CreateAllTestData()
    {
        // Creer les dossiers necessaires
        EnsureDirectories();

        // Creer les armes
        CreateWeapons();

        // Creer les items
        CreateItems();

        // Creer les ennemis
        CreateEnemies();

        // Creer les competences
        CreateSkills();

        AssetDatabase.Refresh();
        Debug.Log("[TestDataCreator] Toutes les donnees de test ont ete creees!");
    }

    private static void EnsureDirectories()
    {
        string[] dirs = {
            "Assets/ScriptableObjects",
            "Assets/ScriptableObjects/Weapons",
            "Assets/ScriptableObjects/Items",
            "Assets/ScriptableObjects/Enemies",
            "Assets/ScriptableObjects/Skills"
        };

        foreach (var dir in dirs)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parts = dir.Split('/');
                string parent = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string newPath = parent + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(parent, parts[i]);
                    }
                    parent = newPath;
                }
            }
        }
    }

    [MenuItem("EpicLegends/Data/Create Weapons")]
    public static void CreateWeapons()
    {
        // Epee de fer (debutant)
        var ironSword = ScriptableObject.CreateInstance<WeaponData>();
        ironSword.weaponName = "Epee de Fer";
        ironSword.description = "Une epee simple mais fiable pour les debutants.";
        ironSword.weaponType = WeaponType.Sword;
        ironSword.baseDamage = 15f;
        ironSword.attackSpeed = 1.2f;
        ironSword.range = 2f;
        ironSword.critChance = 0.05f;
        ironSword.critMultiplier = 1.5f;
        SaveAsset(ironSword, "Assets/ScriptableObjects/Weapons/Sword_Iron.asset");

        // Epee d'acier (intermediaire)
        var steelSword = ScriptableObject.CreateInstance<WeaponData>();
        steelSword.weaponName = "Epee d'Acier";
        steelSword.description = "Une epee forgee dans un acier de qualite.";
        steelSword.weaponType = WeaponType.Sword;
        steelSword.baseDamage = 25f;
        steelSword.attackSpeed = 1.1f;
        steelSword.range = 2.2f;
        steelSword.critChance = 0.08f;
        steelSword.critMultiplier = 1.6f;
        SaveAsset(steelSword, "Assets/ScriptableObjects/Weapons/Sword_Steel.asset");

        // Grande epee (lente mais puissante)
        var greatSword = ScriptableObject.CreateInstance<WeaponData>();
        greatSword.weaponName = "Grande Epee";
        greatSword.description = "Une epee massive qui inflige des degats devastateurs.";
        greatSword.weaponType = WeaponType.Greatsword;
        greatSword.baseDamage = 45f;
        greatSword.attackSpeed = 0.7f;
        greatSword.range = 2.5f;
        greatSword.critChance = 0.1f;
        greatSword.critMultiplier = 2f;
        greatSword.canBreakGuard = true;
        SaveAsset(greatSword, "Assets/ScriptableObjects/Weapons/Sword_Great.asset");

        // Arc de chasseur
        var hunterBow = ScriptableObject.CreateInstance<WeaponData>();
        hunterBow.weaponName = "Arc de Chasseur";
        hunterBow.description = "Un arc precis pour les attaques a distance.";
        hunterBow.weaponType = WeaponType.Bow;
        hunterBow.baseDamage = 20f;
        hunterBow.attackSpeed = 1f;
        hunterBow.range = 25f;
        hunterBow.isRanged = true;
        hunterBow.canCharge = true;
        hunterBow.critChance = 0.15f;
        hunterBow.critMultiplier = 2f;
        SaveAsset(hunterBow, "Assets/ScriptableObjects/Weapons/Bow_Hunter.asset");

        // Baton de mage
        var mageStaff = ScriptableObject.CreateInstance<WeaponData>();
        mageStaff.weaponName = "Baton d'Apprenti";
        mageStaff.description = "Un baton magique qui amplifie les sorts.";
        mageStaff.weaponType = WeaponType.Staff;
        mageStaff.baseDamage = 10f;
        mageStaff.attackSpeed = 0.8f;
        mageStaff.range = 15f;
        mageStaff.isRanged = true;
        mageStaff.damageType = DamageType.Fire;
        mageStaff.critChance = 0.1f;
        mageStaff.critMultiplier = 1.8f;
        SaveAsset(mageStaff, "Assets/ScriptableObjects/Weapons/Staff_Apprentice.asset");

        // Doubles lames
        var dualBlades = ScriptableObject.CreateInstance<WeaponData>();
        dualBlades.weaponName = "Doubles Lames";
        dualBlades.description = "Deux lames rapides pour les assassins.";
        dualBlades.weaponType = WeaponType.DualBlades;
        dualBlades.baseDamage = 12f;
        dualBlades.attackSpeed = 2f;
        dualBlades.range = 1.5f;
        dualBlades.critChance = 0.2f;
        dualBlades.critMultiplier = 2.5f;
        SaveAsset(dualBlades, "Assets/ScriptableObjects/Weapons/DualBlades_Shadow.asset");

        // Faux legendaire
        var legendScythe = ScriptableObject.CreateInstance<WeaponData>();
        legendScythe.weaponName = "Faux du Faucheur";
        legendScythe.description = "Une faux legendaire qui draine la vie des ennemis.";
        legendScythe.weaponType = WeaponType.Scythe;
        legendScythe.baseDamage = 60f;
        legendScythe.attackSpeed = 0.8f;
        legendScythe.range = 2.8f;
        legendScythe.critChance = 0.15f;
        legendScythe.critMultiplier = 2f;
        legendScythe.lifesteal = 0.15f;
        legendScythe.damageType = DamageType.Physical;
        SaveAsset(legendScythe, "Assets/ScriptableObjects/Weapons/Scythe_Reaper.asset");

        // Lance
        var spear = ScriptableObject.CreateInstance<WeaponData>();
        spear.weaponName = "Lance de Soldat";
        spear.description = "Une lance a longue portee pour garder les ennemis a distance.";
        spear.weaponType = WeaponType.Spear;
        spear.baseDamage = 30f;
        spear.attackSpeed = 0.9f;
        spear.range = 3.5f;
        spear.critChance = 0.08f;
        spear.critMultiplier = 1.7f;
        SaveAsset(spear, "Assets/ScriptableObjects/Weapons/Spear_Soldier.asset");

        Debug.Log("[TestDataCreator] 8 armes creees");
    }

    [MenuItem("EpicLegends/Data/Create Items")]
    public static void CreateItems()
    {
        // Potions
        CreateConsumable("Potion de Soin Mineure", "Restaure 50 PV.", ItemRarity.Common, 50, 25, "Potion_HealthSmall");
        CreateConsumable("Potion de Soin", "Restaure 150 PV.", ItemRarity.Uncommon, 150, 75, "Potion_HealthMedium");
        CreateConsumable("Potion de Soin Majeure", "Restaure 400 PV.", ItemRarity.Rare, 400, 200, "Potion_HealthLarge");

        CreateConsumable("Potion de Mana Mineure", "Restaure 30 PM.", ItemRarity.Common, 40, 20, "Potion_ManaSmall");
        CreateConsumable("Potion de Mana", "Restaure 80 PM.", ItemRarity.Uncommon, 120, 60, "Potion_ManaMedium");

        CreateConsumable("Elixir de Force", "Augmente l'attaque de 20% pendant 60s.", ItemRarity.Rare, 300, 150, "Elixir_Strength");
        CreateConsumable("Elixir de Protection", "Augmente la defense de 30% pendant 60s.", ItemRarity.Rare, 300, 150, "Elixir_Defense");

        // Materiaux
        CreateMaterial("Minerai de Fer", "Un minerai commun pour la forge.", ItemRarity.Common, 10, 5, 99, "Material_IronOre");
        CreateMaterial("Lingot d'Acier", "Un lingot d'acier raffine.", ItemRarity.Uncommon, 50, 25, 50, "Material_SteelIngot");
        CreateMaterial("Cristal Magique", "Un cristal charge de magie.", ItemRarity.Rare, 200, 100, 20, "Material_MagicCrystal");
        CreateMaterial("Ecaille de Dragon", "Une ecaille rare d'un dragon.", ItemRarity.Epic, 1000, 500, 10, "Material_DragonScale");
        CreateMaterial("Cuir de Loup", "Du cuir tanne de loup.", ItemRarity.Common, 15, 7, 50, "Material_WolfLeather");
        CreateMaterial("Os de Squelette", "Un os magique de mort-vivant.", ItemRarity.Uncommon, 30, 15, 30, "Material_SkeletonBone");

        // Objets de quete
        CreateQuestItem("Cle Ancienne", "Une vieille cle mysterieuse qui ouvre le donjon.", "Quest_AncientKey");
        CreateQuestItem("Amulette du Village", "Un artefact sacre du village.", "Quest_VillageAmulet");
        CreateQuestItem("Lettre du Chef", "Une lettre importante du chef du village.", "Quest_ChiefLetter");

        Debug.Log("[TestDataCreator] 16 items crees");
    }

    private static void CreateConsumable(string name, string desc, ItemRarity rarity, int buyPrice, int sellPrice, string fileName)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.displayName = name;
        item.description = desc;
        item.category = ItemCategory.Consumable;
        item.rarity = rarity;
        item.isStackable = true;
        item.maxStackSize = 20;
        item.isConsumable = true;
        item.buyPrice = buyPrice;
        item.sellPrice = sellPrice;
        SaveAsset(item, $"Assets/ScriptableObjects/Items/{fileName}.asset");
    }

    private static void CreateMaterial(string name, string desc, ItemRarity rarity, int buyPrice, int sellPrice, int stackSize, string fileName)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.displayName = name;
        item.description = desc;
        item.category = ItemCategory.Material;
        item.rarity = rarity;
        item.isStackable = true;
        item.maxStackSize = stackSize;
        item.isConsumable = false;
        item.buyPrice = buyPrice;
        item.sellPrice = sellPrice;
        SaveAsset(item, $"Assets/ScriptableObjects/Items/{fileName}.asset");
    }

    private static void CreateQuestItem(string name, string desc, string fileName)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.displayName = name;
        item.description = desc;
        item.category = ItemCategory.Quest;
        item.rarity = ItemRarity.Rare;
        item.isStackable = false;
        item.maxStackSize = 1;
        item.isConsumable = false;
        item.buyPrice = 0;
        item.sellPrice = 0;
        SaveAsset(item, $"Assets/ScriptableObjects/Items/{fileName}.asset");
    }

    [MenuItem("EpicLegends/Data/Create Enemies")]
    public static void CreateEnemies()
    {
        // Gobelin (facile)
        var goblin = ScriptableObject.CreateInstance<EnemyData>();
        goblin.enemyName = "Gobelin";
        goblin.description = "Une petite creature verte agressive.";
        goblin.enemyType = EnemyType.Basic;
        goblin.maxHealth = 50f;
        goblin.attackDamage = 8f;
        goblin.defense = 2f;
        goblin.moveSpeed = 4f;
        goblin.chaseSpeed = 5f;
        goblin.attackRange = 1.5f;
        goblin.detectionRange = 10f;
        goblin.attackCooldown = 1.5f;
        goblin.experienceReward = 20;
        goblin.goldMin = 3;
        goblin.goldMax = 8;
        SaveAsset(goblin, "Assets/ScriptableObjects/Enemies/Enemy_Goblin.asset");

        // Loup (rapide)
        var wolf = ScriptableObject.CreateInstance<EnemyData>();
        wolf.enemyName = "Loup Sauvage";
        wolf.description = "Un predateur rapide et feroce.";
        wolf.enemyType = EnemyType.Basic;
        wolf.maxHealth = 40f;
        wolf.attackDamage = 12f;
        wolf.defense = 1f;
        wolf.moveSpeed = 6f;
        wolf.chaseSpeed = 8f;
        wolf.attackRange = 1.5f;
        wolf.detectionRange = 15f;
        wolf.attackCooldown = 1f;
        wolf.experienceReward = 25;
        wolf.goldMin = 1;
        wolf.goldMax = 5;
        SaveAsset(wolf, "Assets/ScriptableObjects/Enemies/Enemy_Wolf.asset");

        // Squelette (resistant)
        var skeleton = ScriptableObject.CreateInstance<EnemyData>();
        skeleton.enemyName = "Squelette Guerrier";
        skeleton.description = "Un guerrier mort-vivant arme d'une epee rouilee.";
        skeleton.enemyType = EnemyType.Basic;
        skeleton.maxHealth = 80f;
        skeleton.attackDamage = 15f;
        skeleton.defense = 5f;
        skeleton.moveSpeed = 3f;
        skeleton.chaseSpeed = 4f;
        skeleton.attackRange = 2f;
        skeleton.detectionRange = 12f;
        skeleton.attackCooldown = 2f;
        skeleton.experienceReward = 35;
        skeleton.goldMin = 5;
        skeleton.goldMax = 15;
        SaveAsset(skeleton, "Assets/ScriptableObjects/Enemies/Enemy_Skeleton.asset");

        // Archer squelette
        var skeletonArcher = ScriptableObject.CreateInstance<EnemyData>();
        skeletonArcher.enemyName = "Squelette Archer";
        skeletonArcher.description = "Un squelette arme d'un arc ancien.";
        skeletonArcher.enemyType = EnemyType.Basic;
        skeletonArcher.maxHealth = 60f;
        skeletonArcher.attackDamage = 18f;
        skeletonArcher.defense = 2f;
        skeletonArcher.moveSpeed = 3f;
        skeletonArcher.chaseSpeed = 3.5f;
        skeletonArcher.attackRange = 15f;
        skeletonArcher.detectionRange = 18f;
        skeletonArcher.attackCooldown = 2.5f;
        skeletonArcher.experienceReward = 40;
        skeletonArcher.goldMin = 8;
        skeletonArcher.goldMax = 18;
        SaveAsset(skeletonArcher, "Assets/ScriptableObjects/Enemies/Enemy_SkeletonArcher.asset");

        // Chevalier noir (elite)
        var darkKnight = ScriptableObject.CreateInstance<EnemyData>();
        darkKnight.enemyName = "Chevalier Noir";
        darkKnight.description = "Un guerrier d'elite en armure sombre.";
        darkKnight.enemyType = EnemyType.Elite;
        darkKnight.maxHealth = 300f;
        darkKnight.attackDamage = 35f;
        darkKnight.defense = 15f;
        darkKnight.moveSpeed = 2.5f;
        darkKnight.chaseSpeed = 3.5f;
        darkKnight.attackRange = 3f;
        darkKnight.detectionRange = 15f;
        darkKnight.attackCooldown = 2.5f;
        darkKnight.experienceReward = 150;
        darkKnight.goldMin = 30;
        darkKnight.goldMax = 70;
        SaveAsset(darkKnight, "Assets/ScriptableObjects/Enemies/Enemy_DarkKnight.asset");

        // Mage sombre (elite)
        var darkMage = ScriptableObject.CreateInstance<EnemyData>();
        darkMage.enemyName = "Mage Sombre";
        darkMage.description = "Un sorcier maitrisant les arts obscurs.";
        darkMage.enemyType = EnemyType.Elite;
        darkMage.maxHealth = 200f;
        darkMage.attackDamage = 45f;
        darkMage.defense = 5f;
        darkMage.moveSpeed = 3f;
        darkMage.chaseSpeed = 4f;
        darkMage.attackRange = 20f;
        darkMage.detectionRange = 20f;
        darkMage.attackCooldown = 3f;
        darkMage.damageType = DamageType.Fire;
        darkMage.experienceReward = 180;
        darkMage.goldMin = 50;
        darkMage.goldMax = 100;
        SaveAsset(darkMage, "Assets/ScriptableObjects/Enemies/Enemy_DarkMage.asset");

        // Dragon (boss)
        var dragon = ScriptableObject.CreateInstance<EnemyData>();
        dragon.enemyName = "Dragon Ancien";
        dragon.description = "Une creature legendaire crachant le feu.";
        dragon.enemyType = EnemyType.Boss;
        dragon.maxHealth = 2000f;
        dragon.attackDamage = 80f;
        dragon.defense = 30f;
        dragon.moveSpeed = 4f;
        dragon.chaseSpeed = 6f;
        dragon.attackRange = 8f;
        dragon.detectionRange = 30f;
        dragon.attackCooldown = 3f;
        dragon.damageType = DamageType.Fire;
        dragon.fireResistance = 0.9f;
        dragon.experienceReward = 1000;
        dragon.goldMin = 300;
        dragon.goldMax = 600;
        SaveAsset(dragon, "Assets/ScriptableObjects/Enemies/Boss_Dragon.asset");

        // Seigneur des Morts (boss)
        var deathLord = ScriptableObject.CreateInstance<EnemyData>();
        deathLord.enemyName = "Seigneur des Morts";
        deathLord.description = "Le maitre du donjon, un necromancien puissant.";
        deathLord.enemyType = EnemyType.Boss;
        deathLord.maxHealth = 1500f;
        deathLord.attackDamage = 60f;
        deathLord.defense = 20f;
        deathLord.moveSpeed = 3f;
        deathLord.chaseSpeed = 4f;
        deathLord.attackRange = 15f;
        deathLord.detectionRange = 25f;
        deathLord.attackCooldown = 2f;
        deathLord.physicalResistance = 0.3f;
        deathLord.experienceReward = 800;
        deathLord.goldMin = 200;
        deathLord.goldMax = 400;
        SaveAsset(deathLord, "Assets/ScriptableObjects/Enemies/Boss_DeathLord.asset");

        Debug.Log("[TestDataCreator] 8 ennemis crees");
    }

    [MenuItem("EpicLegends/Data/Create Skills")]
    public static void CreateSkills()
    {
        // === COMPETENCES OFFENSIVES ===

        // Frappe puissante (melee)
        var powerStrike = ScriptableObject.CreateInstance<SkillData>();
        powerStrike.skillName = "Frappe Puissante";
        powerStrike.description = "Une attaque devastatrice qui inflige 150% des degats.";
        powerStrike.skillType = SkillType.Active;
        powerStrike.category = SkillCategory.Offense;
        powerStrike.targetType = SkillTargetType.SingleEnemy;
        powerStrike.baseDamage = 30f;
        powerStrike.damageScaling = 1.5f;
        powerStrike.manaCost = 15f;
        powerStrike.staminaCost = 20f;
        powerStrike.cooldown = 5f;
        powerStrike.range = 3f;
        powerStrike.damageType = DamageType.Physical;
        powerStrike.animationTrigger = "PowerStrike";
        SaveAsset(powerStrike, "Assets/ScriptableObjects/Skills/Skill_PowerStrike.asset");

        // Tourbillon (AOE melee)
        var whirlwind = ScriptableObject.CreateInstance<SkillData>();
        whirlwind.skillName = "Tourbillon";
        whirlwind.description = "Tournoie en frappant tous les ennemis autour.";
        whirlwind.skillType = SkillType.Active;
        whirlwind.category = SkillCategory.Offense;
        whirlwind.targetType = SkillTargetType.Area;
        whirlwind.baseDamage = 25f;
        whirlwind.damageScaling = 1f;
        whirlwind.manaCost = 25f;
        whirlwind.staminaCost = 30f;
        whirlwind.cooldown = 8f;
        whirlwind.range = 0f;
        whirlwind.areaRadius = 4f;
        whirlwind.damageType = DamageType.Physical;
        whirlwind.hitCount = 3;
        whirlwind.hitInterval = 0.3f;
        whirlwind.animationTrigger = "Whirlwind";
        SaveAsset(whirlwind, "Assets/ScriptableObjects/Skills/Skill_Whirlwind.asset");

        // Boule de feu (magie)
        var fireball = ScriptableObject.CreateInstance<SkillData>();
        fireball.skillName = "Boule de Feu";
        fireball.description = "Lance une boule de feu explosive.";
        fireball.skillType = SkillType.Active;
        fireball.category = SkillCategory.Offense;
        fireball.targetType = SkillTargetType.Area;
        fireball.baseDamage = 45f;
        fireball.damageScaling = 1.5f;
        fireball.manaCost = 30f;
        fireball.cooldown = 6f;
        fireball.castTime = 0.5f;
        fireball.range = 20f;
        fireball.areaRadius = 3f;
        fireball.damageType = DamageType.Fire;
        fireball.appliesElement = true;
        fireball.elementType = ElementType.Fire;
        fireball.elementGauge = 2f;
        fireball.animationTrigger = "CastFireball";
        SaveAsset(fireball, "Assets/ScriptableObjects/Skills/Skill_Fireball.asset");

        // Eclair (magie instantanee)
        var lightning = ScriptableObject.CreateInstance<SkillData>();
        lightning.skillName = "Eclair";
        lightning.description = "Invoque un eclair qui frappe instantanement.";
        lightning.skillType = SkillType.Active;
        lightning.category = SkillCategory.Offense;
        lightning.targetType = SkillTargetType.SingleEnemy;
        lightning.baseDamage = 60f;
        lightning.damageScaling = 1.2f;
        lightning.manaCost = 35f;
        lightning.cooldown = 8f;
        lightning.range = 25f;
        lightning.damageType = DamageType.Electric;
        lightning.appliesElement = true;
        lightning.elementType = ElementType.Electric;
        lightning.animationTrigger = "CastLightning";
        SaveAsset(lightning, "Assets/ScriptableObjects/Skills/Skill_Lightning.asset");

        // Fleche de glace (magie + ralentissement)
        var iceArrow = ScriptableObject.CreateInstance<SkillData>();
        iceArrow.skillName = "Fleche de Glace";
        iceArrow.description = "Tire une fleche de glace qui ralentit l'ennemi.";
        iceArrow.skillType = SkillType.Active;
        iceArrow.category = SkillCategory.Offense;
        iceArrow.targetType = SkillTargetType.SingleEnemy;
        iceArrow.baseDamage = 35f;
        iceArrow.damageScaling = 1f;
        iceArrow.manaCost = 20f;
        iceArrow.cooldown = 4f;
        iceArrow.range = 30f;
        iceArrow.damageType = DamageType.Ice;
        iceArrow.appliesElement = true;
        iceArrow.elementType = ElementType.Ice;
        iceArrow.appliesStatusEffect = true;
        iceArrow.statusDuration = 3f;
        iceArrow.animationTrigger = "CastIceArrow";
        SaveAsset(iceArrow, "Assets/ScriptableObjects/Skills/Skill_IceArrow.asset");

        // === COMPETENCES DE SOIN ===

        // Soin leger
        var healLight = ScriptableObject.CreateInstance<SkillData>();
        healLight.skillName = "Soin Leger";
        healLight.description = "Restaure une petite quantite de PV.";
        healLight.skillType = SkillType.Active;
        healLight.category = SkillCategory.Support;
        healLight.targetType = SkillTargetType.Self;
        healLight.isHeal = true;
        healLight.baseHeal = 50f;
        healLight.damageScaling = 0.5f;
        healLight.manaCost = 20f;
        healLight.cooldown = 10f;
        healLight.castTime = 1f;
        healLight.animationTrigger = "CastHeal";
        SaveAsset(healLight, "Assets/ScriptableObjects/Skills/Skill_HealLight.asset");

        // Soin de groupe
        var healGroup = ScriptableObject.CreateInstance<SkillData>();
        healGroup.skillName = "Soin de Groupe";
        healGroup.description = "Soigne tous les allies proches.";
        healGroup.skillType = SkillType.Active;
        healGroup.category = SkillCategory.Support;
        healGroup.targetType = SkillTargetType.AllAllies;
        healGroup.isHeal = true;
        healGroup.baseHeal = 30f;
        healGroup.damageScaling = 0.3f;
        healGroup.manaCost = 40f;
        healGroup.cooldown = 20f;
        healGroup.castTime = 1.5f;
        healGroup.areaRadius = 8f;
        healGroup.animationTrigger = "CastGroupHeal";
        SaveAsset(healGroup, "Assets/ScriptableObjects/Skills/Skill_HealGroup.asset");

        // === COMPETENCES DEFENSIVES ===

        // Bouclier magique
        var shield = ScriptableObject.CreateInstance<SkillData>();
        shield.skillName = "Bouclier Magique";
        shield.description = "Cree un bouclier absorbant les degats.";
        shield.skillType = SkillType.Active;
        shield.category = SkillCategory.Defense;
        shield.targetType = SkillTargetType.Self;
        shield.manaCost = 30f;
        shield.cooldown = 15f;
        shield.statusDuration = 8f;
        shield.appliesStatusEffect = true;
        shield.passiveDefenseBonus = 50f;
        shield.animationTrigger = "CastShield";
        SaveAsset(shield, "Assets/ScriptableObjects/Skills/Skill_Shield.asset");

        // Provocation (tank)
        var taunt = ScriptableObject.CreateInstance<SkillData>();
        taunt.skillName = "Provocation";
        taunt.description = "Force les ennemis proches a vous attaquer.";
        taunt.skillType = SkillType.Active;
        taunt.category = SkillCategory.Defense;
        taunt.targetType = SkillTargetType.Area;
        taunt.manaCost = 15f;
        taunt.staminaCost = 25f;
        taunt.cooldown = 12f;
        taunt.areaRadius = 8f;
        taunt.appliesStatusEffect = true;
        taunt.statusDuration = 5f;
        taunt.animationTrigger = "Taunt";
        SaveAsset(taunt, "Assets/ScriptableObjects/Skills/Skill_Taunt.asset");

        // === COMPETENCES DE MOBILITE ===

        // Dash
        var dash = ScriptableObject.CreateInstance<SkillData>();
        dash.skillName = "Dash";
        dash.description = "Se precipite rapidement dans une direction.";
        dash.skillType = SkillType.Active;
        dash.category = SkillCategory.Mobility;
        dash.targetType = SkillTargetType.Self;
        dash.staminaCost = 20f;
        dash.cooldown = 3f;
        dash.range = 8f;
        dash.animationTrigger = "Dash";
        SaveAsset(dash, "Assets/ScriptableObjects/Skills/Skill_Dash.asset");

        // Saut heroique
        var heroicLeap = ScriptableObject.CreateInstance<SkillData>();
        heroicLeap.skillName = "Saut Heroique";
        heroicLeap.description = "Saute vers une zone, infligeant des degats a l'atterrissage.";
        heroicLeap.skillType = SkillType.Active;
        heroicLeap.category = SkillCategory.Mobility;
        heroicLeap.targetType = SkillTargetType.Area;
        heroicLeap.baseDamage = 20f;
        heroicLeap.damageScaling = 0.8f;
        heroicLeap.staminaCost = 35f;
        heroicLeap.cooldown = 10f;
        heroicLeap.range = 15f;
        heroicLeap.areaRadius = 3f;
        heroicLeap.animationTrigger = "HeroicLeap";
        SaveAsset(heroicLeap, "Assets/ScriptableObjects/Skills/Skill_HeroicLeap.asset");

        // === COMPETENCES PASSIVES ===

        // Maitrise des armes
        var weaponMastery = ScriptableObject.CreateInstance<SkillData>();
        weaponMastery.skillName = "Maitrise des Armes";
        weaponMastery.description = "Augmente les degats d'attaque de base.";
        weaponMastery.skillType = SkillType.Passive;
        weaponMastery.category = SkillCategory.Offense;
        weaponMastery.passiveAttackBonus = 10f;
        weaponMastery.damagePerLevel = 2f;
        weaponMastery.maxLevel = 10;
        SaveAsset(weaponMastery, "Assets/ScriptableObjects/Skills/Skill_WeaponMastery.asset");

        // Endurance
        var endurance = ScriptableObject.CreateInstance<SkillData>();
        endurance.skillName = "Endurance";
        endurance.description = "Augmente la defense et reduit les degats recus.";
        endurance.skillType = SkillType.Passive;
        endurance.category = SkillCategory.Defense;
        endurance.passiveDefenseBonus = 15f;
        endurance.damagePerLevel = 3f;
        endurance.maxLevel = 10;
        SaveAsset(endurance, "Assets/ScriptableObjects/Skills/Skill_Endurance.asset");

        // Vitesse feline
        var catSpeed = ScriptableObject.CreateInstance<SkillData>();
        catSpeed.skillName = "Vitesse Feline";
        catSpeed.description = "Augmente la vitesse de deplacement.";
        catSpeed.skillType = SkillType.Passive;
        catSpeed.category = SkillCategory.Mobility;
        catSpeed.passiveSpeedBonus = 10f;
        catSpeed.damagePerLevel = 2f;
        catSpeed.maxLevel = 5;
        SaveAsset(catSpeed, "Assets/ScriptableObjects/Skills/Skill_CatSpeed.asset");

        // Oeil critique
        var criticalEye = ScriptableObject.CreateInstance<SkillData>();
        criticalEye.skillName = "Oeil Critique";
        criticalEye.description = "Augmente les chances de coup critique.";
        criticalEye.skillType = SkillType.Passive;
        criticalEye.category = SkillCategory.Offense;
        criticalEye.passiveCritBonus = 5f;
        criticalEye.damagePerLevel = 1f;
        criticalEye.maxLevel = 10;
        SaveAsset(criticalEye, "Assets/ScriptableObjects/Skills/Skill_CriticalEye.asset");

        // === ULTIMATE ===

        // Fureur du berserker
        var berserkerRage = ScriptableObject.CreateInstance<SkillData>();
        berserkerRage.skillName = "Fureur du Berserker";
        berserkerRage.description = "Entre en rage, doublant les degats mais reduisant la defense.";
        berserkerRage.skillType = SkillType.Ultimate;
        berserkerRage.category = SkillCategory.Offense;
        berserkerRage.targetType = SkillTargetType.Self;
        berserkerRage.manaCost = 50f;
        berserkerRage.cooldown = 60f;
        berserkerRage.statusDuration = 15f;
        berserkerRage.passiveAttackBonus = 100f;
        berserkerRage.passiveDefenseBonus = -30f;
        berserkerRage.animationTrigger = "BerserkerRage";
        SaveAsset(berserkerRage, "Assets/ScriptableObjects/Skills/Skill_BerserkerRage.asset");

        // Meteor
        var meteor = ScriptableObject.CreateInstance<SkillData>();
        meteor.skillName = "Meteor";
        meteor.description = "Invoque un meteor devastateur du ciel.";
        meteor.skillType = SkillType.Ultimate;
        meteor.category = SkillCategory.Offense;
        meteor.targetType = SkillTargetType.Area;
        meteor.baseDamage = 200f;
        meteor.damageScaling = 2f;
        meteor.manaCost = 80f;
        meteor.cooldown = 90f;
        meteor.castTime = 2f;
        meteor.range = 30f;
        meteor.areaRadius = 6f;
        meteor.damageType = DamageType.Fire;
        meteor.appliesElement = true;
        meteor.elementType = ElementType.Fire;
        meteor.elementGauge = 5f;
        meteor.animationTrigger = "CastMeteor";
        SaveAsset(meteor, "Assets/ScriptableObjects/Skills/Skill_Meteor.asset");

        Debug.Log("[TestDataCreator] 18 competences creees");
    }

    private static void SaveAsset(Object asset, string path)
    {
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        AssetDatabase.CreateAsset(asset, path);
    }
}
