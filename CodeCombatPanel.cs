// CodeCombatPanel.cs — Code Combat UI widget
using System;
using UnityEditor;
using UnityEngine;

namespace UnityTimeTracker {

    public class CodeCombatPanel {

        float _flashAlpha   = 0f;
        float _flashEndTime = 0f;
        int   _lastDamage   = 0;
        bool  _showKillMsg  = false;
        float _killMsgEnd   = 0f;

        System.Action _repaint;

        // ── Init ──────────────────────────────────────────────────────────────
        public void Init(System.Action repaintCallback) {
            _repaint = repaintCallback;
            CodeCombatCore.OnHit          += OnHit;
            CodeCombatCore.OnKill         += OnKill;
            CodeCombatCore.OnStateChanged += () => _repaint?.Invoke();
            
            EditorApplication.update += Update;
        }
        
        void Update() {
            _repaint?.Invoke();
        }

        public void Destroy() {
            CodeCombatCore.OnHit  -= OnHit;
            CodeCombatCore.OnKill -= OnKill;
        }

        void OnHit(int damage, int newHp) {
            _lastDamage   = damage;
            _flashAlpha   = 1f;
            _flashEndTime = (float)EditorApplication.timeSinceStartup + 0.6f;
            _repaint?.Invoke();
        }

        void OnKill(EnemyDef enemy) {
            _showKillMsg = true;
            _killMsgEnd  = (float)EditorApplication.timeSinceStartup + 2.0f;
            _repaint?.Invoke();
        }

        // ── Draw ──────────────────────────────────────────────────────────────
        public void Draw(float pad, float y, float windowW, float windowH) {
            UpdateTimers();
            float panelW = windowW - pad * 2;
            y += 15f;

            // Draw enable button
            if(!EnableCodeCombat(pad, panelW, y))
                return;

            y += 25f;

            // Header
            y += 20f;
            GUI.Label(new Rect(pad, y, 200, 40),
                "⚔  CODE COMBAT",
                TimeTrackerGUI.Style(11, TimeTrackerGUI.AccentColor, FontStyle.Bold));
            y += 26f;

            // Card
            float imgSize = 200f;
            float cardH   = imgSize + 120f;   // image + hp bar + stats + padding
            Rect  card    = new Rect(pad, y, panelW, cardH);
            EditorGUI.DrawRect(card, TimeTrackerGUI.BgCard);
            DrawCardBorder(card);

            float cx = pad + 12f;
            float cw = panelW - 24f;
            float cy = y + 12f;

            // ── Enemy sprite ──────────────────────────────────────────
            float imgX = cx + cw / 2f - imgSize / 2f;

            if (_flashAlpha > 0f) {
                Color flash = new Color(1f, 0.2f, 0.2f, _flashAlpha * 0.3f);
                EditorGUI.DrawRect(new Rect(imgX - 4, cy - 4, imgSize + 8, imgSize + 8), flash);
            }

            DrawEnemyImage(CodeCombatCore.CurrentEnemy, imgX, cy, imgSize);

            // Damage pop-up
            if (_flashAlpha > 0.1f && _lastDamage > 0) {
                float dmgAlpha = Mathf.Clamp01(_flashAlpha * 2f);
                float dmgY     = cy + imgSize * 0.4f - (1f - _flashAlpha) * 18f;
                GUI.Label(new Rect(imgX + imgSize + 4f, dmgY, 60, 28),
                    $"-{_lastDamage}",
                    new GUIStyle(EditorStyles.label) {
                        fontSize  = 14,
                        fontStyle = FontStyle.Bold,
                        normal    = { textColor = new Color(1f, 0.35f, 0.35f, dmgAlpha) }
                    });
            }
            cy += imgSize + 8f;

            // Kill message
            if (_showKillMsg) {
                GUI.Label(new Rect(cx, cy, cw, 20),
                    "☠  DEFEATED!",
                    TimeTrackerGUI.Style(12, new Color(1f, 0.85f, 0.2f, 1f),
                        FontStyle.Bold, TextAnchor.MiddleCenter));
                cy += 22f;
            }

            // ── HP bar ────────────────────────────────────────────────
            DrawHpBar(cx, ref cy, cw);
            cy += 8f;

            // ── Stats row ─────────────────────────────────────────────
            DrawStats(cx, cy, cw);

            y += cardH + 10f;
            DrawTopDamage(pad, ref y, panelW);
            
            // Footer
            GUI.Label(new Rect(pad, y, panelW - 80f, 14),
                "Write code → save → Unity compiles → enemy takes damage",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            /*
             // Reset
            Rect resetR = new Rect(pad + panelW - 70f, y - 2f, 68f, 18f);
            if (TimeTrackerGUI.DrawDangerButton(resetR, "Reset", 9))
                CodeCombatCore.ResetState();
                */
            

            
        }

        bool EnableCodeCombat(float pad, float panelW, float y)
        {
            float headerH = 28f;

            bool enabled = CodeCombatCore.Enabled;

            Rect toggleRect = new Rect(pad, y, 50f, headerH);

            Color bg = enabled
                ? TimeTrackerGUI.BgDark * 1.25f
                : TimeTrackerGUI.BgDark;

            // fondo
            EditorGUI.DrawRect(toggleRect, bg);

            // hover (misma lógica que tus tabs)
            bool hov = toggleRect.Contains(Event.current.mousePosition);
            if (hov && !enabled) {
                EditorGUI.DrawRect(toggleRect, new Color(1f, 1f, 1f, 0.05f));
                _repaint?.Invoke();
            }

            // label con TU Style API
            GUI.Label(
                toggleRect,
                enabled ? "⚔ ON" : "⚔ OFF",
                TimeTrackerGUI.Style(
                    11,
                    enabled ? TimeTrackerGUI.TextColor : TimeTrackerGUI.LabelColor,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter
                )
            );

            // click (sin GUI.Button, como tu sistema de tabs)
            if (GUI.Button(toggleRect, GUIContent.none, GUIStyle.none)) {
                CodeCombatCore.Enabled = !enabled;
                _repaint?.Invoke();
            }

            return enabled;
        }
        
        void DrawTopDamage(float cx, ref float y, float cw) {

            var list = CodeCombatCore.TopDamage;
            if (list == null || list.Count == 0) return;

            GUI.Label(new Rect(cx, y, cw, 16),
                "📜 Top Damage Scripts",
                TimeTrackerGUI.Style(10, TimeTrackerGUI.LabelColor, FontStyle.Bold));

            y += 18f;

            float rowH = 18f;

            for (int i = 0; i < list.Count; i++) {

                var e = list[i];

                Rect r = new Rect(cx, y + i * (rowH + 2f), cw, rowH);

                EditorGUI.DrawRect(r, new Color(0, 0, 0, 0.15f));

                GUI.Label(new Rect(r.x + 6, r.y, cw * 0.7f, rowH),
                    $"#{i + 1}  {System.IO.Path.GetFileName(e.scriptPath)}",
                    TimeTrackerGUI.Style(9, TimeTrackerGUI.TextColor));

                GUI.Label(new Rect(r.x + cw - 60, r.y, 60, rowH),
                    e.damage.ToString(),
                    TimeTrackerGUI.Style(9, new Color(1f, 0.6f, 0.2f),
                        FontStyle.Bold, TextAnchor.MiddleRight));
            }

            y += list.Count * (rowH + 2f);
        }

        // ── Enemy image ───────────────────────────────────────────────────────
        void DrawEnemyImage(EnemyDef enemy, float x, float y, float size) {
            Texture2D tex = EnemyTextureCache.Get(enemy.imageUrl, _repaint);

            if (tex != null) {
                if (tex.filterMode != FilterMode.Point) {
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode   = TextureWrapMode.Clamp;
                    tex.anisoLevel = 0;
                }

                int baseSize = tex.width;
                float scaleInt  = Mathf.Max(1, Mathf.Round(size / baseSize));
                float finalSize = baseSize * scaleInt;

                float offsetX = x + (size - finalSize) * 0.5f;
                float offsetY = y + (size - finalSize) * 0.5f;

                Rect rect = new Rect(offsetX, offsetY, finalSize, finalSize);

                // ── 🎬 Animación squash/stretch ─────────────────────────────
                float t = (float)EditorApplication.timeSinceStartup;
                float speed = 1.5f;

                float anim = Mathf.Sin(t * speed) * 0.5f + 0.5f;

                float scaleX = Mathf.Lerp(1f, 0.95f, anim);
                float scaleY = Mathf.Lerp(.9f, 1.05f, anim);

                // 🔁 Aplicar matriz alrededor del centro
                Matrix4x4 prev = GUI.matrix;

                Vector2 pivot = rect.center;

                GUIUtility.ScaleAroundPivot(new Vector2(scaleX, scaleY), pivot);

                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);

                GUI.matrix = prev;

                return;
            }

            // Placeholder
            EditorGUI.DrawRect(new Rect(x, y, size, size), new Color(0.12f, 0.12f, 0.12f));
            GUI.Label(
                new Rect(x, y + size * 0.45f, size, 18),
                string.IsNullOrEmpty(enemy.imageUrl) ? "no image" : "loading…",
                new GUIStyle(EditorStyles.label) {
                    fontSize  = 9,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = TimeTrackerGUI.LabelColor }
                }
            );
        }
        
        // ── HP bar ────────────────────────────────────────────────────────────
        void DrawHpBar(float cx, ref float cy, float cw) {
            int   hp       = CodeCombatCore.CurrentHp;
            int   maxHp    = CodeCombatCore.MaxHp;
            float fraction = CodeCombatCore.HpFraction;

            // Colour shifts from green → yellow → red as HP drops
            Color barColor = Color.Lerp(
                new Color(0.85f, 0.20f, 0.20f),   // red   (low HP)
                new Color(0.25f, 0.80f, 0.35f),   // green (full HP)
                fraction);

            GUI.Label(new Rect(cx, cy, cw - 70, 14), "HP",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            GUI.Label(new Rect(cx + cw - 68, cy, 68, 14), $"{hp} / {maxHp}",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.TextColor, anchor: TextAnchor.UpperRight));
            cy += 16f;

            float barH  = 12f;
            Rect  track = new Rect(cx, cy, cw, barH);
            EditorGUI.DrawRect(track, new Color(0.10f, 0.10f, 0.10f));

            float fillW = Mathf.Max(2f, cw * fraction);
            Rect  fill  = new Rect(cx, cy, fillW, barH);
            EditorGUI.DrawRect(fill, barColor);

            if (_flashAlpha > 0f)
                EditorGUI.DrawRect(fill, new Color(1f, 1f, 1f, _flashAlpha * 0.35f));

            DrawRect1px(track, new Color(0f, 0f, 0f, 0.5f));
            cy += barH + 2f;
        }

        // ── Stats row ─────────────────────────────────────────────────────────
        void DrawStats(float cx, float cy, float cw) {
            float colW = cw / 3f;
            DrawStatCell(cx,           cy, colW - 4, "Kills",
                CodeCombatCore.TotalKills.ToString(),  new Color(0.95f, 0.75f, 0.20f));
            DrawStatCell(cx + colW,    cy, colW - 4, "Dmg dealt",
                CodeCombatCore.TotalDamage.ToString(), new Color(0.80f, 0.35f, 0.35f));
        }

        void DrawStatCell(float x, float y, float w, string label, string value, Color valueColor) {
            GUI.Label(new Rect(x, y,      w, 11), label,
                TimeTrackerGUI.Style(8, TimeTrackerGUI.LabelColor));
            GUI.Label(new Rect(x, y + 12, w, 16), value,
                TimeTrackerGUI.Style(13, valueColor, FontStyle.Bold));
        }

        // ── Timers ────────────────────────────────────────────────────────────
        void UpdateTimers() {
            float t = (float)EditorApplication.timeSinceStartup;

            if (_flashAlpha > 0f) {
                _flashAlpha = Mathf.Clamp01((_flashEndTime - t) / 0.6f);
                if (_flashAlpha > 0f) _repaint?.Invoke();
            }

            if (_showKillMsg && t >= _killMsgEnd) {
                _showKillMsg = false;
                _repaint?.Invoke();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        void DrawCardBorder(Rect r) {
            Color c = new Color(0f, 0f, 0f, 0.4f);
            EditorGUI.DrawRect(new Rect(r.x,             r.y,              r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x,             r.y + r.height-1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x,             r.y,              1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.x + r.width-1, r.y,              1, r.height), c);
        }

        void DrawRect1px(Rect r, Color c) {
            EditorGUI.DrawRect(new Rect(r.x,             r.y,              r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x,             r.y + r.height-1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x,             r.y,              1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.x + r.width-1, r.y,              1, r.height), c);
        }
    }
}
