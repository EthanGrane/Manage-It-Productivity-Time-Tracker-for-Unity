// CodeCombatEnemies.cs — Enemy definitions + async texture cache
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityTimeTracker {

    // ── Enemy definition ──────────────────────────────────────────────────────
    // Stripped to essentials: HP scales linearly, sprite comes from a URL.

    public class EnemyDef {
        public string name;
        public int    maxHp;
        public string imageUrl;
    }

    // ── Async texture cache ───────────────────────────────────────────────────

    public static class EnemyTextureCache {

        enum EntryState { Idle, Loading, Done, Failed }

        class Entry {
            public EntryState state   = EntryState.Idle;
            public Texture2D  texture = null;
        }

        static readonly Dictionary<string, Entry> _cache =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>
        /// Returns the cached texture if ready, otherwise kicks off a download
        /// and calls <paramref name="repaint"/> when it completes. Returns null while loading.
        /// </summary>
        public static Texture2D Get(string url, Action repaint = null) {
            if (string.IsNullOrEmpty(url)) return null;

            if (!_cache.TryGetValue(url, out Entry entry)) {
                entry = new Entry();
                _cache[url] = entry;
            }

            switch (entry.state) {
                case EntryState.Done:    return entry.texture;
                case EntryState.Loading:
                case EntryState.Failed:  return null;
            }

            entry.state = EntryState.Loading;
            var req = UnityWebRequestTexture.GetTexture(url);
            var op  = req.SendWebRequest();
            op.completed += _ => {
                if (req.result == UnityWebRequest.Result.Success) {
                    entry.texture = DownloadHandlerTexture.GetContent(req);
                    entry.state   = EntryState.Done;
                } else {
                    Debug.LogWarning($"[CodeCombat] Texture fetch failed for '{url}': {req.error}");
                    entry.state = EntryState.Failed;
                }
                req.Dispose();
                repaint?.Invoke();
            };

            return null;
        }

        public static void Invalidate() => _cache.Clear();
    }

    // ── Enemy roster ─────────────────────────────────────────────────────────
    // HP is linear: enemy i  →  HP_BASE + i * HP_STEP
    // Sprites are hosted on the Resources branch of the project repo.
    // Change ENEMY_COUNT to match however many creature GIFs exist.

    public static class CodeCombatEnemies {

        const int    ENEMY_COUNT = 8;       // ← set to total number of creature GIFs
        const int    HP_BASE     = 30;      // HP of enemy index 0
        const int    HP_STEP     = 40;      // HP added per enemy level

        // Raw-content URL for the Resources branch
        const string GIF_BASE =
            "https://raw.githubusercontent.com/EthanGrane/Manage-It-Productivity-Time-Tracker-for-Unity/Resources/Sprites/creature-{0}.png";

        public static readonly List<EnemyDef> All = BuildRoster();
        
        
        static List<EnemyDef> BuildRoster() {
            string[] enemyNames = new string[] { "1","2","3" };

            var list = new List<EnemyDef>(ENEMY_COUNT);
            for (int i = 0; i < ENEMY_COUNT; i++) {
                list.Add(new EnemyDef {
                    name = enemyNames[(1 / 10) % 10],
                    maxHp    = HP_BASE + i * HP_STEP,
                    imageUrl = string.Format(GIF_BASE, i + 1)
                });
            }
            return list;
        }
    }
}
