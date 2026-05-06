// CodeCombatCore.cs — Code Combat game logic, enemies, persistence
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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

    [Serializable]
    public class ScriptDamageEntry {
        public string scriptPath;
        public int damage;
        public float lastTime;
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

        public static List<EnemyDef> Enemies => CodeCombatEnemies.All;

        // ── Events ───────────────────────────────────────────────────
        public static event Action<int /*damage*/, int /*newHp*/> OnHit;
        public static event Action<EnemyDef>                      OnKill;
        public static event Action                                 OnStateChanged;

        // ── State ────────────────────────────────────────────────────
        static CombatSaveData _state;
        
        static List<ScriptDamageEntry> _topDamage = new List<ScriptDamageEntry>();

        public static IReadOnlyList<ScriptDamageEntry> TopDamage => _topDamage;
        
        public static CombatSaveData State {
            get { if (_state == null) Load(); return _state; }
        }
        
        const string ENABLE_KEY = "CodeCombat_Enabled";
        const string TOP_DAMAGE_KEY = "CodeCombat_TopDamage";
        
        public static bool Enabled {
            get => EditorPrefs.GetBool(ENABLE_KEY, false); // 🔥 por defecto OFF
            set {
                EditorPrefs.SetBool(ENABLE_KEY, value);
                OnStateChanged?.Invoke();
            }
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

        static CodeCombatCore() {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
        }

        static void OnAfterReload() {
            if (!Enabled) return;
            
            LoadTopDamage();
            Load();

            try {
                string assetsPath = Application.dataPath;
                string[] allFiles = Directory.GetFiles(
                    assetsPath,
                    "*.cs",
                    SearchOption.AllDirectories
                );

                Dictionary<string, int> damageByFile = new Dictionary<string, int>();
                FileSnapshot prev = LoadSnapshot();

                int totalChurn = 0;

                if (prev.paths.Count > 0) {

                    var oldContent = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase
                    );

                    for (int i = 0; i < prev.paths.Count; i++)
                        oldContent[prev.paths[i]] = prev.contents[i];

                    foreach (string file in allFiles) {

                        string current;
                        try {
                            current = File.ReadAllText(file);
                        }
                        catch {
                            continue;
                        }

                        if (oldContent.TryGetValue(file, out string old)) {

                            if (old == current)
                                continue;

                            int churn = ComputeChurn(old, current);

                            totalChurn += churn;

                            if (churn > 0) {
                                damageByFile[file] = churn;
                            }
                        }
                    }
                }

                foreach (var kv in damageByFile) {
                    CodeCombatCore.ApplyDamage(kv.Value, kv.Key);
                }
                
                foreach (var kv in damageByFile) {

                    CodeCombatCore.RegisterDamage(kv.Key, kv.Value);
                }

                SaveSnapshot(allFiles);

            } catch (Exception e) {
                Debug.LogError(e);
            }
        }
        
        public static void RegisterDamage(string path, int dmg) {

            var entry = _topDamage.FirstOrDefault(x => x.scriptPath == path);

            if (entry == null) {
                entry = new ScriptDamageEntry {
                    scriptPath = path,
                    damage = dmg,
                    lastTime = Time.realtimeSinceStartup
                };
                _topDamage.Add(entry);
            } else {
                entry.damage += dmg;
                entry.lastTime = Time.realtimeSinceStartup;
            }

            _topDamage = _topDamage
                .OrderByDescending(x => x.damage)
                .Take(10)
                .ToList();
            
            SaveTopDamage();
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
                try { snap.paths.Add(f); snap.contents.Add(File.ReadAllText(f)); }
                catch { }
            }
            try {
                string dir = Path.GetDirectoryName(SnapshotPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snap));
            } catch { }
        }

        // ── Churn diff ────────────────────────────────────────────────

        static int ComputeChurn(string before, string after) {
            DiffLines(SplitLines(before), SplitLines(after), out int added, out int removed);
            return added + removed;
        }

        static string[] SplitLines(string text) =>
            text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                .Select(l => l.TrimEnd())
                .Where(l => l.Length > 0)
                .ToArray();

        static void DiffLines(string[] before, string[] after, out int added, out int removed) {
            int n = before.Length, m = after.Length;
            if (n > 500 || m > 500) {
                int d = m - n;
                added = d > 0 ? d : 0; removed = d < 0 ? -d : 0;
                return;
            }
            int[,] dp = new int[n + 1, m + 1];
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    dp[i, j] = before[i-1] == after[j-1]
                        ? dp[i-1, j-1] + 1
                        : Math.Max(dp[i-1, j], dp[i, j-1]);
            int lcs = dp[n, m];
            removed = n - lcs; added = m - lcs;
        }

        // ── Damage + kill chain ───────────────────────────────────────

        public static void ApplyDamage(int damage, string scriptPath = "Unknown") {
            if (damage <= 0) return;
            Load();

            State.currentHp   -= damage;
            State.totalDamage += damage;
            State.lastHitTime  = DateTime.Now.ToString("HH:mm:ss");

            EnemyDef enemy = CurrentEnemy; // 🔥 snapshot estable

            int actualDamage = damage;

            while (State.currentHp <= 0) {

                State.totalKills++;
                OnKill?.Invoke(enemy);

                State.enemyIndex = (State.enemyIndex + 1) % Enemies.Count;

                int overflow = -State.currentHp;

                enemy = CurrentEnemy; // 🔥 actualizar SOLO después del cambio

                State.currentHp = enemy.maxHp - overflow;

                if (State.currentHp < 0)
                    State.currentHp = 0;
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
            if (_state.currentHp < 0)         _state.currentHp = CurrentEnemy.maxHp;
            if (_state.currentHp > CurrentEnemy.maxHp) _state.currentHp = CurrentEnemy.maxHp;
        }

        static void Save() =>
            EditorPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(_state));

        public static void ResetState() {
            _state = new CombatSaveData { currentHp = CurrentEnemy.maxHp };
            Save();
            OnStateChanged?.Invoke();
        }
        
        static void SaveTopDamage() {
            try {
                string json = JsonUtility.ToJson(new Wrapper { list = _topDamage });
                EditorPrefs.SetString(TOP_DAMAGE_KEY, json);
            } catch { }
        }

        static void LoadTopDamage() {
            try {
                string json = EditorPrefs.GetString(TOP_DAMAGE_KEY, "");
                if (string.IsNullOrEmpty(json)) return;

                var wrapper = JsonUtility.FromJson<Wrapper>(json);
                _topDamage = wrapper?.list ?? new List<ScriptDamageEntry>();
            } catch {
                _topDamage = new List<ScriptDamageEntry>();
            }
        }

        [Serializable]
        class Wrapper {
            public List<ScriptDamageEntry> list;
        }
    }
}
