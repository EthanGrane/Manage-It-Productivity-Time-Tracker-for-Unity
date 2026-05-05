// CodeCombatCore.cs — Code Combat game logic, enemies, persistence
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityTimeTracker {
    
    // ── Persistent combat state ───────────────────────────────────────────────

    [Serializable]
    public class CombatSaveData {
        public int    enemyIndex  = 0;
        public int    currentHp   = -1;   // -1 = first load, use enemy.maxHp
        public int    totalKills  = 0;
        public int    totalDamage = 0;
        public string lastHitTime = "";
    }

    // ── Compile snapshot ──────────────────────────────────────────────────────

    [Serializable]
    public class FileSnapshot {
        public List<string> paths    = new List<string>();
        public List<string> contents = new List<string>();
    }

    // ── Core ──────────────────────────────────────────────────────────────────

    [InitializeOnLoad]
    public static class CodeCombatCore {

        // ── Enemy roster — defined in CodeCombatEnemies.cs ───────────
        public static List<EnemyDef> Enemies => CodeCombatEnemies.All;

        // ── Events ───────────────────────────────────────────────────
        public static event Action<int /*damage*/, int /*newHp*/> OnHit;
        public static event Action<EnemyDef>                      OnKill;
        public static event Action                                 OnStateChanged;

        // ── State ────────────────────────────────────────────────────
        static CombatSaveData _state;
        public static CombatSaveData State {
            get { if (_state == null) Load(); return _state; }
        }

        public static EnemyDef CurrentEnemy =>
            Enemies[Mathf.Clamp(State.enemyIndex, 0, Enemies.Count - 1)];

        public static int   CurrentHp   => State.currentHp;
        public static int   MaxHp       => CurrentEnemy.maxHp;
        public static int   TotalKills  => State.totalKills;
        public static int   TotalDamage => State.totalDamage;
        public static float HpFraction  =>
            MaxHp > 0 ? Mathf.Clamp01((float)CurrentHp / MaxHp) : 0f;

        const string PREFS_KEY = "CodeCombat_State";

        static string SnapshotPath =>
            Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "Temp", "CodeCombatSnapshot.json");

        // ── Constructor ───────────────────────────────────────────────
        static CodeCombatCore() {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
        }

        // ─────────────────────────────────────────────────────────────
        //  Main entry point — runs after every successful compile
        // ─────────────────────────────────────────────────────────────
        static void OnAfterReload() {
            Load();

            try {
                string   assetsPath = Application.dataPath;
                string[] allFiles   = Directory
                    .GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories);

                // ── 1. Diff against previous snapshot ─────────────────
                int          totalChurn = 0;
                FileSnapshot prev       = LoadSnapshot();

                if (prev.paths.Count > 0) {
                    var oldContent = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < prev.paths.Count; i++)
                        oldContent[prev.paths[i]] = prev.contents[i];

                    foreach (string file in allFiles) {
                        string current;
                        try { current = File.ReadAllText(file); }
                        catch { continue; }

                        if (oldContent.TryGetValue(file, out string old)) {
                            if (old == current) continue;
                            totalChurn += ComputeChurn(old, current);
                        }
                    }
                }

                // ── 2. Apply damage ───────────────────────────────────
                if (totalChurn > 0)
                    ApplyDamage(totalChurn);

                // ── 3. Save current state as baseline for next compile ─
                SaveSnapshot(allFiles);

            } catch { /* never crash the editor */ }
        }

        // ── Snapshot I/O ──────────────────────────────────────────────

        static FileSnapshot LoadSnapshot() {
            if (!File.Exists(SnapshotPath)) return new FileSnapshot();
            try {
                var s = JsonUtility.FromJson<FileSnapshot>(File.ReadAllText(SnapshotPath));
                return s ?? new FileSnapshot();
            } catch { return new FileSnapshot(); }
        }

        static void SaveSnapshot(string[] files) {
            var snap = new FileSnapshot();
            foreach (string f in files) {
                try {
                    snap.paths.Add(f);
                    snap.contents.Add(File.ReadAllText(f));
                } catch { }
            }
            try {
                string dir = Path.GetDirectoryName(SnapshotPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snap));
            } catch { }
        }

        // ── Gross churn: lines added + lines removed ──────────────────
        static int ComputeChurn(string before, string after) {
            string[] lBefore = SplitLines(before);
            string[] lAfter  = SplitLines(after);
            DiffLines(lBefore, lAfter, out int added, out int removed);
            return added + removed;
        }

        static string[] SplitLines(string text) =>
            text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                .Select(l => l.TrimEnd())
                .Where(l => l.Length > 0)
                .ToArray();

        static void DiffLines(string[] before, string[] after, out int added, out int removed) {
            int n = before.Length;
            int m = after.Length;

            if (n > 500 || m > 500) {
                int delta = m - n;
                added   = delta > 0 ? delta : 0;
                removed = delta < 0 ? -delta : 0;
                return;
            }

            int[,] dp = new int[n + 1, m + 1];
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    dp[i, j] = before[i - 1] == after[j - 1]
                        ? dp[i - 1, j - 1] + 1
                        : Math.Max(dp[i - 1, j], dp[i, j - 1]);

            int lcs = dp[n, m];
            removed = n - lcs;
            added   = m - lcs;
        }

        // ── Deal damage and handle kill chain ────────────────────────
        public static void ApplyDamage(int damage) {
            if (damage <= 0) return;
            Load();

            State.currentHp   -= damage;
            State.totalDamage += damage;
            State.lastHitTime  = DateTime.Now.ToString("HH:mm:ss");

            int actualDamage = damage;

            while (State.currentHp <= 0) {
                State.totalKills++;
                OnKill?.Invoke(CurrentEnemy);

                State.enemyIndex = (State.enemyIndex + 1) % Enemies.Count;
                int overflow = -State.currentHp;
                State.currentHp = CurrentEnemy.maxHp - overflow;
                if (State.currentHp < 0) State.currentHp = 0;
            }

            Save();
            OnHit?.Invoke(actualDamage, State.currentHp);
            OnStateChanged?.Invoke();
        }

        // ── Persistence ──────────────────────────────────────────────
        static void Load() {
            string json = EditorPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(json))
                _state = new CombatSaveData();
            else {
                try { _state = JsonUtility.FromJson<CombatSaveData>(json); }
                catch { _state = new CombatSaveData(); }
            }

            if (_state.currentHp < 0)
                _state.currentHp = CurrentEnemy.maxHp;

            if (_state.currentHp > CurrentEnemy.maxHp)
                _state.currentHp = CurrentEnemy.maxHp;
        }

        static void Save() {
            EditorPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(_state));
        }

        public static void ResetState() {
            _state = new CombatSaveData();
            _state.currentHp = CurrentEnemy.maxHp;
            Save();
            OnStateChanged?.Invoke();
        }
    }
}
