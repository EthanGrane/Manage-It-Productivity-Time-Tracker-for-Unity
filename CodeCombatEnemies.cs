// CodeCombatEnemies.cs — Enemy definitions + async texture cache
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityTimeTracker {

    // ── Enemy definition ──────────────────────────────────────────────────────
    // Set imageUrl to a direct .png/.jpg URL to use an image instead of emoji.
    // Leave imageUrl empty (or null) to fall back to the emoji character.

    public class EnemyDef {
        public string name;
        public int    maxHp;
        public string emoji;        // fallback when imageUrl is empty
        public string imageUrl;     // optional: "https://…/icon.png"
        public string subtitle;
        public Color  hpColor;
    }

    // ── Async texture cache ───────────────────────────────────────────────────
    // Call EnemyTextureCache.Get(url, repaint) from OnGUI.
    // Returns null on the first call and triggers a background download;
    // subsequent calls return the cached Texture2D once it arrives.

    public static class EnemyTextureCache {

        enum State { Idle, Loading, Done, Failed }

        class Entry {
            public State     state   = State.Idle;
            public Texture2D texture = null;
        }

        static readonly Dictionary<string, Entry> _cache =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>
        /// Returns the texture if already downloaded, otherwise starts a download
        /// and calls <paramref name="repaint"/> when it arrives. Returns null while loading.
        /// </summary>
        public static Texture2D Get(string url, Action repaint = null) {
            if (string.IsNullOrEmpty(url)) return null;

            if (!_cache.TryGetValue(url, out Entry entry)) {
                entry = new Entry();
                _cache[url] = entry;
            }

            switch (entry.state) {
                case State.Done:    return entry.texture;
                case State.Loading:
                case State.Failed:  return null;
            }

            // Kick off download
            entry.state = State.Loading;
            var req = UnityWebRequestTexture.GetTexture(url);
            var op  = req.SendWebRequest();
            op.completed += _ => {
                if (req.result == UnityWebRequest.Result.Success) {
                    entry.texture = DownloadHandlerTexture.GetContent(req);
                    entry.state   = State.Done;
                } else {
                    Debug.LogWarning($"[CodeCombat] Failed to load enemy image '{url}': {req.error}");
                    entry.state = State.Failed;
                }
                req.Dispose();
                repaint?.Invoke();
            };

            return null;
        }

        /// <summary>Clears the cache (e.g. after changing enemy URLs).</summary>
        public static void Invalidate() => _cache.Clear();
    }

    // ── Enemy roster ─────────────────────────────────────────────────────────

    public static class CodeCombatEnemies {

        public static readonly List<EnemyDef> All = new List<EnemyDef> {
            new EnemyDef {
                name     = "Syntax Slime",
                maxHp    = 30,
                emoji    = "🟢",
                imageUrl = "",          // ← paste a direct image URL here to override emoji
                subtitle = "Level 1 — the basics",
                hpColor  = new Color(0.25f, 0.75f, 0.35f)
            },
            new EnemyDef {
                name     = "Null Pointer",
                maxHp    = 60,
                emoji    = "💀",
                imageUrl = "",
                subtitle = "Level 2 — beware the null",
                hpColor  = new Color(0.75f, 0.35f, 0.25f)
            },
            new EnemyDef {
                name     = "Stack Overflow",
                maxHp    = 100,
                emoji    = "🌀",
                imageUrl = "",
                subtitle = "Level 3 — infinite recursion",
                hpColor  = new Color(0.30f, 0.45f, 0.95f)
            },
            new EnemyDef {
                name     = "Memory Leak",
                maxHp    = 150,
                emoji    = "🕳️",
                imageUrl = "",
                subtitle = "Level 4 — unmanaged chaos",
                hpColor  = new Color(0.80f, 0.60f, 0.10f)
            },
            new EnemyDef {
                name     = "Deadlock Demon",
                maxHp    = 200,
                emoji    = "🔒",
                imageUrl = "https://api.dicebear.com/9.x/avataaars/svg",
                subtitle = "Level 5 — nothing moves",
                hpColor  = new Color(0.65f, 0.20f, 0.80f)
            },
            new EnemyDef {
                name     = "Race Condition",
                maxHp    = 260,
                emoji    = "⚡",
                imageUrl = "",
                subtitle = "Level 6 — timing is everything",
                hpColor  = new Color(0.95f, 0.75f, 0.10f)
            },
            new EnemyDef {
                name     = "God Object",
                maxHp    = 350,
                emoji    = "🧠",
                imageUrl = "",
                subtitle = "Level 7 — knows too much",
                hpColor  = new Color(0.20f, 0.80f, 0.75f)
            },
            new EnemyDef {
                name     = "Legacy Monolith",
                maxHp    = 500,
                emoji    = "🏚️",
                imageUrl = "",
                subtitle = "Level 8 — ancient and unmaintained",
                hpColor  = new Color(0.55f, 0.50f, 0.45f)
            },
        };
    }
}