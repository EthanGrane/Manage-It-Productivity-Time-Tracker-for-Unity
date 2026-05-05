// CodeCombatPanel.cs — Code Combat UI widget
using System;
using UnityEditor;
using UnityEngine;

namespace UnityTimeTracker {

    public class CodeCombatPanel {

        // ── Hit flash state ───────────────────────────────────────────────────
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

            DrawHeader(pad, ref y, panelW);

            float cardH = 312f;
            Rect  card  = new Rect(pad, y, panelW, cardH);
            EditorGUI.DrawRect(card, TimeTrackerGUI.BgCard);
            DrawCardBorder(card);

            float cx = pad + 12f;
            float cw = panelW - 24f;
            float cy = y + 12f;

            // ── Enemy name ────────────────────────────────────────────
            GUI.Label(new Rect(cx, cy, cw, 22),
                CodeCombatCore.CurrentEnemy.name,
                TimeTrackerGUI.Style(16, TimeTrackerGUI.BrightColor, FontStyle.Bold));
            cy += 24f;

            // Subtitle
            GUI.Label(new Rect(cx, cy, cw, 14),
                CodeCombatCore.CurrentEnemy.subtitle,
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));
            cy += 20f;

            // ── Enemy image ───────────────────────────────────────────
            float imgSize = 256f;
            float imgX    = cx + cw / 2f - imgSize / 2f;

            // Hit flash overlay (drawn behind the image)
            if (_flashAlpha > 0f) {
                Color flash = new Color(1f, 0.2f, 0.2f, _flashAlpha * 0.25f);
                EditorGUI.DrawRect(new Rect(imgX - 4, cy - 4, imgSize + 8, imgSize + 8), flash);
            }

            DrawEnemyImage(CodeCombatCore.CurrentEnemy, imgX, cy, imgSize);

            // Damage number pop-up
            if (_flashAlpha > 0.1f && _lastDamage > 0) {
                float dmgAlpha = Mathf.Clamp01(_flashAlpha * 2f);
                float dmgY     = cy + 4f - (1f - _flashAlpha) * 18f;
                GUI.Label(new Rect(imgX + imgSize, dmgY, 60, 28),
                    $"-{_lastDamage}",
                    new GUIStyle(EditorStyles.label) {
                        fontSize  = 14,
                        fontStyle = FontStyle.Bold,
                        normal    = { textColor = new Color(1f, 0.35f, 0.35f, dmgAlpha) }
                    });
            }
            cy += imgSize + 10f;

            // ── Kill message overlay ──────────────────────────────────
            if (_showKillMsg) {
                GUI.Label(new Rect(cx, cy - 16, cw, 20),
                    "☠ DEFEATED!",
                    TimeTrackerGUI.Style(12, new Color(1f, 0.85f, 0.2f, 1f),
                        FontStyle.Bold, TextAnchor.MiddleCenter));
                cy += 6f;
            }

            // ── HP bar ────────────────────────────────────────────────
            DrawHpBar(cx, ref cy, cw);
            cy += 10f;

            // ── Stats row ─────────────────────────────────────────────
            DrawStats(cx, cy, cw);
            cy += 22f;

            y += cardH + 10f;

            // ── Footer hint ───────────────────────────────────────────
            GUI.Label(new Rect(pad, y, panelW - 80f, 14),
                "Write code → save → Unity compiles → enemy takes damage",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor));

            Rect resetR = new Rect(pad + panelW - 70f, y - 2f, 68f, 18f);
            if (TimeTrackerGUI.DrawDangerButton(resetR, "Reset", 9))
                CodeCombatCore.ResetState();

            y += 18f;
        }

        // ── Enemy image: texture if available, emoji otherwise ────────────────
        void DrawEnemyImage(EnemyDef enemy, float x, float y, float size) {
            bool hasUrl = !string.IsNullOrEmpty(enemy.imageUrl);

            if (hasUrl) {
                // Ask the cache — returns null while downloading, texture once ready
                Texture2D tex = EnemyTextureCache.Get(enemy.imageUrl, _repaint);

                if (tex != null) {
                    // Draw the downloaded texture, scaled to fit
                    GUI.DrawTexture(new Rect(x, y, size, size), tex,
                        ScaleMode.ScaleToFit, alphaBlend: true);
                    return;
                }

                // Still downloading — show a small spinner text
                GUI.Label(new Rect(x, y + size * 0.4f, size, 16),
                    "loading…",
                    new GUIStyle(EditorStyles.label) {
                        fontSize  = 9,
                        alignment = TextAnchor.MiddleCenter,
                        normal    = { textColor = TimeTrackerGUI.LabelColor }
                    });
                return;
            }

            // No URL — fall back to emoji
            GUI.Label(new Rect(x, y, size, size),
                enemy.emoji,
                new GUIStyle(EditorStyles.label) {
                    fontSize  = 52,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = Color.white }
                });
        }

        // ── Header ────────────────────────────────────────────────────────────
        void DrawHeader(float pad, ref float y, float panelW) {
            GUI.Label(new Rect(pad, y, 200, 20),
                "⚔  CODE COMBAT",
                TimeTrackerGUI.Style(11, TimeTrackerGUI.AccentColor, FontStyle.Bold));
            y += 26f;
        }

        // ── HP bar ────────────────────────────────────────────────────────────
        void DrawHpBar(float cx, ref float cy, float cw) {
            int   hp       = CodeCombatCore.CurrentHp;
            int   maxHp    = CodeCombatCore.MaxHp;
            float fraction = CodeCombatCore.HpFraction;
            Color barColor = CodeCombatCore.CurrentEnemy.hpColor;

            GUI.Label(new Rect(cx, cy, cw - 70, 14),
                "HP",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.LabelColor, FontStyle.Bold));
            GUI.Label(new Rect(cx + cw - 68, cy, 68, 14),
                $"{hp} / {maxHp}",
                TimeTrackerGUI.Style(9, TimeTrackerGUI.TextColor, anchor: TextAnchor.UpperRight));
            cy += 16f;

            float barH  = 12f;
            Rect  track = new Rect(cx, cy, cw, barH);
            EditorGUI.DrawRect(track, new Color(0.10f, 0.10f, 0.10f, 1f));

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
            DrawStatCell(cx,           cy, colW - 4, "Kills",     CodeCombatCore.TotalKills.ToString(),  new Color(0.95f, 0.75f, 0.20f));
            DrawStatCell(cx + colW,    cy, colW - 4, "Dmg dealt", CodeCombatCore.TotalDamage.ToString(), new Color(0.80f, 0.35f, 0.35f));
            DrawStatCell(cx + colW*2f, cy, colW - 4, "Enemy #",
                $"{CodeCombatCore.State.enemyIndex + 1}/{CodeCombatCore.Enemies.Count}",
                TimeTrackerGUI.LabelColor);
        }

        void DrawStatCell(float x, float y, float w, string label, string value, Color valueColor) {
            GUI.Label(new Rect(x, y,      w, 11), label,
                TimeTrackerGUI.Style(8, TimeTrackerGUI.LabelColor));
            GUI.Label(new Rect(x, y + 12, w, 16), value,
                TimeTrackerGUI.Style(13, valueColor, FontStyle.Bold));
        }

        // ── Animation timers ──────────────────────────────────────────────────
        void UpdateTimers() {
            float t = (float)EditorApplication.timeSinceStartup;

            if (_flashAlpha > 0f) {
                float remaining = _flashEndTime - t;
                _flashAlpha = Mathf.Clamp01(remaining / 0.6f);
                if (_flashAlpha > 0f) _repaint?.Invoke();
            }

            if (_showKillMsg && t >= _killMsgEnd) {
                _showKillMsg = false;
                _repaint?.Invoke();
            }
        }

        // ── Drawing helpers ───────────────────────────────────────────────────
        void DrawCardBorder(Rect r) {
            Color border = new Color(0f, 0f, 0f, 0.4f);
            EditorGUI.DrawRect(new Rect(r.x,             r.y,              r.width, 1), border);
            EditorGUI.DrawRect(new Rect(r.x,             r.y + r.height-1, r.width, 1), border);
            EditorGUI.DrawRect(new Rect(r.x,             r.y,              1, r.height), border);
            EditorGUI.DrawRect(new Rect(r.x + r.width-1, r.y,              1, r.height), border);
        }

        void DrawRect1px(Rect r, Color c) {
            EditorGUI.DrawRect(new Rect(r.x,             r.y,              r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x,             r.y + r.height-1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x,             r.y,              1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.x + r.width-1, r.y,              1, r.height), c);
        }
    }
}