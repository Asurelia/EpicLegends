using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editeur avance de stats de personnages avec courbes de scaling et preview.
/// Menu: EpicLegends > Tools > Character Stats Editor
/// </summary>
public class CharacterStatsEditor : EditorWindow
{
    #region Types

    [System.Serializable]
    public class CharacterTemplate
    {
        public string name = "New Character";
        public CharacterClass characterClass = CharacterClass.Warrior;
        public Rarity rarity = Rarity.Common;
        public Element element = Element.None;

        // Base stats (Level 1)
        public float baseHP = 1000f;
        public float baseATK = 100f;
        public float baseDEF = 50f;
        public float baseSpeed = 100f;
        public float baseCritRate = 5f;
        public float baseCritDMG = 50f;
        public float baseElementalMastery = 0f;
        public float baseEnergyRecharge = 100f;

        // Scaling curves
        public AnimationCurve hpCurve = AnimationCurve.Linear(1, 1, 90, 10);
        public AnimationCurve atkCurve = AnimationCurve.Linear(1, 1, 90, 8);
        public AnimationCurve defCurve = AnimationCurve.Linear(1, 1, 90, 6);

        // Ascension bonuses
        public List<AscensionBonus> ascensions = new List<AscensionBonus>();

        // Talents
        public List<TalentData> talents = new List<TalentData>();

        // Constellation/Eidolon
        public List<ConstellationData> constellations = new List<ConstellationData>();

        // Visual
        public Sprite portrait;
        public Color themeColor = Color.white;
        public bool isExpanded = true;
    }

    [System.Serializable]
    public class AscensionBonus
    {
        public int level = 20;
        public StatType bonusStat = StatType.HP_Percent;
        public float bonusValue = 10f;
        public string materialRequired = "";
        public int materialCount = 0;
    }

    [System.Serializable]
    public class TalentData
    {
        public string name = "Talent";
        public TalentType type = TalentType.Normal;
        public string description = "";
        public AnimationCurve damageCurve = AnimationCurve.Linear(1, 100, 10, 200);
        public float cooldown = 0f;
        public float energyCost = 0f;
    }

    [System.Serializable]
    public class ConstellationData
    {
        public string name = "Constellation";
        public string description = "";
        public StatType bonusStat = StatType.ATK_Percent;
        public float bonusValue = 10f;
    }

    public enum CharacterClass
    {
        Warrior,
        Mage,
        Archer,
        Assassin,
        Support,
        Tank
    }

    public enum Rarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5
    }

    public enum Element
    {
        None,
        Fire,
        Water,
        Wind,
        Earth,
        Lightning,
        Ice,
        Light,
        Dark
    }

    public enum StatType
    {
        HP_Flat,
        HP_Percent,
        ATK_Flat,
        ATK_Percent,
        DEF_Flat,
        DEF_Percent,
        Speed,
        CritRate,
        CritDMG,
        ElementalMastery,
        EnergyRecharge,
        ElementalDMG
    }

    public enum TalentType
    {
        Normal,
        Skill,
        Burst,
        Passive
    }

    #endregion

    #region State

    private List<CharacterTemplate> _characters = new List<CharacterTemplate>();
    private int _selectedCharIndex = -1;
    private Vector2 _scrollPos;
    private Vector2 _charListScroll;

    // Preview
    private int _previewLevel = 1;
    private int _previewAscension = 0;
    private int _previewConstellation = 0;
    private int[] _previewTalentLevels = new int[3];

    // Tabs
    private int _selectedTab = 0;
    private readonly string[] TABS = { "Base Stats", "Scaling", "Talents", "Constellations", "Preview", "Compare" };

    // Compare mode
    private List<int> _compareIndices = new List<int>();

    #endregion

    [MenuItem("EpicLegends/Tools/Character Stats Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<CharacterStatsEditor>("Character Stats");
        window.minSize = new Vector2(600, 700);
    }

    private void OnEnable()
    {
        CreateDefaultCharacters();
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawCharacterList();

        if (_selectedCharIndex >= 0 && _selectedCharIndex < _characters.Count)
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, TABS);
            EditorGUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0: DrawBaseStatsTab(); break;
                case 1: DrawScalingTab(); break;
                case 2: DrawTalentsTab(); break;
                case 3: DrawConstellationsTab(); break;
                case 4: DrawPreviewTab(); break;
                case 5: DrawCompareTab(); break;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Character Stats Editor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Design and balance character stats with level scaling curves, talent systems, " +
            "and constellation bonuses. Preview stats at any level instantly.",
            MessageType.Info
        );
        EditorGUILayout.Space(5);
    }

    private void DrawCharacterList()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);

        if (GUILayout.Button("+ New", GUILayout.Width(60)))
        {
            CreateNewCharacter();
        }

        if (GUILayout.Button("Import", GUILayout.Width(60)))
        {
            ImportFromScriptableObject();
        }

        EditorGUILayout.EndHorizontal();

        _charListScroll = EditorGUILayout.BeginScrollView(_charListScroll, GUILayout.Height(100));

        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < _characters.Count; i++)
        {
            DrawCharacterCard(i);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);
    }

    private void DrawCharacterCard(int index)
    {
        var character = _characters[index];
        bool isSelected = index == _selectedCharIndex;

        GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox);
        if (isSelected)
        {
            cardStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 0.5f, 0.8f, 0.5f));
        }

        EditorGUILayout.BeginVertical(cardStyle, GUILayout.Width(100), GUILayout.Height(80));

        // Rarity color bar
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(90, 4), GetRarityColor(character.rarity));

        // Portrait placeholder
        if (character.portrait != null)
        {
            GUILayout.Label(character.portrait.texture, GUILayout.Width(40), GUILayout.Height(40));
        }
        else
        {
            string classIcon = GetClassIcon(character.characterClass);
            GUILayout.Label(classIcon, new GUIStyle { fontSize = 24, alignment = TextAnchor.MiddleCenter }, GUILayout.Height(40));
        }

        // Name
        if (GUILayout.Button(character.name, EditorStyles.miniLabel))
        {
            _selectedCharIndex = index;
        }

        // Element
        GUILayout.Label(GetElementIcon(character.element), new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleCenter });

        EditorGUILayout.EndVertical();
    }

    private void DrawBaseStatsTab()
    {
        var character = _characters[_selectedCharIndex];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Basic info
        EditorGUILayout.LabelField("Basic Info", EditorStyles.miniBoldLabel);
        character.name = EditorGUILayout.TextField("Name", character.name);
        character.characterClass = (CharacterClass)EditorGUILayout.EnumPopup("Class", character.characterClass);
        character.rarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", character.rarity);
        character.element = (Element)EditorGUILayout.EnumPopup("Element", character.element);
        character.portrait = (Sprite)EditorGUILayout.ObjectField("Portrait", character.portrait, typeof(Sprite), false);
        character.themeColor = EditorGUILayout.ColorField("Theme Color", character.themeColor);

        EditorGUILayout.Space(10);

        // Base stats
        EditorGUILayout.LabelField("Base Stats (Level 1)", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();

        character.baseHP = EditorGUILayout.FloatField("HP", character.baseHP);
        character.baseATK = EditorGUILayout.FloatField("ATK", character.baseATK);
        character.baseDEF = EditorGUILayout.FloatField("DEF", character.baseDEF);
        character.baseSpeed = EditorGUILayout.FloatField("Speed", character.baseSpeed);

        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();

        character.baseCritRate = EditorGUILayout.FloatField("Crit Rate %", character.baseCritRate);
        character.baseCritDMG = EditorGUILayout.FloatField("Crit DMG %", character.baseCritDMG);
        character.baseElementalMastery = EditorGUILayout.FloatField("Elemental Mastery", character.baseElementalMastery);
        character.baseEnergyRecharge = EditorGUILayout.FloatField("Energy Recharge %", character.baseEnergyRecharge);

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Quick presets
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Balanced"))
        {
            ApplyBalancedPreset(character);
        }
        if (GUILayout.Button("DPS"))
        {
            ApplyDPSPreset(character);
        }
        if (GUILayout.Button("Tank"))
        {
            ApplyTankPreset(character);
        }
        if (GUILayout.Button("Support"))
        {
            ApplySupportPreset(character);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawScalingTab()
    {
        var character = _characters[_selectedCharIndex];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Stat Scaling Curves", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "Define how stats scale from Level 1 to 90. The Y value is a multiplier applied to base stats.",
            MessageType.Info
        );

        EditorGUILayout.Space(5);

        // HP Curve
        EditorGUILayout.LabelField("HP Scaling", EditorStyles.miniBoldLabel);
        character.hpCurve = EditorGUILayout.CurveField("HP Curve", character.hpCurve, Color.green, new Rect(1, 0, 89, 15));
        DrawCurvePreview(character.hpCurve, character.baseHP, "HP");

        EditorGUILayout.Space(5);

        // ATK Curve
        EditorGUILayout.LabelField("ATK Scaling", EditorStyles.miniBoldLabel);
        character.atkCurve = EditorGUILayout.CurveField("ATK Curve", character.atkCurve, Color.red, new Rect(1, 0, 89, 12));
        DrawCurvePreview(character.atkCurve, character.baseATK, "ATK");

        EditorGUILayout.Space(5);

        // DEF Curve
        EditorGUILayout.LabelField("DEF Scaling", EditorStyles.miniBoldLabel);
        character.defCurve = EditorGUILayout.CurveField("DEF Curve", character.defCurve, Color.blue, new Rect(1, 0, 89, 10));
        DrawCurvePreview(character.defCurve, character.baseDEF, "DEF");

        EditorGUILayout.Space(10);

        // Ascension bonuses
        EditorGUILayout.LabelField("Ascension Bonuses", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("+ Add Ascension"))
        {
            character.ascensions.Add(new AscensionBonus { level = 20 + character.ascensions.Count * 10 });
        }

        for (int i = 0; i < character.ascensions.Count; i++)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            character.ascensions[i].level = EditorGUILayout.IntField("Level", character.ascensions[i].level, GUILayout.Width(100));
            character.ascensions[i].bonusStat = (StatType)EditorGUILayout.EnumPopup(character.ascensions[i].bonusStat, GUILayout.Width(120));
            character.ascensions[i].bonusValue = EditorGUILayout.FloatField(character.ascensions[i].bonusValue, GUILayout.Width(60));

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                character.ascensions.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCurvePreview(AnimationCurve curve, float baseValue, string label)
    {
        EditorGUILayout.BeginHorizontal();

        int[] levels = { 1, 20, 40, 60, 80, 90 };
        foreach (int level in levels)
        {
            float value = baseValue * curve.Evaluate(level);
            EditorGUILayout.LabelField($"L{level}: {value:F0}", EditorStyles.miniLabel, GUILayout.Width(70));
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTalentsTab()
    {
        var character = _characters[_selectedCharIndex];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Talents / Skills", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("+ Add Talent"))
        {
            character.talents.Add(new TalentData { name = $"Talent {character.talents.Count + 1}" });
        }

        for (int i = 0; i < character.talents.Count; i++)
        {
            var talent = character.talents[i];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            talent.name = EditorGUILayout.TextField(talent.name);
            talent.type = (TalentType)EditorGUILayout.EnumPopup(talent.type, GUILayout.Width(80));

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                character.talents.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            talent.description = EditorGUILayout.TextArea(talent.description, GUILayout.Height(40));

            EditorGUILayout.BeginHorizontal();

            if (talent.type != TalentType.Passive)
            {
                talent.damageCurve = EditorGUILayout.CurveField("Damage %", talent.damageCurve, Color.yellow, new Rect(1, 50, 9, 300), GUILayout.Width(200));

                if (talent.type == TalentType.Skill)
                {
                    talent.cooldown = EditorGUILayout.FloatField("CD (s)", talent.cooldown, GUILayout.Width(80));
                }
                else if (talent.type == TalentType.Burst)
                {
                    talent.energyCost = EditorGUILayout.FloatField("Energy", talent.energyCost, GUILayout.Width(80));
                }
            }

            EditorGUILayout.EndHorizontal();

            // Preview damage at different levels
            if (talent.type != TalentType.Passive)
            {
                EditorGUILayout.BeginHorizontal();
                for (int lvl = 1; lvl <= 10; lvl += 3)
                {
                    float dmg = talent.damageCurve.Evaluate(lvl);
                    EditorGUILayout.LabelField($"Lv{lvl}: {dmg:F0}%", EditorStyles.miniLabel, GUILayout.Width(70));
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawConstellationsTab()
    {
        var character = _characters[_selectedCharIndex];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Constellations / Eidolons", EditorStyles.miniBoldLabel);

        // Ensure 6 constellations
        while (character.constellations.Count < 6)
        {
            character.constellations.Add(new ConstellationData { name = $"C{character.constellations.Count + 1}" });
        }

        for (int i = 0; i < 6; i++)
        {
            var const_ = character.constellations[i];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header with star icon
            EditorGUILayout.BeginHorizontal();
            string stars = new string('★', i + 1);
            EditorGUILayout.LabelField($"C{i + 1} {stars}", EditorStyles.boldLabel, GUILayout.Width(100));
            const_.name = EditorGUILayout.TextField(const_.name);
            EditorGUILayout.EndHorizontal();

            const_.description = EditorGUILayout.TextArea(const_.description, GUILayout.Height(30));

            EditorGUILayout.BeginHorizontal();
            const_.bonusStat = (StatType)EditorGUILayout.EnumPopup("Bonus", const_.bonusStat, GUILayout.Width(200));
            const_.bonusValue = EditorGUILayout.FloatField(const_.bonusValue, GUILayout.Width(60));
            EditorGUILayout.LabelField(GetStatSuffix(const_.bonusStat), GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewTab()
    {
        var character = _characters[_selectedCharIndex];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Stats Preview", EditorStyles.miniBoldLabel);

        // Level slider
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Level", GUILayout.Width(50));
        _previewLevel = EditorGUILayout.IntSlider(_previewLevel, 1, 90);
        EditorGUILayout.EndHorizontal();

        // Ascension
        int maxAscension = character.ascensions.Count(a => a.level <= _previewLevel);
        _previewAscension = EditorGUILayout.IntSlider("Ascension", _previewAscension, 0, maxAscension);

        // Constellation
        _previewConstellation = EditorGUILayout.IntSlider("Constellation", _previewConstellation, 0, 6);

        // Talent levels
        EditorGUILayout.LabelField("Talent Levels", EditorStyles.miniBoldLabel);
        for (int i = 0; i < Mathf.Min(3, character.talents.Count); i++)
        {
            _previewTalentLevels[i] = EditorGUILayout.IntSlider(character.talents[i].name, _previewTalentLevels[i], 1, 10);
        }

        EditorGUILayout.Space(10);

        // Calculate and display stats
        var stats = CalculateStats(character, _previewLevel, _previewAscension, _previewConstellation);

        EditorGUILayout.LabelField($"Stats at Level {_previewLevel}", EditorStyles.boldLabel);

        DrawStatBar("HP", stats.hp, 50000f, Color.green);
        DrawStatBar("ATK", stats.atk, 5000f, Color.red);
        DrawStatBar("DEF", stats.def, 3000f, Color.blue);
        DrawStatBar("Speed", stats.speed, 200f, Color.cyan);

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Crit Rate: {stats.critRate:F1}%", GUILayout.Width(120));
        EditorGUILayout.LabelField($"Crit DMG: {stats.critDMG:F1}%", GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"EM: {stats.em:F0}", GUILayout.Width(120));
        EditorGUILayout.LabelField($"ER: {stats.er:F1}%", GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // DPS Estimate
        float avgDamage = stats.atk * (1f + stats.critRate / 100f * stats.critDMG / 100f);
        EditorGUILayout.LabelField($"Estimated Avg ATK (with crit): {avgDamage:F0}", EditorStyles.boldLabel);

        EditorGUILayout.EndVertical();
    }

    private void DrawStatBar(string label, float value, float max, Color color)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(label, GUILayout.Width(50));

        Rect barRect = GUILayoutUtility.GetRect(200, 20);
        EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

        float fillWidth = Mathf.Clamp01(value / max) * barRect.width;
        EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), color);

        GUI.Label(barRect, $" {value:F0}", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCompareTab()
    {
        EditorGUILayout.LabelField("Compare Characters", EditorStyles.boldLabel);

        // Character selection for comparison
        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < _characters.Count; i++)
        {
            bool isCompared = _compareIndices.Contains(i);
            GUI.backgroundColor = isCompared ? Color.green : Color.white;

            if (GUILayout.Toggle(isCompared, _characters[i].name, "Button", GUILayout.Width(80)))
            {
                if (!isCompared && _compareIndices.Count < 4)
                {
                    _compareIndices.Add(i);
                }
            }
            else if (isCompared)
            {
                _compareIndices.Remove(i);
            }

            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Comparison level
        _previewLevel = EditorGUILayout.IntSlider("Compare at Level", _previewLevel, 1, 90);

        EditorGUILayout.Space(10);

        if (_compareIndices.Count > 0)
        {
            // Draw comparison table
            EditorGUILayout.BeginHorizontal();

            // Stat labels column
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            EditorGUILayout.LabelField("", GUILayout.Height(20));
            EditorGUILayout.LabelField("HP", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ATK", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("DEF", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Speed", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Crit Rate", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Crit DMG", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            // Character columns
            foreach (int index in _compareIndices)
            {
                var character = _characters[index];
                var stats = CalculateStats(character, _previewLevel, 0, 0);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(100));

                // Character name with color
                GUI.backgroundColor = character.themeColor;
                EditorGUILayout.LabelField(character.name, EditorStyles.boldLabel, GUILayout.Height(20));
                GUI.backgroundColor = Color.white;

                EditorGUILayout.LabelField($"{stats.hp:F0}");
                EditorGUILayout.LabelField($"{stats.atk:F0}");
                EditorGUILayout.LabelField($"{stats.def:F0}");
                EditorGUILayout.LabelField($"{stats.speed:F0}");
                EditorGUILayout.LabelField($"{stats.critRate:F1}%");
                EditorGUILayout.LabelField($"{stats.critDMG:F1}%");

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    #endregion

    #region Logic

    private struct CalculatedStats
    {
        public float hp, atk, def, speed;
        public float critRate, critDMG, em, er;
    }

    private CalculatedStats CalculateStats(CharacterTemplate character, int level, int ascension, int constellation)
    {
        var stats = new CalculatedStats();

        // Base stats with curve scaling
        stats.hp = character.baseHP * character.hpCurve.Evaluate(level);
        stats.atk = character.baseATK * character.atkCurve.Evaluate(level);
        stats.def = character.baseDEF * character.defCurve.Evaluate(level);
        stats.speed = character.baseSpeed;
        stats.critRate = character.baseCritRate;
        stats.critDMG = character.baseCritDMG;
        stats.em = character.baseElementalMastery;
        stats.er = character.baseEnergyRecharge;

        // Apply ascension bonuses
        for (int i = 0; i < Mathf.Min(ascension, character.ascensions.Count); i++)
        {
            var bonus = character.ascensions[i];
            ApplyStatBonus(ref stats, bonus.bonusStat, bonus.bonusValue);
        }

        // Apply constellation bonuses
        for (int i = 0; i < Mathf.Min(constellation, character.constellations.Count); i++)
        {
            var const_ = character.constellations[i];
            ApplyStatBonus(ref stats, const_.bonusStat, const_.bonusValue);
        }

        return stats;
    }

    private void ApplyStatBonus(ref CalculatedStats stats, StatType type, float value)
    {
        switch (type)
        {
            case StatType.HP_Flat: stats.hp += value; break;
            case StatType.HP_Percent: stats.hp *= (1 + value / 100f); break;
            case StatType.ATK_Flat: stats.atk += value; break;
            case StatType.ATK_Percent: stats.atk *= (1 + value / 100f); break;
            case StatType.DEF_Flat: stats.def += value; break;
            case StatType.DEF_Percent: stats.def *= (1 + value / 100f); break;
            case StatType.Speed: stats.speed += value; break;
            case StatType.CritRate: stats.critRate += value; break;
            case StatType.CritDMG: stats.critDMG += value; break;
            case StatType.ElementalMastery: stats.em += value; break;
            case StatType.EnergyRecharge: stats.er += value; break;
        }
    }

    private void CreateDefaultCharacters()
    {
        _characters.Clear();

        // Warrior example
        var warrior = new CharacterTemplate
        {
            name = "Iron Knight",
            characterClass = CharacterClass.Warrior,
            rarity = Rarity.Epic,
            element = Element.Fire,
            baseHP = 1200f,
            baseATK = 120f,
            baseDEF = 80f,
            themeColor = new Color(0.8f, 0.3f, 0.2f)
        };
        warrior.talents.Add(new TalentData { name = "Slash", type = TalentType.Normal });
        warrior.talents.Add(new TalentData { name = "Flame Strike", type = TalentType.Skill, cooldown = 8f });
        warrior.talents.Add(new TalentData { name = "Inferno Burst", type = TalentType.Burst, energyCost = 60f });
        _characters.Add(warrior);

        // Mage example
        var mage = new CharacterTemplate
        {
            name = "Storm Mage",
            characterClass = CharacterClass.Mage,
            rarity = Rarity.Legendary,
            element = Element.Lightning,
            baseHP = 800f,
            baseATK = 150f,
            baseDEF = 40f,
            baseCritRate = 10f,
            baseCritDMG = 60f,
            themeColor = new Color(0.5f, 0.3f, 0.8f)
        };
        mage.talents.Add(new TalentData { name = "Thunder Bolt", type = TalentType.Normal });
        mage.talents.Add(new TalentData { name = "Chain Lightning", type = TalentType.Skill, cooldown = 12f });
        mage.talents.Add(new TalentData { name = "Storm Call", type = TalentType.Burst, energyCost = 80f });
        _characters.Add(mage);

        // Support example
        var support = new CharacterTemplate
        {
            name = "Light Healer",
            characterClass = CharacterClass.Support,
            rarity = Rarity.Epic,
            element = Element.Light,
            baseHP = 1400f,
            baseATK = 80f,
            baseDEF = 60f,
            baseEnergyRecharge = 120f,
            themeColor = new Color(0.9f, 0.9f, 0.5f)
        };
        support.talents.Add(new TalentData { name = "Holy Light", type = TalentType.Normal });
        support.talents.Add(new TalentData { name = "Healing Aura", type = TalentType.Skill, cooldown = 15f });
        support.talents.Add(new TalentData { name = "Divine Blessing", type = TalentType.Burst, energyCost = 70f });
        _characters.Add(support);

        _selectedCharIndex = 0;
    }

    private void CreateNewCharacter()
    {
        _characters.Add(new CharacterTemplate
        {
            name = $"Character {_characters.Count + 1}",
            themeColor = Random.ColorHSV(0f, 1f, 0.5f, 0.8f, 0.6f, 0.9f)
        });
        _selectedCharIndex = _characters.Count - 1;
    }

    private void ImportFromScriptableObject()
    {
        // Placeholder for importing from existing PlayerStats or similar
        Debug.Log("[CharacterStatsEditor] Import from ScriptableObject");
    }

    private void ApplyBalancedPreset(CharacterTemplate character)
    {
        character.baseHP = 1000f;
        character.baseATK = 100f;
        character.baseDEF = 50f;
        character.baseSpeed = 100f;
        character.baseCritRate = 5f;
        character.baseCritDMG = 50f;
    }

    private void ApplyDPSPreset(CharacterTemplate character)
    {
        character.baseHP = 800f;
        character.baseATK = 140f;
        character.baseDEF = 40f;
        character.baseSpeed = 110f;
        character.baseCritRate = 10f;
        character.baseCritDMG = 70f;
    }

    private void ApplyTankPreset(CharacterTemplate character)
    {
        character.baseHP = 1500f;
        character.baseATK = 80f;
        character.baseDEF = 100f;
        character.baseSpeed = 90f;
        character.baseCritRate = 3f;
        character.baseCritDMG = 40f;
    }

    private void ApplySupportPreset(CharacterTemplate character)
    {
        character.baseHP = 1200f;
        character.baseATK = 90f;
        character.baseDEF = 60f;
        character.baseSpeed = 100f;
        character.baseCritRate = 5f;
        character.baseCritDMG = 50f;
        character.baseEnergyRecharge = 130f;
    }

    #endregion

    #region Helpers

    private Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common: return Color.gray;
            case Rarity.Uncommon: return Color.green;
            case Rarity.Rare: return Color.blue;
            case Rarity.Epic: return new Color(0.6f, 0.2f, 0.8f);
            case Rarity.Legendary: return new Color(1f, 0.8f, 0.2f);
            default: return Color.white;
        }
    }

    private string GetClassIcon(CharacterClass charClass)
    {
        switch (charClass)
        {
            case CharacterClass.Warrior: return "⚔";
            case CharacterClass.Mage: return "🔮";
            case CharacterClass.Archer: return "🏹";
            case CharacterClass.Assassin: return "🗡";
            case CharacterClass.Support: return "💚";
            case CharacterClass.Tank: return "🛡";
            default: return "?";
        }
    }

    private string GetElementIcon(Element element)
    {
        switch (element)
        {
            case Element.Fire: return "🔥";
            case Element.Water: return "💧";
            case Element.Wind: return "🌪";
            case Element.Earth: return "🌍";
            case Element.Lightning: return "⚡";
            case Element.Ice: return "❄";
            case Element.Light: return "☀";
            case Element.Dark: return "🌙";
            default: return "";
        }
    }

    private string GetStatSuffix(StatType type)
    {
        switch (type)
        {
            case StatType.HP_Percent:
            case StatType.ATK_Percent:
            case StatType.DEF_Percent:
            case StatType.CritRate:
            case StatType.CritDMG:
            case StatType.EnergyRecharge:
            case StatType.ElementalDMG:
                return "%";
            default:
                return "";
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    #endregion
}
