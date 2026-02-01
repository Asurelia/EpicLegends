using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Optimiseur de composition d'équipe
    /// Analyse les synergies, résonances et recommande des builds optimaux
    /// </summary>
    public class TeamBuildOptimizer : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Team Build Optimizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<TeamBuildOptimizer>("Team Optimizer");
            window.minSize = new Vector2(900, 700);
        }

        // Enums
        private enum ElementType { Pyro, Hydro, Electro, Anemo, Cryo, Geo, Dendro }
        private enum WeaponType { Sword, Claymore, Polearm, Bow, Catalyst }
        private enum CharacterRole { MainDPS, SubDPS, Support, Healer, Shielder, Buffer, Enabler }

        // State
        private Vector2 _scrollPos;
        private List<CharacterData> _allCharacters = new List<CharacterData>();
        private List<CharacterData> _teamSlots = new List<CharacterData>(4);
        private int _selectedTab = 0;
        private readonly string[] _tabs = { "🎮 Team Builder", "📊 Analysis", "🏆 Meta Teams", "⚙️ Characters" };

        // Analysis results
        private TeamAnalysis _currentAnalysis;
        private List<ReactionCombo> _availableReactions = new List<ReactionCombo>();

        // Filters
        private ElementType? _filterElement;
        private WeaponType? _filterWeapon;
        private CharacterRole? _filterRole;
        private string _searchFilter = "";

        // Resonance definitions
        private static readonly Dictionary<string, ResonanceBonus> Resonances = new Dictionary<string, ResonanceBonus>
        {
            { "Fervent Flames", new ResonanceBonus { Elements = new[] { ElementType.Pyro, ElementType.Pyro }, Description = "ATK +25%, Affected by Cryo -40%", AtkBonus = 25 } },
            { "Soothing Water", new ResonanceBonus { Elements = new[] { ElementType.Hydro, ElementType.Hydro }, Description = "Max HP +25%, Healing +30%", HpBonus = 25, HealingBonus = 30 } },
            { "High Voltage", new ResonanceBonus { Elements = new[] { ElementType.Electro, ElementType.Electro }, Description = "Electro particles, Affected by Hydro -40%", EnergyBonus = 20 } },
            { "Impetuous Winds", new ResonanceBonus { Elements = new[] { ElementType.Anemo, ElementType.Anemo }, Description = "CD -5%, Movement +10%, Stamina -15%", CdrBonus = 5 } },
            { "Shattering Ice", new ResonanceBonus { Elements = new[] { ElementType.Cryo, ElementType.Cryo }, Description = "CRIT Rate +15% vs Frozen/Cryo", CritBonus = 15 } },
            { "Enduring Rock", new ResonanceBonus { Elements = new[] { ElementType.Geo, ElementType.Geo }, Description = "Shield +15%, DMG +15% when shielded", ShieldBonus = 15, DmgBonus = 15 } },
            { "Sprawling Greenery", new ResonanceBonus { Elements = new[] { ElementType.Dendro, ElementType.Dendro }, Description = "EM +50/30/20 based on reactions", EmBonus = 50 } },
            { "Protective Canopy", new ResonanceBonus { Elements = null, Description = "All Elemental RES +15%", AllResBonus = 15 } }
        };

        private void OnEnable()
        {
            // Initialize 4 empty team slots
            while (_teamSlots.Count < 4)
                _teamSlots.Add(null);

            if (_allCharacters.Count == 0)
                CreateSampleCharacters();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎮 Team Build Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Build and analyze team compositions", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs, GUILayout.Height(28));

            EditorGUILayout.Space(5);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawTeamBuilder(); break;
                case 1: DrawAnalysis(); break;
                case 2: DrawMetaTeams(); break;
                case 3: DrawCharacterManager(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTeamBuilder()
        {
            EditorGUILayout.BeginHorizontal();

            // Left - Team slots
            EditorGUILayout.BeginVertical(GUILayout.Width(350));
            DrawTeamSlots();
            EditorGUILayout.EndVertical();

            // Right - Character selection
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawCharacterSelection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Quick analysis
            if (_teamSlots.Any(s => s != null))
            {
                DrawQuickAnalysis();
            }
        }

        private void DrawTeamSlots()
        {
            EditorGUILayout.LabelField("Team Composition", EditorStyles.boldLabel);

            for (int i = 0; i < 4; i++)
            {
                DrawTeamSlot(i);
            }

            EditorGUILayout.Space(10);

            // Team actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Team"))
            {
                for (int i = 0; i < 4; i++)
                    _teamSlots[i] = null;
                _currentAnalysis = null;
            }
            if (GUILayout.Button("Random Team"))
            {
                RandomizeTeam();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("🔍 Analyze Team", GUILayout.Height(30)))
            {
                AnalyzeTeam();
            }

            EditorGUILayout.Space(10);

            // Active resonances
            DrawActiveResonances();
        }

        private void DrawTeamSlot(int index)
        {
            var character = _teamSlots[index];

            Color slotColor = character != null ? GetElementColor(character.Element) : new Color(0.25f, 0.25f, 0.25f);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = slotColor;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(80));
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();

            // Slot number
            string[] slotLabels = { "1️⃣ On-Field", "2️⃣ Sub DPS", "3️⃣ Support", "4️⃣ Flex" };
            EditorGUILayout.LabelField(slotLabels[index], EditorStyles.miniBoldLabel, GUILayout.Width(80));

            if (character != null)
            {
                // Character info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(character.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{character.Element} | {character.Weapon} | {character.Role}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("×", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    _teamSlots[index] = null;
                    _currentAnalysis = null;
                }
            }
            else
            {
                EditorGUILayout.LabelField("Empty Slot", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndHorizontal();

            // Drop zone
            Rect dropRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(dropRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            GUI.Label(dropRect, "Drop character here", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawCharacterSelection()
        {
            EditorGUILayout.LabelField("Available Characters", EditorStyles.boldLabel);

            // Filters
            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));

            // Element filter
            if (GUILayout.Button(_filterElement?.ToString() ?? "Element", EditorStyles.toolbarDropDown, GUILayout.Width(70)))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("All"), _filterElement == null, () => _filterElement = null);
                foreach (ElementType elem in Enum.GetValues(typeof(ElementType)))
                {
                    ElementType e = elem;
                    menu.AddItem(new GUIContent(elem.ToString()), _filterElement == elem, () => _filterElement = e);
                }
                menu.ShowAsContext();
            }

            // Role filter
            if (GUILayout.Button(_filterRole?.ToString() ?? "Role", EditorStyles.toolbarDropDown, GUILayout.Width(70)))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("All"), _filterRole == null, () => _filterRole = null);
                foreach (CharacterRole role in Enum.GetValues(typeof(CharacterRole)))
                {
                    CharacterRole r = role;
                    menu.AddItem(new GUIContent(role.ToString()), _filterRole == role, () => _filterRole = r);
                }
                menu.ShowAsContext();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Character grid
            var filtered = _allCharacters.Where(c =>
                (string.IsNullOrEmpty(_searchFilter) || c.Name.ToLower().Contains(_searchFilter.ToLower())) &&
                (!_filterElement.HasValue || c.Element == _filterElement.Value) &&
                (!_filterRole.HasValue || c.Role == _filterRole.Value)
            ).ToList();

            int columns = 4;
            int index = 0;

            EditorGUILayout.BeginVertical();
            while (index < filtered.Count)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < columns && index < filtered.Count; col++, index++)
                {
                    var character = filtered[index];
                    DrawCharacterCard(character);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCharacterCard(CharacterData character)
        {
            bool isInTeam = _teamSlots.Contains(character);

            Color cardColor = GetElementColor(character.Element);
            if (isInTeam) cardColor = Color.Lerp(cardColor, Color.gray, 0.5f);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = cardColor;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(100), GUILayout.Height(60));
            GUI.backgroundColor = prevBg;

            EditorGUILayout.LabelField(character.Name, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            EditorGUILayout.LabelField($"{GetElementEmoji(character.Element)} {character.Role}", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });

            GUI.enabled = !isInTeam;
            if (GUILayout.Button("Add", GUILayout.Height(18)))
            {
                AddToTeam(character);
            }
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
        }

        private void DrawActiveResonances()
        {
            EditorGUILayout.LabelField("Active Resonances", EditorStyles.boldLabel);

            var activeResonances = GetActiveResonances();

            if (activeResonances.Count == 0)
            {
                EditorGUILayout.HelpBox("No resonances active. Need 2+ of same element or 4 unique elements.", MessageType.Info);
            }
            else
            {
                foreach (var res in activeResonances)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"✨ {res.Key}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(res.Value.Description, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
        }

        private void DrawQuickAnalysis()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Analysis", EditorStyles.boldLabel);

            // Element coverage
            var elements = _teamSlots.Where(s => s != null).Select(s => s.Element).Distinct().ToList();
            EditorGUILayout.LabelField($"Elements: {string.Join(", ", elements.Select(e => GetElementEmoji(e) + e))}");

            // Role coverage
            var roles = _teamSlots.Where(s => s != null).Select(s => s.Role).ToList();
            bool hasMainDPS = roles.Contains(CharacterRole.MainDPS);
            bool hasHealer = roles.Contains(CharacterRole.Healer) || roles.Contains(CharacterRole.Shielder);
            bool hasSupport = roles.Contains(CharacterRole.Support) || roles.Contains(CharacterRole.Buffer);

            EditorGUILayout.BeginHorizontal();
            DrawCheckmark("Main DPS", hasMainDPS);
            DrawCheckmark("Sustain", hasHealer);
            DrawCheckmark("Support", hasSupport);
            EditorGUILayout.EndHorizontal();

            // Available reactions
            var reactions = GetAvailableReactions();
            if (reactions.Count > 0)
            {
                EditorGUILayout.LabelField($"Reactions: {string.Join(", ", reactions.Take(5))}");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCheckmark(string label, bool isChecked)
        {
            string icon = isChecked ? "✅" : "❌";
            Color color = isChecked ? Color.green : Color.red;
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = color } };
            EditorGUILayout.LabelField($"{icon} {label}", style, GUILayout.Width(100));
        }

        private void DrawAnalysis()
        {
            if (_currentAnalysis == null)
            {
                EditorGUILayout.HelpBox("Build a team and click 'Analyze Team' to see detailed analysis.", MessageType.Info);
                return;
            }

            // Overall score
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Team Score", EditorStyles.boldLabel);

            Rect scoreRect = EditorGUILayout.GetControlRect(GUILayout.Height(30));
            EditorGUI.ProgressBar(scoreRect, _currentAnalysis.OverallScore / 100f, $"{_currentAnalysis.OverallScore:F0}/100");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"DPS: {_currentAnalysis.DpsScore:F0}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"Synergy: {_currentAnalysis.SynergyScore:F0}", GUILayout.Width(100));
            EditorGUILayout.LabelField($"Survival: {_currentAnalysis.SurvivalScore:F0}", GUILayout.Width(100));
            EditorGUILayout.LabelField($"Utility: {_currentAnalysis.UtilityScore:F0}", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Strengths
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💪 Strengths", EditorStyles.boldLabel);
            foreach (var strength in _currentAnalysis.Strengths)
            {
                EditorGUILayout.LabelField($"• {strength}", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Weaknesses
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚠️ Weaknesses", EditorStyles.boldLabel);
            foreach (var weakness in _currentAnalysis.Weaknesses)
            {
                EditorGUILayout.LabelField($"• {weakness}", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Recommendations
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💡 Recommendations", EditorStyles.boldLabel);
            foreach (var rec in _currentAnalysis.Recommendations)
            {
                EditorGUILayout.LabelField($"• {rec}", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Reaction chart
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🔥 Reaction Potential", EditorStyles.boldLabel);

            foreach (var reaction in _currentAnalysis.ReactionPotential)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(reaction.Name, GUILayout.Width(150));
                Rect barRect = EditorGUILayout.GetControlRect(GUILayout.Height(18));
                EditorGUI.ProgressBar(barRect, reaction.Potential / 100f, $"{reaction.Potential:F0}%");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMetaTeams()
        {
            EditorGUILayout.LabelField("Meta Team Compositions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Pre-built team compositions optimized for different content.", MessageType.Info);

            DrawMetaTeamCard("National Team", "Xiangling, Bennett, Xingqiu, Raiden",
                "High sustained DPS with constant reactions. Great for single-target and AoE.",
                new[] { ElementType.Pyro, ElementType.Pyro, ElementType.Hydro, ElementType.Electro }, 95);

            DrawMetaTeamCard("Freeze Team", "Ayaka, Shenhe, Kokomi, Kazuha",
                "Permanent freeze control with high burst damage. Excellent for grouped enemies.",
                new[] { ElementType.Cryo, ElementType.Cryo, ElementType.Hydro, ElementType.Anemo }, 92);

            DrawMetaTeamCard("Hyperbloom", "Nahida, Xingqiu, Raiden, Yaoyao",
                "Dendro core reactions for consistent AoE damage. Low investment high return.",
                new[] { ElementType.Dendro, ElementType.Hydro, ElementType.Electro, ElementType.Dendro }, 90);

            DrawMetaTeamCard("Double Geo", "Itto, Gorou, Albedo, Zhongli",
                "Mono-Geo with strong shields. Comfortable gameplay, geo resonance bonus.",
                new[] { ElementType.Geo, ElementType.Geo, ElementType.Geo, ElementType.Geo }, 88);

            DrawMetaTeamCard("Taser Team", "Sucrose, Fischl, Beidou, Xingqiu",
                "Electro-charged spam with swirl. Great for mobbing content.",
                new[] { ElementType.Anemo, ElementType.Electro, ElementType.Electro, ElementType.Hydro }, 85);

            DrawMetaTeamCard("Vaporize Carry", "Hu Tao, Xingqiu, Yelan, Zhongli",
                "High ceiling vaporize damage. Requires skill but massive rewards.",
                new[] { ElementType.Pyro, ElementType.Hydro, ElementType.Hydro, ElementType.Geo }, 94);
        }

        private void DrawMetaTeamCard(string name, string characters, string description, ElementType[] elements, int score)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Score: {score}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            // Elements
            EditorGUILayout.BeginHorizontal();
            foreach (var elem in elements)
            {
                Color c = GetElementColor(elem);
                var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = c } };
                EditorGUILayout.LabelField(GetElementEmoji(elem), style, GUILayout.Width(25));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(characters, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button("Load This Team"))
            {
                // Would load the team composition
                Debug.Log($"Would load team: {name}");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCharacterManager()
        {
            EditorGUILayout.LabelField("Character Database", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Character"))
            {
                _allCharacters.Add(new CharacterData { Name = "New Character" });
            }
            if (GUILayout.Button("Load Genshin Roster"))
            {
                LoadGenshinRoster();
            }
            if (GUILayout.Button("Load HSR Roster"))
            {
                LoadHSRRoster();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            for (int i = 0; i < _allCharacters.Count; i++)
            {
                var character = _allCharacters[i];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                character.Name = EditorGUILayout.TextField(character.Name, GUILayout.Width(120));
                character.Element = (ElementType)EditorGUILayout.EnumPopup(character.Element, GUILayout.Width(80));
                character.Weapon = (WeaponType)EditorGUILayout.EnumPopup(character.Weapon, GUILayout.Width(80));
                character.Role = (CharacterRole)EditorGUILayout.EnumPopup(character.Role, GUILayout.Width(80));
                character.Rarity = EditorGUILayout.IntSlider(character.Rarity, 4, 5, GUILayout.Width(100));

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _allCharacters.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private void AddToTeam(CharacterData character)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_teamSlots[i] == null)
                {
                    _teamSlots[i] = character;
                    _currentAnalysis = null;
                    return;
                }
            }
        }

        private void RandomizeTeam()
        {
            var available = _allCharacters.ToList();
            System.Random rng = new System.Random();

            for (int i = 0; i < 4; i++)
            {
                if (available.Count > 0)
                {
                    int idx = rng.Next(available.Count);
                    _teamSlots[i] = available[idx];
                    available.RemoveAt(idx);
                }
            }
            _currentAnalysis = null;
        }

        private Dictionary<string, ResonanceBonus> GetActiveResonances()
        {
            var active = new Dictionary<string, ResonanceBonus>();
            var elements = _teamSlots.Where(s => s != null).Select(s => s.Element).ToList();

            if (elements.Count < 2) return active;

            // Check for dual element resonances
            var grouped = elements.GroupBy(e => e).Where(g => g.Count() >= 2);
            foreach (var group in grouped)
            {
                var resonance = Resonances.FirstOrDefault(r =>
                    r.Value.Elements != null &&
                    r.Value.Elements.All(e => e == group.Key));

                if (!string.IsNullOrEmpty(resonance.Key))
                    active[resonance.Key] = resonance.Value;
            }

            // Check for Protective Canopy (4 unique elements)
            if (elements.Distinct().Count() >= 4)
            {
                active["Protective Canopy"] = Resonances["Protective Canopy"];
            }

            return active;
        }

        private List<string> GetAvailableReactions()
        {
            var reactions = new List<string>();
            var elements = _teamSlots.Where(s => s != null).Select(s => s.Element).Distinct().ToList();

            if (elements.Contains(ElementType.Pyro) && elements.Contains(ElementType.Hydro))
                reactions.Add("Vaporize");
            if (elements.Contains(ElementType.Pyro) && elements.Contains(ElementType.Cryo))
                reactions.Add("Melt");
            if (elements.Contains(ElementType.Electro) && elements.Contains(ElementType.Hydro))
                reactions.Add("Electro-Charged");
            if (elements.Contains(ElementType.Electro) && elements.Contains(ElementType.Pyro))
                reactions.Add("Overloaded");
            if (elements.Contains(ElementType.Cryo) && elements.Contains(ElementType.Electro))
                reactions.Add("Superconduct");
            if (elements.Contains(ElementType.Cryo) && elements.Contains(ElementType.Hydro))
                reactions.Add("Frozen");
            if (elements.Contains(ElementType.Anemo))
                reactions.Add("Swirl");
            if (elements.Contains(ElementType.Geo))
                reactions.Add("Crystallize");
            if (elements.Contains(ElementType.Dendro) && elements.Contains(ElementType.Hydro))
                reactions.Add("Bloom");
            if (elements.Contains(ElementType.Dendro) && elements.Contains(ElementType.Electro))
                reactions.Add("Quicken");
            if (elements.Contains(ElementType.Dendro) && elements.Contains(ElementType.Pyro))
                reactions.Add("Burning");

            return reactions;
        }

        private void AnalyzeTeam()
        {
            _currentAnalysis = new TeamAnalysis();

            var team = _teamSlots.Where(s => s != null).ToList();
            if (team.Count == 0) return;

            // Calculate scores
            _currentAnalysis.DpsScore = CalculateDpsScore(team);
            _currentAnalysis.SynergyScore = CalculateSynergyScore(team);
            _currentAnalysis.SurvivalScore = CalculateSurvivalScore(team);
            _currentAnalysis.UtilityScore = CalculateUtilityScore(team);

            _currentAnalysis.OverallScore = (_currentAnalysis.DpsScore + _currentAnalysis.SynergyScore +
                                            _currentAnalysis.SurvivalScore + _currentAnalysis.UtilityScore) / 4f;

            // Determine strengths
            if (_currentAnalysis.DpsScore >= 80)
                _currentAnalysis.Strengths.Add("High damage potential");
            if (_currentAnalysis.SynergyScore >= 80)
                _currentAnalysis.Strengths.Add("Excellent elemental synergy");
            if (_currentAnalysis.SurvivalScore >= 80)
                _currentAnalysis.Strengths.Add("Strong survivability");

            var reactions = GetAvailableReactions();
            if (reactions.Contains("Vaporize") || reactions.Contains("Melt"))
                _currentAnalysis.Strengths.Add("Access to amplifying reactions");

            // Determine weaknesses
            if (!team.Any(c => c.Role == CharacterRole.Healer || c.Role == CharacterRole.Shielder))
                _currentAnalysis.Weaknesses.Add("No dedicated sustain character");
            if (!team.Any(c => c.Role == CharacterRole.MainDPS))
                _currentAnalysis.Weaknesses.Add("No clear main DPS");
            if (team.Select(c => c.Element).Distinct().Count() == 1)
                _currentAnalysis.Weaknesses.Add("Mono-element team may struggle against immune enemies");

            // Recommendations
            if (!team.Any(c => c.Element == ElementType.Anemo))
                _currentAnalysis.Recommendations.Add("Consider adding an Anemo character for VV shred");
            if (team.Count < 4)
                _currentAnalysis.Recommendations.Add($"Add {4 - team.Count} more characters to complete the team");

            // Reaction potential
            foreach (var reaction in reactions)
            {
                float potential = CalculateReactionPotential(team, reaction);
                _currentAnalysis.ReactionPotential.Add(new ReactionPotentialData { Name = reaction, Potential = potential });
            }
        }

        private float CalculateDpsScore(List<CharacterData> team)
        {
            float score = 50f;

            if (team.Any(c => c.Role == CharacterRole.MainDPS)) score += 20;
            if (team.Any(c => c.Role == CharacterRole.SubDPS)) score += 15;
            if (team.Count(c => c.Rarity == 5) >= 2) score += 10;

            return Mathf.Clamp(score, 0, 100);
        }

        private float CalculateSynergyScore(List<CharacterData> team)
        {
            float score = 40f;

            var reactions = GetAvailableReactions();
            score += reactions.Count * 8f;

            var resonances = GetActiveResonances();
            score += resonances.Count * 10f;

            return Mathf.Clamp(score, 0, 100);
        }

        private float CalculateSurvivalScore(List<CharacterData> team)
        {
            float score = 30f;

            if (team.Any(c => c.Role == CharacterRole.Healer)) score += 30;
            if (team.Any(c => c.Role == CharacterRole.Shielder)) score += 25;
            if (team.Any(c => c.Element == ElementType.Geo)) score += 10;

            return Mathf.Clamp(score, 0, 100);
        }

        private float CalculateUtilityScore(List<CharacterData> team)
        {
            float score = 40f;

            if (team.Any(c => c.Element == ElementType.Anemo)) score += 20;
            if (team.Any(c => c.Role == CharacterRole.Buffer)) score += 15;
            if (team.Any(c => c.Role == CharacterRole.Enabler)) score += 15;

            return Mathf.Clamp(score, 0, 100);
        }

        private float CalculateReactionPotential(List<CharacterData> team, string reaction)
        {
            // Simplified calculation
            return UnityEngine.Random.Range(50f, 100f);
        }

        private Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Pyro => new Color(1f, 0.4f, 0.2f),
                ElementType.Hydro => new Color(0.2f, 0.6f, 1f),
                ElementType.Electro => new Color(0.7f, 0.3f, 1f),
                ElementType.Anemo => new Color(0.4f, 0.9f, 0.7f),
                ElementType.Cryo => new Color(0.6f, 0.9f, 1f),
                ElementType.Geo => new Color(1f, 0.8f, 0.3f),
                ElementType.Dendro => new Color(0.4f, 0.8f, 0.2f),
                _ => Color.gray
            };
        }

        private string GetElementEmoji(ElementType element)
        {
            return element switch
            {
                ElementType.Pyro => "🔥",
                ElementType.Hydro => "💧",
                ElementType.Electro => "⚡",
                ElementType.Anemo => "🌀",
                ElementType.Cryo => "❄️",
                ElementType.Geo => "🪨",
                ElementType.Dendro => "🌿",
                _ => "❓"
            };
        }

        private void CreateSampleCharacters()
        {
            LoadGenshinRoster();
        }

        private void LoadGenshinRoster()
        {
            _allCharacters.Clear();

            // Pyro
            _allCharacters.Add(new CharacterData { Name = "Hu Tao", Element = ElementType.Pyro, Weapon = WeaponType.Polearm, Role = CharacterRole.MainDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Diluc", Element = ElementType.Pyro, Weapon = WeaponType.Claymore, Role = CharacterRole.MainDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Xiangling", Element = ElementType.Pyro, Weapon = WeaponType.Polearm, Role = CharacterRole.SubDPS, Rarity = 4 });
            _allCharacters.Add(new CharacterData { Name = "Bennett", Element = ElementType.Pyro, Weapon = WeaponType.Sword, Role = CharacterRole.Buffer, Rarity = 4 });

            // Hydro
            _allCharacters.Add(new CharacterData { Name = "Neuvillette", Element = ElementType.Hydro, Weapon = WeaponType.Catalyst, Role = CharacterRole.MainDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Xingqiu", Element = ElementType.Hydro, Weapon = WeaponType.Sword, Role = CharacterRole.SubDPS, Rarity = 4 });
            _allCharacters.Add(new CharacterData { Name = "Yelan", Element = ElementType.Hydro, Weapon = WeaponType.Bow, Role = CharacterRole.SubDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Kokomi", Element = ElementType.Hydro, Weapon = WeaponType.Catalyst, Role = CharacterRole.Healer, Rarity = 5 });

            // Electro
            _allCharacters.Add(new CharacterData { Name = "Raiden", Element = ElementType.Electro, Weapon = WeaponType.Polearm, Role = CharacterRole.MainDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Fischl", Element = ElementType.Electro, Weapon = WeaponType.Bow, Role = CharacterRole.SubDPS, Rarity = 4 });
            _allCharacters.Add(new CharacterData { Name = "Beidou", Element = ElementType.Electro, Weapon = WeaponType.Claymore, Role = CharacterRole.SubDPS, Rarity = 4 });

            // Anemo
            _allCharacters.Add(new CharacterData { Name = "Kazuha", Element = ElementType.Anemo, Weapon = WeaponType.Sword, Role = CharacterRole.Support, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Venti", Element = ElementType.Anemo, Weapon = WeaponType.Bow, Role = CharacterRole.Support, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Sucrose", Element = ElementType.Anemo, Weapon = WeaponType.Catalyst, Role = CharacterRole.Support, Rarity = 4 });

            // Cryo
            _allCharacters.Add(new CharacterData { Name = "Ayaka", Element = ElementType.Cryo, Weapon = WeaponType.Sword, Role = CharacterRole.MainDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Ganyu", Element = ElementType.Cryo, Weapon = WeaponType.Bow, Role = CharacterRole.MainDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Shenhe", Element = ElementType.Cryo, Weapon = WeaponType.Polearm, Role = CharacterRole.Buffer, Rarity = 5 });

            // Geo
            _allCharacters.Add(new CharacterData { Name = "Zhongli", Element = ElementType.Geo, Weapon = WeaponType.Polearm, Role = CharacterRole.Shielder, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Albedo", Element = ElementType.Geo, Weapon = WeaponType.Sword, Role = CharacterRole.SubDPS, Rarity = 5 });

            // Dendro
            _allCharacters.Add(new CharacterData { Name = "Nahida", Element = ElementType.Dendro, Weapon = WeaponType.Catalyst, Role = CharacterRole.Enabler, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Alhaitham", Element = ElementType.Dendro, Weapon = WeaponType.Sword, Role = CharacterRole.MainDPS, Rarity = 5 });
        }

        private void LoadHSRRoster()
        {
            _allCharacters.Clear();

            _allCharacters.Add(new CharacterData { Name = "Seele", Element = ElementType.Electro, Weapon = WeaponType.Sword, Role = CharacterRole.MainDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Kafka", Element = ElementType.Electro, Weapon = WeaponType.Bow, Role = CharacterRole.MainDPS, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Fu Xuan", Element = ElementType.Geo, Weapon = WeaponType.Catalyst, Role = CharacterRole.Shielder, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Luocha", Element = ElementType.Anemo, Weapon = WeaponType.Polearm, Role = CharacterRole.Healer, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Bronya", Element = ElementType.Anemo, Weapon = WeaponType.Bow, Role = CharacterRole.Buffer, Rarity = 5 });
            _allCharacters.Add(new CharacterData { Name = "Silver Wolf", Element = ElementType.Electro, Weapon = WeaponType.Catalyst, Role = CharacterRole.Support, Rarity = 5 });
        }

        // Data classes
        [Serializable]
        private class CharacterData
        {
            public string Name;
            public ElementType Element;
            public WeaponType Weapon;
            public CharacterRole Role;
            public int Rarity = 4;
        }

        private class ResonanceBonus
        {
            public ElementType[] Elements;
            public string Description;
            public float AtkBonus;
            public float HpBonus;
            public float HealingBonus;
            public float EnergyBonus;
            public float CdrBonus;
            public float CritBonus;
            public float ShieldBonus;
            public float DmgBonus;
            public float EmBonus;
            public float AllResBonus;
        }

        private class TeamAnalysis
        {
            public float OverallScore;
            public float DpsScore;
            public float SynergyScore;
            public float SurvivalScore;
            public float UtilityScore;
            public List<string> Strengths = new List<string>();
            public List<string> Weaknesses = new List<string>();
            public List<string> Recommendations = new List<string>();
            public List<ReactionPotentialData> ReactionPotential = new List<ReactionPotentialData>();
        }

        private class ReactionPotentialData
        {
            public string Name;
            public float Potential;
        }

        private class ReactionCombo
        {
            public string Name;
            public ElementType Element1;
            public ElementType Element2;
        }
    }
}
