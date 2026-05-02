// TaskManagerCore.cs — models, load/save for the Task Manager
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace UnityTimeTracker {

    // ── Task status ───────────────────────────────────────────────────────────

    [Serializable]
    public class TaskStatus {
        public string id;
        public string label;
        public float[] color = { 0.35f, 0.35f, 0.40f, 1f };

        public Color GetColor() => new Color(color[0], color[1], color[2], color[3]);
        public void  SetColor(Color c) { color[0]=c.r; color[1]=c.g; color[2]=c.b; color[3]=c.a; }

        // Built-in status IDs
        public const string TODO       = "todo";
        public const string WORKING_ON = "working_on";
        public const string DONE       = "done";
    }

    // ── Tag ───────────────────────────────────────────────────────────────────

    [Serializable]
    public class TaskTag {
        public string id;
        public string label;
        public float[] color = { 0.25f, 0.55f, 0.95f, 1f };

        public Color GetColor() => new Color(color[0], color[1], color[2], color[3]);
        public void  SetColor(Color c) { color[0]=c.r; color[1]=c.g; color[2]=c.b; color[3]=c.a; }
    }

    // ── Task ──────────────────────────────────────────────────────────────────

    // ── Asset reference ──────────────────────────────────────────────────────

    [Serializable]
    public class TaskAssetRef {
        public string guid;   // AssetDatabase GUID
        public string label;  // cached display name
    }

    // ── Task ──────────────────────────────────────────────────────────────────

    [Serializable]
    public class TrackerTask {
        public string       id;
        public string       title;
        public string       description;
        public string       statusId;
        public List<string> tagIds      = new List<string>();
        public string       createdAt;
        public string       dueDate;       // "yyyy-MM-dd" or empty
        public string       completedAt;
        public int          order;
        public List<TaskAssetRef> assetRefs = new List<TaskAssetRef>(); // dropped assets

        // Derived helpers (not serialized)
        public bool HasDueDate => !string.IsNullOrEmpty(dueDate);
        public bool IsDueToday => HasDueDate && dueDate == DateTime.Today.ToString("yyyy-MM-dd");
        public bool IsOverdue  => HasDueDate && string.IsNullOrEmpty(completedAt) &&
                                  DateTime.TryParse(dueDate, out var d) && d.Date < DateTime.Today;
    }

    // ── Board data ────────────────────────────────────────────────────────────

    [Serializable]
    public class TaskBoardData {
        public List<TaskStatus> statuses = new List<TaskStatus>();
        public List<TaskTag>    tags     = new List<TaskTag>();
        public List<TrackerTask> tasks   = new List<TrackerTask>();
    }

    // ── Settings (EditorPrefs) ────────────────────────────────────────────────

    [Serializable]
    public class TaskManagerUIState {
        public string lastTagId       = "";   // last selected tag for new tasks
        public string activeFilterTag = "";   // "" = show all
    }

    // ── Core static ───────────────────────────────────────────────────────────

    public static class TaskManagerCore {

        const string PREFS_UI_KEY = "UnityTimeTracker_TaskUI";

        static string FilePath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "TaskBoard.json")
        );

        // ── UI state (persisted) ─────────────────────────────────────

        static TaskManagerUIState _uiState;
        public static TaskManagerUIState UIState {
            get {
                if (_uiState != null) return _uiState;
                string json = EditorPrefs.GetString(PREFS_UI_KEY, "");
                _uiState = string.IsNullOrEmpty(json)
                    ? new TaskManagerUIState()
                    : JsonUtility.FromJson<TaskManagerUIState>(json) ?? new TaskManagerUIState();
                return _uiState;
            }
        }

        public static void SaveUIState() {
            EditorPrefs.SetString(PREFS_UI_KEY, JsonUtility.ToJson(UIState));
        }

        // ── Load / Save ──────────────────────────────────────────────

        public static TaskBoardData LoadBoard() {
            if (!File.Exists(FilePath)) return CreateDefaultBoard();
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return CreateDefaultBoard();
            var board = JsonUtility.FromJson<TaskBoardData>(json);
            if (board == null) return CreateDefaultBoard();
            // Ensure default statuses always exist
            EnsureDefaultStatuses(board);
            return board;
        }

        public static void SaveBoard(TaskBoardData board) {
            File.WriteAllText(FilePath, JsonUtility.ToJson(board, prettyPrint: true));
        }

        // ── Defaults ────────────────────────────────────────────────

        static TaskBoardData CreateDefaultBoard() {
            var board = new TaskBoardData();
            EnsureDefaultStatuses(board);
            return board;
        }

        static void EnsureDefaultStatuses(TaskBoardData board) {
            if (!board.statuses.Any(s => s.id == TaskStatus.TODO))
                board.statuses.Insert(0, new TaskStatus {
                    id = TaskStatus.TODO, label = "To Do",
                    color = new float[] { 0.35f, 0.35f, 0.42f, 1f }
                });
            if (!board.statuses.Any(s => s.id == TaskStatus.WORKING_ON))
                board.statuses.Insert(1, new TaskStatus {
                    id = TaskStatus.WORKING_ON, label = "Working On",
                    color = new float[] { 0.20f, 0.55f, 0.90f, 1f }
                });
            if (!board.statuses.Any(s => s.id == TaskStatus.DONE))
                board.statuses.Insert(2, new TaskStatus {
                    id = TaskStatus.DONE, label = "Done",
                    color = new float[] { 0.20f, 0.75f, 0.50f, 1f }
                });
        }

        // ── Queries ──────────────────────────────────────────────────

        public static List<TrackerTask> GetTasksForStatus(
                TaskBoardData board, string statusId, string filterTagId = "") {
            return board.tasks
                .Where(t => t.statusId == statusId)
                .Where(t => string.IsNullOrEmpty(filterTagId) || t.tagIds.Contains(filterTagId))
                .OrderBy(t => t.order)
                .ToList();
        }

        public static List<TrackerTask> GetTasksDueToday(TaskBoardData board) {
            return board.tasks
                .Where(t => (t.IsDueToday || t.IsOverdue) && t.statusId != TaskStatus.DONE)
                .ToList();
        }

        public static List<TrackerTask> GetWorkingOn(TaskBoardData board) {
            return board.tasks
                .Where(t => t.statusId == TaskStatus.WORKING_ON)
                .ToList();
        }

        public static TaskTag GetTag(TaskBoardData board, string id) =>
            board.tags.FirstOrDefault(t => t.id == id);

        public static TaskStatus GetStatus(TaskBoardData board, string id) =>
            board.statuses.FirstOrDefault(s => s.id == id);

        // ── Mutations ────────────────────────────────────────────────

        public static TrackerTask CreateTask(TaskBoardData board, string title,
                string statusId, string tagId = "") {
            var task = new TrackerTask {
                id        = Guid.NewGuid().ToString("N").Substring(0, 8),
                title     = title,
                statusId  = statusId,
                createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                order     = board.tasks.Count
            };
            // Always assign a tag — fall back to "default_tag" if none provided
            string resolvedTag = !string.IsNullOrEmpty(tagId) ? tagId : "default_tag";
            task.tagIds.Add(resolvedTag);
            board.tasks.Add(task);
            return task;
        }

        public static TaskTag CreateTag(TaskBoardData board, string label, Color color) {
            var tag = new TaskTag {
                id    = Guid.NewGuid().ToString("N").Substring(0, 8),
                label = label
            };
            tag.SetColor(color);
            board.tags.Add(tag);
            return tag;
        }

        public static TaskStatus CreateStatus(TaskBoardData board, string label, Color color) {
            var status = new TaskStatus {
                id    = Guid.NewGuid().ToString("N").Substring(0, 8),
                label = label
            };
            status.SetColor(color);
            board.statuses.Add(status);
            return status;
        }

        public static void MoveTask(TaskBoardData board, TrackerTask task, string newStatusId) {
            task.statusId = newStatusId;
            if (newStatusId == TaskStatus.DONE && string.IsNullOrEmpty(task.completedAt))
                task.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            else if (newStatusId != TaskStatus.DONE)
                task.completedAt = "";
        }

        public static void DeleteTask(TaskBoardData board, TrackerTask task) {
            board.tasks.Remove(task);
        }

        public static void DeleteTag(TaskBoardData board, TaskTag tag) {
            board.tags.Remove(tag);
            foreach (var t in board.tasks)
                t.tagIds.Remove(tag.id);
        }

        public static void DeleteStatus(TaskBoardData board, TaskStatus status) {
            // Don't delete built-in statuses
            if (status.id == TaskStatus.TODO ||
                status.id == TaskStatus.WORKING_ON ||
                status.id == TaskStatus.DONE) return;
            // Move tasks to todo
            foreach (var t in board.tasks.Where(t => t.statusId == status.id))
                t.statusId = TaskStatus.TODO;
            board.statuses.Remove(status);
        }
    }
}
