// TaskManagerPanel.cs — Kanban board panel drawn inside the main window
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityTimeTracker {

    public class TaskManagerPanel {

        // ── State ─────────────────────────────────────────────────────────────
        TaskBoardData board;

        // Editing
        TrackerTask editingTask;
        bool        isCreatingTask;
        string      newTaskTitle   = "";
        string      newTaskDueDate = "";
        string      newTaskDesc    = "";

        // Tag creation
        bool   isCreatingTag  = false;
        string newTagLabel    = "";
        Color  newTagColor    = new Color(0.35f, 0.60f, 0.95f, 1f);

        // Status creation
        bool   isCreatingStatus = false;
        string newStatusLabel   = "";
        Color  newStatusColor   = new Color(0.60f, 0.40f, 0.80f, 1f);

        // Scroll
        Vector2 kanbanScroll;
        string  hoveredTaskId = "";
        Vector2 detailScroll;
        Vector2 settingsScroll;

        // Sub-tab for task manager
        bool showTaskSettings = false;

        // Column config
        const float COL_GAP   = 8f;
        const float COL_MIN_W = 250f;
        float colWidthOverride = 0f; // 0 = auto, >=10 = manual
        
        // Card layout constants
        const float CARD_PAD_H = 8f;
        const float CARD_PAD_X = 10f;
        const float LINE_H     = 16f;
        const float TAG_CHIP_H = 14f;
        const float NAV_ROW_H  = 20f;

        
        
        System.Action repaint;

        // ── Media caches ──────────────────────────────────────────────────────
        // key: task.id → Texture2D fetched from URL in description
        readonly Dictionary<string, Texture2D>    _urlImageCache   = new Dictionary<string, Texture2D>();
        readonly HashSet<string>                   _urlFetching     = new HashSet<string>();
        // key: guid → preview texture from AssetPreview
        readonly Dictionary<string, Texture2D>    _assetPreviewCache = new Dictionary<string, Texture2D>();


        static readonly Regex UrlRegex = new Regex(
            @"https?://[^\s""'<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly HashSet<string> ImageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

        // Working On color override (yellow)
        static readonly Color WORKING_ON_COL = new Color(1.00f, 0.78f, 0.20f, 1f);

        // Brighter text — less dim than LabelColor
        static Color BrightText => new Color(
            TimeTrackerGUI.TextColor.r,
            TimeTrackerGUI.TextColor.g,
            TimeTrackerGUI.TextColor.b,
            0.82f);

        // ── Init ──────────────────────────────────────────────────────────────

        public void Init(System.Action repaintCallback) {
            repaint = repaintCallback;
            Reload();
        }

        public void Reload() {
            board = TaskManagerCore.LoadBoard();
            EnsureDefaultTags();
        }

        void EnsureDefaultTags() {
            if (!board.tags.Any(t => t.id == "default_tag" || t.label == "Default")) {
                var def = new TaskTag { id = "default_tag", label = "Default" };
                def.SetColor(new Color(0.55f, 0.55f, 0.60f, 1f));
                board.tags.Insert(0, def);
            }
            if (!board.tags.Any(t => t.id == "bugs_default" || t.label == "Bugs")) {
                var bugs = new TaskTag { id = "bugs_default", label = "Bugs" };
                bugs.SetColor(new Color(0.20f, 0.78f, 0.35f, 1f));
                board.tags.Insert(1, bugs);
            }

            // Ensure every task has at least one valid tag — assign "default_tag" if missing
            bool dirty = false;
            foreach (var task in board.tasks) {
                bool hasValidTag = task.tagIds != null &&
                                   task.tagIds.Any(id => board.tags.Any(t => t.id == id));
                if (!hasValidTag) {
                    if (task.tagIds == null) task.tagIds = new System.Collections.Generic.List<string>();
                    task.tagIds.Clear();
                    task.tagIds.Add("default_tag");
                    dirty = true;
                }
            }

            // Also default lastTagId so new tasks always get a tag pre-selected
            if (string.IsNullOrEmpty(TaskManagerCore.UIState.lastTagId))
                TaskManagerCore.UIState.lastTagId = "default_tag";

            // Ensure assetRefs is never null
            foreach (var t2 in board.tasks)
                if (t2.assetRefs == null) t2.assetRefs = new System.Collections.Generic.List<TaskAssetRef>();

            if (dirty) TaskManagerCore.SaveUIState();
            TaskManagerCore.SaveBoard(board);
        }

        void Save() {
            TaskManagerCore.SaveBoard(board);
            TaskManagerCore.SaveUIState();
        }

        // ── Main draw ─────────────────────────────────────────────────────────

        public void Draw(float pad, float y, float windowW, float windowH) {
            float trackW = windowW - pad * 2;

            DrawTopBar(pad, ref y, trackW);

            if (editingTask != null) {
                DrawTaskDetail(pad, ref y, trackW, windowH - y - 8f);
                return;
            }

            if (showTaskSettings) {
                DrawTaskSettings(pad, ref y, trackW, windowH - y - 8f);
                return;
            }

            if (isCreatingTask) {
                DrawNewTaskModal(windowW, windowH);
                return;
            }

            float colW = colWidthOverride >= 10f
                ? colWidthOverride
                : Mathf.Max(COL_MIN_W,
                    (trackW - (board.statuses.Count - 1) * COL_GAP) / board.statuses.Count);

            float contentW = board.statuses.Count * (colW + COL_GAP);
            float contentH = EstimateContentHeight();

            Rect scrollView = new Rect(pad, y, trackW, windowH - y - 8f);
            Rect content    = new Rect(0, 0, contentW, contentH);
            kanbanScroll    = GUI.BeginScrollView(scrollView, kanbanScroll, content);
            DrawKanban(0, 0, colW, contentH);
            GUI.EndScrollView();

            // ── Slider abajo a la derecha, flotando sobre el kanban ───────────────
            // ── Slider abajo a la derecha, flotando sobre el kanban ───────────────
            float sliderW  = 100f;
            float widgetW  = sliderW + 52f;
            float widgetH  = 20f;
            float wx       = pad + trackW - widgetW - 6f;
            float wy       = windowH - widgetH - 32f;

            // Label valor
            string valLabel = colWidthOverride < 10f ? "Auto" : $"{(int)colWidthOverride}px";
            GUI.Label(new Rect(wx, wy + 3f, 36f, 14f), valLabel,
                TimeTrackerGUI.Style(9, TimeTrackerGUI.AccentColor));

            // Slider
            float newOverride = GUI.HorizontalSlider(
                new Rect(wx + 38f, wy + 5f, sliderW, 12f),
                colWidthOverride, 0f, 500f);
            if (Mathf.Abs(newOverride - colWidthOverride) > 0.5f) {
                colWidthOverride = newOverride;
                repaint?.Invoke();
            }

            // Botón Auto (solo visible cuando hay override activo)
            if (colWidthOverride >= 10f) {
                Rect resetR = new Rect(wx + 38f + sliderW + 4f, wy + 2f, 34f, 16f);
                EditorGUI.DrawRect(resetR, TimeTrackerGUI.BgDark);
                GUI.Label(resetR, "Auto",
                    TimeTrackerGUI.Style(9, BrightText, anchor: TextAnchor.MiddleCenter));
                if (GUI.Button(resetR, GUIContent.none, GUIStyle.none)) {
                    colWidthOverride = 0f;
                    repaint?.Invoke();
                }
            }
        }


        // ── Top bar ───────────────────────────────────────────────────────────

void DrawTopBar(float pad, ref float y, float trackW)
{
    var ui = TaskManagerCore.UIState;

    GUI.Label(new Rect(pad, y + 4, 50, 16), "FILTER",
        TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));

    float tx = pad + 56f;

    // All chip
    bool allActive = string.IsNullOrEmpty(ui.activeFilterTag);
    DrawFilterChip(ref tx, y, "All", null, allActive, () => {
        ui.activeFilterTag = "";
        TaskManagerCore.SaveUIState();
    });

    // Tag chips
    foreach (var tag in board.tags) {
        bool active = ui.activeFilterTag == tag.id;
        var  cap    = tag;
        DrawFilterChip(ref tx, y, tag.label, tag.GetColor(), active, () => {
            ui.activeFilterTag = active ? "" : cap.id;
            TaskManagerCore.SaveUIState();
        });
    }

    // + Tag
    if (!isCreatingTag) {
        if (DrawSmallButton(ref tx, y, "+ Tag")) {
            isCreatingTag = true;
            newTagLabel   = "";
            newTagColor   = new Color(
                UnityEngine.Random.Range(0.25f, 0.85f),
                UnityEngine.Random.Range(0.25f, 0.85f),
                UnityEngine.Random.Range(0.25f, 0.85f), 1f);
        }
    } else {
        DrawTagCreator(pad, ref y, trackW);
        return;
    }

    // + Column
    tx += 4f;
    if (DrawSmallButton(ref tx, y, "+ Column")) {
        isCreatingStatus = true;
        newStatusLabel   = "";
    }

    // ⚙ gear — far right
    Rect gearR = new Rect(pad + trackW - 28f, y, 28f, 24f);

    // + New Task
    float btnW = 92f;
    Rect  addR = new Rect(pad + trackW - 28f - 4f - btnW, y, btnW, 24f);
    EditorGUI.DrawRect(addR, TimeTrackerGUI.AccentColor);
    GUI.Label(addR, "+ New Task",
        TimeTrackerGUI.Style(11, TimeTrackerGUI.BgColor, FontStyle.Bold, TextAnchor.MiddleCenter));
    if (GUI.Button(addR, GUIContent.none, GUIStyle.none)) {
        isCreatingTask = true;
        newTaskTitle   = "";
        newTaskDueDate = DateTime.Today.ToString("yyyy-MM-dd");
        newTaskDesc    = "";
        if (string.IsNullOrEmpty(TaskManagerCore.UIState.lastTagId))
            TaskManagerCore.UIState.lastTagId = "default_tag";
    }

    Color gearBg = showTaskSettings
        ? new Color(TimeTrackerGUI.AccentColor.r, TimeTrackerGUI.AccentColor.g,
                    TimeTrackerGUI.AccentColor.b, 0.25f)
        : TimeTrackerGUI.BgDark;
    EditorGUI.DrawRect(gearR, gearBg);
    GUI.Label(gearR, "⚙",
        TimeTrackerGUI.Style(13, showTaskSettings ? TimeTrackerGUI.AccentColor : BrightText,
            anchor: TextAnchor.MiddleCenter));
    if (GUI.Button(gearR, GUIContent.none, GUIStyle.none)) {
        showTaskSettings = !showTaskSettings;
        editingTask      = null;
    }

    y += 32f;

    if (isCreatingStatus)
        DrawStatusCreator(pad, ref y, trackW);

    EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), TimeTrackerGUI.DivColor);
    y += 8f;
}
        // ── Kanban columns ────────────────────────────────────────────────────

        void DrawKanban(float x, float y, float colW, float contentH) {
            string filter = TaskManagerCore.UIState.activeFilterTag;

            for (int ci = 0; ci < board.statuses.Count; ci++) {
                var   status = board.statuses[ci];
                float colX   = x + ci * (colW + COL_GAP);
                Color stCol  = GetStatusColor(status);

                EditorGUI.DrawRect(new Rect(colX, y, colW, contentH - 4), TimeTrackerGUI.BgDark);
                EditorGUI.DrawRect(new Rect(colX, y, colW, 4), stCol);

                var tasks = TaskManagerCore.GetTasksForStatus(board, status.id, filter);

                // Header
                GUI.Label(new Rect(colX + 10, y + 10, colW - 50, 18),
                    status.label.ToUpper(),
                    TimeTrackerGUI.Style(10, TimeTrackerGUI.TextColor, FontStyle.Bold));
                GUI.Label(new Rect(colX + colW - 42, y + 12, 36, 14),
                    tasks.Count.ToString(),
                    TimeTrackerGUI.Style(10, stCol, anchor: TextAnchor.UpperRight));

                float cardY = y + 36f;
                foreach (var task in tasks) {
                    float cardH = MeasureCard(task);
                    DrawCard(task, colX, cardY, colW, cardH, ci);
                    cardY += cardH + 6f;
                }

                if (tasks.Count == 0)
                    GUI.Label(new Rect(colX + 10, y + 52, colW - 20, 18), "No tasks",
                        TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, anchor: TextAnchor.UpperCenter));
            }
        }

        // ── Media helpers ─────────────────────────────────────────────────────

        // Returns the first URL found in description, or null.
        string ExtractUrl(string description) {
            if (string.IsNullOrEmpty(description)) return null;
            var m = UrlRegex.Match(description);
            return m.Success ? m.Value : null;
        }

        bool IsImageUrl(string url) {
            if (string.IsNullOrEmpty(url)) return false;
            string lower = url.ToLower().Split('?')[0];
            foreach (var ext in ImageExts)
                if (lower.EndsWith(ext)) return true;
            return false;
        }

        // Kick off async fetch if not cached; returns null while loading.
        Texture2D GetUrlImage(string taskId, string url) {
            if (_urlImageCache.TryGetValue(taskId, out var cached)) return cached;
            if (_urlFetching.Contains(taskId)) return null;
            _urlFetching.Add(taskId);
            var req = UnityWebRequestTexture.GetTexture(url);
            var op  = req.SendWebRequest();
            op.completed += _ => {
                _urlFetching.Remove(taskId);
                if (req.result == UnityWebRequest.Result.Success) {
                    _urlImageCache[taskId] = DownloadHandlerTexture.GetContent(req);
                    repaint?.Invoke();
                }
                req.Dispose();
            };
            return null;
        }

        // Returns the best available icon for an asset GUID.
        // Uses GetMiniThumbnail first (instant, file-type icon) as fallback,
        // then requests GetAssetPreview (async, actual content preview).
        Texture2D GetAssetPreview(string guid) {
            if (string.IsNullOrEmpty(guid)) return null;
            if (_assetPreviewCache.TryGetValue(guid, out var cached) && cached != null) return cached;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj == null) return null;
            // Try full preview first (textures, prefabs, materials show content)
            var preview = AssetPreview.GetAssetPreview(obj);
            if (preview != null) { _assetPreviewCache[guid] = preview; return preview; }
            // If preview not ready yet, request it and return mini-thumb for now
            AssetPreview.SetPreviewTextureCacheSize(64);
            // Mini-thumbnail: instant, always available — shows the Unity file-type icon
            var thumb = AssetPreview.GetMiniThumbnail(obj);
            if (thumb != null) {
                // Don't cache mini-thumb permanently — check again next frame for real preview
                repaint?.Invoke();
                return thumb;
            }
            return null;
        }

        // ── Card height ───────────────────────────────────────────────────────

        const float CARD_IMG_SIZE = 44f; // thumbnail square side

        bool CardHasMedia(TrackerTask task) {
            string url = ExtractUrl(task.description);
            if (!string.IsNullOrEmpty(url)) return true;
            if (task.assetRefs != null && task.assetRefs.Count > 0) return true;
            return false;
        }

        bool CardHasAssetRef(TrackerTask task) =>
            task.assetRefs != null && task.assetRefs.Count > 0 && string.IsNullOrEmpty(ExtractUrl(task.description));

        int GetDescLines(TrackerTask task, float availW) {
            if (string.IsNullOrEmpty(task.description)) return 0;
            int charsPerLine = Mathf.Max(1, (int)(availW / 6.5f));
            return Mathf.Clamp(Mathf.CeilToInt((float)task.description.Length / charsPerLine), 1, 3);
        }
        
        float MeasureCard(TrackerTask task) {
            bool  hasMedia    = CardHasMedia(task);
            bool  hasAssetRow = CardHasAssetRef(task);
            float minH        = hasMedia ? CARD_IMG_SIZE + CARD_PAD_H * 2f : 0f;

            float availW  = hasMedia
                ? COL_MIN_W - CARD_IMG_SIZE - CARD_PAD_X * 3 - 6f
                : COL_MIN_W - CARD_PAD_X * 2;
            int descLines = GetDescLines(task, availW);

            float rh = CARD_PAD_H;
            rh += LINE_H;                                              // título
            rh += (LINE_H - 2f) * Mathf.Max(1, descLines);           // descripción (1-3 líneas) o due date
            if (hasAssetRow) rh += LINE_H - 2f;                       // asset ref
            rh += CARD_PAD_H;

            return Mathf.Max(minH, rh);
        }

        // ── Card drawing ──────────────────────────────────────────────────────

        void DrawCard(TrackerTask task, float colX, float cardY, float colW, float cardH, int colIdx) {
            // Safety-net: task must always have at least one valid tag
            if (task.tagIds == null || !task.tagIds.Any(id => board.tags.Any(t => t.id == id))) {
                if (task.tagIds == null) task.tagIds = new List<string>();
                task.tagIds.Clear();
                task.tagIds.Add("default_tag");
                TaskManagerCore.SaveBoard(board);
            }

            Rect cardRect = new Rect(colX + 5, cardY, colW - 10, cardH);

            var  evt     = Event.current;
            bool hovered = hoveredTaskId == task.id;
            if (evt.type == EventType.MouseMove || evt.type == EventType.MouseDrag) {
                bool over = cardRect.Contains(evt.mousePosition);
                if (over && hoveredTaskId != task.id)  { hoveredTaskId = task.id; repaint?.Invoke(); }
                else if (!over && hoveredTaskId == task.id) { hoveredTaskId = ""; repaint?.Invoke(); }
            }

            // Card background
            Color bg = hovered
                ? new Color(TimeTrackerGUI.BgCard.r + 0.05f,
                            TimeTrackerGUI.BgCard.g + 0.05f,
                            TimeTrackerGUI.BgCard.b + 0.07f)
                : TimeTrackerGUI.BgCard;
            EditorGUI.DrawRect(cardRect, bg);

            // Status accent — top border
            var stColor = GetStatusColor(TaskManagerCore.GetStatus(board, task.statusId));
            EditorGUI.DrawRect(new Rect(colX + 5, cardY, colW - 10, 2), stColor);

            // ── Left media column ─────────────────────────────────────────────
            bool   hasMedia  = CardHasMedia(task);
            float  imgSize   = CARD_IMG_SIZE;
            float  imgX      = colX + 5 + CARD_PAD_X - 2f;
            float  imgY      = cardY + (cardH - imgSize) * 0.5f;
            float  contentX  = hasMedia ? imgX + imgSize + 6f : colX + 5 + CARD_PAD_X;
            float  contentW  = hasMedia
                                ? (colX + colW - 10) - contentX - CARD_PAD_X
                                : colW - 10 - CARD_PAD_X * 2;

            if (hasMedia) {
                Rect imgRect = new Rect(imgX, imgY, imgSize, imgSize);
                string url = ExtractUrl(task.description);

                if (!string.IsNullOrEmpty(url)) {
                    if (IsImageUrl(url)) {
                        // URL image
                        var tex = GetUrlImage(task.id, url);
                        if (tex != null)
                            GUI.DrawTexture(imgRect, tex, ScaleMode.ScaleAndCrop);
                        else
                            DrawMediaPlaceholder(imgRect, "⏳");
                    } else {
                        // Non-image URL — show link icon + domain
                        DrawMediaPlaceholder(imgRect, "🔗");
                        string domain = "";
                        try { domain = new Uri(url).Host; } catch { domain = "link"; }
                        // Click opens browser — registered before clickBody
                        if (GUI.Button(imgRect, GUIContent.none, GUIStyle.none))
                            Application.OpenURL(url);
                        GUI.Label(new Rect(imgX, imgY + imgSize - 14f, imgSize, 12),
                            domain, TimeTrackerGUI.Style(7, BrightText, anchor: TextAnchor.MiddleCenter));
                    }
                } else if (task.assetRefs != null && task.assetRefs.Count > 0) {
                    // First asset reference
                    var aref = task.assetRefs[0];
                    var preview = GetAssetPreview(aref.guid);
                    if (preview != null)
                        GUI.DrawTexture(imgRect, preview, ScaleMode.ScaleAndCrop);
                    else
                        DrawMediaPlaceholder(imgRect, "📄");

                    // Click pings asset in Project window — registered before clickBody
                    if (GUI.Button(imgRect, GUIContent.none, GUIStyle.none)) {
                        string path = AssetDatabase.GUIDToAssetPath(aref.guid);
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (obj != null) {
                            EditorGUIUtility.PingObject(obj);
                            Selection.activeObject = obj;
                        }
                    }
                }
            }

            // ── Right content column ──────────────────────────────────────────
            // Row 1: Title  |  Tag  |  Arrows (on hover)
            float row1Y = cardY + CARD_PAD_H + 2f;
            float arrowsW = 0f;
            bool  canLeft  = colIdx > 0;
            bool  canRight = colIdx < board.statuses.Count - 1;
            if (hovered) arrowsW = (canLeft ? 21f : 0f) + (canRight ? 21f : 0f) + 2f;

            // Title row — background tinted with first tag color
            var   firstTag  = task.tagIds != null && task.tagIds.Count > 0
                              ? TaskManagerCore.GetTag(board, task.tagIds[0]) : null;
            Color tagTint   = firstTag != null
                              ? new Color(firstTag.GetColor().r, firstTag.GetColor().g,
                                          firstTag.GetColor().b, 0.28f)
                              : new Color(1f, 1f, 1f, 0.04f);
            float titleRowH = LINE_H + 2f;
            float titleW    = contentW - arrowsW - 4f;
            Rect  titleRow  = new Rect(contentX - 2f, row1Y - 2f, titleW + 2f, titleRowH);
            EditorGUI.DrawRect(titleRow, tagTint);
            GUI.Label(new Rect(contentX + 2f, row1Y, titleW - 4f, LINE_H),
                task.title, TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor, FontStyle.Bold));

            // Row 2: description o due date
            float row2Y   = row1Y + LINE_H + 1f;
            int   dLines  = GetDescLines(task, contentW);
            float descH   = (LINE_H - 2f) * Mathf.Max(1, dLines);

            if (!string.IsNullOrEmpty(task.description)) {
                int maxChars = Mathf.Max(1, (int)(contentW / 6.5f)) * 3;
                string descText = task.description.Length > maxChars
                    ? task.description.Substring(0, maxChars - 1) + "…"
                    : task.description;
                var descStyle      = TimeTrackerGUI.Style(9, BrightText);
                descStyle.wordWrap = true;
                descStyle.clipping = TextClipping.Clip;
                GUI.Label(new Rect(contentX, row2Y, contentW, descH), descText, descStyle);
            } else if (task.HasDueDate) {
                bool  overdue  = task.IsOverdue;
                bool  dueToday = task.IsDueToday;
                Color dueCol   = overdue  ? new Color(0.95f, 0.30f, 0.30f, 1f)
                    : dueToday ? new Color(1.00f, 0.75f, 0.20f, 1f)
                    : BrightText;
                GUI.Label(new Rect(contentX, row2Y, contentW, LINE_H - 2),
                    dueToday ? "📅 today" : overdue ? $"📅 ⚠ {task.dueDate}" : $"📅 {task.dueDate}",
                    TimeTrackerGUI.Style(9, dueCol));
            }
            // Row 3: asset ref — posicionado DESPUÉS de la altura real de la descripción
            if (CardHasAssetRef(task)) {
                float row3Y  = row2Y + descH;   // <-- antes era row2Y + LINE_H - 2f (fijo)
                var   aref   = task.assetRefs[0];
                var   prev3  = GetAssetPreview(aref.guid);
                float iconSz = 12f;
                if (prev3 != null)
                    GUI.DrawTexture(new Rect(contentX, row3Y + 1f, iconSz, iconSz), prev3, ScaleMode.ScaleToFit);
                else
                    GUI.Label(new Rect(contentX, row3Y, iconSz + 2f, LINE_H - 2),
                        "📄", TimeTrackerGUI.Style(8, BrightText));
                string refLabel = string.IsNullOrEmpty(aref.label) ? "asset" : aref.label;
                GUI.Label(new Rect(contentX + iconSz + 3f, row3Y, contentW - iconSz - 3f, LINE_H - 2),
                    refLabel, TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
                if (GUI.Button(new Rect(contentX, row3Y, contentW, LINE_H - 2), GUIContent.none, GUIStyle.none)) {
                    string apath = AssetDatabase.GUIDToAssetPath(aref.guid);
                    var aobj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(apath);
                    if (aobj != null) { EditorGUIUtility.PingObject(aobj); Selection.activeObject = aobj; }
                }
            }

            // ── Nav arrows — top-right, registered BEFORE clickBody ───────────
            if (hovered) {
                const float navBtnW = 18f;
                const float navBtnH = 16f;
                float btnTop = row1Y;
                float rightX = colX + colW - 10 - 4f;

                if (canRight) {
                    Rect rr = new Rect(rightX - navBtnW, btnTop, navBtnW, navBtnH);
                    EditorGUI.DrawRect(rr, new Color(1f, 1f, 1f, 0.18f));
                    GUI.Label(rr, "▶", TimeTrackerGUI.Style(9, BrightText, anchor: TextAnchor.MiddleCenter));
                    if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) {
                        TaskManagerCore.MoveTask(board, task, board.statuses[colIdx + 1].id);
                        Save(); repaint?.Invoke();
                    }
                    rightX -= navBtnW + 3f;
                }
                if (canLeft) {
                    Rect lr = new Rect(rightX - navBtnW, btnTop, navBtnW, navBtnH);
                    EditorGUI.DrawRect(lr, new Color(1f, 1f, 1f, 0.18f));
                    GUI.Label(lr, "◀", TimeTrackerGUI.Style(9, BrightText, anchor: TextAnchor.MiddleCenter));
                    if (GUI.Button(lr, GUIContent.none, GUIStyle.none)) {
                        TaskManagerCore.MoveTask(board, task, board.statuses[colIdx - 1].id);
                        Save(); repaint?.Invoke();
                    }
                }
            }

            // ── Clickable body — LAST so media/arrow buttons win ──────────────
            Rect clickBody = new Rect(hasMedia ? contentX : colX + 5, cardY,
                                      hasMedia ? contentW  : colW - 10, cardH);
            if (GUI.Button(clickBody, GUIContent.none, GUIStyle.none)) {
                editingTask  = task;
                detailScroll = Vector2.zero;
                repaint?.Invoke();
            }
        }

        void DrawMediaPlaceholder(Rect r, string icon) {
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.25f));
            GUI.Label(r, icon, TimeTrackerGUI.Style(18, BrightText, anchor: TextAnchor.MiddleCenter));
        }

        // ── Task detail ───────────────────────────────────────────────────────

        void DrawTaskDetail(float pad, ref float y, float trackW, float scrollH) {
            if (editingTask == null) return;

            // ‹ back
            if (GUI.Button(new Rect(pad, y, 22, 22), "‹",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 14, normal = { textColor = TimeTrackerGUI.TextColor } })) {
                Save(); editingTask = null; repaint?.Invoke(); return;
            }

            GUI.Label(new Rect(pad + 30, y + 3, trackW - 130, 18), "TASK DETAIL",
                TimeTrackerGUI.Style(12, TimeTrackerGUI.LabelColor, FontStyle.Bold));

            // Delete — red bg, white text
            Rect delR = new Rect(pad + trackW - 72, y, 72, 22);
            EditorGUI.DrawRect(delR, new Color(0.80f, 0.18f, 0.18f, 1f));
            GUI.Label(delR, "🗑 Delete",
                TimeTrackerGUI.Style(10, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter));
            if (GUI.Button(delR, GUIContent.none, GUIStyle.none)) {
                TaskManagerCore.DeleteTask(board, editingTask);
                Save(); editingTask = null; repaint?.Invoke(); return;
            }

            y += 32f;
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), TimeTrackerGUI.DivColor);
            y += 10f;

            Rect scrollRect = new Rect(pad, y, trackW, scrollH);
            Rect content    = new Rect(0, 0, trackW - 16f, 900f);
            detailScroll    = GUI.BeginScrollView(scrollRect, detailScroll, content);

            float sy = 0f;
            float cw = content.width;
            var   evt = Event.current;

            // Title
            SectionLabel(0, sy, "TITLE"); sy += 16f;
            var newTitle = EditorGUI.TextField(new Rect(0, sy, cw, 20),
                editingTask.title, new GUIStyle(EditorStyles.textField) { fontSize = 12 });
            if (newTitle != editingTask.title) { editingTask.title = newTitle; Save(); }
            sy += 28f;

            // Description
            SectionLabel(0, sy, "DESCRIPTION"); sy += 16f;
            var newDesc = EditorGUI.TextArea(new Rect(0, sy, cw, 54),
                editingTask.description,
                new GUIStyle(EditorStyles.textArea) { fontSize = 11, wordWrap = true });
            if (newDesc != editingTask.description) { editingTask.description = newDesc; Save(); }
            sy += 62f;

            // Status
            SectionLabel(0, sy, "STATUS"); sy += 16f;
            float sbW = (cw - (board.statuses.Count - 1) * 6f) / board.statuses.Count;
            for (int i = 0; i < board.statuses.Count; i++) {
                var  st  = board.statuses[i];
                bool sel = editingTask.statusId == st.id;
                Rect r   = new Rect(i * (sbW + 6f), sy, sbW, 24);
                Color stCol = GetStatusColor(st);
                EditorGUI.DrawRect(r, sel ? stCol : TimeTrackerGUI.BgDark);
                GUI.Label(r, st.label,
                    TimeTrackerGUI.Style(10, sel ? TimeTrackerGUI.BgColor : BrightText,
                        FontStyle.Bold, TextAnchor.MiddleCenter));
                if (!sel && GUI.Button(r, GUIContent.none, GUIStyle.none)) { TaskManagerCore.MoveTask(board, editingTask, st.id); Save(); }
            }
            sy += 32f;

            // Due date
            SectionLabel(0, sy, "DUE DATE"); sy += 16f;
            var newDue = EditorGUI.TextField(new Rect(0, sy, 110, 18),
                editingTask.dueDate, new GUIStyle(EditorStyles.textField) { fontSize = 11 });
            if (newDue != editingTask.dueDate) { editingTask.dueDate = newDue; Save(); }

            float dbx = 116f;
            (string lbl, int delta)[] dateBtns = {
                ("Today", 0), ("+1 Day", 1), ("+1 Week", 7), ("-1 Day", -1), ("-1 Week", -7)
            };
            foreach (var (dl, dd) in dateBtns) {
                float dw = dl.Length * 6.5f + 8f;
                Color dc = dd == 0 ? TimeTrackerGUI.AccentColor : BrightText;
                if (GUI.Button(new Rect(dbx, sy, dw, 18), dl,
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 9, normal = { textColor = dc } })) {
                    if (dd == 0) editingTask.dueDate = DateTime.Today.ToString("yyyy-MM-dd");
                    else {
                        DateTime.TryParse(editingTask.dueDate, out var cur);
                        if (cur == default) cur = DateTime.Today;
                        editingTask.dueDate = cur.AddDays(dd).ToString("yyyy-MM-dd");
                    }
                    Save();
                }
                dbx += dw + 4f;
            }
            sy += 28f;

            // Tags
            SectionLabel(0, sy, "TAGS"); sy += 16f;
            float tagX = 0f;
            foreach (var tag in board.tags) {
                bool  has    = editingTask.tagIds.Contains(tag.id);
                Color chipBg = has ? tag.GetColor() : TimeTrackerGUI.BgDark;
                Color chipTx = has ? TimeTrackerGUI.BgColor : BrightText;
                float chipW  = Mathf.Max(48f, tag.label.Length * 7f + 16f);
                if (tagX + chipW > cw) { tagX = 0; sy += 24f; }
                Rect cr = new Rect(tagX, sy, chipW, 20);
                EditorGUI.DrawRect(cr, chipBg);
                GUI.Label(cr, tag.label,
                    TimeTrackerGUI.Style(9, chipTx, anchor: TextAnchor.MiddleCenter));
                var cap = tag;
                if (GUI.Button(cr, GUIContent.none, GUIStyle.none)) {
                    if (has) {
                        if (editingTask.tagIds.Count > 1) { editingTask.tagIds.Remove(cap.id); Save(); }
                    } else {
                        editingTask.tagIds.Add(cap.id);
                        TaskManagerCore.UIState.lastTagId = cap.id;
                        Save();
                    }
                }
                tagX += chipW + 6f;
            }
            sy += 28f;

            // ── Asset references ──────────────────────────────────────────────
            SectionLabel(0, sy, "ASSET REFERENCES"); sy += 16f;

            // Native Unity object field — accepts any asset, drag & drop included
            var picked = EditorGUI.ObjectField(
                new Rect(0, sy, cw, 18),
                GUIContent.none,
                null,
                typeof(UnityEngine.Object),
                allowSceneObjects: false);
            if (picked != null) {
                string pickedPath = AssetDatabase.GetAssetPath(picked);
                string pickedGuid = AssetDatabase.AssetPathToGUID(pickedPath);
                if (!string.IsNullOrEmpty(pickedGuid)) {
                    if (editingTask.assetRefs == null)
                        editingTask.assetRefs = new List<TaskAssetRef>();
                    if (!editingTask.assetRefs.Any(r2 => r2.guid == pickedGuid)) {
                        editingTask.assetRefs.Add(new TaskAssetRef {
                            guid  = pickedGuid,
                            label = picked.name
                        });
                        Save();
                        repaint?.Invoke();
                    }
                }
            }
            sy += 24f;

            // List existing refs
            if (editingTask.assetRefs != null && editingTask.assetRefs.Count > 0) {
                TaskAssetRef toRemove = null;
                foreach (var aref in editingTask.assetRefs) {
                    Rect row = new Rect(0, sy, cw, 26f);
                    EditorGUI.DrawRect(row, TimeTrackerGUI.BgDark);

                    // Tiny preview
                    var preview = GetAssetPreview(aref.guid);
                    if (preview != null)
                        GUI.DrawTexture(new Rect(4, sy + 3, 20, 20), preview, ScaleMode.ScaleToFit);
                    else
                        GUI.Label(new Rect(4, sy + 4, 20, 18), "📄",
                            TimeTrackerGUI.Style(11, BrightText, anchor: TextAnchor.MiddleCenter));

                    // Label — click pings
                    string displayName = string.IsNullOrEmpty(aref.label) ? aref.guid : aref.label;
                    GUI.Label(new Rect(28, sy + 5, cw - 60, 16),
                        displayName, TimeTrackerGUI.Style(10, TimeTrackerGUI.TextColor));
                    if (GUI.Button(new Rect(28, sy, cw - 60, 26), GUIContent.none, GUIStyle.none)) {
                        string path = AssetDatabase.GUIDToAssetPath(aref.guid);
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (obj != null) { EditorGUIUtility.PingObject(obj); Selection.activeObject = obj; }
                    }

                    // ✕ remove
                    Rect removeR = new Rect(cw - 22, sy + 4, 18, 18);
                    EditorGUI.DrawRect(removeR, new Color(0.7f, 0.15f, 0.15f, 0.8f));
                    GUI.Label(removeR, "✕",
                        TimeTrackerGUI.Style(8, Color.white, anchor: TextAnchor.MiddleCenter));
                    if (GUI.Button(removeR, GUIContent.none, GUIStyle.none))
                        toRemove = aref;

                    sy += 30f;
                }
                if (toRemove != null) {
                    editingTask.assetRefs.Remove(toRemove);
                    Save(); repaint?.Invoke();
                }
                sy += 4f;
            }

            // Footer
            EditorGUI.DrawRect(new Rect(0, sy, cw, 1), TimeTrackerGUI.DivColor); sy += 10f;
            string comp = !string.IsNullOrEmpty(editingTask.completedAt)
                ? $"  ·  Done: {editingTask.completedAt}" : "";
            GUI.Label(new Rect(0, sy, cw, 14),
                $"Created: {editingTask.createdAt}{comp}",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            sy += 20f;

            GUI.EndScrollView();
        }

        // ── New task modal ────────────────────────────────────────────────────

        void DrawNewTaskModal(float windowW, float windowH) {
            var ui = TaskManagerCore.UIState;
            
            // Overlay
            EditorGUI.DrawRect(new Rect(0, 0, windowW, windowH), new Color(0, 0, 0, 0.55f));

            float mw = Mathf.Min(windowW - 80, 400);
            float mh = 340f;
            float mx = (windowW - mw) * 0.5f;
            float my = (windowH - mh) * 0.5f;

            EditorGUI.DrawRect(new Rect(mx, my, mw, mh), TimeTrackerGUI.BgColor);
            EditorGUI.DrawRect(new Rect(mx, my, mw, 2), TimeTrackerGUI.AccentColor);

            float sy = my + 14f;


            // Header + ✕ close
            GUI.Label(new Rect(mx + 16, sy, mw - 60, 18), "NEW TASK",
                TimeTrackerGUI.Style(13, TimeTrackerGUI.TextColor, FontStyle.Bold));
            Rect closeR = new Rect(mx + mw - 32, sy - 1, 26, 22);
            EditorGUI.DrawRect(closeR, new Color(1f, 1f, 1f, 0.08f));
            GUI.Label(closeR, "✕",
                TimeTrackerGUI.Style(13, BrightText, anchor: TextAnchor.MiddleCenter));
            if (GUI.Button(closeR, GUIContent.none, GUIStyle.none)) {
                isCreatingTask = false; repaint?.Invoke();
            }
            sy += 28f;

            // Title
            GUI.Label(new Rect(mx + 16, sy, 60, 14), "Title",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            sy += 16f;
            newTaskTitle = EditorGUI.TextField(new Rect(mx + 16, sy, mw - 32, 20),
                newTaskTitle, new GUIStyle(EditorStyles.textField) { fontSize = 12 });
            sy += 28f;

            // Description
            GUI.Label(new Rect(mx + 16, sy, 80, 14), "Description",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            sy += 16f;
            newTaskDesc = EditorGUI.TextArea(new Rect(mx + 16, sy, mw - 32, 40),
                newTaskDesc, new GUIStyle(EditorStyles.textArea) { fontSize = 11, wordWrap = true });
            sy += 48f;

            // Due date
            GUI.Label(new Rect(mx + 16, sy, 60, 14), "Due date",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            sy += 16f;

            newTaskDueDate = EditorGUI.TextField(new Rect(mx + 16, sy, 100, 18),
                newTaskDueDate, new GUIStyle(EditorStyles.textField) { fontSize = 11 });

            // 4 date buttons: +1 Day, +1 Week, -1 Day, -1 Week
            float dbx = mx + 122f;
            (string lbl, int delta)[] dateBtns = {
                ("+1 Day", 1), ("+1 Week", 7), ("-1 Day", -1), ("-1 Week", -7)
            };
            foreach (var (dl, dd) in dateBtns) {
                float dw = dl.Length * 6.5f + 8f;
                if (GUI.Button(new Rect(dbx, sy, dw, 18), dl,
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 9, normal = { textColor = BrightText } })) {
                    DateTime.TryParse(newTaskDueDate, out var cur);
                    if (cur == default) cur = DateTime.Today;
                    newTaskDueDate = cur.AddDays(dd).ToString("yyyy-MM-dd");
                }
                dbx += dw + 3f;
            }
            sy += 28f;

            // Tags
            GUI.Label(new Rect(mx + 16, sy, 40, 14), "Tags",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            sy += 16f;

            float tagX = mx + 16f;
            foreach (var tag in board.tags) {
                bool  active  = ui.lastTagId == tag.id;
                Color chipBg  = active ? tag.GetColor() : TimeTrackerGUI.BgDark;
                Color chipTxt = active ? TimeTrackerGUI.BgColor : BrightText;
                float chipW   = Mathf.Max(44f, tag.label.Length * 7f + 12f);
                if (tagX + chipW > mx + mw - 16f) { tagX = mx + 16f; sy += 24f; }
                Rect cr = new Rect(tagX, sy, chipW, 18);
                EditorGUI.DrawRect(cr, chipBg);
                GUI.Label(cr, tag.label,
                    TimeTrackerGUI.Style(9, chipTxt, anchor: TextAnchor.MiddleCenter));
                var cap = tag;
                if (GUI.Button(cr, GUIContent.none, GUIStyle.none)) {
                    ui.lastTagId = active ? "" : cap.id;
                    TaskManagerCore.SaveUIState();
                }
                tagX += chipW + 5f;
            }
            sy += 28f;

            // Create button (full width)
            bool  canCreate = !string.IsNullOrWhiteSpace(newTaskTitle);
            Color createBg  = canCreate ? TimeTrackerGUI.AccentColor : TimeTrackerGUI.BgDark;
            Color createTxt = canCreate ? TimeTrackerGUI.BgColor     : TimeTrackerGUI.LabelColor;
            Rect  createR   = new Rect(mx + 16, sy, mw - 32, 28);
            EditorGUI.DrawRect(createR, createBg);
            GUI.Label(createR, "Create Task",
                TimeTrackerGUI.Style(12, createTxt, FontStyle.Bold, TextAnchor.MiddleCenter));
            if (canCreate && GUI.Button(createR, GUIContent.none, GUIStyle.none)) {
                var task = TaskManagerCore.CreateTask(board, newTaskTitle.Trim(),
                    TaskStatus.TODO, ui.lastTagId);
                task.dueDate        = newTaskDueDate;
                task.description    = newTaskDesc;
                isCreatingTask = false;
                Save();
                repaint?.Invoke();
            }

            // Escape
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Escape) {
                isCreatingTask = false; Event.current.Use();
            }
        }

        // ── Tag creator inline ────────────────────────────────────────────────

        void DrawTagCreator(float pad, ref float y, float trackW) {
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 30), TimeTrackerGUI.BgDark);
            GUI.Label(new Rect(pad + 8, y + 7, 52, 16), "New tag:",
                TimeTrackerGUI.Style(10, TimeTrackerGUI.LabelColor));
            newTagLabel = EditorGUI.TextField(new Rect(pad + 64, y + 6, 110, 18),
                newTagLabel, new GUIStyle(EditorStyles.textField) { fontSize = 11 });
            newTagColor = EditorGUI.ColorField(new Rect(pad + 180, y + 6, 40, 18), newTagColor);

            if (GUI.Button(new Rect(pad + 226, y + 5, 50, 20), "Create",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 10, normal = { textColor = TimeTrackerGUI.AccentColor } })) {
                if (!string.IsNullOrWhiteSpace(newTagLabel)) {
                    var tag = TaskManagerCore.CreateTag(board, newTagLabel.Trim(), newTagColor);
                    TaskManagerCore.UIState.lastTagId = tag.id;
                    Save();
                }
                isCreatingTag = false;
            }
            if (GUI.Button(new Rect(pad + 282, y + 5, 44, 20), "Cancel",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 10, normal = { textColor = TimeTrackerGUI.LabelColor } }))
                isCreatingTag = false;

            y += 38f;
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), TimeTrackerGUI.DivColor);
            y += 8f;
        }

        // ── Status creator inline ─────────────────────────────────────────────

        void DrawStatusCreator(float pad, ref float y, float trackW) {
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 30), TimeTrackerGUI.BgDark);
            GUI.Label(new Rect(pad + 8, y + 7, 70, 16), "New column:",
                TimeTrackerGUI.Style(10, TimeTrackerGUI.LabelColor));
            newStatusLabel = EditorGUI.TextField(new Rect(pad + 82, y + 6, 110, 18),
                newStatusLabel, new GUIStyle(EditorStyles.textField) { fontSize = 11 });
            newStatusColor = EditorGUI.ColorField(new Rect(pad + 198, y + 6, 40, 18), newStatusColor);

            if (GUI.Button(new Rect(pad + 244, y + 5, 50, 20), "Create",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 10, normal = { textColor = TimeTrackerGUI.AccentColor } })) {
                if (!string.IsNullOrWhiteSpace(newStatusLabel)) {
                    TaskManagerCore.CreateStatus(board, newStatusLabel.Trim(), newStatusColor);
                    Save();
                }
                isCreatingStatus = false;
            }
            if (GUI.Button(new Rect(pad + 300, y + 5, 44, 20), "Cancel",
                    new GUIStyle(EditorStyles.miniButton) {
                        fontSize = 10, normal = { textColor = TimeTrackerGUI.LabelColor } }))
                isCreatingStatus = false;

            y += 38f;
        }

        // ── Task Settings ─────────────────────────────────────────────────────

        void DrawTaskSettings(float pad, ref float y, float trackW, float scrollH) {
            GUI.Label(new Rect(pad, y + 3, trackW, 16), "TASK SETTINGS",
                TimeTrackerGUI.Style(12, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            y += 28f;
            EditorGUI.DrawRect(new Rect(pad, y, trackW, 1), TimeTrackerGUI.DivColor);
            y += 10f;

            Rect scrollRect = new Rect(pad, y, trackW, scrollH);
            float contentH  = 40f + board.tags.Count * 28f + 60f
                            + board.statuses.Count * 28f + 40f;
            Rect content    = new Rect(0, 0, trackW - 16f, contentH);
            settingsScroll  = GUI.BeginScrollView(scrollRect, settingsScroll, content);

            float sy = 0f;
            float cw = content.width;
            var   evt = Event.current;

            // ── Tags ──────────────────────────────────────────────────
            SectionLabel(0, sy, "TAGS"); sy += 20f;

            TaskTag tagToDelete = null;
            foreach (var tag in board.tags) {
                bool isDefault = tag.id == "default_tag" || tag.label == "Default";
                // Color swatch
                Color newCol = EditorGUI.ColorField(new Rect(0, sy, 40, 20), tag.GetColor());
                if (newCol != tag.GetColor()) { tag.SetColor(newCol); Save(); }

                // Label
                GUI.Label(new Rect(48, sy + 2, cw - 100, 16), tag.label,
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));

                if (!isDefault) {
                    // Delete — red ✕
                    Rect delR = new Rect(cw - 44, sy, 44, 20);
                    EditorGUI.DrawRect(delR, new Color(0.75f, 0.15f, 0.15f, 1f));
                    GUI.Label(delR, "✕ Del",
                        TimeTrackerGUI.Style(9, Color.white, anchor: TextAnchor.MiddleCenter));
                    if (GUI.Button(delR, GUIContent.none, GUIStyle.none))
                        tagToDelete = tag;
                } else {
                    GUI.Label(new Rect(cw - 52, sy + 2, 52, 16), "default",
                        TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
                }

                sy += 28f;
            }
            if (tagToDelete != null) {
                TaskManagerCore.DeleteTag(board, tagToDelete);
                Save(); repaint?.Invoke();
            }

            // Add tag inline
            sy += 4f;
            if (!isCreatingTag) {
                Rect addTagR = new Rect(0, sy, 80, 20);
                EditorGUI.DrawRect(addTagR, TimeTrackerGUI.BgDark);
                GUI.Label(addTagR, "+ Add Tag",
                    TimeTrackerGUI.Style(9, BrightText, anchor: TextAnchor.MiddleCenter));
                if (GUI.Button(addTagR, GUIContent.none, GUIStyle.none)) {
                    isCreatingTag = true; newTagLabel = "";
                    newTagColor = new Color(
                        UnityEngine.Random.Range(0.25f, 0.85f),
                        UnityEngine.Random.Range(0.25f, 0.85f),
                        UnityEngine.Random.Range(0.25f, 0.85f), 1f);
                }
                sy += 28f;
            } else {
                newTagColor  = EditorGUI.ColorField(new Rect(0, sy, 40, 20), newTagColor);
                newTagLabel  = EditorGUI.TextField(new Rect(48, sy, cw - 140, 20),
                    newTagLabel, new GUIStyle(EditorStyles.textField) { fontSize = 11 });
                if (GUI.Button(new Rect(cw - 86, sy, 40, 20), "Add",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 10, normal = { textColor = TimeTrackerGUI.AccentColor } })) {
                    if (!string.IsNullOrWhiteSpace(newTagLabel)) {
                        TaskManagerCore.CreateTag(board, newTagLabel.Trim(), newTagColor);
                        Save();
                    }
                    isCreatingTag = false;
                }
                if (GUI.Button(new Rect(cw - 42, sy, 42, 20), "Cancel",
                        new GUIStyle(EditorStyles.miniButton) {
                            fontSize = 9, normal = { textColor = TimeTrackerGUI.LabelColor } }))
                    isCreatingTag = false;
                sy += 28f;
            }

            sy += 8f;
            EditorGUI.DrawRect(new Rect(0, sy, cw, 1), TimeTrackerGUI.DivColor);
            sy += 12f;

            // ── Columns ───────────────────────────────────────────────
            SectionLabel(0, sy, "COLUMNS"); sy += 20f;

            TaskStatus statusToDelete = null;
            foreach (var st in board.statuses) {
                bool isBuiltin = st.id == TaskStatus.TODO ||
                                 st.id == TaskStatus.WORKING_ON ||
                                 st.id == TaskStatus.DONE;

                Color displayCol = GetStatusColor(st);
                if (!isBuiltin) {
                    Color newCol = EditorGUI.ColorField(new Rect(0, sy, 40, 20), st.GetColor());
                    if (newCol != st.GetColor()) { st.SetColor(newCol); Save(); }
                } else {
                    EditorGUI.DrawRect(new Rect(0, sy + 5, 40, 10), displayCol);
                }

                GUI.Label(new Rect(48, sy + 2, cw - 100, 16), st.label,
                    TimeTrackerGUI.Style(11, TimeTrackerGUI.TextColor));

                if (!isBuiltin) {
                    Rect delR = new Rect(cw - 44, sy, 44, 20);
                    EditorGUI.DrawRect(delR, new Color(0.75f, 0.15f, 0.15f, 1f));
                    GUI.Label(delR, "✕ Del",
                        TimeTrackerGUI.Style(9, Color.white, anchor: TextAnchor.MiddleCenter));
                    if (GUI.Button(delR, GUIContent.none, GUIStyle.none))
                        statusToDelete = st;
                } else {
                    GUI.Label(new Rect(cw - 52, sy + 2, 52, 16), "built-in",
                        TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
                }
                sy += 28f;
            }
            if (statusToDelete != null) {
                TaskManagerCore.DeleteStatus(board, statusToDelete);
                Save(); repaint?.Invoke();
            }

            GUI.EndScrollView();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        Color GetStatusColor(TaskStatus status) {
            if (status == null) return TimeTrackerGUI.LabelColor;
            if (status.id == TaskStatus.WORKING_ON) return WORKING_ON_COL;
            return status.GetColor();
        }

        void SectionLabel(float x, float y, string text) =>
            GUI.Label(new Rect(x, y, 120, 14), text,
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));

        void DrawFilterChip(ref float x, float y, string label, Color? col, bool active,
                System.Action onClick) {
            float w = Mathf.Max(36f, label.Length * 7f + 14f);
            Rect  r = new Rect(x, y, w, 22);

            Color bg  = active ? (col.HasValue ? col.Value : Color.white) : TimeTrackerGUI.BgDark;
            Color txt = active ? TimeTrackerGUI.BgColor : BrightText;

            EditorGUI.DrawRect(r, bg);
            if (col.HasValue && !active) {
                EditorGUI.DrawRect(new Rect(x + 5, y + 8, 6, 6), col.Value);
                GUI.Label(new Rect(x + 14, y + 3, w - 18, 16), label,
                    TimeTrackerGUI.Style(9, txt));
            } else {
                GUI.Label(r, label, TimeTrackerGUI.Style(9, txt, anchor: TextAnchor.MiddleCenter));
            }
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) onClick?.Invoke();
            x += w + 5f;
        }

        bool DrawSmallButton(ref float x, float y, string label) {
            float w = label.Length * 7f + 14f;
            Rect  r = new Rect(x, y + 1, w, 20);
            EditorGUI.DrawRect(r, TimeTrackerGUI.BgDark);
            GUI.Label(r, label, TimeTrackerGUI.Style(9, BrightText, anchor: TextAnchor.MiddleCenter));
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            x += w + 6f;
            return clicked;
        }

        void DrawInlineTagChip(ref float x, float y, TaskTag tag) {
            float w  = Mathf.Max(34f, tag.label.Length * 6f + 8f);
            Rect  r  = new Rect(x, y, w, TAG_CHIP_H);
            Color bg = tag.GetColor();
            EditorGUI.DrawRect(r, new Color(bg.r, bg.g, bg.b, 0.28f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), bg);
            GUI.Label(new Rect(r.x + 5, r.y, r.width - 5, r.height), tag.label,
                TimeTrackerGUI.Style(8, TimeTrackerGUI.TextColor));
            x += w + 4f;
        }

        float EstimateContentHeight() {
            string filter   = TaskManagerCore.UIState.activeFilterTag;
            int    maxCards = board.statuses
                .Select(s => TaskManagerCore.GetTasksForStatus(board, s.id, filter).Count)
                .DefaultIfEmpty(0).Max();
            float avgCard = CARD_PAD_H + TAG_CHIP_H + 4f + LINE_H + (LINE_H - 2f) + NAV_ROW_H + CARD_PAD_H;
            return 40f + maxCards * (avgCard + 6f) + 80f;
        }

        // ── Public ────────────────────────────────────────────────────────────

        public List<TrackerTask> GetTasksDueToday() => TaskManagerCore.GetTasksDueToday(board);
        public List<TrackerTask> GetWorkingOn()      => TaskManagerCore.GetWorkingOn(board);
        public TaskBoardData     Board               => board;
    }
}
