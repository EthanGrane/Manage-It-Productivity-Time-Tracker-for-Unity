// TimeTrackerSettings.cs — persistent settings model (saved in EditorPrefs)
using System;
using UnityEngine;
using UnityEditor;

namespace UnityTimeTracker {

    // ── Icon style enum ───────────────────────────────────────────────────────
    public enum TimelineIconStyle {
        ZzzWork,    // Work-focused: sleep 💤 and worker 👷
        SunMoon,    // Classic: moon 🌙 and sun ☀
    }

    // ── Serializable theme snapshot ───────────────────────────────────────────
    [Serializable]
    public class TimeTrackerThemeData {
        // General colors — Unity dark skin palette
        public float[] accentColor  = { 0.15f, 0.351f, 1f, 1f };   // #468ee2 brighter blue
        public float[] bgColor      = { 0.2196f, 0.2196f, 0.2196f, 1f };   // #383838 Unity panel
        public float[] bgDarkColor  = { 0.1300f, 0.1300f, 0.1300f, 1f };   // #212121 deeper dark
        public float[] textColor    = { 0.8900f, 0.8900f, 0.8900f, 1f };   // #e3e3e3 brighter text

        // Timeline colors — clear off/work separation
        public float[] offColor     = { 0.1300f, 0.1300f, 0.1300f, 1f };   // #212121 deep dark off
        public float[] workColor    = { 0.2700f, 0.2700f, 0.2700f, 1f };   // #454545 visible work zone
        public float[] moonColor    = { 0.6275f, 0.7059f, 0.8863f, 0.90f };
        public float[] sunColor     = { 0.9608f, 0.8235f, 0.3098f, 0.95f };
        public float[] sessionColor = { 0.3f, 0.4635f, 1f, 1f };   // same as accent

        // Commit marker color
        public float[] commitColor  = { 0.9800f, 0.7600f, 0.2500f, 1.00f };

        // Icon style
        public int iconStyleIndex = 0;

        // Icon visibility
        public bool showOffIcon  = true;
        public bool showWorkIcon = true;

        // Transition hours
        public float workStartHour = 8f;
        public float workEndHour   = 17f;

        public float nightEndHour   = 6f;
        public float dayStartHour   = 7f;
        public float dayEndHour     = 17f;
        public float nightStartHour = 18f;

        public TimelineIconStyle IconStyle {
            get => (TimelineIconStyle)iconStyleIndex;
            set => iconStyleIndex = (int)value;
        }

        public Color GetAccent()   => Arr(accentColor);
        public Color GetBg()       => Arr(bgColor);
        public Color GetBgDark()   => Arr(bgDarkColor);
        public Color GetText()     => Arr(textColor);
        public Color GetOff()      => Arr(offColor);
        public Color GetWork()     => Arr(workColor);
        public Color GetMoon()     => Arr(moonColor);
        public Color GetSun()      => Arr(sunColor);
        public Color GetSession()  => Arr(sessionColor);
        public Color GetCommit()   => Arr(commitColor);

        public void SetAccent(Color c)   => Set(accentColor, c);
        public void SetBg(Color c)       => Set(bgColor, c);
        public void SetBgDark(Color c)   => Set(bgDarkColor, c);
        public void SetText(Color c)     => Set(textColor, c);
        public void SetOff(Color c)      => Set(offColor, c);
        public void SetWork(Color c)     => Set(workColor, c);
        public void SetMoon(Color c)     => Set(moonColor, c);
        public void SetSun(Color c)      => Set(sunColor, c);
        public void SetSession(Color c)  => Set(sessionColor, c);
        public void SetCommit(Color c)   => Set(commitColor, c);

        static Color Arr(float[] a) => new Color(a[0], a[1], a[2], a[3]);
        static void  Set(float[] a, Color c) { a[0]=c.r; a[1]=c.g; a[2]=c.b; a[3]=c.a; }
    }

    // ── GitHub integration settings ───────────────────────────────────────────
    [Serializable]
    public class GitHubSettings {
        public bool   enabled         = false;
        public string token           = "";
        public string owner           = "";
        public string repo            = "";
        public bool   showOnTimeline  = true;
        public bool   showOnCompact   = true;
    }

    // ── Static settings manager ───────────────────────────────────────────────
    public static class TimeTrackerSettings {
        const string PREFS_KEY        = "UnityTimeTracker_Theme";
        const string GITHUB_PREFS_KEY = "UnityTimeTracker_GitHub";

        static TimeTrackerThemeData _current;
        static GitHubSettings       _github;

        public static TimeTrackerThemeData Current {
            get { if (_current == null) Load(); return _current; }
        }

        public static GitHubSettings GitHub {
            get { if (_github == null) Load(); return _github; }
        }

        public static void Load() {
            string json = EditorPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(json))
                _current = new TimeTrackerThemeData();
            else {
                try { _current = JsonUtility.FromJson<TimeTrackerThemeData>(json); }
                catch { _current = new TimeTrackerThemeData(); }
            }

            string ghJson = EditorPrefs.GetString(GITHUB_PREFS_KEY, "");
            if (string.IsNullOrEmpty(ghJson))
                _github = new GitHubSettings();
            else {
                try { _github = JsonUtility.FromJson<GitHubSettings>(ghJson); }
                catch { _github = new GitHubSettings(); }
            }
        }

        public static void Save() {
            EditorPrefs.SetString(PREFS_KEY,        JsonUtility.ToJson(_current));
            EditorPrefs.SetString(GITHUB_PREFS_KEY, JsonUtility.ToJson(_github));
        }

        public static void Reset() {
            _current = new TimeTrackerThemeData(); // new defaults = Unity style
            Save();
        }
    }
}
