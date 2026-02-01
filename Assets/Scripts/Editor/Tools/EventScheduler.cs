using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Planificateur d'événements de jeu avec calendrier visuel
    /// Gère les bannières, événements, et contenus temporaires
    /// </summary>
    public class EventScheduler : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Event Scheduler")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventScheduler>("Event Scheduler");
            window.minSize = new Vector2(1000, 700);
        }

        // Enums
        private enum EventType
        {
            Banner, LimitedEvent, MaintenanceWindow, DoubleDrops,
            Login, Shop, Anniversary, Collab, Story, Abyss, NewArea
        }

        private enum ViewMode { Calendar, Timeline, List }

        // State
        private Vector2 _scrollPos;
        private ViewMode _viewMode = ViewMode.Calendar;
        private DateTime _viewDate = DateTime.Now;
        private List<GameEvent> _events = new List<GameEvent>();
        private int _selectedEventIdx = -1;

        // Calendar state
        private int _calendarMonth;
        private int _calendarYear;

        // Colors per event type
        private readonly Dictionary<EventType, Color> _eventColors = new Dictionary<EventType, Color>
        {
            { EventType.Banner, new Color(1f, 0.8f, 0.3f) },
            { EventType.LimitedEvent, new Color(0.5f, 0.8f, 1f) },
            { EventType.MaintenanceWindow, new Color(0.5f, 0.5f, 0.5f) },
            { EventType.DoubleDrops, new Color(0.4f, 1f, 0.4f) },
            { EventType.Login, new Color(0.8f, 0.5f, 1f) },
            { EventType.Shop, new Color(1f, 0.5f, 0.5f) },
            { EventType.Anniversary, new Color(1f, 0.9f, 0.5f) },
            { EventType.Collab, new Color(1f, 0.6f, 0.8f) },
            { EventType.Story, new Color(0.6f, 0.9f, 0.6f) },
            { EventType.Abyss, new Color(0.9f, 0.4f, 0.4f) },
            { EventType.NewArea, new Color(0.4f, 0.7f, 0.9f) }
        };

        private void OnEnable()
        {
            _calendarMonth = DateTime.Now.Month;
            _calendarYear = DateTime.Now.Year;

            if (_events.Count == 0)
                CreateSampleEvents();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📅 Event Scheduler", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Plan and schedule in-game events", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Toolbar
            DrawToolbar();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // Main view area
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_viewMode)
            {
                case ViewMode.Calendar:
                    DrawCalendarView();
                    break;
                case ViewMode.Timeline:
                    DrawTimelineView();
                    break;
                case ViewMode.List:
                    DrawListView();
                    break;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Right panel - Event details
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            DrawEventPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // View mode
            if (GUILayout.Toggle(_viewMode == ViewMode.Calendar, "📅 Calendar", EditorStyles.toolbarButton, GUILayout.Width(80)))
                _viewMode = ViewMode.Calendar;
            if (GUILayout.Toggle(_viewMode == ViewMode.Timeline, "📊 Timeline", EditorStyles.toolbarButton, GUILayout.Width(80)))
                _viewMode = ViewMode.Timeline;
            if (GUILayout.Toggle(_viewMode == ViewMode.List, "📋 List", EditorStyles.toolbarButton, GUILayout.Width(80)))
                _viewMode = ViewMode.List;

            GUILayout.FlexibleSpace();

            // Navigation
            if (GUILayout.Button("◀ Prev", EditorStyles.toolbarButton, GUILayout.Width(60)))
                NavigatePrevious();
            if (GUILayout.Button("Today", EditorStyles.toolbarButton, GUILayout.Width(50)))
                NavigateToday();
            if (GUILayout.Button("Next ▶", EditorStyles.toolbarButton, GUILayout.Width(60)))
                NavigateNext();

            GUILayout.Space(20);

            if (GUILayout.Button("+ New Event", EditorStyles.toolbarButton, GUILayout.Width(80)))
                CreateNewEvent();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCalendarView()
        {
            // Month header
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{new DateTime(_calendarYear, _calendarMonth, 1):MMMM yyyy}",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter },
                GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Day headers
            string[] dayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            EditorGUILayout.BeginHorizontal();
            foreach (var day in dayNames)
            {
                EditorGUILayout.LabelField(day, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(85));
            }
            EditorGUILayout.EndHorizontal();

            // Calendar grid
            DateTime firstDay = new DateTime(_calendarYear, _calendarMonth, 1);
            int startDayOfWeek = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_calendarYear, _calendarMonth);

            int day_num = 1;
            for (int week = 0; week < 6; week++)
            {
                if (day_num > daysInMonth) break;

                EditorGUILayout.BeginHorizontal();

                for (int dayOfWeek = 0; dayOfWeek < 7; dayOfWeek++)
                {
                    Rect cellRect = GUILayoutUtility.GetRect(85, 80);

                    if ((week == 0 && dayOfWeek < startDayOfWeek) || day_num > daysInMonth)
                    {
                        EditorGUI.DrawRect(cellRect, new Color(0.15f, 0.15f, 0.15f));
                    }
                    else
                    {
                        DateTime cellDate = new DateTime(_calendarYear, _calendarMonth, day_num);
                        DrawCalendarCell(cellRect, cellDate, day_num);
                        day_num++;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCalendarCell(Rect rect, DateTime date, int dayNum)
        {
            // Background
            bool isToday = date.Date == DateTime.Today;
            Color bgColor = isToday ? new Color(0.3f, 0.4f, 0.5f) : new Color(0.2f, 0.2f, 0.2f);
            EditorGUI.DrawRect(rect, bgColor);

            // Border
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.gray);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.gray);

            // Day number
            GUI.Label(new Rect(rect.x + 3, rect.y + 2, 25, 15), dayNum.ToString(),
                isToday ? EditorStyles.whiteBoldLabel : EditorStyles.miniLabel);

            // Events on this day
            var dayEvents = _events.Where(e => date >= e.StartDate.Date && date <= e.EndDate.Date).ToList();
            float y = rect.y + 18;

            foreach (var evt in dayEvents.Take(3))
            {
                Rect eventRect = new Rect(rect.x + 2, y, rect.width - 4, 14);
                EditorGUI.DrawRect(eventRect, _eventColors.GetValueOrDefault(evt.Type, Color.gray));

                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.white },
                    clipping = TextClipping.Clip
                };
                GUI.Label(eventRect, " " + evt.Name, style);

                // Selection handling
                if (Event.current.type == UnityEngine.EventType.MouseDown && eventRect.Contains(Event.current.mousePosition))
                {
                    _selectedEventIdx = _events.IndexOf(evt);
                    Event.current.Use();
                    Repaint();
                }

                y += 15;
            }

            if (dayEvents.Count > 3)
            {
                GUI.Label(new Rect(rect.x + 2, y, rect.width - 4, 12),
                    $"+{dayEvents.Count - 3} more", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawTimelineView()
        {
            EditorGUILayout.LabelField("Timeline View", EditorStyles.boldLabel);

            // Timeline header
            DateTime startDate = new DateTime(_calendarYear, _calendarMonth, 1);
            DateTime endDate = startDate.AddMonths(3);
            int totalDays = (endDate - startDate).Days;

            Rect timelineRect = GUILayoutUtility.GetRect(100, 50 + _events.Count * 30, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(timelineRect, new Color(0.15f, 0.15f, 0.15f));

            float dayWidth = (timelineRect.width - 150) / totalDays;

            // Draw month headers
            DateTime current = startDate;
            while (current < endDate)
            {
                float x = timelineRect.x + 150 + (current - startDate).Days * dayWidth;
                float width = DateTime.DaysInMonth(current.Year, current.Month) * dayWidth;

                if (current.Month != startDate.Month || current.Year != startDate.Year)
                {
                    EditorGUI.DrawRect(new Rect(x, timelineRect.y, 1, timelineRect.height), Color.gray);
                }

                GUI.Label(new Rect(x + 5, timelineRect.y + 5, width, 20), current.ToString("MMM yyyy"), EditorStyles.boldLabel);

                current = current.AddMonths(1);
                current = new DateTime(current.Year, current.Month, 1);
            }

            // Draw today line
            if (DateTime.Today >= startDate && DateTime.Today <= endDate)
            {
                float todayX = timelineRect.x + 150 + (DateTime.Today - startDate).Days * dayWidth;
                EditorGUI.DrawRect(new Rect(todayX, timelineRect.y + 25, 2, timelineRect.height - 25), Color.red);
            }

            // Draw events
            float eventY = timelineRect.y + 30;
            for (int i = 0; i < _events.Count; i++)
            {
                var evt = _events[i];

                // Event label
                GUI.Label(new Rect(timelineRect.x + 5, eventY, 140, 25), evt.Name, EditorStyles.miniLabel);

                // Event bar
                if (evt.EndDate >= startDate && evt.StartDate <= endDate)
                {
                    DateTime barStart = evt.StartDate < startDate ? startDate : evt.StartDate;
                    DateTime barEnd = evt.EndDate > endDate ? endDate : evt.EndDate;

                    float barX = timelineRect.x + 150 + (barStart - startDate).Days * dayWidth;
                    float barWidth = Mathf.Max((barEnd - barStart).Days * dayWidth, 10);

                    Rect barRect = new Rect(barX, eventY + 2, barWidth, 20);

                    Color color = _eventColors.GetValueOrDefault(evt.Type, Color.gray);
                    if (i == _selectedEventIdx)
                        color = Color.Lerp(color, Color.white, 0.3f);

                    EditorGUI.DrawRect(barRect, color);

                    // Duration text
                    if (barWidth > 50)
                    {
                        int days = (evt.EndDate - evt.StartDate).Days + 1;
                        GUI.Label(barRect, $" {days}d", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });
                    }

                    // Selection
                    if (Event.current.type == UnityEngine.EventType.MouseDown && barRect.Contains(Event.current.mousePosition))
                    {
                        _selectedEventIdx = i;
                        Event.current.Use();
                        Repaint();
                    }
                }

                eventY += 25;
            }
        }

        private void DrawListView()
        {
            EditorGUILayout.LabelField("All Events", EditorStyles.boldLabel);

            // Filters
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(50));
            if (GUILayout.Button("All", GUILayout.Width(40))) { }
            if (GUILayout.Button("Active", GUILayout.Width(50))) { }
            if (GUILayout.Button("Upcoming", GUILayout.Width(70))) { }
            if (GUILayout.Button("Past", GUILayout.Width(50))) { }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Event list
            for (int i = 0; i < _events.Count; i++)
            {
                var evt = _events[i];
                bool isSelected = i == _selectedEventIdx;
                bool isActive = DateTime.Now >= evt.StartDate && DateTime.Now <= evt.EndDate;
                bool isPast = DateTime.Now > evt.EndDate;

                Color bgColor = _eventColors.GetValueOrDefault(evt.Type, Color.gray);
                if (isPast) bgColor = Color.Lerp(bgColor, Color.gray, 0.7f);
                if (isSelected) bgColor = Color.Lerp(bgColor, Color.white, 0.2f);

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = bgColor;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = prevBg;

                EditorGUILayout.BeginHorizontal();

                // Status indicator
                string status = isActive ? "🟢" : (isPast ? "⚪" : "🟡");
                EditorGUILayout.LabelField(status, GUILayout.Width(20));

                // Event info
                EditorGUILayout.LabelField(evt.Name, EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField(evt.Type.ToString(), GUILayout.Width(100));
                EditorGUILayout.LabelField($"{evt.StartDate:MMM dd} - {evt.EndDate:MMM dd}", GUILayout.Width(150));

                int daysLeft = (evt.EndDate - DateTime.Now).Days;
                if (isActive)
                    EditorGUILayout.LabelField($"{daysLeft}d left", GUILayout.Width(60));
                else if (!isPast)
                    EditorGUILayout.LabelField($"in {(evt.StartDate - DateTime.Now).Days}d", GUILayout.Width(60));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Edit", GUILayout.Width(40)))
                    _selectedEventIdx = i;

                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    _events.RemoveAt(i);
                    if (_selectedEventIdx >= _events.Count)
                        _selectedEventIdx = _events.Count - 1;
                    break;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawEventPanel()
        {
            EditorGUILayout.LabelField("Event Details", EditorStyles.boldLabel);

            if (_selectedEventIdx < 0 || _selectedEventIdx >= _events.Count)
            {
                EditorGUILayout.HelpBox("Select an event to edit", MessageType.Info);

                EditorGUILayout.Space(10);

                // Quick create buttons
                EditorGUILayout.LabelField("Quick Create:", EditorStyles.miniBoldLabel);

                foreach (EventType type in Enum.GetValues(typeof(EventType)))
                {
                    if (GUILayout.Button($"+ {type}"))
                    {
                        CreateNewEvent(type);
                    }
                }

                return;
            }

            var evt = _events[_selectedEventIdx];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            evt.Name = EditorGUILayout.TextField("Name", evt.Name);
            evt.Type = (EventType)EditorGUILayout.EnumPopup("Type", evt.Type);

            EditorGUILayout.Space(5);

            // Date range
            EditorGUILayout.LabelField("Duration", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Start:", GUILayout.Width(40));
            int startYear = EditorGUILayout.IntField(evt.StartDate.Year, GUILayout.Width(50));
            int startMonth = EditorGUILayout.IntField(evt.StartDate.Month, GUILayout.Width(30));
            int startDay = EditorGUILayout.IntField(evt.StartDate.Day, GUILayout.Width(30));
            evt.StartDate = new DateTime(
                Mathf.Clamp(startYear, 2020, 2030),
                Mathf.Clamp(startMonth, 1, 12),
                Mathf.Clamp(startDay, 1, 28));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("End:", GUILayout.Width(40));
            int endYear = EditorGUILayout.IntField(evt.EndDate.Year, GUILayout.Width(50));
            int endMonth = EditorGUILayout.IntField(evt.EndDate.Month, GUILayout.Width(30));
            int endDay = EditorGUILayout.IntField(evt.EndDate.Day, GUILayout.Width(30));
            evt.EndDate = new DateTime(
                Mathf.Clamp(endYear, 2020, 2030),
                Mathf.Clamp(endMonth, 1, 12),
                Mathf.Clamp(endDay, 1, 28));
            EditorGUILayout.EndHorizontal();

            int duration = (evt.EndDate - evt.StartDate).Days + 1;
            EditorGUILayout.LabelField($"Duration: {duration} days", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // Quick duration buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Week")) evt.EndDate = evt.StartDate.AddDays(6);
            if (GUILayout.Button("2 Weeks")) evt.EndDate = evt.StartDate.AddDays(13);
            if (GUILayout.Button("3 Weeks")) evt.EndDate = evt.StartDate.AddDays(20);
            if (GUILayout.Button("Patch")) evt.EndDate = evt.StartDate.AddDays(41);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Event-specific settings
            DrawEventTypeSettings(evt);

            EditorGUILayout.Space(5);

            // Description
            EditorGUILayout.LabelField("Description", EditorStyles.miniBoldLabel);
            evt.Description = EditorGUILayout.TextArea(evt.Description, GUILayout.Height(60));

            // Tags
            evt.Tags = EditorGUILayout.TextField("Tags (comma-separated)", evt.Tags);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Conflict check
            var conflicts = FindConflicts(evt);
            if (conflicts.Count > 0)
            {
                EditorGUILayout.HelpBox($"Overlaps with: {string.Join(", ", conflicts.Select(c => c.Name))}", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Duplicate"))
            {
                var dup = evt.Clone();
                dup.Name += " (Copy)";
                dup.StartDate = dup.StartDate.AddDays(duration);
                dup.EndDate = dup.EndDate.AddDays(duration);
                _events.Add(dup);
                _selectedEventIdx = _events.Count - 1;
            }
            if (GUILayout.Button("Delete"))
            {
                _events.RemoveAt(_selectedEventIdx);
                _selectedEventIdx = -1;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(20);

            // Export
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

            if (GUILayout.Button("Export All to JSON"))
                ExportToJson();

            if (GUILayout.Button("Generate Schedule Code"))
                GenerateScheduleCode();
        }

        private void DrawEventTypeSettings(GameEvent evt)
        {
            EditorGUILayout.LabelField("Type Settings", EditorStyles.miniBoldLabel);

            switch (evt.Type)
            {
                case EventType.Banner:
                    evt.FeaturedCharacter = EditorGUILayout.TextField("Featured Character", evt.FeaturedCharacter);
                    evt.FeaturedWeapon = EditorGUILayout.TextField("Featured Weapon", evt.FeaturedWeapon);
                    evt.IsRerun = EditorGUILayout.Toggle("Is Rerun", evt.IsRerun);
                    break;

                case EventType.LimitedEvent:
                    evt.RewardPrimogems = EditorGUILayout.IntField("Primogem Rewards", evt.RewardPrimogems);
                    evt.HasExclusiveReward = EditorGUILayout.Toggle("Exclusive Reward", evt.HasExclusiveReward);
                    evt.ExclusiveRewardName = EditorGUILayout.TextField("Exclusive Item", evt.ExclusiveRewardName);
                    break;

                case EventType.DoubleDrops:
                    evt.AffectedDomain = EditorGUILayout.TextField("Affected Domain", evt.AffectedDomain);
                    evt.DropMultiplier = EditorGUILayout.FloatField("Multiplier", evt.DropMultiplier);
                    break;

                case EventType.Login:
                    evt.LoginDays = EditorGUILayout.IntField("Login Days Required", evt.LoginDays);
                    evt.RewardPrimogems = EditorGUILayout.IntField("Total Primogems", evt.RewardPrimogems);
                    break;

                case EventType.MaintenanceWindow:
                    evt.MaintenanceHours = EditorGUILayout.FloatField("Duration (hours)", evt.MaintenanceHours);
                    evt.CompensationPrimogems = EditorGUILayout.IntField("Compensation", evt.CompensationPrimogems);
                    break;

                case EventType.Abyss:
                    evt.AbyssVersion = EditorGUILayout.TextField("Abyss Version", evt.AbyssVersion);
                    evt.BlessingName = EditorGUILayout.TextField("Blessing", evt.BlessingName);
                    break;
            }
        }

        private List<GameEvent> FindConflicts(GameEvent evt)
        {
            return _events.Where(e =>
                e != evt &&
                e.Type == evt.Type &&
                !(e.EndDate < evt.StartDate || e.StartDate > evt.EndDate)
            ).ToList();
        }

        private void NavigatePrevious()
        {
            _calendarMonth--;
            if (_calendarMonth < 1)
            {
                _calendarMonth = 12;
                _calendarYear--;
            }
        }

        private void NavigateNext()
        {
            _calendarMonth++;
            if (_calendarMonth > 12)
            {
                _calendarMonth = 1;
                _calendarYear++;
            }
        }

        private void NavigateToday()
        {
            _calendarMonth = DateTime.Now.Month;
            _calendarYear = DateTime.Now.Year;
        }

        private void CreateNewEvent()
        {
            CreateNewEvent(EventType.LimitedEvent);
        }

        private void CreateNewEvent(EventType type)
        {
            var evt = new GameEvent
            {
                Name = $"New {type}",
                Type = type,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(13),
                DropMultiplier = 2f,
                LoginDays = 7,
                MaintenanceHours = 5
            };

            _events.Add(evt);
            _selectedEventIdx = _events.Count - 1;
        }

        private void CreateSampleEvents()
        {
            DateTime now = DateTime.Today;

            _events.Add(new GameEvent
            {
                Name = "Character Banner 1",
                Type = EventType.Banner,
                StartDate = now,
                EndDate = now.AddDays(20),
                FeaturedCharacter = "Furina",
                Description = "First half banner"
            });

            _events.Add(new GameEvent
            {
                Name = "Character Banner 2",
                Type = EventType.Banner,
                StartDate = now.AddDays(21),
                EndDate = now.AddDays(41),
                FeaturedCharacter = "Neuvillette",
                Description = "Second half banner"
            });

            _events.Add(new GameEvent
            {
                Name = "Lantern Rite",
                Type = EventType.LimitedEvent,
                StartDate = now.AddDays(5),
                EndDate = now.AddDays(25),
                RewardPrimogems = 1600,
                HasExclusiveReward = true,
                ExclusiveRewardName = "Lantern Glider",
                Description = "Annual Liyue festival event"
            });

            _events.Add(new GameEvent
            {
                Name = "Double Ley Line",
                Type = EventType.DoubleDrops,
                StartDate = now.AddDays(10),
                EndDate = now.AddDays(17),
                AffectedDomain = "Ley Line Outcrops",
                DropMultiplier = 2f
            });

            _events.Add(new GameEvent
            {
                Name = "Version 5.0 Maintenance",
                Type = EventType.MaintenanceWindow,
                StartDate = now.AddDays(42),
                EndDate = now.AddDays(42),
                MaintenanceHours = 5,
                CompensationPrimogems = 300
            });
        }

        private void ExportToJson()
        {
            string path = EditorUtility.SaveFilePanel("Export Events", "", "event_schedule", "json");
            if (string.IsNullOrEmpty(path)) return;

            var export = new EventExportData
            {
                exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                events = _events.Select(e => new EventExportItem
                {
                    name = e.Name,
                    type = e.Type.ToString(),
                    startDate = e.StartDate.ToString("yyyy-MM-dd"),
                    endDate = e.EndDate.ToString("yyyy-MM-dd"),
                    description = e.Description,
                    tags = e.Tags
                }).ToList()
            };

            string json = JsonUtility.ToJson(export, true);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Exported {_events.Count} events to {path}");
        }

        private void GenerateScheduleCode()
        {
            string code = "// Generated Event Schedule\n";
            code += "public static class EventSchedule\n{\n";
            code += "    public static readonly (string Name, DateTime Start, DateTime End, string Type)[] Events = new[]\n";
            code += "    {\n";

            foreach (var evt in _events.OrderBy(e => e.StartDate))
            {
                code += $"        (\"{evt.Name}\", new DateTime({evt.StartDate.Year}, {evt.StartDate.Month}, {evt.StartDate.Day}), ";
                code += $"new DateTime({evt.EndDate.Year}, {evt.EndDate.Month}, {evt.EndDate.Day}), \"{evt.Type}\"),\n";
            }

            code += "    };\n}\n";

            EditorGUIUtility.systemCopyBuffer = code;
            Debug.Log("Schedule code copied to clipboard!");
            EditorUtility.DisplayDialog("Generated", "C# schedule code copied to clipboard", "OK");
        }

        // Data classes
        [Serializable]
        private class GameEvent
        {
            public string Name;
            public EventType Type;
            public DateTime StartDate;
            public DateTime EndDate;
            public string Description = "";
            public string Tags = "";

            // Banner
            public string FeaturedCharacter = "";
            public string FeaturedWeapon = "";
            public bool IsRerun;

            // Limited Event
            public int RewardPrimogems;
            public bool HasExclusiveReward;
            public string ExclusiveRewardName = "";

            // Double Drops
            public string AffectedDomain = "";
            public float DropMultiplier = 2f;

            // Login
            public int LoginDays = 7;

            // Maintenance
            public float MaintenanceHours = 5f;
            public int CompensationPrimogems = 300;

            // Abyss
            public string AbyssVersion = "";
            public string BlessingName = "";

            public GameEvent Clone()
            {
                return (GameEvent)MemberwiseClone();
            }
        }

        [Serializable]
        private class EventExportData
        {
            public string exportDate;
            public List<EventExportItem> events;
        }

        [Serializable]
        private class EventExportItem
        {
            public string name;
            public string type;
            public string startDate;
            public string endDate;
            public string description;
            public string tags;
        }
    }
}
