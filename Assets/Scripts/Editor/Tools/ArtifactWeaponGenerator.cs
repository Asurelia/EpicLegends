using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Générateur d'artefacts et d'armes avec substats aléatoires
    /// Simule le système de drops Genshin Impact avec toutes les règles
    /// </summary>
    public class ArtifactWeaponGenerator : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Artifact & Weapon Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<ArtifactWeaponGenerator>("Artifact/Weapon Gen");
            window.minSize = new Vector2(700, 600);
        }

        // Enums
        private enum GeneratorMode { Artifact, Weapon }
        private enum ArtifactSlot { Flower, Plume, Sands, Goblet, Circlet }
        private enum WeaponType { Sword, Claymore, Polearm, Bow, Catalyst }
        private enum Rarity { ThreeStar = 3, FourStar = 4, FiveStar = 5 }

        private enum MainStatType
        {
            HP_Flat, ATK_Flat, HP_Percent, ATK_Percent, DEF_Percent,
            ElementalMastery, EnergyRecharge, CritRate, CritDamage,
            PhysDMG, PyroDMG, HydroDMG, ElectroDMG, AnemoDMG, CryoDMG, GeoDMG, DendroDMG,
            HealingBonus
        }

        private enum SubStatType
        {
            HP_Flat, ATK_Flat, DEF_Flat,
            HP_Percent, ATK_Percent, DEF_Percent,
            ElementalMastery, EnergyRecharge, CritRate, CritDamage
        }

        // State
        private GeneratorMode _mode = GeneratorMode.Artifact;
        private Vector2 _scrollPos;
        private int _generateCount = 100;

        // Artifact settings
        private ArtifactSlot _artifactSlot = ArtifactSlot.Flower;
        private Rarity _artifactRarity = Rarity.FiveStar;
        private string _setName = "Gladiator's Finale";
        private int _startingSubstats = 3; // 3 or 4

        // Weapon settings
        private WeaponType _weaponType = WeaponType.Sword;
        private Rarity _weaponRarity = Rarity.FourStar;
        private string _weaponName = "Prototype Rancour";
        private int _refinementLevel = 1;

        // Generation results
        private List<GeneratedArtifact> _generatedArtifacts = new List<GeneratedArtifact>();
        private List<GeneratedWeapon> _generatedWeapons = new List<GeneratedWeapon>();

        // Analysis
        private bool _showAnalysis = true;
        private Dictionary<SubStatType, int> _substatDistribution = new Dictionary<SubStatType, int>();
        private int _godRollCount;
        private float _avgCritValue;

        // Substat values (5-star artifact)
        private static readonly Dictionary<SubStatType, float[]> SubstatValues5Star = new Dictionary<SubStatType, float[]>
        {
            { SubStatType.HP_Flat, new float[] { 209f, 239f, 269f, 299f } },
            { SubStatType.ATK_Flat, new float[] { 14f, 16f, 18f, 19f } },
            { SubStatType.DEF_Flat, new float[] { 16f, 19f, 21f, 23f } },
            { SubStatType.HP_Percent, new float[] { 4.1f, 4.7f, 5.3f, 5.8f } },
            { SubStatType.ATK_Percent, new float[] { 4.1f, 4.7f, 5.3f, 5.8f } },
            { SubStatType.DEF_Percent, new float[] { 5.1f, 5.8f, 6.6f, 7.3f } },
            { SubStatType.ElementalMastery, new float[] { 16f, 19f, 21f, 23f } },
            { SubStatType.EnergyRecharge, new float[] { 4.5f, 5.2f, 5.8f, 6.5f } },
            { SubStatType.CritRate, new float[] { 2.7f, 3.1f, 3.5f, 3.9f } },
            { SubStatType.CritDamage, new float[] { 5.4f, 6.2f, 7.0f, 7.8f } }
        };

        // Mainstat options by slot
        private static readonly Dictionary<ArtifactSlot, MainStatType[]> MainStatsBySlot = new Dictionary<ArtifactSlot, MainStatType[]>
        {
            { ArtifactSlot.Flower, new[] { MainStatType.HP_Flat } },
            { ArtifactSlot.Plume, new[] { MainStatType.ATK_Flat } },
            { ArtifactSlot.Sands, new[] { MainStatType.HP_Percent, MainStatType.ATK_Percent, MainStatType.DEF_Percent, MainStatType.ElementalMastery, MainStatType.EnergyRecharge } },
            { ArtifactSlot.Goblet, new[] { MainStatType.HP_Percent, MainStatType.ATK_Percent, MainStatType.DEF_Percent, MainStatType.ElementalMastery, MainStatType.PhysDMG, MainStatType.PyroDMG, MainStatType.HydroDMG, MainStatType.ElectroDMG, MainStatType.AnemoDMG, MainStatType.CryoDMG, MainStatType.GeoDMG, MainStatType.DendroDMG } },
            { ArtifactSlot.Circlet, new[] { MainStatType.HP_Percent, MainStatType.ATK_Percent, MainStatType.DEF_Percent, MainStatType.ElementalMastery, MainStatType.CritRate, MainStatType.CritDamage, MainStatType.HealingBonus } }
        };

        // Mainstat weights for RNG
        private static readonly Dictionary<MainStatType, float> SandsWeights = new Dictionary<MainStatType, float>
        {
            { MainStatType.HP_Percent, 26.68f }, { MainStatType.ATK_Percent, 26.66f }, { MainStatType.DEF_Percent, 26.66f },
            { MainStatType.ElementalMastery, 10f }, { MainStatType.EnergyRecharge, 10f }
        };

        private static readonly Dictionary<MainStatType, float> GobletWeights = new Dictionary<MainStatType, float>
        {
            { MainStatType.HP_Percent, 19.25f }, { MainStatType.ATK_Percent, 19.25f }, { MainStatType.DEF_Percent, 19f },
            { MainStatType.ElementalMastery, 2.5f }, { MainStatType.PhysDMG, 5f },
            { MainStatType.PyroDMG, 5f }, { MainStatType.HydroDMG, 5f }, { MainStatType.ElectroDMG, 5f },
            { MainStatType.AnemoDMG, 5f }, { MainStatType.CryoDMG, 5f }, { MainStatType.GeoDMG, 5f }, { MainStatType.DendroDMG, 5f }
        };

        private static readonly Dictionary<MainStatType, float> CircletWeights = new Dictionary<MainStatType, float>
        {
            { MainStatType.HP_Percent, 22f }, { MainStatType.ATK_Percent, 22f }, { MainStatType.DEF_Percent, 22f },
            { MainStatType.ElementalMastery, 4f }, { MainStatType.CritRate, 10f }, { MainStatType.CritDamage, 10f },
            { MainStatType.HealingBonus, 10f }
        };

        // Substat weights
        private static readonly Dictionary<SubStatType, float> SubstatWeights = new Dictionary<SubStatType, float>
        {
            { SubStatType.HP_Flat, 15.79f }, { SubStatType.ATK_Flat, 15.79f }, { SubStatType.DEF_Flat, 15.79f },
            { SubStatType.HP_Percent, 10.53f }, { SubStatType.ATK_Percent, 10.53f }, { SubStatType.DEF_Percent, 10.53f },
            { SubStatType.ElementalMastery, 10.53f }, { SubStatType.EnergyRecharge, 10.53f },
            { SubStatType.CritRate, 0f }, { SubStatType.CritDamage, 0f } // Will be calculated
        };

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚔️ Artifact & Weapon Generator", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Simulate drops with accurate RNG and substat rolls", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Mode tabs
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_mode == GeneratorMode.Artifact, "🏺 Artifacts", "Button", GUILayout.Height(30)))
                _mode = GeneratorMode.Artifact;
            if (GUILayout.Toggle(_mode == GeneratorMode.Weapon, "⚔️ Weapons", "Button", GUILayout.Height(30)))
                _mode = GeneratorMode.Weapon;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_mode == GeneratorMode.Artifact)
                DrawArtifactGenerator();
            else
                DrawWeaponGenerator();

            EditorGUILayout.Space(10);

            // Generate button
            EditorGUILayout.BeginHorizontal();
            _generateCount = EditorGUILayout.IntSlider("Generate Count", _generateCount, 1, 10000);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("🎲 Generate!", GUILayout.Height(35)))
            {
                if (_mode == GeneratorMode.Artifact)
                    GenerateArtifacts();
                else
                    GenerateWeapons();
            }

            EditorGUILayout.Space(10);

            // Results
            if (_mode == GeneratorMode.Artifact && _generatedArtifacts.Count > 0)
                DrawArtifactResults();
            else if (_mode == GeneratorMode.Weapon && _generatedWeapons.Count > 0)
                DrawWeaponResults();

            EditorGUILayout.EndScrollView();
        }

        private void DrawArtifactGenerator()
        {
            EditorGUILayout.LabelField("Artifact Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _setName = EditorGUILayout.TextField("Set Name", _setName);
            _artifactSlot = (ArtifactSlot)EditorGUILayout.EnumPopup("Slot", _artifactSlot);
            _artifactRarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", _artifactRarity);

            EditorGUILayout.Space(5);

            // Show possible mainstats for this slot
            var possibleMains = MainStatsBySlot[_artifactSlot];
            EditorGUILayout.LabelField($"Possible Main Stats: {string.Join(", ", possibleMains.Select(FormatStatName))}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            _startingSubstats = EditorGUILayout.IntSlider("Starting Substats", _startingSubstats, 3, 4);
            EditorGUILayout.HelpBox(
                _startingSubstats == 4
                    ? "4 substats: All 5 upgrades go to existing substats"
                    : "3 substats: 1 upgrade unlocks 4th substat, 4 go to existing",
                MessageType.Info);

            EditorGUILayout.EndVertical();

            // Preset artifact sets
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Gladiator")) _setName = "Gladiator's Finale";
            if (GUILayout.Button("VV")) _setName = "Viridescent Venerer";
            if (GUILayout.Button("Emblem")) _setName = "Emblem of Severed Fate";
            if (GUILayout.Button("Crimson")) _setName = "Crimson Witch of Flames";
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Blizzard")) _setName = "Blizzard Strayer";
            if (GUILayout.Button("Noblesse")) _setName = "Noblesse Oblige";
            if (GUILayout.Button("Tenacity")) _setName = "Tenacity of the Millelith";
            if (GUILayout.Button("Deepwood")) _setName = "Deepwood Memories";
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeaponGenerator()
        {
            EditorGUILayout.LabelField("Weapon Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _weaponName = EditorGUILayout.TextField("Weapon Name", _weaponName);
            _weaponType = (WeaponType)EditorGUILayout.EnumPopup("Type", _weaponType);
            _weaponRarity = (Rarity)EditorGUILayout.EnumPopup("Rarity", _weaponRarity);
            _refinementLevel = EditorGUILayout.IntSlider("Refinement", _refinementLevel, 1, 5);

            EditorGUILayout.EndVertical();

            // Weapon presets by type
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Prototype Rancour")) { _weaponName = "Prototype Rancour"; _weaponType = WeaponType.Sword; _weaponRarity = Rarity.FourStar; }
            if (GUILayout.Button("Whiteblind")) { _weaponName = "Whiteblind"; _weaponType = WeaponType.Claymore; _weaponRarity = Rarity.FourStar; }
            if (GUILayout.Button("Crescent Pike")) { _weaponName = "Crescent Pike"; _weaponType = WeaponType.Polearm; _weaponRarity = Rarity.FourStar; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Amos' Bow")) { _weaponName = "Amos' Bow"; _weaponType = WeaponType.Bow; _weaponRarity = Rarity.FiveStar; }
            if (GUILayout.Button("Lost Prayer")) { _weaponName = "Lost Prayer to the Sacred Winds"; _weaponType = WeaponType.Catalyst; _weaponRarity = Rarity.FiveStar; }
            if (GUILayout.Button("Jade Cutter")) { _weaponName = "Primordial Jade Cutter"; _weaponType = WeaponType.Sword; _weaponRarity = Rarity.FiveStar; }
            EditorGUILayout.EndHorizontal();
        }

        private void GenerateArtifacts()
        {
            _generatedArtifacts.Clear();
            _substatDistribution.Clear();
            _godRollCount = 0;
            float totalCritValue = 0;

            foreach (SubStatType substat in Enum.GetValues(typeof(SubStatType)))
                _substatDistribution[substat] = 0;

            System.Random rng = new System.Random();

            for (int i = 0; i < _generateCount; i++)
            {
                var artifact = GenerateSingleArtifact(rng);
                _generatedArtifacts.Add(artifact);

                // Track stats
                foreach (var sub in artifact.Substats)
                    _substatDistribution[sub.Type]++;

                totalCritValue += artifact.CritValue;
                if (artifact.CritValue >= 40f) _godRollCount++;
            }

            _avgCritValue = totalCritValue / _generateCount;

            // Sort by crit value
            _generatedArtifacts = _generatedArtifacts.OrderByDescending(a => a.CritValue).ToList();
        }

        private GeneratedArtifact GenerateSingleArtifact(System.Random rng)
        {
            var artifact = new GeneratedArtifact
            {
                SetName = _setName,
                Slot = _artifactSlot,
                Rarity = _artifactRarity,
                Substats = new List<SubstatRoll>()
            };

            // Roll mainstat
            artifact.MainStat = RollMainStat(rng);

            // Get available substats (exclude mainstat equivalent)
            var availableSubs = GetAvailableSubstats(artifact.MainStat);

            // Roll initial substats
            int initialCount = _startingSubstats;
            for (int i = 0; i < initialCount; i++)
            {
                var subType = RollSubstat(rng, availableSubs);
                availableSubs.Remove(subType);

                float value = RollSubstatValue(rng, subType);
                artifact.Substats.Add(new SubstatRoll { Type = subType, Value = value, Rolls = 1 });
            }

            // Upgrades at +4, +8, +12, +16, +20
            int upgrades = 5;
            for (int i = 0; i < upgrades; i++)
            {
                if (artifact.Substats.Count < 4 && availableSubs.Count > 0)
                {
                    // Unlock 4th substat
                    var subType = RollSubstat(rng, availableSubs);
                    availableSubs.Remove(subType);

                    float value = RollSubstatValue(rng, subType);
                    artifact.Substats.Add(new SubstatRoll { Type = subType, Value = value, Rolls = 1 });
                }
                else
                {
                    // Upgrade random existing substat
                    int idx = rng.Next(artifact.Substats.Count);
                    float value = RollSubstatValue(rng, artifact.Substats[idx].Type);
                    artifact.Substats[idx].Value += value;
                    artifact.Substats[idx].Rolls++;
                }
            }

            return artifact;
        }

        private MainStatType RollMainStat(System.Random rng)
        {
            var possible = MainStatsBySlot[_artifactSlot];
            if (possible.Length == 1) return possible[0];

            Dictionary<MainStatType, float> weights = _artifactSlot switch
            {
                ArtifactSlot.Sands => SandsWeights,
                ArtifactSlot.Goblet => GobletWeights,
                ArtifactSlot.Circlet => CircletWeights,
                _ => null
            };

            if (weights == null) return possible[rng.Next(possible.Length)];

            float total = possible.Sum(p => weights.GetValueOrDefault(p, 1f));
            float roll = (float)rng.NextDouble() * total;
            float cumulative = 0;

            foreach (var stat in possible)
            {
                cumulative += weights.GetValueOrDefault(stat, 1f);
                if (roll <= cumulative) return stat;
            }

            return possible[0];
        }

        private List<SubStatType> GetAvailableSubstats(MainStatType mainStat)
        {
            var all = new List<SubStatType>((SubStatType[])Enum.GetValues(typeof(SubStatType)));

            // Remove mainstat equivalent
            switch (mainStat)
            {
                case MainStatType.HP_Flat: all.Remove(SubStatType.HP_Flat); break;
                case MainStatType.ATK_Flat: all.Remove(SubStatType.ATK_Flat); break;
                case MainStatType.HP_Percent: all.Remove(SubStatType.HP_Percent); break;
                case MainStatType.ATK_Percent: all.Remove(SubStatType.ATK_Percent); break;
                case MainStatType.DEF_Percent: all.Remove(SubStatType.DEF_Percent); break;
                case MainStatType.ElementalMastery: all.Remove(SubStatType.ElementalMastery); break;
                case MainStatType.EnergyRecharge: all.Remove(SubStatType.EnergyRecharge); break;
                case MainStatType.CritRate: all.Remove(SubStatType.CritRate); break;
                case MainStatType.CritDamage: all.Remove(SubStatType.CritDamage); break;
            }

            return all;
        }

        private SubStatType RollSubstat(System.Random rng, List<SubStatType> available)
        {
            // Calculate weights
            float total = 0;
            var weights = new Dictionary<SubStatType, float>();

            foreach (var sub in available)
            {
                float w = SubstatWeights.GetValueOrDefault(sub, 10f);
                // Crit has ~7.77% each
                if (sub == SubStatType.CritRate || sub == SubStatType.CritDamage)
                    w = 7.77f;
                weights[sub] = w;
                total += w;
            }

            float roll = (float)rng.NextDouble() * total;
            float cumulative = 0;

            foreach (var sub in available)
            {
                cumulative += weights[sub];
                if (roll <= cumulative) return sub;
            }

            return available[0];
        }

        private float RollSubstatValue(System.Random rng, SubStatType type)
        {
            if (!SubstatValues5Star.TryGetValue(type, out var values))
                return 0;

            // Adjust for rarity
            float multiplier = _artifactRarity switch
            {
                Rarity.ThreeStar => 0.7f,
                Rarity.FourStar => 0.8f,
                Rarity.FiveStar => 1f,
                _ => 1f
            };

            int idx = rng.Next(values.Length);
            return values[idx] * multiplier;
        }

        private void DrawArtifactResults()
        {
            _showAnalysis = EditorGUILayout.Foldout(_showAnalysis, "📊 Analysis", true);
            if (_showAnalysis)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField($"Generated: {_generatedArtifacts.Count} artifacts");
                EditorGUILayout.LabelField($"Average Crit Value: {_avgCritValue:F1}");
                EditorGUILayout.LabelField($"God Rolls (CV ≥ 40): {_godRollCount} ({100f * _godRollCount / _generateCount:F2}%)");

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Substat Distribution:", EditorStyles.boldLabel);

                foreach (var kvp in _substatDistribution.OrderByDescending(k => k.Value))
                {
                    float pct = 100f * kvp.Value / _generatedArtifacts.Sum(a => a.Substats.Count);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  {FormatStatName(kvp.Key)}", GUILayout.Width(150));
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Width(200)), pct / 20f, $"{pct:F1}%");
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🏆 Top 10 Artifacts by Crit Value", EditorStyles.boldLabel);

            for (int i = 0; i < Mathf.Min(10, _generatedArtifacts.Count); i++)
            {
                var art = _generatedArtifacts[i];
                DrawArtifactCard(art, i + 1);
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("📋 Export to JSON"))
                ExportArtifactsToJson();
        }

        private void DrawArtifactCard(GeneratedArtifact art, int rank)
        {
            Color bgColor = art.CritValue >= 40 ? new Color(1f, 0.9f, 0.5f) :
                           art.CritValue >= 30 ? new Color(0.7f, 0.9f, 0.7f) : Color.white;

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"#{rank}", EditorStyles.boldLabel, GUILayout.Width(30));
            EditorGUILayout.LabelField($"{art.SetName} - {art.Slot}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"CV: {art.CritValue:F1}", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Main: {FormatStatName(art.MainStat)}", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            foreach (var sub in art.Substats)
            {
                string valueStr = sub.Type.ToString().Contains("Percent") ||
                                 sub.Type == SubStatType.CritRate ||
                                 sub.Type == SubStatType.CritDamage ||
                                 sub.Type == SubStatType.EnergyRecharge
                    ? $"{sub.Value:F1}%"
                    : $"{sub.Value:F0}";

                bool isCrit = sub.Type == SubStatType.CritRate || sub.Type == SubStatType.CritDamage;
                var style = isCrit ? new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold } : EditorStyles.miniLabel;

                EditorGUILayout.LabelField($"{FormatStatName(sub.Type)}: {valueStr} ({sub.Rolls})", style, GUILayout.Width(130));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void GenerateWeapons()
        {
            _generatedWeapons.Clear();
            System.Random rng = new System.Random();

            for (int i = 0; i < _generateCount; i++)
            {
                _generatedWeapons.Add(GenerateSingleWeapon(rng));
            }
        }

        private GeneratedWeapon GenerateSingleWeapon(System.Random rng)
        {
            // Base stats based on rarity and type
            float baseAtk = _weaponRarity switch
            {
                Rarity.ThreeStar => 38 + rng.Next(10),
                Rarity.FourStar => 41 + rng.Next(15),
                Rarity.FiveStar => 46 + rng.Next(20),
                _ => 40
            };

            // Random substat
            string[] possibleSubs = { "ATK%", "CRIT Rate", "CRIT DMG", "Energy Recharge", "Elemental Mastery", "HP%", "DEF%", "Physical DMG%" };
            float[] subValues5 = { 9.6f, 4.8f, 9.6f, 8f, 36f, 10.8f, 12f, 12f };
            float[] subValues4 = { 6f, 3f, 6f, 5f, 24f, 6.7f, 7.5f, 7.5f };

            int subIdx = rng.Next(possibleSubs.Length);
            float subValue = _weaponRarity == Rarity.FiveStar ? subValues5[subIdx] : subValues4[subIdx];

            return new GeneratedWeapon
            {
                Name = _weaponName,
                Type = _weaponType,
                Rarity = _weaponRarity,
                BaseAttack = baseAtk,
                SubStat = possibleSubs[subIdx],
                SubStatValue = subValue,
                Refinement = _refinementLevel
            };
        }

        private void DrawWeaponResults()
        {
            EditorGUILayout.LabelField($"Generated {_generatedWeapons.Count} weapons", EditorStyles.boldLabel);

            for (int i = 0; i < Mathf.Min(20, _generatedWeapons.Count); i++)
            {
                var wep = _generatedWeapons[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{wep.Name} (R{wep.Refinement})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Type: {wep.Type} | Base ATK: {wep.BaseAttack:F0} | {wep.SubStat}: {wep.SubStatValue:F1}%");
                EditorGUILayout.EndVertical();
            }
        }

        private void ExportArtifactsToJson()
        {
            string path = EditorUtility.SaveFilePanel("Export Artifacts", "", "generated_artifacts", "json");
            if (string.IsNullOrEmpty(path)) return;

            var export = new
            {
                generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                count = _generatedArtifacts.Count,
                avgCritValue = _avgCritValue,
                godRolls = _godRollCount,
                artifacts = _generatedArtifacts.Select(a => new
                {
                    set = a.SetName,
                    slot = a.Slot.ToString(),
                    mainStat = a.MainStat.ToString(),
                    critValue = a.CritValue,
                    substats = a.Substats.Select(s => new
                    {
                        type = s.Type.ToString(),
                        value = s.Value,
                        rolls = s.Rolls
                    })
                })
            };

            string json = JsonUtility.ToJson(export, true);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Exported artifacts to {path}");
        }

        private string FormatStatName(MainStatType stat)
        {
            return stat switch
            {
                MainStatType.HP_Flat => "HP",
                MainStatType.ATK_Flat => "ATK",
                MainStatType.HP_Percent => "HP%",
                MainStatType.ATK_Percent => "ATK%",
                MainStatType.DEF_Percent => "DEF%",
                MainStatType.ElementalMastery => "EM",
                MainStatType.EnergyRecharge => "ER%",
                MainStatType.CritRate => "Crit Rate",
                MainStatType.CritDamage => "Crit DMG",
                MainStatType.PhysDMG => "Phys DMG%",
                MainStatType.PyroDMG => "Pyro DMG%",
                MainStatType.HydroDMG => "Hydro DMG%",
                MainStatType.ElectroDMG => "Electro DMG%",
                MainStatType.AnemoDMG => "Anemo DMG%",
                MainStatType.CryoDMG => "Cryo DMG%",
                MainStatType.GeoDMG => "Geo DMG%",
                MainStatType.DendroDMG => "Dendro DMG%",
                MainStatType.HealingBonus => "Healing%",
                _ => stat.ToString()
            };
        }

        private string FormatStatName(SubStatType stat)
        {
            return stat switch
            {
                SubStatType.HP_Flat => "HP",
                SubStatType.ATK_Flat => "ATK",
                SubStatType.DEF_Flat => "DEF",
                SubStatType.HP_Percent => "HP%",
                SubStatType.ATK_Percent => "ATK%",
                SubStatType.DEF_Percent => "DEF%",
                SubStatType.ElementalMastery => "EM",
                SubStatType.EnergyRecharge => "ER%",
                SubStatType.CritRate => "Crit Rate",
                SubStatType.CritDamage => "Crit DMG",
                _ => stat.ToString()
            };
        }

        // Data classes
        private class GeneratedArtifact
        {
            public string SetName;
            public ArtifactSlot Slot;
            public Rarity Rarity;
            public MainStatType MainStat;
            public List<SubstatRoll> Substats;

            public float CritValue
            {
                get
                {
                    float cv = 0;
                    foreach (var sub in Substats)
                    {
                        if (sub.Type == SubStatType.CritRate) cv += sub.Value * 2;
                        else if (sub.Type == SubStatType.CritDamage) cv += sub.Value;
                    }
                    return cv;
                }
            }
        }

        private class SubstatRoll
        {
            public SubStatType Type;
            public float Value;
            public int Rolls;
        }

        private class GeneratedWeapon
        {
            public string Name;
            public WeaponType Type;
            public Rarity Rarity;
            public float BaseAttack;
            public string SubStat;
            public float SubStatValue;
            public int Refinement;
        }
    }
}
