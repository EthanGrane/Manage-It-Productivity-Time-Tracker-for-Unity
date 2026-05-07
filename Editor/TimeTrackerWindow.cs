// TimeTrackerWindow.cs — main window with top-level nav + task manager
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityTimeTracker {

    public class TimeTrackerWindow : EditorWindow {

        // ── Top-level nav ─────────────────────────────────────────────
        enum AppView { TimeTracker, TaskManager, CodeCombat }
        AppView currentView = AppView.TimeTracker;

        // ── Tracker tabs ──────────────────────────────────────────────
        enum Tab { Today, Week, Month, AllTime, Settings }
        Tab selectedTab = Tab.Today;
        static readonly string[] TAB_LABELS = { "TODAY", "WEEK", "MONTH", "ALL TIME", "⚙" };

        // ── State ─────────────────────────────────────────────────────
        TimeTrackingData data;

        int weekOffset = 0;
        int monthYear  = DateTime.Today.Year;
        int monthMonth = DateTime.Today.Month;

        DateTime? inspectedDay = null;

        static readonly string[] MONTH_NAMES = {
            "Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"
        };

        static readonly CultureInfo EN = CultureInfo.InvariantCulture;

        Vector2 weekScroll;
        Vector2 monthScroll;
        Vector2 settingsScroll;

        // ── Task Manager ──────────────────────────────────────────────
        TaskManagerPanel taskPanel;

        // ── Code Combat ───────────────────────────────────────────────
        CodeCombatPanel combatPanel;

        [MenuItem("Tools/Manage It")]
        public static void Open() {
            var w = GetWindow<TimeTrackerWindow>("Manage It");
            w.minSize = new Vector2(460, 400);
        }

        void OnEnable() {
            wantsMouseMove = true;
            TimeTrackerSettings.Load();
            Refresh();
            taskPanel = new TaskManagerPanel();
            taskPanel.Init(() => Repaint());
            combatPanel = new CodeCombatPanel();
            combatPanel.Init(() => Repaint());
        }

        void OnFocus() => Refresh();

        void Refresh() {
            data = TimeTrackerCore.LoadData();
            GitHubCommitCache.InvalidateCache();
            taskPanel?.Reload();
            Repaint();
        }

        // ════════════════════════════════════════════════════════════
        //  OnGUI
        // ════════════════════════════════════════════════════════════
        void OnGUI() {
            // Full window background — Unity panel color
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), TimeTrackerGUI.BgColor);

            float pad = 24f;
            float y   = 0f;

            DrawTopNav(pad, ref y);

            if (currentView == AppView.TimeTracker)
                DrawTrackerView(pad, ref y);
            else if (currentView == AppView.TaskManager)
                taskPanel?.Draw(pad, y, position.width, position.height);
            else
                combatPanel?.Draw(pad, y, position.width, position.height);
        }

        // ════════════════════════════════════════════════════════════
        //  TOP NAV  (Time Tracker | Task Manager)
        // ════════════════════════════════════════════════════════════
        void DrawTopNav(float pad, ref float y) {
            float navH   = 28f;
            float totalW = position.width - pad * 2;

            // Background
            EditorGUI.DrawRect(new Rect(0, y, position.width, navH),
                TimeTrackerGUI.BgDark);

            // Bottom border
            EditorGUI.DrawRect(new Rect(0, y + navH, position.width, 1),
                TimeTrackerGUI.DivColor);

            string[] navLabels = { "⏱  Time Tracker", "✓  Task Manager", "⚔" };
            AppView[] navViews = { AppView.TimeTracker, AppView.TaskManager, AppView.CodeCombat };

            float swordW = 36f; // ⚔ pequeño
            float otherW = (totalW - swordW - 12f) / 2f;

            float[] navW = { otherW, otherW, swordW };

            float[] navX = {
                pad,
                pad + otherW + 6f,
                pad + (otherW + 6f) * 2f
            };

            for (int i = 0; i < navLabels.Length; i++) {
                bool active = currentView == navViews[i];
                Rect r = new Rect(navX[i], y, navW[i], navH);

                // Active tab
                if (active) {
                    EditorGUI.DrawRect(r, TimeTrackerGUI.BgColor);
                    EditorGUI.DrawRect(new Rect(r.x, y, r.width, 2), TimeTrackerGUI.AccentColor);

                    GUI.Label(r, navLabels[i],
                        TimeTrackerGUI.Style(11,
                            TimeTrackerGUI.TextColor,
                            FontStyle.Bold,
                            TextAnchor.MiddleCenter));
                }
                else {
                    bool hov = r.Contains(Event.current.mousePosition);

                    if (hov) {
                        EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.05f));
                        Repaint();
                    }

                    GUI.Label(r, navLabels[i],
                        TimeTrackerGUI.Style(i == 2 ? 14 : 11,
                            hov ? TimeTrackerGUI.TextColor : TimeTrackerGUI.LabelColor,
                            FontStyle.Normal,
                            TextAnchor.MiddleCenter));

                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) {
                        currentView = navViews[i];
                        inspectedDay = null;
                        Repaint();
                    }
                }
            }

            y += navH + 1f;
        }
                
        // ════════════════════════════════════════════════════════════
        //  TRACKER VIEW (tabs)
        // ════════════════════════════════════════════════════════════
        void DrawTrackerView(float pad, ref float y) {
            DrawTabs(pad, ref y);
            y += 10f;

            switch (selectedTab) {
                case Tab.Today:    DrawToday(pad, ref y);    break;
                case Tab.Week:     DrawWeek(pad, ref y);     break;
                case Tab.Month:    DrawMonth(pad, ref y);    break;
                case Tab.AllTime:  DrawAllTime(pad, ref y);  break;
                case Tab.Settings: DrawSettings(pad, ref y); break;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  TABS BAR
        // ════════════════════════════════════════════════════════════
        void DrawTabs(float pad, ref float y) {
            int   mainCount = TAB_LABELS.Length - 1;
            float gearW     = 30f;
            float gap       = 1f;  // Unity uses hairline separators
            float mainW     = (position.width - pad * 2 - (mainCount - 1) * gap - gap - gearW) / mainCount;
            float tabH      = 24f;

            // Tab bar background
            EditorGUI.DrawRect(new Rect(0, y, position.width, tabH),
                new Color(0.1882f, 0.1882f, 0.1882f, 1f));
            // Bottom border
            EditorGUI.DrawRect(new Rect(0, y + tabH, position.width, 1), TimeTrackerGUI.DivColor);

            for (int i = 0; i < TAB_LABELS.Length; i++) {
                bool  active = (int)selectedTab == i;
                float tw     = i < mainCount ? mainW : gearW;
                float tx     = pad + (i < mainCount
                    ? i * (mainW + gap)
                    : mainCount * (mainW + gap));

                Rect r = new Rect(tx, y, tw, tabH);

                // Tab background
                Color tabBg = active
                    ? TimeTrackerGUI.BgColor          // active = same as panel (raised look)
                    : new Color(0.1882f, 0.1882f, 0.1882f, 1f); // inactive = darker
                EditorGUI.DrawRect(r, tabBg);

                // Active: accent line on top
                if (active)
                    EditorGUI.DrawRect(new Rect(tx, y, tw, 2), TimeTrackerGUI.AccentColor);

                // Vertical separator between tabs
                if (i < TAB_LABELS.Length - 1)
                    EditorGUI.DrawRect(new Rect(tx + tw, y, 1, tabH),
                        new Color(0.12f, 0.12f, 0.12f, 1f));

                bool hov = !active && r.Contains(Event.current.mousePosition);
                if (hov) { EditorGUI.DrawRect(r, new Color(1f,1f,1f,0.04f)); Repaint(); }

                GUI.Label(r, TAB_LABELS[i], TimeTrackerGUI.Style(
                    i < mainCount ? 10 : 13,
                    active   ? TimeTrackerGUI.TextColor
                  : hov      ? TimeTrackerGUI.TextColor
                  : TimeTrackerGUI.LabelColor,
                    anchor: TextAnchor.MiddleCenter));

                if (!active && GUI.Button(r, GUIContent.none, GUIStyle.none)) {
                    selectedTab  = (Tab)i;
                    inspectedDay = null;
                    Repaint();
                }
            }

            y += tabH;
        }

        // ════════════════════════════════════════════════════════════
        //  TODAY
        // ════════════════════════════════════════════════════════════
        void DrawToday(float pad, ref float y) {
            float trackW  = position.width - pad * 2;
            var   sessions = TimeTrackerCore.GetSessionsForDate(data, DateTime.Today);
            double total   = sessions.Sum(s => (s.end - s.start).TotalMinutes);

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(total),
                $"{sessions.Count} session{(sessions.Count != 1 ? "s" : "")} today");

            EnsureCommitsFetched(DateTime.Today, DateTime.Today);
            var commits = GitHubCommitCache.GetForDate(DateTime.Today);

            TimeTrackerGUI.DrawTimeline(pad, trackW, ref y, sessions, commits);

            if (commits.Count > 0) {
                var gh = TimeTrackerSettings.GitHub;
                if (gh.enabled) {
                    GUI.Label(new Rect(pad, y, trackW, 14),
                        $"◆ {commits.Count} commit{(commits.Count != 1 ? "s" : "")} today  ({gh.owner}/{gh.repo})",
                        TimeTrackerGUI.Style(9, TimeTrackerGUI.CommitColor));
                    y += 16f;
                }
            } else if (GitHubCommitCache.IsFetching) {
                GUI.Label(new Rect(pad, y, trackW, 14), "◆ Fetching commits…",
                    TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
                y += 16f;
            }

            y += 8f;

            DrawTodayTasksIntegration(pad, trackW, ref y);

            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "SESSIONS");
            y += 20f;

            foreach (var (i, s) in sessions.Select((s, i) => (i, s)))
                TimeTrackerGUI.DrawSessionRow(pad, trackW, ref y, s.start, s.end, i == sessions.Count - 1);

            if (sessions.Count == 0) {
                GUI.Label(new Rect(pad, y, trackW, 24), "No sessions recorded today",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor, anchor: TextAnchor.MiddleCenter));
                y += 24f;
            }

            if (commits.Count > 0) {
                y += 4f;
                TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
                TimeTrackerGUI.DrawSectionLabel(pad, y, "COMMITS");
                y += 20f;
                foreach (var c in commits.Take(10))
                    DrawCommitRow(pad, trackW, ref y, c);
                if (commits.Count > 10) {
                    GUI.Label(new Rect(pad, y, trackW, 14),
                        $"  + {commits.Count - 10} more commits",
                        TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
                    y += 16f;
                }
            }
        }

        // ── Task integration block in Today view ──────────────────────
        void DrawTodayTasksIntegration(float pad, float trackW, ref float y) {
            if (taskPanel == null) return;

            var workingOn  = taskPanel.GetWorkingOn();
            var dueToday   = taskPanel.GetTasksDueToday();

            bool hasWorking = workingOn.Count > 0;
            bool hasDue     = dueToday.Count > 0;

            if (!hasWorking && !hasDue) return;

            TimeTrackerGUI.DrawSectionLabel(pad, y, "TASKS");
            y += 18f;

            if (hasWorking) {
                foreach (var task in workingOn)
                    DrawTaskPill(pad, trackW, ref y, task,
                        new Color(0.20f, 0.55f, 0.90f, 1f), "Working On");
            }

            if (hasDue) {
                foreach (var task in dueToday) {
                    if (task.statusId == "working_on") continue;
                    bool overdue = task.IsOverdue;
                    Color col = overdue
                        ? new Color(0.95f, 0.30f, 0.30f, 1f)
                        : new Color(1.00f, 0.75f, 0.20f, 1f);
                    DrawTaskPill(pad, trackW, ref y, task, col,
                        overdue ? "Overdue" : "Due Today");
                }
            }

            y += 4f;
        }

        // ── Inline task block ─────────────────────────────────────────
        void DrawTasksBlock(float x, float cw, ref float sy) {
            if (taskPanel == null) return;
            var workingOn = taskPanel.GetWorkingOn();
            var dueToday  = taskPanel.GetTasksDueToday();
            if (workingOn.Count == 0 && dueToday.Count == 0) return;

            EditorGUI.DrawRect(new Rect(x, sy, cw, 1), TimeTrackerGUI.DivColor);
            sy += 10f;
            GUI.Label(new Rect(x, sy, 80, 14), "TASKS",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 18f;

            foreach (var task in workingOn)
                DrawTaskPillInline(x, cw, ref sy, task,
                    new Color(0.20f, 0.55f, 0.90f, 1f), "Working On");

            foreach (var task in dueToday) {
                if (task.statusId == TaskStatus.WORKING_ON) continue;
                bool overdue = task.IsOverdue;
                Color col = overdue ? new Color(0.95f, 0.30f, 0.30f, 1f)
                                    : new Color(1.00f, 0.75f, 0.20f, 1f);
                DrawTaskPillInline(x, cw, ref sy, task, col,
                    overdue ? "Overdue" : "Due Today");
            }
            sy += 4f;
        }

        void DrawTaskPillInline(float x, float cw, ref float sy,
                TrackerTask task, Color accentCol, string badge) {
            float pillH = 26f;
            Rect  r     = new Rect(x, sy, cw, pillH);
            EditorGUI.DrawRect(r, TimeTrackerGUI.BgCard);
            EditorGUI.DrawRect(new Rect(x, sy, cw, 1),   TimeTrackerGUI.DivColor);
            EditorGUI.DrawRect(new Rect(x, sy, 3, pillH), accentCol);

            float dotX = x + 10f;
            foreach (var tagId in task.tagIds) {
                var tag = TaskManagerCore.GetTag(taskPanel.Board, tagId);
                if (tag == null) continue;
                EditorGUI.DrawRect(new Rect(dotX, sy + 10, 5, 5), tag.GetColor());
                dotX += 9f;
            }

            GUI.Label(new Rect(dotX + 2, sy + 4, cw - 130, 18),
                task.title, TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));

            float bw = badge.Length * 7f + 12f;
            Rect  br = new Rect(x + cw - bw - 4, sy + 4, bw, 18);
            EditorGUI.DrawRect(br, new Color(accentCol.r, accentCol.g, accentCol.b, 0.2f));
            GUI.Label(br, badge,
                TimeTrackerGUI.Style(9, accentCol, FontStyle.Bold, TextAnchor.MiddleCenter));

            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) {
                currentView = AppView.TaskManager;
                Repaint();
            }
            sy += pillH + 4f;
        }

        void DrawTaskPill(float pad, float trackW, ref float y,
                TrackerTask task, Color accentCol, string badge) {
            float pillH = 26f;
            Rect  r     = new Rect(pad, y, trackW, pillH);

            EditorGUI.DrawRect(r, TimeTrackerGUI.BgCard);
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), TimeTrackerGUI.DivColor);
            EditorGUI.DrawRect(new Rect(pad, y, 3, pillH), accentCol);

            float dotX = pad + 10f;
            foreach (var tagId in task.tagIds) {
                var tag = TaskManagerCore.GetTag(taskPanel.Board, tagId);
                if (tag == null) continue;
                EditorGUI.DrawRect(new Rect(dotX, y + 10, 5, 5), tag.GetColor());
                dotX += 9f;
            }

            GUI.Label(new Rect(dotX + 2, y + 4, trackW - 130, 18),
                task.title, TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));

            float bw = badge.Length * 7f + 12f;
            Rect  br = new Rect(pad + trackW - bw - 4, y + 4, bw, 18);
            EditorGUI.DrawRect(br, new Color(accentCol.r, accentCol.g, accentCol.b, 0.2f));
            GUI.Label(br, badge,
                TimeTrackerGUI.Style(9, accentCol, FontStyle.Bold, TextAnchor.MiddleCenter));

            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) {
                currentView = AppView.TaskManager;
                Repaint();
            }

            y += pillH + 4f;
        }

        // ════════════════════════════════════════════════════════════
        //  WEEK
        // ════════════════════════════════════════════════════════════
        void DrawWeek(float pad, ref float y) {
            float trackW  = position.width - pad * 2;
            float scrollH = position.height - y - 8f;

            var navStyle = new GUIStyle(EditorStyles.miniButton) {
                fontSize = 14,
                normal   = { textColor = TimeTrackerGUI.TextColor }
            };

            float pickerY = y;
            if (GUI.Button(new Rect(pad, pickerY, 22, 22), "‹", navStyle)) { weekOffset--; Repaint(); }
            if (GUI.Button(new Rect(pad + 28, pickerY, 22, 22), "›", navStyle)) {
                if (weekOffset < 0) weekOffset++;
                Repaint();
            }

            if (weekOffset != 0) {
                if (GUI.Button(new Rect(pad + 60, pickerY, 50, 22), "today",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 10,
                            normal   = { textColor = TimeTrackerGUI.AccentColor }
                        })) { weekOffset = 0; Repaint(); }
            }

            DateTime refDay    = DateTime.Today.AddDays(weekOffset * 7);
            var (from, to)     = TimeTrackerCore.GetWeekRange(refDay);
            bool isCurrentWeek = weekOffset == 0;

            string weekLabel = isCurrentWeek
                ? $"This week  ·  {from.ToString("dd MMM", EN)} – {to.ToString("dd MMM yyyy", EN)}"
                : $"{from.ToString("dd MMM", EN)} – {to.ToString("dd MMM yyyy", EN)}";
            GUI.Label(new Rect(pad + (weekOffset != 0 ? 120 : 60), pickerY + 3, 260, 18), weekLabel,
                TimeTrackerGUI.Style(13, TimeTrackerGUI.TextColor, FontStyle.Bold));

            y += 34f;

            EnsureCommitsFetched(from, to);

            var ps = TimeTrackerCore.ComputeStats(data, from, to);
            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"{ps.activeDays}/7 days active  ·  {ps.totalSessions} sessions");

            float dayRowH  = 110f;
            int   taskCount = (taskPanel?.GetWorkingOn().Count ?? 0) + (taskPanel?.GetTasksDueToday().Count ?? 0);
            float contentH = 100f + 7 * dayRowH + 200f + (taskCount > 0 ? 40f + taskCount * 30f : 0f);

            Rect scrollView = new Rect(pad, y, trackW, scrollH);
            Rect content    = new Rect(0, 0, trackW - 16f, contentH);
            weekScroll      = GUI.BeginScrollView(scrollView, weekScroll, content);

            float cw = content.width;
            float sy = 0f;

            TimeTrackerGUI.DrawBarChart(0, cw, ref sy, ps.dailyMinutes, "ddd", EN);

            DrawInlineDiv(0, cw, ref sy);
            GUI.Label(new Rect(0, sy, cw, 16), "DAILY TIMELINES",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 22f;

            string[] dayNames = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
            for (int d = 0; d < 7; d++) {
                DateTime day        = from.AddDays(d);
                bool     isToday    = day.Date == DateTime.Today;
                var      daySess    = TimeTrackerCore.GetSessionsForDate(data, day);
                double   dayMins    = daySess.Sum(s => (s.end - s.start).TotalMinutes);
                var      dayCommits = GitHubCommitCache.GetForDate(day);

                Color  dayLabelCol = isToday ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.TextColor;
                string dayHeader   = $"{dayNames[d]}  {day.ToString("dd MMM", EN)}";
                string dayTotal    = dayMins > 0 ? TimeTrackerCore.FormatDuration(dayMins) : "—";

                string commitBadge = "";
                var gh = TimeTrackerSettings.GitHub;
                if (gh.enabled && dayCommits.Count > 0)
                    commitBadge = $"  ◆{dayCommits.Count}";

                GUI.Label(new Rect(0, sy, cw - 80, 16), dayHeader,
                    TimeTrackerGUI.Style(11, dayLabelCol, isToday ? FontStyle.Bold : FontStyle.Normal));
                GUI.Label(new Rect(cw - 80, sy, 80, 16), dayTotal + commitBadge,
                    TimeTrackerGUI.Style(11, isToday ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.LabelColor,
                        anchor: TextAnchor.UpperRight));

                if (isToday)
                    EditorGUI.DrawRect(new Rect(0, sy + 15, cw, 1),
                        new Color(TimeTrackerGUI.AccentColor.r, TimeTrackerGUI.AccentColor.g,
                                  TimeTrackerGUI.AccentColor.b, 0.3f));
                sy += 20f;

                TimeTrackerGUI.DrawTimelineCompact(0, cw, ref sy, daySess, dayCommits);

                if (daySess.Count > 0) {
                    sy += 4f;
                    int showCount = Mathf.Min(daySess.Count, 4);
                    for (int si = 0; si < showCount; si++) {
                        var (ss, se) = daySess[si];
                        bool isLast  = si == daySess.Count - 1;
                        TimeTrackerGUI.DrawSessionRow(0, cw, ref sy, ss, se, isLast && isToday);
                    }
                    if (daySess.Count > 4) {
                        GUI.Label(new Rect(0, sy, cw, 14),
                            $"  + {daySess.Count - 4} more sessions",
                            TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
                        sy += 16f;
                    }
                }

                sy += 10f;
                if (d < 6) {
                    EditorGUI.DrawRect(new Rect(0, sy, cw, 1), TimeTrackerGUI.DivColor);
                    sy += 10f;
                }
            }

            DrawTasksBlock(0, cw, ref sy);

            DrawInlineDiv(0, cw, ref sy);
            GUI.Label(new Rect(0, sy, cw, 16), "AVERAGES & RECORDS",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 22f;
            TimeTrackerGUI.DrawStatsGrid(0, cw, ref sy, ps);

            GUI.EndScrollView();
        }

        // ════════════════════════════════════════════════════════════
        //  MONTH
        // ════════════════════════════════════════════════════════════
        void DrawMonth(float pad, ref float y) {
            float trackW  = position.width - pad * 2;
            float scrollH = position.height - y - 8f;

            float pickerY = y;
            var navStyle  = new GUIStyle(EditorStyles.miniButton) {
                fontSize = 14,
                normal   = { textColor = TimeTrackerGUI.TextColor }
            };

            if (GUI.Button(new Rect(pad, pickerY, 22, 22), "‹", navStyle)) {
                monthMonth--;
                if (monthMonth < 1) { monthMonth = 12; monthYear--; }
                inspectedDay = null;
                Repaint();
            }
            if (GUI.Button(new Rect(pad + 28, pickerY, 22, 22), "›", navStyle)) {
                monthMonth++;
                if (monthMonth > 12) { monthMonth = 1; monthYear++; }
                inspectedDay = null;
                Repaint();
            }

            bool isCurrentMonth = monthYear == DateTime.Today.Year && monthMonth == DateTime.Today.Month;
            if (!isCurrentMonth) {
                if (GUI.Button(new Rect(pad + 60, pickerY, 50, 22), "today",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 10,
                            normal   = { textColor = TimeTrackerGUI.AccentColor }
                        })) {
                    monthYear  = DateTime.Today.Year;
                    monthMonth = DateTime.Today.Month;
                    inspectedDay = null;
                    Repaint();
                }
            }

            string monthLabel = $"{MONTH_NAMES[monthMonth - 1]} {monthYear}";
            GUI.Label(new Rect(pad + (!isCurrentMonth ? 120 : 60), pickerY + 3, 160, 18), monthLabel,
                TimeTrackerGUI.Style(13, TimeTrackerGUI.TextColor, FontStyle.Bold));
            y += 34f;

            var (from, to)  = TimeTrackerCore.GetMonthRange(monthYear, monthMonth);
            int daysInMonth = (int)(to - from).TotalDays + 1;
            var ps          = TimeTrackerCore.ComputeStats(data, from, to);

            EnsureCommitsFetched(from, to);

            if (inspectedDay.HasValue) {
                DrawDayInspector(pad, trackW, ref y, inspectedDay.Value, scrollH);
                return;
            }

            int   taskCountM = (taskPanel?.GetWorkingOn().Count ?? 0) + (taskPanel?.GetTasksDueToday().Count ?? 0);
            float contentH  = 80f + 100f + 200f + 200f + (taskCountM > 0 ? 40f + taskCountM * 30f : 0f);
            Rect scrollView = new Rect(pad, y, trackW, scrollH);
            Rect content    = new Rect(0, 0, trackW - 16f, contentH);
            monthScroll     = GUI.BeginScrollView(scrollView, monthScroll, content);

            float cw = content.width;
            float sy = 0f;

            DrawBigNumber(0, cw, ref sy, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"{monthLabel}  ·  {ps.activeDays}/{daysInMonth} days active  ·  {ps.totalSessions} sessions");

            if (daysInMonth <= 14)
                TimeTrackerGUI.DrawBarChart(0, cw, ref sy, ps.dailyMinutes, "dd", EN);
            else
                TimeTrackerGUI.DrawBarChart(0, cw, ref sy, GroupByWeek(ps.dailyMinutes, from), "dd/MM", EN);

            DrawInlineDiv(0, cw, ref sy);
            GUI.Label(new Rect(0, sy, cw, 16), "TAP A DAY TO INSPECT",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 22f;

            DrawMonthDayGrid(0, cw, ref sy, ps, from, daysInMonth);

            DrawTasksBlock(0, cw, ref sy);

            DrawInlineDiv(0, cw, ref sy);
            GUI.Label(new Rect(0, sy, cw, 16), "AVERAGES & RECORDS",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            sy += 22f;
            TimeTrackerGUI.DrawStatsGrid(0, cw, ref sy, ps);

            GUI.EndScrollView();
        }

        void DrawMonthDayGrid(float x, float cw, ref float sy,
                PeriodStats ps, DateTime from, int daysInMonth) {

            string[] dayLabels = { "M", "T", "W", "T", "F", "S", "S" };
            float cellW = (cw - 6 * 4) / 7f;
            float cellH = 44f;
            float gap   = 4f;

            for (int col = 0; col < 7; col++) {
                GUI.Label(new Rect(x + col * (cellW + gap), sy, cellW, 14),
                    dayLabels[col],
                    TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, anchor: TextAnchor.UpperCenter));
            }
            sy += 16f;

            int firstDow = (int)from.DayOfWeek;
            int startCol = firstDow == 0 ? 6 : firstDow - 1;

            double maxDayMins = ps.dailyMinutes.Count > 0
                ? ps.dailyMinutes.Max(d => d.minutes) : 1;
            if (maxDayMins <= 0) maxDayMins = 1;

            int   col2 = startCol;
            float rowY = sy;
            var   gh   = TimeTrackerSettings.GitHub;

            for (int d = 0; d < daysInMonth; d++) {
                DateTime day    = from.AddDays(d);
                double   mins   = ps.dailyMinutes[d].minutes;
                bool     today  = day.Date == DateTime.Today;
                bool     future = day.Date > DateTime.Today;

                float cx = x + col2 * (cellW + gap);

                // Unity inspector cell style
                Color cellBg = today
                    ? new Color(TimeTrackerGUI.AccentColor.r, TimeTrackerGUI.AccentColor.g,
                                TimeTrackerGUI.AccentColor.b, 0.15f)
                    : TimeTrackerGUI.BgCard;
                EditorGUI.DrawRect(new Rect(cx, rowY, cellW, cellH), cellBg);
                EditorGUI.DrawRect(new Rect(cx, rowY, cellW, 1), TimeTrackerGUI.DivColor);
                EditorGUI.DrawRect(new Rect(cx, rowY, 1, cellH), TimeTrackerGUI.DivColor);

                if (mins > 0 && !future) {
                    float fillH   = Mathf.Max(2f, (float)(mins / maxDayMins) * (cellH - 2));
                    Color fillCol = today ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.SessionDim;
                    EditorGUI.DrawRect(new Rect(cx, rowY + cellH - fillH, cellW, fillH), fillCol);
                }

                if (gh.enabled) {
                    var dayCommits = GitHubCommitCache.GetForDate(day);
                    if (dayCommits.Count > 0)
                        EditorGUI.DrawRect(new Rect(cx + cellW - 6, rowY + 3, 4, 4),
                            TimeTrackerGUI.CommitColor);
                }

                Color numCol = today ? TimeTrackerGUI.AccentColor
                    : (future ? TimeTrackerGUI.LabelColor : TimeTrackerGUI.TextColor);
                GUI.Label(new Rect(cx + 2, rowY + 3, cellW - 4, 14),
                    day.Day.ToString(),
                    TimeTrackerGUI.Style(10, numCol,
                        today ? FontStyle.Bold : FontStyle.Normal,
                        TextAnchor.UpperCenter));

                if (mins > 0) {
                    GUI.Label(new Rect(cx + 1, rowY + cellH - 16, cellW - 2, 13),
                        TimeTrackerCore.FormatDuration(mins),
                        TimeTrackerGUI.Style(8, TimeTrackerGUI.TextColor, anchor: TextAnchor.UpperCenter));
                }

                if (!future && GUI.Button(new Rect(cx, rowY, cellW, cellH),
                        GUIContent.none, GUIStyle.none)) {
                    inspectedDay = day;
                    Repaint();
                }

                if (today)
                    DrawCellBorder(cx, rowY, cellW, cellH, TimeTrackerGUI.AccentColor);

                col2++;
                if (col2 >= 7) {
                    col2 = 0;
                    rowY += cellH + gap;
                }
            }

            int totalCells = startCol + daysInMonth;
            int rows       = (int)Math.Ceiling(totalCells / 7.0);
            sy += rows * (cellH + gap) + 10f;
        }

        void DrawDayInspector(float pad, float trackW, ref float y,
                DateTime day, float scrollH) {

            if (GUI.Button(new Rect(pad, y, 22, 22), "‹",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 14,
                        normal   = { textColor = TimeTrackerGUI.TextColor }
                    })) {
                inspectedDay = null;
                Repaint();
                return;
            }

            if (GUI.Button(new Rect(pad + 28, y, 22, 22), "›",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 14,
                        normal   = { textColor = TimeTrackerGUI.TextColor }
                    })) {
                DateTime next = day.AddDays(1);
                if (next.Date <= DateTime.Today) {
                    inspectedDay = next;
                    if (next.Month != monthMonth || next.Year != monthYear) {
                        monthMonth = next.Month; monthYear = next.Year;
                    }
                }
                Repaint();
                return;
            }

            float labelX = pad + 56;
            GUI.Label(new Rect(labelX, y + 3, 200, 18),
                day.ToString("dddd, dd MMM yyyy", EN),
                TimeTrackerGUI.Style(13, TimeTrackerGUI.TextColor, FontStyle.Bold));

            float arrowX = labelX + 210;
            if (day.Date > DateTime.MinValue.Date) {
                if (GUI.Button(new Rect(arrowX, y, 22, 22), "←",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 11,
                            normal   = { textColor = TimeTrackerGUI.LabelColor }
                        })) {
                    inspectedDay = day.AddDays(-1);
                    DateTime nd  = inspectedDay.Value;
                    if (nd.Month != monthMonth || nd.Year != monthYear) {
                        monthMonth = nd.Month; monthYear = nd.Year;
                    }
                    Repaint();
                    return;
                }
            }
            if (day.Date < DateTime.Today) {
                if (GUI.Button(new Rect(arrowX + 28, y, 22, 22), "→",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 11,
                            normal   = { textColor = TimeTrackerGUI.LabelColor }
                        })) {
                    inspectedDay = day.AddDays(1);
                    DateTime nd  = inspectedDay.Value;
                    if (nd.Month != monthMonth || nd.Year != monthYear) {
                        monthMonth = nd.Month; monthYear = nd.Year;
                    }
                    Repaint();
                    return;
                }
            }

            y += 34f;

            var    sessions = TimeTrackerCore.GetSessionsForDate(data, day);
            double total    = sessions.Sum(s => (s.end - s.start).TotalMinutes);
            bool   isToday  = day.Date == DateTime.Today;
            var    commits  = GitHubCommitCache.GetForDate(day);

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(total),
                $"{sessions.Count} session{(sessions.Count != 1 ? "s" : "")} · {day.ToString("dddd", EN)}");

            TimeTrackerGUI.DrawTimeline(pad, trackW, ref y, sessions, commits);
            y += 12f;

            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "SESSIONS");
            y += 20f;

            if (sessions.Count == 0) {
                GUI.Label(new Rect(pad, y, trackW, 24), "No sessions recorded",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor, anchor: TextAnchor.MiddleCenter));
                y += 24f;
            } else {
                foreach (var (i, s) in sessions.Select((s, i) => (i, s)))
                    TimeTrackerGUI.DrawSessionRow(pad, trackW, ref y,
                        s.start, s.end, isToday && i == sessions.Count - 1);
            }

            if (commits.Count > 0) {
                y += 8f;
                TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
                TimeTrackerGUI.DrawSectionLabel(pad, y, "COMMITS");
                y += 20f;
                foreach (var c in commits)
                    DrawCommitRow(pad, trackW, ref y, c);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  ALL TIME
        // ════════════════════════════════════════════════════════════
        void DrawAllTime(float pad, ref float y) {
            float trackW = position.width - pad * 2;

            if (data == null || data.sessions.Count == 0) {
                GUI.Label(new Rect(pad, y, trackW, 24), "No data recorded yet.",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor, anchor: TextAnchor.MiddleCenter));
                return;
            }

            DateTime first     = data.sessions
                .Select(s => DateTime.TryParse(s.start, out var d) ? d : DateTime.MaxValue).Min();
            DateTime last      = DateTime.Today;
            int      totalDays = (int)(last - first).TotalDays + 1;
            var      ps        = TimeTrackerCore.ComputeStats(data, first, last);

            DrawBigNumber(pad, trackW, ref y, TimeTrackerCore.FormatDuration(ps.totalMinutes),
                $"since {first.ToString("dd MMM yyyy", EN)}  ·  {ps.activeDays}/{totalDays} days active  ·  {ps.totalSessions} sessions");

            var monthly = GroupByMonth(ps.dailyMinutes);
            TimeTrackerGUI.DrawBarChart(pad, trackW, ref y, monthly, "MMM", EN);
            TimeTrackerGUI.DrawDivider(pad, trackW, ref y);
            TimeTrackerGUI.DrawSectionLabel(pad, y, "AVERAGES & RECORDS");
            y += 20f;
            TimeTrackerGUI.DrawStatsGrid(pad, trackW, ref y, ps);
        }

        // ════════════════════════════════════════════════════════════
        //  SETTINGS
        // ════════════════════════════════════════════════════════════
        void DrawSettings(float pad, ref float y) {
            float trackW  = position.width - pad * 2;
            float scrollH = position.height - y - 8f;
            var   theme   = TimeTrackerSettings.Current;
            var   gh      = TimeTrackerSettings.GitHub;
            bool  changed = false;

            Rect scrollView = new Rect(pad, y, trackW, scrollH);
            Rect content    = new Rect(0, 0, trackW - 16f, 1080f);
            settingsScroll  = GUI.BeginScrollView(scrollView, settingsScroll, content);

            float sy = 0f;
            float cw = content.width;

            DrawSettingsHeader(0, sy, cw, "GITHUB INTEGRATION");
            sy += 24f;

            bool newGhEnabled = GUI.Toggle(new Rect(0, sy, cw, 20), gh.enabled,
                "  Enable GitHub commit overlay", new GUIStyle(EditorStyles.toggle) {
                    normal   = { textColor = TimeTrackerGUI.TextColor },
                    fontSize = 11
                });
            if (newGhEnabled != gh.enabled) { gh.enabled = newGhEnabled; changed = true; }
            sy += 28f;

            if (gh.enabled) {
                GUI.Label(new Rect(0, sy, 120, 18), "Personal Access Token",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));
                sy += 20f;
                string newToken = EditorGUI.PasswordField(new Rect(0, sy, cw, 18), gh.token);
                if (newToken != gh.token) { gh.token = newToken; changed = true; }
                GUI.Label(new Rect(0, sy + 20, cw, 12),
                    "Requires  read:repo  scope  (Settings → Developer settings → Personal access tokens)",
                    TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
                sy += 40f;

                GUI.Label(new Rect(0, sy, 60, 18), "Owner",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));
                string newOwner = EditorGUI.TextField(new Rect(0, sy + 20, cw * 0.48f, 18),
                    gh.owner, new GUIStyle(EditorStyles.textField) { fontSize = 11 });
                if (newOwner != gh.owner) { gh.owner = newOwner; changed = true; }

                GUI.Label(new Rect(cw * 0.52f, sy, 60, 18), "Repository",
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));
                string newRepo = EditorGUI.TextField(new Rect(cw * 0.52f, sy + 20, cw * 0.48f, 18),
                    gh.repo, new GUIStyle(EditorStyles.textField) { fontSize = 11 });
                if (newRepo != gh.repo) { gh.repo = newRepo; changed = true; }
                sy += 48f;

                bool newShowTl = GUI.Toggle(new Rect(0, sy, cw * 0.5f, 20), gh.showOnTimeline,
                    "  Show on full timeline", new GUIStyle(EditorStyles.toggle) {
                        normal   = { textColor = TimeTrackerGUI.TextColor }, fontSize = 11 });
                if (newShowTl != gh.showOnTimeline) { gh.showOnTimeline = newShowTl; changed = true; }

                bool newShowCp = GUI.Toggle(new Rect(cw * 0.52f, sy, cw * 0.48f, 20), gh.showOnCompact,
                    "  Show on compact (week)", new GUIStyle(EditorStyles.toggle) {
                        normal   = { textColor = TimeTrackerGUI.TextColor }, fontSize = 11 });
                if (newShowCp != gh.showOnCompact) { gh.showOnCompact = newShowCp; changed = true; }
                sy += 28f;

                changed |= ColorRow(0, ref sy, cw, "Commit marker color", theme.GetCommit(), c => theme.SetCommit(c));

                bool canFetch = !string.IsNullOrEmpty(gh.token) &&
                                !string.IsNullOrEmpty(gh.owner) &&
                                !string.IsNullOrEmpty(gh.repo);
                string statusLabel = GitHubCommitCache.IsFetching
                    ? "Fetching…"
                    : canFetch ? $"Configured: {gh.owner}/{gh.repo}" : "⚠ Fill in token, owner and repository";
                Color statusCol = canFetch ? TimeTrackerGUI.CommitColor : TimeTrackerGUI.LabelColor;
                GUI.Label(new Rect(0, sy, cw - 100, 16), statusLabel,
                    TimeTrackerGUI.Style(9, statusCol));

                if (canFetch && !GitHubCommitCache.IsFetching) {
                    if (GUI.Button(new Rect(cw - 96, sy - 2, 96, 20), "Test fetch today",
                            new GUIStyle(EditorStyles.miniButton) {
                                fontSize = 10,
                                normal   = { textColor = TimeTrackerGUI.AccentColor }
                            })) {
                        GitHubCommitCache.InvalidateCache();
                        GitHubCommitCache.FetchRange(DateTime.Today, DateTime.Today, () => Repaint());
                    }
                }
                sy += 24f;
            }

            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            DrawSettingsHeader(0, sy, cw, "GENERAL COLORS");
            sy += 24f;

            changed |= ColorRow(0, ref sy, cw, "Accent color",      theme.GetAccent(),  c => theme.SetAccent(c));
            changed |= ColorRow(0, ref sy, cw, "Background",        theme.GetBg(),      c => theme.SetBg(c));
            changed |= ColorRow(0, ref sy, cw, "Background (dark)", theme.GetBgDark(),  c => theme.SetBgDark(c));
            changed |= ColorRow(0, ref sy, cw, "Text",              theme.GetText(),    c => theme.SetText(c));
            changed |= ColorRow(0, ref sy, cw, "Session bars",      theme.GetSession(), c => theme.SetSession(c));

            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            DrawSettingsHeader(0, sy, cw, "TIMELINE — ZONE COLORS");
            sy += 24f;

            changed |= ColorRow(0, ref sy, cw, "Off-hours color",             theme.GetOff(),  c => theme.SetOff(c));
            changed |= ColorRow(0, ref sy, cw, "Work-hours color",            theme.GetWork(), c => theme.SetWork(c));
            changed |= ColorRow(0, ref sy, cw, "Icon color A (moon / 💤)",    theme.GetMoon(), c => theme.SetMoon(c));
            changed |= ColorRow(0, ref sy, cw, "Icon color B (sun / 👷)",     theme.GetSun(),  c => theme.SetSun(c));

            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            DrawSettingsHeader(0, sy, cw, "TIMELINE — TRANSITION HOURS");
            sy += 24f;

            changed |= HourSlider(0, ref sy, cw, "Work starts", ref theme.workStartHour, 0f, 24f);
            changed |= HourSlider(0, ref sy, cw, "Work ends",   ref theme.workEndHour,   0f, 24f);

            if (theme.workEndHour <= theme.workStartHour)
                theme.workEndHour = theme.workStartHour + 0.25f;
            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            DrawSettingsHeader(0, sy, cw, "TIMELINE — ICONS");
            sy += 24f;

            GUI.Label(new Rect(0, sy, cw - 160f, 18), "Icon style",
                TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));
            TimelineIconStyle newStyle = (TimelineIconStyle)EditorGUI.EnumPopup(
                new Rect(cw - 156f, sy, 156f, 18), theme.IconStyle);
            if (newStyle != theme.IconStyle) { theme.IconStyle = newStyle; changed = true; }
            sy += 26f;

            string previewLabel = theme.IconStyle == TimelineIconStyle.SunMoon
                ? "🌙 moon (off hours)  ·  ☀ sun (work hours)"
                : "💤 zzz (off hours)  ·  👷 worker (work hours)";
            GUI.Label(new Rect(0, sy, cw, 16), previewLabel,
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            sy += 22f;

            bool newOff = GUI.Toggle(new Rect(0, sy, 220, 20), theme.showOffIcon,
                "  Show off-hours icon", new GUIStyle(EditorStyles.toggle) {
                    normal   = { textColor = TimeTrackerGUI.TextColor },
                    fontSize = 11
                });
            if (newOff != theme.showOffIcon) { theme.showOffIcon = newOff; changed = true; }
            sy += 26f;

            bool newWork = GUI.Toggle(new Rect(0, sy, 220, 20), theme.showWorkIcon,
                "  Show work-hours icon", new GUIStyle(EditorStyles.toggle) {
                    normal   = { textColor = TimeTrackerGUI.TextColor },
                    fontSize = 11
                });
            if (newWork != theme.showWorkIcon) { theme.showWorkIcon = newWork; changed = true; }
            sy += 30f;

            DrawSettingsDivider(0, ref sy, cw);
            DrawSettingsHeader(0, sy, cw, "PREVIEW");
            sy += 24f;

            var previewSessions = new List<(DateTime, DateTime)> {
                (DateTime.Today.AddHours(9), DateTime.Today.AddHours(11.5))
            };
            var previewCommits = gh.enabled ? new List<CommitInfo> {
                new CommitInfo { timestamp = DateTime.Today.AddHours(10), sha = "abc1234",
                    message = "feat: sample commit", author = "you" }
            } : null;
            TimeTrackerGUI.DrawTimeline(0, cw, ref sy, previewSessions, previewCommits);

            sy += 8f;
            DrawSettingsDivider(0, ref sy, cw);

            float btnW    = (cw - 8f) / 2f;
            GUIStyle btnStyle = new GUIStyle(EditorStyles.miniButton) {
                fontSize = 11,
                normal   = { textColor = TimeTrackerGUI.TextColor }
            };

            if (GUI.Button(new Rect(0, sy, btnW, 26), "💾  Save changes", btnStyle)) {
                TimeTrackerSettings.Save();
                GitHubCommitCache.InvalidateCache();
                Repaint();
            }
            if (GUI.Button(new Rect(btnW + 8f, sy, btnW, 26), "↺  Reset to defaults", btnStyle)) {
                TimeTrackerSettings.Reset();
                Repaint();
            }
            sy += 34f;

            GUI.EndScrollView();
            if (changed) Repaint();
        }

        // ════════════════════════════════════════════════════════════
        //  SETTINGS HELPERS
        // ════════════════════════════════════════════════════════════

        void DrawSettingsHeader(float x, float y, float w, string text) {
            GUI.Label(new Rect(x, y, w, 18), text,
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
        }

        void DrawSettingsDivider(float x, ref float y, float w) {
            EditorGUI.DrawRect(new Rect(x, y, w, 1), TimeTrackerGUI.DivColor);
            y += 14f;
        }

        bool ColorRow(float x, ref float y, float w, string label, Color current, System.Action<Color> setter) {
            GUI.Label(new Rect(x, y + 2, w - 60, 18),
                label, TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));
            Color next = EditorGUI.ColorField(new Rect(x + w - 56, y, 56, 18), current);
            y += 26f;
            if (next != current) { setter(next); return true; }
            return false;
        }

        bool HourSlider(float x, ref float y, float w, string label, ref float value, float min, float max) {
            GUI.Label(new Rect(x, y, w - 80, 18),
                label, TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));

            int    h   = (int)value;
            int    m   = (int)((value - h) * 60);
            string lbl = $"{h:00}:{m:00}";
            GUI.Label(new Rect(x + w - 76, y, 36, 18),
                lbl, TimeTrackerGUI.Style(11, TimeTrackerGUI.AccentColor, anchor: TextAnchor.UpperRight));

            float next = GUI.HorizontalSlider(new Rect(x, y + 18, w - 40, 14), value, min, max);
            next = Mathf.Round(next * 4f) / 4f;
            y   += 38f;

            if (Mathf.Abs(next - value) > 0.01f) { value = next; return true; }
            return false;
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════

        void DrawBigNumber(float pad, float trackW, ref float y, string value, string sub) {
            GUI.Label(new Rect(pad, y,      trackW, 36), value,
                TimeTrackerGUI.Style(26, TimeTrackerGUI.BrightColor, FontStyle.Bold));
            GUI.Label(new Rect(pad, y + 30, trackW, 18), sub,
                TimeTrackerGUI.Style(11, TimeTrackerGUI.LabelColor));
            y += 56f;
        }

        void DrawInlineDiv(float x, float w, ref float sy) {
            EditorGUI.DrawRect(new Rect(x, sy, w, 1), TimeTrackerGUI.DivColor);
            sy += 12f;
        }

        void DrawCellBorder(float cx, float cy, float cw, float ch, Color col) {
            EditorGUI.DrawRect(new Rect(cx,          cy,          cw, 1),  col);
            EditorGUI.DrawRect(new Rect(cx,          cy + ch - 1, cw, 1),  col);
            EditorGUI.DrawRect(new Rect(cx,          cy,          1,  ch), col);
            EditorGUI.DrawRect(new Rect(cx + cw - 1, cy,          1,  ch), col);
        }

        void DrawCommitRow(float pad, float trackW, ref float y, CommitInfo c) {
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 18), new Color(1f,1f,1f,0.02f));
            EditorGUI.DrawRect(new Rect(pad, y + 5, 4, 4), TimeTrackerGUI.CommitColor);
            GUI.Label(new Rect(pad + 12, y, 50, 16), c.timestamp.ToString("HH:mm"),
                TimeTrackerGUI.Style(10, TimeTrackerGUI.LabelColor));
            GUI.Label(new Rect(pad + 62, y, 52, 16), $"[{c.sha}]",
                TimeTrackerGUI.Style(10, TimeTrackerGUI.CommitColor));
            GUI.Label(new Rect(pad + 118, y, trackW - 118, 16), c.message,
                TimeTrackerGUI.Style(10, TimeTrackerGUI.TextColor));
            y += 18f;
        }

        void EnsureCommitsFetched(DateTime from, DateTime to) {
            var gh = TimeTrackerSettings.GitHub;
            if (!gh.enabled) return;
            GitHubCommitCache.FetchRange(from, to, () => Repaint());
        }

        static List<(DateTime date, double minutes)> GroupByWeek(
                List<(DateTime date, double minutes)> daily, DateTime monthStart) {
            var weeks = new List<(DateTime, double)>();
            int i     = 0;
            while (i < daily.Count) {
                double   sum       = 0;
                DateTime weekLabel = daily[i].date;
                int      j         = 0;
                while (j < 7 && i + j < daily.Count) { sum += daily[i + j].minutes; j++; }
                weeks.Add((weekLabel, sum));
                i += j;
            }
            return weeks;
        }

        static List<(DateTime date, double minutes)> GroupByMonth(
                List<(DateTime date, double minutes)> daily) {
            var dict = new System.Collections.Generic.SortedDictionary<DateTime, double>();
            foreach (var (date, mins) in daily) {
                var key = new DateTime(date.Year, date.Month, 1);
                if (!dict.ContainsKey(key)) dict[key] = 0;
                dict[key] += mins;
            }
            return dict.Select(kv => (kv.Key, kv.Value)).ToList();
        }
    }
}
