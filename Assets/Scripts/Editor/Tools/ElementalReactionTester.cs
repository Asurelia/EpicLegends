using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Testeur de reactions elementaires en live avec preview des degats.
/// Menu: EpicLegends > Tools > Elemental Reaction Tester
/// </summary>
public class ElementalReactionTester : EditorWindow
{
    #region Types

    public enum ElementType
    {
        None,
        Pyro,
        Hydro,
        Electro,
        Cryo,
        Anemo,
        Geo,
        Dendro
    }

    [System.Serializable]
    public class ReactionData
    {
        public string name;
        public ElementType trigger;
        public ElementType aura;
        public ReactionType type;
        public float multiplier = 1f;
        public float emScaling = 1f;
        public bool isTransformative;
        public Color color = Color.white;
        public string description;
    }

    public enum ReactionType
    {
        Amplifying,     // Vaporize, Melt
        Transformative, // Overloaded, Superconduct, etc.
        Catalyze,       // Quicken, Aggravate, Spread
        Crystallize,
        Swirl
    }

    #endregion

    #region State

    // Test parameters
    private ElementType _auraElement = ElementType.Pyro;
    private ElementType _triggerElement = ElementType.Hydro;
    private float _baseDamage = 1000f;
    private float _attackerATK = 2000f;
    private float _attackerLevel = 90;
    private float _targetLevel = 90;
    private float _elementalMastery = 200f;
    private float _critRate = 50f;
    private float _critDMG = 150f;
    private float _elementalDMGBonus = 50f;

    // Target stats
    private float _targetDEF = 500f;
    private float _targetResistance = 10f;
    private bool _targetHasShield = false;

    // Results
    private ReactionData _currentReaction;
    private float _calculatedDamage = 0f;
    private float _calculatedReactionDamage = 0f;
    private float _totalDamage = 0f;

    // UI
    private Vector2 _scrollPos;
    private bool _showAdvanced = false;
    private bool _autoUpdate = true;

    // Reaction database
    private Dictionary<(ElementType, ElementType), ReactionData> _reactions;

    // History
    private List<DamageHistoryEntry> _damageHistory = new List<DamageHistoryEntry>();

    private struct DamageHistoryEntry
    {
        public string reaction;
        public float damage;
        public float em;
    }

    #endregion

    [MenuItem("EpicLegends/Tools/Elemental Reaction Tester")]
    public static void ShowWindow()
    {
        var window = GetWindow<ElementalReactionTester>("Reaction Tester");
        window.minSize = new Vector2(500, 700);
    }

    private void OnEnable()
    {
        InitializeReactions();
        CalculateDamage();
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawElementSelector();
        DrawReactionDisplay();
        DrawAttackerStats();
        DrawTargetStats();
        DrawCalculationResults();
        DrawDamageFormula();
        DrawHistory();
        DrawActions();

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Elemental Reaction Tester", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Test elemental reactions in real-time. See how Elemental Mastery, " +
            "damage bonuses, and resistances affect your damage output.",
            MessageType.Info
        );

        _autoUpdate = EditorGUILayout.Toggle("Auto Calculate", _autoUpdate);

        EditorGUILayout.Space(10);
    }

    private void DrawElementSelector()
    {
        EditorGUILayout.LabelField("Element Selection", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Aura element
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Aura (on target)", GUILayout.Width(120));

        foreach (ElementType elem in System.Enum.GetValues(typeof(ElementType)))
        {
            if (elem == ElementType.None) continue;

            Color elemColor = GetElementColor(elem);
            GUI.backgroundColor = _auraElement == elem ? elemColor : Color.white;

            if (GUILayout.Button(GetElementIcon(elem), GUILayout.Width(35), GUILayout.Height(30)))
            {
                _auraElement = elem;
                if (_autoUpdate) CalculateDamage();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Trigger element
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Trigger (attack)", GUILayout.Width(120));

        foreach (ElementType elem in System.Enum.GetValues(typeof(ElementType)))
        {
            if (elem == ElementType.None) continue;

            Color elemColor = GetElementColor(elem);
            GUI.backgroundColor = _triggerElement == elem ? elemColor : Color.white;

            if (GUILayout.Button(GetElementIcon(elem), GUILayout.Width(35), GUILayout.Height(30)))
            {
                _triggerElement = elem;
                if (_autoUpdate) CalculateDamage();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawReactionDisplay()
    {
        EditorGUILayout.LabelField("Reaction Result", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_currentReaction != null)
        {
            // Reaction name with color
            GUI.backgroundColor = _currentReaction.color;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField(
                $"{GetElementIcon(_auraElement)} + {GetElementIcon(_triggerElement)} = {_currentReaction.name}",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter },
                GUILayout.Height(30)
            );

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;

            // Reaction details
            EditorGUILayout.LabelField($"Type: {_currentReaction.type}");
            EditorGUILayout.LabelField($"Multiplier: {_currentReaction.multiplier:F2}x");
            EditorGUILayout.LabelField($"EM Scaling: {_currentReaction.emScaling:F2}");

            if (!string.IsNullOrEmpty(_currentReaction.description))
            {
                EditorGUILayout.HelpBox(_currentReaction.description, MessageType.None);
            }
        }
        else
        {
            EditorGUILayout.LabelField("No reaction between these elements", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawAttackerStats()
    {
        EditorGUILayout.LabelField("Attacker Stats", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUI.BeginChangeCheck();

        _baseDamage = EditorGUILayout.FloatField("Base Damage (Skill %)", _baseDamage);
        _attackerATK = EditorGUILayout.FloatField("Total ATK", _attackerATK);
        _attackerLevel = EditorGUILayout.Slider("Character Level", _attackerLevel, 1, 90);

        EditorGUILayout.Space(5);

        _elementalMastery = EditorGUILayout.Slider("Elemental Mastery", _elementalMastery, 0, 1000);

        // Show EM bonus
        float emBonus = CalculateEMBonus(_elementalMastery, _currentReaction);
        EditorGUILayout.LabelField($"  → EM Bonus: +{emBonus:F1}%", EditorStyles.miniLabel);

        EditorGUILayout.Space(5);

        _critRate = EditorGUILayout.Slider("Crit Rate %", _critRate, 0, 100);
        _critDMG = EditorGUILayout.Slider("Crit DMG %", _critDMG, 50, 300);
        _elementalDMGBonus = EditorGUILayout.Slider("Elemental DMG Bonus %", _elementalDMGBonus, 0, 200);

        if (EditorGUI.EndChangeCheck() && _autoUpdate)
        {
            CalculateDamage();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawTargetStats()
    {
        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Target Stats (Advanced)", true);

        if (_showAdvanced)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();

            _targetLevel = EditorGUILayout.Slider("Enemy Level", _targetLevel, 1, 100);
            _targetDEF = EditorGUILayout.FloatField("Enemy DEF", _targetDEF);
            _targetResistance = EditorGUILayout.Slider("Elemental RES %", _targetResistance, -100, 100);
            _targetHasShield = EditorGUILayout.Toggle("Has Elemental Shield", _targetHasShield);

            if (EditorGUI.EndChangeCheck() && _autoUpdate)
            {
                CalculateDamage();
            }

            // Show DEF multiplier
            float defMult = CalculateDEFMultiplier();
            EditorGUILayout.LabelField($"  → DEF Multiplier: {defMult:F2}x", EditorStyles.miniLabel);

            // Show RES multiplier
            float resMult = CalculateRESMultiplier();
            EditorGUILayout.LabelField($"  → RES Multiplier: {resMult:F2}x", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);
    }

    private void DrawCalculationResults()
    {
        EditorGUILayout.LabelField("Damage Calculation", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Base damage
        float rawDamage = _baseDamage / 100f * _attackerATK;
        EditorGUILayout.LabelField($"Raw Damage: {rawDamage:F0}");

        // With DMG bonus
        float withBonus = rawDamage * (1 + _elementalDMGBonus / 100f);
        EditorGUILayout.LabelField($"With DMG Bonus: {withBonus:F0}");

        // After DEF
        float defMult = CalculateDEFMultiplier();
        float afterDEF = withBonus * defMult;
        EditorGUILayout.LabelField($"After DEF: {afterDEF:F0}");

        // After RES
        float resMult = CalculateRESMultiplier();
        float afterRES = afterDEF * resMult;
        EditorGUILayout.LabelField($"After RES: {afterRES:F0}");

        EditorGUILayout.Space(5);

        // Reaction damage
        if (_currentReaction != null)
        {
            if (_currentReaction.type == ReactionType.Amplifying)
            {
                float emBonus = CalculateEMBonus(_elementalMastery, _currentReaction);
                float amplifiedDamage = afterRES * _currentReaction.multiplier * (1 + emBonus / 100f);

                EditorGUILayout.LabelField($"Reaction Multiplier: {_currentReaction.multiplier:F2}x");
                EditorGUILayout.LabelField($"EM Bonus: +{emBonus:F1}%");

                GUI.backgroundColor = _currentReaction.color;
                EditorGUILayout.LabelField($"AMPLIFIED DAMAGE: {amplifiedDamage:F0}",
                    new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
                GUI.backgroundColor = Color.white;

                _calculatedDamage = amplifiedDamage;
            }
            else if (_currentReaction.isTransformative)
            {
                // Transformative reaction (fixed damage based on level and EM)
                float baseTrans = GetTransformativeBaseDamage(_attackerLevel, _currentReaction);
                float emBonus = CalculateTransformativeEMBonus(_elementalMastery);
                float transDamage = baseTrans * (1 + emBonus / 100f) * resMult;

                EditorGUILayout.LabelField($"Transformative Base: {baseTrans:F0}");
                EditorGUILayout.LabelField($"EM Bonus: +{emBonus:F1}%");

                GUI.backgroundColor = _currentReaction.color;
                EditorGUILayout.LabelField($"REACTION DAMAGE: {transDamage:F0}",
                    new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField($"TOTAL (Hit + Reaction): {afterRES + transDamage:F0}",
                    EditorStyles.boldLabel);

                _calculatedDamage = afterRES;
                _calculatedReactionDamage = transDamage;
            }
        }
        else
        {
            _calculatedDamage = afterRES;
        }

        EditorGUILayout.Space(5);

        // Crit versions
        float critDamage = _calculatedDamage * (1 + _critDMG / 100f);
        float avgDamage = _calculatedDamage * (1 + _critRate / 100f * _critDMG / 100f);

        EditorGUILayout.LabelField($"Non-Crit: {_calculatedDamage:F0}");
        EditorGUILayout.LabelField($"Crit: {critDamage:F0}");
        EditorGUILayout.LabelField($"Average (with crit chance): {avgDamage:F0}");

        _totalDamage = avgDamage;

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawDamageFormula()
    {
        EditorGUILayout.LabelField("Formula Reference", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Amplifying Reaction:", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("  Damage × Multiplier × (1 + EM_Bonus)", EditorStyles.miniLabel);

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField("Transformative Reaction:", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("  BaseDMG(Lv) × (1 + EM_Bonus) × RES_Mult", EditorStyles.miniLabel);

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField("EM Bonus (Amplifying):", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("  2.78 × EM / (EM + 1400)", EditorStyles.miniLabel);

        EditorGUILayout.Space(3);

        EditorGUILayout.LabelField("EM Bonus (Transformative):", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("  16 × EM / (EM + 2000)", EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawHistory()
    {
        if (_damageHistory.Count == 0) return;

        EditorGUILayout.LabelField("Damage History", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        for (int i = _damageHistory.Count - 1; i >= Mathf.Max(0, _damageHistory.Count - 5); i--)
        {
            var entry = _damageHistory[i];
            EditorGUILayout.LabelField($"{entry.reaction}: {entry.damage:F0} (EM: {entry.em:F0})");
        }

        if (GUILayout.Button("Clear History"))
        {
            _damageHistory.Clear();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
        if (GUILayout.Button("Calculate", GUILayout.Height(30)))
        {
            CalculateDamage();

            // Add to history
            _damageHistory.Add(new DamageHistoryEntry
            {
                reaction = _currentReaction?.name ?? "None",
                damage = _totalDamage,
                em = _elementalMastery
            });
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Test in Scene", GUILayout.Height(30)))
        {
            TestInScene();
        }

        if (GUILayout.Button("Export Data", GUILayout.Height(30)))
        {
            ExportReactionData();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Quick EM comparison
        EditorGUILayout.LabelField("Quick EM Comparison", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        float[] emValues = { 0, 100, 200, 400, 800, 1000 };
        foreach (float em in emValues)
        {
            float bonus = _currentReaction?.type == ReactionType.Amplifying
                ? CalculateEMBonus(em, _currentReaction)
                : CalculateTransformativeEMBonus(em);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"EM {em:F0}", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"+{bonus:F1}%", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Logic

    private void InitializeReactions()
    {
        _reactions = new Dictionary<(ElementType, ElementType), ReactionData>();

        // Vaporize (Hydro + Pyro)
        _reactions[(ElementType.Pyro, ElementType.Hydro)] = new ReactionData
        {
            name = "Vaporize (2x)",
            trigger = ElementType.Hydro,
            aura = ElementType.Pyro,
            type = ReactionType.Amplifying,
            multiplier = 2.0f,
            emScaling = 2.78f,
            color = new Color(0.5f, 0.3f, 0.8f),
            description = "Hydro triggers on Pyro aura for 2x damage"
        };

        _reactions[(ElementType.Hydro, ElementType.Pyro)] = new ReactionData
        {
            name = "Vaporize (1.5x)",
            trigger = ElementType.Pyro,
            aura = ElementType.Hydro,
            type = ReactionType.Amplifying,
            multiplier = 1.5f,
            emScaling = 2.78f,
            color = new Color(0.8f, 0.4f, 0.3f),
            description = "Pyro triggers on Hydro aura for 1.5x damage"
        };

        // Melt (Cryo + Pyro)
        _reactions[(ElementType.Cryo, ElementType.Pyro)] = new ReactionData
        {
            name = "Melt (2x)",
            trigger = ElementType.Pyro,
            aura = ElementType.Cryo,
            type = ReactionType.Amplifying,
            multiplier = 2.0f,
            emScaling = 2.78f,
            color = new Color(1f, 0.5f, 0.2f),
            description = "Pyro triggers on Cryo aura for 2x damage"
        };

        _reactions[(ElementType.Pyro, ElementType.Cryo)] = new ReactionData
        {
            name = "Melt (1.5x)",
            trigger = ElementType.Cryo,
            aura = ElementType.Pyro,
            type = ReactionType.Amplifying,
            multiplier = 1.5f,
            emScaling = 2.78f,
            color = new Color(0.5f, 0.8f, 1f),
            description = "Cryo triggers on Pyro aura for 1.5x damage"
        };

        // Overloaded (Pyro + Electro)
        _reactions[(ElementType.Pyro, ElementType.Electro)] = new ReactionData
        {
            name = "Overloaded",
            trigger = ElementType.Electro,
            aura = ElementType.Pyro,
            type = ReactionType.Transformative,
            multiplier = 4.0f,
            isTransformative = true,
            color = new Color(1f, 0.3f, 0.5f),
            description = "AoE Pyro damage, causes knockback"
        };
        _reactions[(ElementType.Electro, ElementType.Pyro)] = _reactions[(ElementType.Pyro, ElementType.Electro)];

        // Superconduct (Cryo + Electro)
        _reactions[(ElementType.Cryo, ElementType.Electro)] = new ReactionData
        {
            name = "Superconduct",
            trigger = ElementType.Electro,
            aura = ElementType.Cryo,
            type = ReactionType.Transformative,
            multiplier = 1.0f,
            isTransformative = true,
            color = new Color(0.7f, 0.5f, 1f),
            description = "AoE Cryo damage, reduces Physical RES by 40%"
        };
        _reactions[(ElementType.Electro, ElementType.Cryo)] = _reactions[(ElementType.Cryo, ElementType.Electro)];

        // Electro-Charged (Hydro + Electro)
        _reactions[(ElementType.Hydro, ElementType.Electro)] = new ReactionData
        {
            name = "Electro-Charged",
            trigger = ElementType.Electro,
            aura = ElementType.Hydro,
            type = ReactionType.Transformative,
            multiplier = 2.4f,
            isTransformative = true,
            color = new Color(0.5f, 0.3f, 1f),
            description = "DoT Electro damage, can chain to nearby wet enemies"
        };
        _reactions[(ElementType.Electro, ElementType.Hydro)] = _reactions[(ElementType.Hydro, ElementType.Electro)];

        // Frozen (Hydro + Cryo)
        _reactions[(ElementType.Hydro, ElementType.Cryo)] = new ReactionData
        {
            name = "Frozen",
            trigger = ElementType.Cryo,
            aura = ElementType.Hydro,
            type = ReactionType.Transformative,
            multiplier = 0f,
            isTransformative = false,
            color = new Color(0.6f, 0.9f, 1f),
            description = "Freezes the target in place. Shatter deals physical damage."
        };
        _reactions[(ElementType.Cryo, ElementType.Hydro)] = _reactions[(ElementType.Hydro, ElementType.Cryo)];

        // Burning (Pyro + Dendro)
        _reactions[(ElementType.Pyro, ElementType.Dendro)] = new ReactionData
        {
            name = "Burning",
            trigger = ElementType.Dendro,
            aura = ElementType.Pyro,
            type = ReactionType.Transformative,
            multiplier = 0.5f,
            isTransformative = true,
            color = new Color(1f, 0.5f, 0.1f),
            description = "DoT Pyro damage"
        };
        _reactions[(ElementType.Dendro, ElementType.Pyro)] = _reactions[(ElementType.Pyro, ElementType.Dendro)];

        // Quicken (Electro + Dendro)
        _reactions[(ElementType.Electro, ElementType.Dendro)] = new ReactionData
        {
            name = "Quicken",
            trigger = ElementType.Dendro,
            aura = ElementType.Electro,
            type = ReactionType.Catalyze,
            multiplier = 1.25f,
            color = new Color(0.7f, 1f, 0.4f),
            description = "Applies Quicken aura. Follow up with Aggravate or Spread."
        };
        _reactions[(ElementType.Dendro, ElementType.Electro)] = _reactions[(ElementType.Electro, ElementType.Dendro)];

        // Bloom (Hydro + Dendro)
        _reactions[(ElementType.Hydro, ElementType.Dendro)] = new ReactionData
        {
            name = "Bloom",
            trigger = ElementType.Dendro,
            aura = ElementType.Hydro,
            type = ReactionType.Transformative,
            multiplier = 4.0f,
            isTransformative = true,
            color = new Color(0.3f, 0.9f, 0.5f),
            description = "Creates Dendro Core that explodes. Can Hyperbloom or Burgeon."
        };
        _reactions[(ElementType.Dendro, ElementType.Hydro)] = _reactions[(ElementType.Hydro, ElementType.Dendro)];

        // Swirl (Anemo + Any element)
        foreach (ElementType elem in new[] { ElementType.Pyro, ElementType.Hydro, ElementType.Electro, ElementType.Cryo })
        {
            _reactions[(elem, ElementType.Anemo)] = new ReactionData
            {
                name = $"Swirl ({elem})",
                trigger = ElementType.Anemo,
                aura = elem,
                type = ReactionType.Swirl,
                multiplier = 1.2f,
                isTransformative = true,
                color = new Color(0.5f, 1f, 0.8f),
                description = "Spreads the element and deals AoE elemental damage"
            };
        }

        // Crystallize (Geo + Any element)
        foreach (ElementType elem in new[] { ElementType.Pyro, ElementType.Hydro, ElementType.Electro, ElementType.Cryo })
        {
            _reactions[(elem, ElementType.Geo)] = new ReactionData
            {
                name = $"Crystallize ({elem})",
                trigger = ElementType.Geo,
                aura = elem,
                type = ReactionType.Crystallize,
                multiplier = 0f,
                isTransformative = false,
                color = new Color(0.9f, 0.8f, 0.4f),
                description = "Creates an elemental shield"
            };
        }
    }

    private void CalculateDamage()
    {
        // Find reaction
        _currentReaction = null;
        if (_reactions.TryGetValue((_auraElement, _triggerElement), out ReactionData reaction))
        {
            _currentReaction = reaction;
        }

        Repaint();
    }

    private float CalculateEMBonus(float em, ReactionData reaction)
    {
        if (reaction == null) return 0f;

        // Amplifying: 2.78 × EM / (EM + 1400) × 100
        return 2.78f * em / (em + 1400f) * 100f;
    }

    private float CalculateTransformativeEMBonus(float em)
    {
        // 16 × EM / (EM + 2000) × 100
        return 16f * em / (em + 2000f) * 100f;
    }

    private float CalculateDEFMultiplier()
    {
        // DEF formula: (Lv + 100) / ((Lv + 100) + (DEF × (1 - DEF_Reduction)))
        float attackerLvFactor = _attackerLevel + 100f;
        float defFactor = _targetLevel + 100f;
        return attackerLvFactor / (attackerLvFactor + defFactor);
    }

    private float CalculateRESMultiplier()
    {
        float res = _targetResistance / 100f;

        if (res < 0)
        {
            return 1f - res / 2f;
        }
        else if (res < 0.75f)
        {
            return 1f - res;
        }
        else
        {
            return 1f / (4f * res + 1f);
        }
    }

    private float GetTransformativeBaseDamage(float level, ReactionData reaction)
    {
        // Base transformative damage scales with level
        // These are approximate values
        float[] levelMultipliers = {
            17.17f,   // Level 1
            47.29f,   // Level 10
            107.41f,  // Level 20
            185.76f,  // Level 30
            285.09f,  // Level 40
            404.02f,  // Level 50
            543.26f,  // Level 60
            723.15f,  // Level 70
            914.66f,  // Level 80
            1077.44f  // Level 90
        };

        int index = Mathf.Clamp((int)(level / 10f), 0, 9);
        float baseDamage = levelMultipliers[index];

        return baseDamage * reaction.multiplier;
    }

    private void TestInScene()
    {
        // Create a test setup in the scene
        Debug.Log($"[ElementalReactionTester] Would deal {_totalDamage:F0} damage with {_currentReaction?.name ?? "no reaction"}");
    }

    private void ExportReactionData()
    {
        string path = EditorUtility.SaveFilePanel("Export Reaction Data", "", "reactions", "json");
        if (string.IsNullOrEmpty(path)) return;

        // Export all reactions as JSON
        var export = new List<object>();
        foreach (var kvp in _reactions)
        {
            export.Add(new
            {
                aura = kvp.Key.Item1.ToString(),
                trigger = kvp.Key.Item2.ToString(),
                name = kvp.Value.name,
                type = kvp.Value.type.ToString(),
                multiplier = kvp.Value.multiplier
            });
        }

        string json = JsonUtility.ToJson(export, true);
        System.IO.File.WriteAllText(path, json);

        Debug.Log($"[ElementalReactionTester] Exported to {path}");
    }

    #endregion

    #region Helpers

    private Color GetElementColor(ElementType element)
    {
        switch (element)
        {
            case ElementType.Pyro: return new Color(1f, 0.4f, 0.2f);
            case ElementType.Hydro: return new Color(0.2f, 0.6f, 1f);
            case ElementType.Electro: return new Color(0.7f, 0.3f, 1f);
            case ElementType.Cryo: return new Color(0.5f, 0.9f, 1f);
            case ElementType.Anemo: return new Color(0.5f, 0.9f, 0.7f);
            case ElementType.Geo: return new Color(0.9f, 0.7f, 0.3f);
            case ElementType.Dendro: return new Color(0.5f, 0.9f, 0.3f);
            default: return Color.gray;
        }
    }

    private string GetElementIcon(ElementType element)
    {
        switch (element)
        {
            case ElementType.Pyro: return "🔥";
            case ElementType.Hydro: return "💧";
            case ElementType.Electro: return "⚡";
            case ElementType.Cryo: return "❄";
            case ElementType.Anemo: return "🌪";
            case ElementType.Geo: return "🪨";
            case ElementType.Dendro: return "🌿";
            default: return "?";
        }
    }

    #endregion
}
