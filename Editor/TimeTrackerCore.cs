// TimeTrackerCore.cs — models, load/save, calculations, GitHub fetch
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

// ── Models ────────────────────────────────────────────────────────────────────
namespace UnityTimeTracker {

    [Serializable]
    public class WorkSession {
        public string start;
        public string lastCheck;
    }

    [Serializable]
    public class TimeTrackingData {
        public List<WorkSession> sessions = new List<WorkSession>();
    }

    public struct PeriodStats {
        public double totalMinutes;
        public double avgMinutesPerDay;
        public double avgMinutesPerActiveDay;
        public double longestSessionMinutes;
        public double shortestSessionMinutes;
        public int activeDays;
        public int totalSessions;
        public List<(DateTime date, double minutes)> dailyMinutes;
        public (DateTime start, DateTime end) longestSession;
        public double earliestStartMinutes;
        public double latestEndMinutes;
    }

    // ── Commit model ─────────────────────────────────────────────────────────

    public class CommitInfo {
        public DateTime timestamp;
        public string   sha;
        public string   message;
        public string   author;
    }

    // ── Minimal JSON wrappers for GitHub API responses ────────────────────────

    [Serializable]
    class GH_CommitItem {
        public string sha;
        public GH_Commit commit;
    }
    [Serializable]
    class GH_Commit {
        public string    message;
        public GH_Author author;
        public GH_Author committer;
    }
    [Serializable]
    class GH_Author {
        public string name;
        public string date; // ISO 8601
    }
    [Serializable]
    class GH_CommitList {
        public List<GH_CommitItem> items;
    }

    // ── GitHub commit fetcher ─────────────────────────────────────────────────

    public static class GitHubCommitCache {
        // key: "yyyy-MM-dd", value: list of commits for that day
        static readonly Dictionary<string, List<CommitInfo>> _cache
            = new Dictionary<string, List<CommitInfo>>();

        static bool _fetching = false;
        static string _lastFetchedRange = "";

        public static bool IsFetching => _fetching;

        public static List<CommitInfo> GetForDate(DateTime date) {
            string key = date.ToString("yyyy-MM-dd");
            return _cache.TryGetValue(key, out var list) ? list : new List<CommitInfo>();
        }

        public static bool HasDataForDate(DateTime date) {
            return _cache.ContainsKey(date.ToString("yyyy-MM-dd"));
        }

        /// <summary>
        /// Kicks off an async fetch for all commits in [from, to].
        /// Results land in the cache; call Repaint() in the callback.
        /// </summary>
        public static void FetchRange(DateTime from, DateTime to, Action onComplete = null) {
            var gh = TimeTrackerSettings.GitHub;
            if (!gh.enabled || string.IsNullOrEmpty(gh.token) ||
                string.IsNullOrEmpty(gh.owner) || string.IsNullOrEmpty(gh.repo))
                return;

            string rangeKey = $"{from:yyyy-MM-dd}_{to:yyyy-MM-dd}_{gh.owner}_{gh.repo}";
            if (_fetching || rangeKey == _lastFetchedRange) return;

            _lastFetchedRange = rangeKey;
            _fetching = true;

            // GitHub API: commits between since/until (ISO 8601 UTC)
            string since = from.Date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            string until = to.Date.AddDays(1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            string url   = $"https://api.github.com/repos/{gh.owner}/{gh.repo}/commits" +
                           $"?since={since}&until={until}&per_page=100";

            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"token {gh.token}");
            request.SetRequestHeader("User-Agent",    "UnityTimeTracker/1.0");
            request.SetRequestHeader("Accept",        "application/vnd.github.v3+json");

            var op = request.SendWebRequest();
            op.completed += _ => {
                _fetching = false;
                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogWarning($"[TimeTracker] GitHub fetch failed: {request.error}");
                    request.Dispose();
                    onComplete?.Invoke();
                    return;
                }

                string json = request.downloadHandler.text;
                request.Dispose();

                // GitHub returns a JSON array — wrap it for JsonUtility
                string wrapped = "{\"items\":" + json + "}";
                var list = JsonUtility.FromJson<GH_CommitList>(wrapped);

                if (list?.items != null) {
                    foreach (var item in list.items) {
                        if (item?.commit == null) continue;
                        string dateStr = item.commit.committer?.date ?? item.commit.author?.date;
                        if (string.IsNullOrEmpty(dateStr)) continue;
                        if (!DateTime.TryParse(dateStr, null,
                                System.Globalization.DateTimeStyles.RoundtripKind,
                                out DateTime utc))
                            continue;

                        DateTime local = utc.ToLocalTime();
                        string   key   = local.ToString("yyyy-MM-dd");

                        if (!_cache.ContainsKey(key))
                            _cache[key] = new List<CommitInfo>();

                        _cache[key].Add(new CommitInfo {
                            timestamp = local,
                            sha       = item.sha?.Substring(0, Math.Min(7, item.sha.Length)) ?? "",
                            message   = FirstLine(item.commit.message),
                            author    = item.commit.author?.name ?? ""
                        });
                    }
                }

                onComplete?.Invoke();
            };
        }

        public static void InvalidateCache() {
            _cache.Clear();
            _lastFetchedRange = "";
        }

        static string FirstLine(string s) {
            if (string.IsNullOrEmpty(s)) return "";
            int nl = s.IndexOf('\n');
            return nl >= 0 ? s.Substring(0, nl).Trim() : s.Trim();
        }
    }

// ── Core static ──────────────────────────────────────────────────────────────

    public static class TimeTrackerCore {
        public const string DATE_FORMAT          = "yyyy-MM-dd HH:mm:ss";
        public const double SESSION_GAP_MINUTES  = 15;

        public static string FilePath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "TimeTracking.json")
        );

        // ── Load / Save ──────────────────────────────────────────────

        public static TimeTrackingData LoadData() {
            if (!File.Exists(FilePath)) return new TimeTrackingData();
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return new TimeTrackingData();
            return JsonUtility.FromJson<TimeTrackingData>(json) ?? new TimeTrackingData();
        }

        public static void SaveData(TimeTrackingData data) {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
        }

        // ── Queries ──────────────────────────────────────────────────

        public static List<(DateTime start, DateTime end)> GetSessionsForDate(TimeTrackingData data, DateTime date) {
            var result = new List<(DateTime, DateTime)>();
            if (data == null) return result;

            foreach (var session in data.sessions) {
                if (!DateTime.TryParse(session.start, out DateTime start)) continue;
                if (start.Date != date.Date) continue;

                DateTime end = DateTime.Now;
                if (!string.IsNullOrEmpty(session.lastCheck) &&
                    DateTime.TryParse(session.lastCheck, out DateTime lc))
                    end = lc;

                result.Add((start, end));
            }
            return result;
        }

        public static List<(DateTime start, DateTime end)> GetSessionsInRange(TimeTrackingData data, DateTime from, DateTime to) {
            var result = new List<(DateTime, DateTime)>();
            if (data == null) return result;

            for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
                result.AddRange(GetSessionsForDate(data, d));

            return result;
        }

        // ── Stats ────────────────────────────────────────────────────

        public static PeriodStats ComputeStats(TimeTrackingData data, DateTime from, DateTime to) {
            var ps = new PeriodStats();
            ps.dailyMinutes = new List<(DateTime, double)>();
            ps.longestSessionMinutes  = 0;
            ps.shortestSessionMinutes = double.MaxValue;
            ps.earliestStartMinutes   = double.MaxValue;
            ps.latestEndMinutes       = -1;

            int totalDays = (int)(to.Date - from.Date).TotalDays + 1;

            for (int d = 0; d < totalDays; d++) {
                DateTime day      = from.Date.AddDays(d);
                var      sessions = GetSessionsForDate(data, day);
                double   dayMins  = sessions.Sum(s => (s.end - s.start).TotalMinutes);

                ps.dailyMinutes.Add((day, dayMins));
                ps.totalMinutes += dayMins;
                if (dayMins > 0) ps.activeDays++;

                foreach (var (start, end) in sessions) {
                    double mins = (end - start).TotalMinutes;
                    ps.totalSessions++;

                    if (mins > ps.longestSessionMinutes) {
                        ps.longestSessionMinutes = mins;
                        ps.longestSession = (start, end);
                    }
                    if (mins < ps.shortestSessionMinutes)
                        ps.shortestSessionMinutes = mins;

                    double sm = start.TimeOfDay.TotalMinutes;
                    double em = end.TimeOfDay.TotalMinutes;
                    if (sm < ps.earliestStartMinutes) ps.earliestStartMinutes = sm;
                    if (em > ps.latestEndMinutes)     ps.latestEndMinutes     = em;
                }
            }

            ps.avgMinutesPerDay       = ps.totalMinutes / totalDays;
            ps.avgMinutesPerActiveDay = ps.activeDays > 0 ? ps.totalMinutes / ps.activeDays : 0;

            if (ps.shortestSessionMinutes == double.MaxValue) ps.shortestSessionMinutes = 0;
            if (ps.earliestStartMinutes   == double.MaxValue) ps.earliestStartMinutes   = -1;

            return ps;
        }

        // ── Helpers ──────────────────────────────────────────────────

        public static string FormatDuration(double minutes) {
            int h = (int)(minutes / 60);
            int m = (int)(minutes % 60);
            return h > 0 ? $"{h}h {m:00}m" : $"{m}m";
        }

        public static (DateTime from, DateTime to) GetWeekRange(DateTime reference) {
            int dow   = (int)reference.DayOfWeek;
            int toMon = (dow == 0) ? 6 : dow - 1;
            DateTime mon = reference.Date.AddDays(-toMon);
            return (mon, mon.AddDays(6));
        }

        public static (DateTime from, DateTime to) GetMonthRange(int year, int month) {
            var from = new DateTime(year, month, 1);
            var to   = from.AddMonths(1).AddDays(-1);
            return (from, to);
        }
    }
}
