// TimeTrackerGUI.cs — reusable drawing helpers
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace UnityTimeTracker {

    public static class TimeTrackerGUI {

        // ── Unity dark skin palette constants ────────────────────────
        public static readonly Color U_BG        = new Color(0.2196f, 0.2196f, 0.2196f, 1f); // #383838 panel bg
        public static readonly Color U_BG_DARK   = new Color(0.1647f, 0.1647f, 0.1647f, 1f); // #2a2a2a darker bg
        public static readonly Color U_BG_MID    = new Color(0.2800f, 0.2800f, 0.2800f, 1f); // #474747 card bg
        public static readonly Color U_BG_LIGHT  = new Color(0.3400f, 0.3400f, 0.3400f, 1f); // #575757 hover/active
        public static readonly Color U_BORDER    = new Color(0.1400f, 0.1400f, 0.1400f, 1f); // #242424 border dark
        public static readonly Color U_BORDER_HI = new Color(0.4200f, 0.4200f, 0.4200f, 1f); // #6b6b6b border light
        public static readonly Color U_TEXT_BRIGHT = new Color(1.0000f, 1.0000f, 1.0000f, 1f); // #ffffff  headers/titles
        public static readonly Color U_TEXT        = new Color(0.8600f, 0.8600f, 0.8600f, 1f); // #dbdbdb  normal text
        public static readonly Color U_TEXT_DIM    = new Color(0.7000f, 0.7000f, 0.7000f, 1f); // #b3b3b3  secondary/captions
        public static readonly Color U_TEXT_MUTED  = new Color(0.5500f, 0.5500f, 0.5500f, 1f); // #8c8c8c  placeholder/disabled
        public static readonly Color U_BLUE        = new Color(0.1725f, 0.3647f, 0.5294f, 1f); // #2c5d87  Unity selection blue

        // ── Active theme accessors ────────────────────────────────────
        static TimeTrackerThemeData T => TimeTrackerSettings.Current;

        public static Color BgColor      => T.GetBg();
        public static Color BgDark       => T.GetBgDark();
        public static Color BgCard       => U_BG_MID;
        public static Color SessionColor => T.GetSession();
        public static Color SessionDim   => new Color(T.GetSession().r, T.GetSession().g, T.GetSession().b, 0.35f);
        public static Color CommitColor  => T.GetCommit();
        public static Color TickColor    => new Color(1f, 1f, 1f, 0.10f);
        public static Color LabelColor   => U_TEXT_DIM;    // secondary / caption
        public static Color TextColor    => U_TEXT;        // normal body text
        public static Color DarkTextColor    => U_BORDER;        // normal body text
        public static Color BrightColor  => U_TEXT_BRIGHT; // headers, selected labels
        public static Color MutedColor   => U_TEXT_MUTED;  // disabled / placeholder
        public static Color AccentColor  => T.GetAccent();
        public static Color DivColor     => U_BORDER;

        // ── Styles ───────────────────────────────────────────────────
        public static GUIStyle Style(int size, Color color, FontStyle fontStyle = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
            => new GUIStyle(EditorStyles.label) {
                fontSize  = size,
                fontStyle = fontStyle,
                alignment = anchor,
                normal    = { textColor = color }
            };

        // ── Button theme accessors ────────────────────────────────────
        public static Color BtnPrimaryBg     => T.GetBtnPrimaryBg();
        public static Color BtnPrimaryText   => T.GetBtnPrimaryText();
        public static Color BtnSecondaryBg   => T.GetBtnSecondaryBg();
        public static Color BtnSecondaryText => T.GetBtnSecondaryText();
        public static Color BtnDangerBg      => T.GetBtnDangerBg();
        public static Color BtnDangerText    => T.GetBtnDangerText();

        // ── Button drawing — public API ───────────────────────────────
        
        /// <summary>Filled accent-colored button. Use for the main call-to-action.</summary>
        public static bool DrawPrimaryButton(Rect r, string label, int fontSize = 10)
            => DrawButtonCore(r, label, fontSize, BtnPrimaryBg, BtnPrimaryText);

        /// <summary>Neutral background button. Use for secondary / cancel actions.</summary>
        public static bool DrawSecondaryButton(Rect r, string label, int fontSize = 10, bool active = false) {
            Color bg = active ? U_BG_LIGHT : BtnSecondaryBg;
            return DrawButtonCore(r, label, fontSize, bg, BtnSecondaryText);
        }

        /// <summary>Red button. Use for destructive / irreversible actions.</summary>
        public static bool DrawDangerButton(Rect r, string label, int fontSize = 10)
            => DrawButtonCore(r, label, fontSize, BtnDangerBg, BtnDangerText);

        // ── Legacy alias — kept for backwards compatibility ───────────
        /// <summary>
        /// Prefer DrawPrimaryButton / DrawSecondaryButton / DrawDangerButton.
        /// This overload is kept so existing callers don't break.
        /// </summary>
        public static bool DrawButton(Rect r, string label, int fontSize = 10,
                bool active = false, bool primary = false) {
            if (primary) return DrawPrimaryButton(r, label, fontSize);
            return DrawSecondaryButton(r, label, fontSize, active);
        }

        // ── Private core renderer ─────────────────────────────────────
        static bool DrawButtonCore(Rect r, string label, int fontSize, Color bg, Color txt, bool enabled = true) {
            bool hov = r.Contains(Event.current.mousePosition);

            // Lighten bg on hover
            if (hov) bg = new Color(
                Mathf.Min(bg.r + 0.08f, 1f),
                Mathf.Min(bg.g + 0.08f, 1f),
                Mathf.Min(bg.b + 0.08f, 1f), bg.a);

            // Body
            EditorGUI.DrawRect(r, bg);
            // Top highlight
            EditorGUI.DrawRect(new Rect(r.x,             r.y,             r.width, 1), U_BORDER_HI);
            // Bottom shadow
            EditorGUI.DrawRect(new Rect(r.x,             r.y + r.height-1, r.width, 1), U_BORDER);
            // Left edge
            EditorGUI.DrawRect(new Rect(r.x,             r.y,             1, r.height), U_BORDER_HI);
            // Right edge
            EditorGUI.DrawRect(new Rect(r.x + r.width-1, r.y,             1, r.height), U_BORDER);

            GUI.Label(r, label, new GUIStyle(EditorStyles.label) {
                fontSize  = fontSize,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = txt }
            });

            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        // ── Components ───────────────────────────────────────────────

        public static void DrawDivider(float pad, float trackW, ref float y) {
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), DivColor);
            y += 12f;
        }

        public static void DrawSectionLabel(float pad, float y, string text) {
            GUI.Label(new Rect(pad, y, 300, 16), text, Style(9, BrightColor, FontStyle.Bold));
        }

        public static void DrawStatCard(Rect r, string label, string value, string sub = "") {
            EditorGUI.DrawRect(r, U_BG_MID);
            EditorGUI.DrawRect(new Rect(r.x,              r.y,              r.width, 1), U_BORDER);
            EditorGUI.DrawRect(new Rect(r.x,              r.y + r.height-1, r.width, 1), U_BORDER_HI);
            EditorGUI.DrawRect(new Rect(r.x,              r.y,              1, r.height), U_BORDER);
            EditorGUI.DrawRect(new Rect(r.x + r.width-1,  r.y,              1, r.height), U_BORDER);
            GUI.Label(new Rect(r.x + 10, r.y + 8,  r.width - 20, 14), label, Style(9,  LabelColor));     // caption — dim
            GUI.Label(new Rect(r.x + 10, r.y + 20, r.width - 20, 24), value, Style(16, BrightColor, FontStyle.Bold)); // value — bright
            if (!string.IsNullOrEmpty(sub))
                GUI.Label(new Rect(r.x + 10, r.y + 38, r.width - 20, 12), sub, Style(9, MutedColor));    // sub — muted
        }

        // ── Returns flat bg color for a given hour ────────────────────
        public static Color TimelineColorAt(float hour) {
            var th = T;
            bool isWork = hour >= th.workStartHour && hour < th.workEndHour;
            return isWork ? th.GetWork() : th.GetOff();
        }

        // ── Draw moon (crescent via two discs) ────────────────────────
        public static void DrawMoon(float cx, float cy, float r, Color moonColor, Color bgColor) {
            int steps = Mathf.Max(4, (int)(r * 2));
            for (int row = -steps; row <= steps; row++) {
                float fy    = row / (float)steps * r;
                float halfW = Mathf.Sqrt(Mathf.Max(0, r * r - fy * fy));
                EditorGUI.DrawRect(new Rect(cx - halfW, cy + fy, halfW * 2f, 1f), moonColor);
            }
            float offset = r * 0.38f;
            float rInner = r * 0.82f;
            for (int row = -steps; row <= steps; row++) {
                float fy    = row / (float)steps * r;
                float halfW = Mathf.Sqrt(Mathf.Max(0, rInner * rInner - fy * fy));
                EditorGUI.DrawRect(new Rect(cx - halfW + offset, cy + fy, halfW * 2f, 1f), bgColor);
            }
        }

        // ── Draw sun (disc + 8 rays) ──────────────────────────────────
        public static void DrawSun(float cx, float cy, float r, Color sunColor) {
            int steps = Mathf.Max(4, (int)(r * 2));
            for (int row = -steps; row <= steps; row++) {
                float fy    = row / (float)steps * r;
                float halfW = Mathf.Sqrt(Mathf.Max(0, r * r - fy * fy));
                EditorGUI.DrawRect(new Rect(cx - halfW, cy + fy, halfW * 2f, 1f), sunColor);
            }
            float rayLen   = r * 0.55f;
            float rayStart = r + 1.5f;
            for (int i = 0; i < 8; i++) {
                float angle = i * (360f / 8f) * Mathf.Deg2Rad;
                float cos   = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                float x0    = cx + cos * rayStart,            y0 = cy + sin * rayStart;
                float x1    = cx + cos * (rayStart + rayLen), y1 = cy + sin * (rayStart + rayLen);
                int   ss    = Mathf.Max(2, (int)rayLen);
                for (int s = 0; s <= ss; s++) {
                    float t = s / (float)ss;
                    EditorGUI.DrawRect(new Rect(Mathf.Lerp(x0,x1,t) - 0.5f, Mathf.Lerp(y0,y1,t) - 0.5f, 1.5f, 1.5f), sunColor);
                }
            }
        }

        // ── Draw emoji label centered at (cx, cy) ─────────────────────
        static void DrawEmoji(float cx, float cy, string emoji) {
            float w = 20f, h = 18f;
            GUI.Label(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h), emoji,
                new GUIStyle(EditorStyles.label) {
                    fontSize  = 11,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = Color.white }
                });
        }

        // ── Draw a commit diamond marker on the timeline ──────────────
        static void DrawCommitDiamond(float cx, float cy, float size, Color col) {
            for (int i = 0; i <= (int)size; i++) {
                float half = size - i;
                EditorGUI.DrawRect(new Rect(cx - half, cy - i, half * 2f, 1f), col);
                EditorGUI.DrawRect(new Rect(cx - half, cy + i, half * 2f, 1f), col);
            }
        }

        // ── Overlay commit markers on a timeline strip ────────────────
        static string _hoveredCommitTooltip = null;
        static Rect   _tooltipRect;

        public static void DrawCommitMarkers(float pad, float trackW, float trackTop, float trackH,
                List<CommitInfo> commits, bool aboveTimeline = true) {
            if (commits == null || commits.Count == 0) return;

            var gh = TimeTrackerSettings.GitHub;
            if (!gh.enabled || !gh.showOnTimeline) return;

            Color col = T.GetCommit();

            var byMinute = new Dictionary<int, List<CommitInfo>>();
            foreach (var c in commits) {
                int bucket = (int)(c.timestamp.TimeOfDay.TotalMinutes);
                if (!byMinute.ContainsKey(bucket)) byMinute[bucket] = new List<CommitInfo>();
                byMinute[bucket].Add(c);
            }

            float markerY = aboveTimeline ? trackTop - 6f : trackTop + trackH + 4f;

            foreach (var kv in byMinute) {
                float cx = pad + (kv.Key / 1440f) * trackW;
                float size = kv.Value.Count > 1 ? 5f : 4f;

                DrawCommitDiamond(cx, markerY, size + 1.5f, new Color(col.r, col.g, col.b, 0.15f));
                DrawCommitDiamond(cx, markerY, size, col);
                if (kv.Value.Count > 1) {
                    GUI.Label(new Rect(cx + 5, markerY - 6, 20, 12),
                        $"×{kv.Value.Count}", Style(7, col));
                }

                Rect hitRect = new Rect(cx - 8, markerY - 8, 16, 16);
                if (hitRect.Contains(Event.current.mousePosition)) {
                    string tip = string.Join("\n", kv.Value.Select(
                        c => $"{c.timestamp:HH:mm}  [{c.sha}]  {c.message}"));
                    GUI.Label(new Rect(cx + 10, markerY - 20, 260, 14 * kv.Value.Count + 6),
                        tip, new GUIStyle(EditorStyles.helpBox) {
                            fontSize = 9,
                            normal = { textColor = TextColor }
                        });
                }
            }
        }

        // ── Compact commit tick (for compact timelines in week view) ──
        public static void DrawCommitTicks(float pad, float trackW, float trackY, float trackH,
                List<CommitInfo> commits) {
            if (commits == null || commits.Count == 0) return;

            var gh = TimeTrackerSettings.GitHub;
            if (!gh.enabled || !gh.showOnCompact) return;

            Color col = T.GetCommit();
            foreach (var c in commits) {
                float cx = pad + (float)(c.timestamp.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                EditorGUI.DrawRect(new Rect(cx - 0.5f, trackY, 1.5f, trackH), new Color(col.r, col.g, col.b, 0.70f));
                EditorGUI.DrawRect(new Rect(cx - 1.5f, trackY, 4f, 2f), col);
            }
        }

        // ── Timeline with flat day/off zones ─────────────────────────
        public static void DrawTimeline(float pad, float trackW, ref float y,
                List<(DateTime start, DateTime end)> sessions,
                List<CommitInfo> commits = null) {

            float trackH    = 32f;
            float trackTop  = y;
            int   numSlices = 240;

            // Flat background
            for (int i = 0; i < numSlices; i++) {
                float t0   = i / (float)numSlices;
                float t1   = (i + 1) / (float)numSlices;
                float hour = t0 * 24f;
                EditorGUI.DrawRect(new Rect(pad + t0 * trackW, y, (t1 - t0) * trackW + 1f, trackH),
                    TimelineColorAt(hour));
            }

            // Borders
            EditorGUI.DrawRect(new Rect(pad, y,              trackW, 1), U_BORDER);
            EditorGUI.DrawRect(new Rect(pad, y + trackH - 1, trackW, 1), U_BORDER_HI);

            // Session blocks
            var sc = T.GetSession();
            foreach (var (start, end) in sessions) {
                const float vPadding = 4;
                bool isLive = (DateTime.Now - end).TotalMinutes < TimeTrackerCore.SESSION_GAP_MINUTES;

                float x0 = pad + (float)(start.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                float x1 = pad + (float)(end.TimeOfDay.TotalMinutes   / 1440.0) * trackW;
                float w  = Mathf.Max(x1 - x0, 3f);

                float h       = isLive ? trackH - vPadding * 2 : trackH;
                float yOffset = isLive ? vPadding : 0f;

                EditorGUI.DrawRect(new Rect(x0-1, y-1 + yOffset, w+2, h+2),
                    new Color(sc.r, sc.g, sc.b, 0.18f));
                EditorGUI.DrawRect(new Rect(x0, y + yOffset, w, h),
                    new Color(sc.r, sc.g, sc.b, 0.55f));
                EditorGUI.DrawRect(new Rect(x0, y + yOffset, w, 2),
                    new Color(1f, 1f, 1f, 0.25f));
            }

            // "Now" marker
            bool hasLive = sessions.Count > 0 &&
                (DateTime.Now - sessions.Last().end).TotalMinutes < TimeTrackerCore.SESSION_GAP_MINUTES;
            if (hasLive) {
                float nowX = pad + (float)(DateTime.Now.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                EditorGUI.DrawRect(new Rect(nowX - 1, y - 2, 2, trackH + 4), Color.white);
            }

            y += trackH;

            if (commits != null && commits.Count > 0)
                DrawCommitMarkers(pad, trackW, trackTop, trackH, commits, aboveTimeline: false);

            float iconR = 5f;
            float iconY = y + 2f + iconR + (commits != null && commits.Count > 0 ? 12f : 0f);

            var th = T;
            TimelineIconStyle style = th.IconStyle;

            if (style == TimelineIconStyle.SunMoon) {
                if (th.showOffIcon) {
                    Color moonCol   = th.GetMoon();
                    float moonLHour = th.nightEndHour / 2f;
                    float moonRHour = (th.nightStartHour + 24f) / 2f;
                    DrawMoon(pad + (moonLHour / 24f) * trackW,                   iconY, iconR, moonCol, TimelineColorAt(moonLHour));
                    DrawMoon(pad + (Mathf.Min(moonRHour, 23.5f) / 24f) * trackW, iconY, iconR, moonCol, TimelineColorAt(th.nightStartHour + 1.5f));
                }
                if (th.showWorkIcon) {
                    float sunCenter = (th.dayStartHour + th.dayEndHour) / 2f;
                    DrawSun(pad + (sunCenter / 24f) * trackW, iconY, iconR, th.GetSun());
                }
            } else {
                if (th.showWorkIcon) {
                    float workCenter     = (th.workStartHour + th.workEndHour) / 2f;
                    float offLeftCenter  = th.workStartHour / 2f;
                    float offRightCenter = (th.workEndHour + 24f) / 2f;
                    DrawEmoji(pad + (offLeftCenter / 24f) * trackW,                    iconY, "💤");
                    DrawEmoji(pad + (Mathf.Min(offRightCenter, 23.5f) / 24f) * trackW, iconY, "💤");
                    DrawEmoji(pad + (workCenter / 24f) * trackW,                       iconY, "👷");
                }
            }

            y += iconR * 2f + 6f;

            // Hour ticks
            int[] hours = { 0, 3, 6, 9, 12, 15, 18, 21, 24 };
            foreach (int h in hours) {
                float tx = pad + (h / 24f) * trackW;
                EditorGUI.DrawRect(new Rect(tx, y, 1, 4), TickColor);
                if (h < 24)
                    GUI.Label(new Rect(tx-13, y+4, 28, 14), $"{h:00}h",
                        Style(9, LabelColor, anchor: TextAnchor.UpperCenter));
            }
            y += 20f;
        }

        // ── Compact timeline (for inline day rows) ────────────────────
        public static void DrawTimelineCompact(float pad, float trackW, ref float y,
                List<(DateTime start, DateTime end)> sessions,
                List<CommitInfo> commits = null) {

            float trackH    = 18f;
            float trackTop  = y;
            int   numSlices = 120;

            for (int i = 0; i < numSlices; i++) {
                float t0   = i / (float)numSlices;
                float t1   = (i + 1) / (float)numSlices;
                float hour = t0 * 24f;
                EditorGUI.DrawRect(new Rect(pad + t0 * trackW, y, (t1 - t0) * trackW + 1f, trackH),
                    TimelineColorAt(hour));
            }

            EditorGUI.DrawRect(new Rect(pad, y,              trackW, 1), U_BORDER);
            EditorGUI.DrawRect(new Rect(pad, y + trackH - 1, trackW, 1), U_BORDER_HI);

            var sc = T.GetSession();
            foreach (var (start, end) in sessions) {
                float x0 = pad + (float)(start.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                float x1 = pad + (float)(end.TimeOfDay.TotalMinutes   / 1440.0) * trackW;
                float w  = Mathf.Max(x1 - x0, 3f);
                EditorGUI.DrawRect(new Rect(x0-1, y-1, w+2, trackH+2), new Color(sc.r, sc.g, sc.b, 0.18f));
                EditorGUI.DrawRect(new Rect(x0,   y,   w,   trackH),   new Color(sc.r, sc.g, sc.b, 0.55f));
                EditorGUI.DrawRect(new Rect(x0,   y,   w,   2),        new Color(1f, 1f, 1f, 0.25f));
            }

            bool hasLive = sessions.Count > 0 &&
                (DateTime.Now - sessions.Last().end).TotalMinutes < TimeTrackerCore.SESSION_GAP_MINUTES;
            if (hasLive) {
                float nowX = pad + (float)(DateTime.Now.TimeOfDay.TotalMinutes / 1440.0) * trackW;
                EditorGUI.DrawRect(new Rect(nowX - 1, y - 2, 2, trackH + 4), Color.white);
            }

            if (commits != null && commits.Count > 0)
                DrawCommitTicks(pad, trackW, y, trackH, commits);

            y += trackH + 4f;
        }

        public static void DrawBarChart(float pad, float trackW, ref float y,
                List<(DateTime date, double minutes)> dailyMinutes,
                string labelFormat = "ddd",
                System.Globalization.CultureInfo culture = null) {
            if (dailyMinutes == null || dailyMinutes.Count == 0) return;

            if (culture == null) culture = System.Globalization.CultureInfo.InvariantCulture;

            float  barAreaH = 64f;
            int    count    = dailyMinutes.Count;
            float  barW     = (trackW - (count - 1) * 4f) / count;
            double maxMins  = dailyMinutes.Max(d => d.minutes);
            if (maxMins <= 0) maxMins = 1;

            // Container
            EditorGUI.DrawRect(new Rect(pad, y, trackW, barAreaH + 30), U_BG_DARK);
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), U_BORDER);
            EditorGUI.DrawRect(new Rect(pad, y + barAreaH + 29, trackW, 1), U_BORDER);

            for (int i = 0; i < count; i++) {
                var (date, mins) = dailyMinutes[i];
                float bx      = pad + i * (barW + 4);
                float bh      = (float)(mins / maxMins) * (barAreaH - 10);
                float by      = y + barAreaH - bh;
                bool  isToday = date.Date == DateTime.Today;

                EditorGUI.DrawRect(new Rect(bx, y+4, barW, barAreaH-10), new Color(1f,1f,1f,0.03f));
                if (mins > 0)
                    EditorGUI.DrawRect(new Rect(bx, by, barW, bh), isToday ? AccentColor : SessionDim);

                GUI.Label(new Rect(bx, y+barAreaH-2, barW, 14),
                    date.ToString(labelFormat, culture).ToUpper(),
                    Style(9, isToday ? BrightColor : LabelColor, anchor: TextAnchor.UpperCenter));
                if (mins > 15)
                    GUI.Label(new Rect(bx, by-14, barW, 14),
                        $"{(int)(mins/60)}h", Style(8, BrightColor, anchor: TextAnchor.UpperCenter));
                if (isToday)
                    EditorGUI.DrawRect(new Rect(bx, y+barAreaH+14, barW, 2), AccentColor);
            }
            y += barAreaH + 32f;
        }

        public static void DrawSessionRow(float pad, float trackW, ref float y,
                DateTime start, DateTime end, bool isLast) {
            bool   isLive = isLast && (DateTime.Now - end).TotalMinutes < TimeTrackerCore.SESSION_GAP_MINUTES;
            double mins   = (end - start).TotalMinutes;

            // Row background — alternating Unity inspector style
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 20), new Color(1f, 1f, 1f, 0.02f));
            EditorGUI.DrawRect(new Rect(pad, y + 5, 4, 4), isLive ? AccentColor : SessionDim);
            GUI.Label(new Rect(pad+14,  y, 130, 18), $"{start:HH:mm} → {end:HH:mm}", Style(11, BrightColor));
            GUI.Label(new Rect(pad+150, y,  80, 18), TimeTrackerCore.FormatDuration(mins),
                Style(11, isLive ? AccentColor : TextColor));
            if (isLive)
                GUI.Label(new Rect(pad+240, y+2, 50, 14), "● LIVE", Style(9, AccentColor, FontStyle.Bold));
            y += 20f;
        }

        public static void DrawStatsGrid(float pad, float trackW, ref float y, PeriodStats ps) {
            float cardW = (trackW - 8) / 2f;
            float cardH = 54f;

            var ic = System.Globalization.CultureInfo.InvariantCulture;

            DrawStatCard(new Rect(pad,         y, cardW, cardH), "AVG / DAY",        TimeTrackerCore.FormatDuration(ps.avgMinutesPerDay));
            DrawStatCard(new Rect(pad+cardW+8, y, cardW, cardH), "AVG / ACTIVE DAY", TimeTrackerCore.FormatDuration(ps.avgMinutesPerActiveDay));
            y += cardH + 8f;

            string longestSub = ps.longestSession.start != default
                ? ps.longestSession.start.ToString("ddd dd HH:mm", ic)
                : "";
            DrawStatCard(new Rect(pad,         y, cardW, cardH), "LONGEST SESSION",  TimeTrackerCore.FormatDuration(ps.longestSessionMinutes), longestSub);
            DrawStatCard(new Rect(pad+cardW+8, y, cardW, cardH), "SHORTEST SESSION", TimeTrackerCore.FormatDuration(ps.shortestSessionMinutes));
            y += cardH + 8f;

            string earliest = ps.earliestStartMinutes >= 0 ? $"{(int)(ps.earliestStartMinutes/60):00}:{(int)(ps.earliestStartMinutes%60):00}" : "--";
            string latest   = ps.latestEndMinutes    >= 0 ? $"{(int)(ps.latestEndMinutes   /60):00}:{(int)(ps.latestEndMinutes   %60):00}" : "--";
            DrawStatCard(new Rect(pad,         y, cardW, cardH), "EARLIEST START", earliest);
            DrawStatCard(new Rect(pad+cardW+8, y, cardW, cardH), "LATEST END",     latest);
            y += cardH + 8f;
        }
    }
}
