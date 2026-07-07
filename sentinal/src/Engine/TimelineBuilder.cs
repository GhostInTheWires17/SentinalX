using System;
using System.Collections.Generic;
using System.Linq;
using SentinelX.Models;

namespace SentinelX.Engine
{
    public class TimelineBuilder
    {
        public List<TelemetryEvent> BuildTimeline(List<TelemetryEvent> rawEvents)
        {
            var timeline = new List<TelemetryEvent>(rawEvents);

            // 1. Build Process Lookup Table
            var processMap = new Dictionary<int, ProcessLookupInfo>();
            foreach (var ev in timeline)
            {
                if (ev.EventType == "Process" && ev.Details.ContainsKey("ProcessId"))
                {
                    try
                    {
                        int pid = Convert.ToInt32(ev.Details["ProcessId"]);
                        if (!processMap.ContainsKey(pid))
                        {
                            string path = ev.Details.ContainsKey("ExecutablePath") ? ev.Details["ExecutablePath"].ToString() : string.Empty;
                            string name = ev.Details.ContainsKey("Name") ? ev.Details["Name"].ToString() : ev.Target;
                            string owner = ev.Details.ContainsKey("Owner") ? ev.Details["Owner"].ToString() : ev.User;

                            processMap[pid] = new ProcessLookupInfo
                            {
                                Pid = pid,
                                Name = name,
                                ExecutablePath = path,
                                Owner = owner
                            };
                        }
                    }
                    catch {}
                }
            }

            // 2. Correlate Events
            foreach (var ev in timeline)
            {
                int? pid = ev.ActorPid;
                if (!pid.HasValue && ev.Details.ContainsKey("ProcessId"))
                {
                    try
                    {
                        pid = Convert.ToInt32(ev.Details["ProcessId"]);
                    }
                    catch {}
                }

                if (pid.HasValue && pid.Value > 0)
                {
                    ProcessLookupInfo info;
                    if (processMap.TryGetValue(pid.Value, out info))
                    {
                        ev.ActorPid = pid.Value;
                        ev.ActorName = info.Name;
                        if (string.IsNullOrEmpty(ev.User))
                        {
                            ev.User = info.Owner;
                        }

                        if (!ev.Details.ContainsKey("ProcessName"))
                        {
                            ev.Details["ProcessName"] = info.Name;
                        }
                        if (!ev.Details.ContainsKey("ExecutablePath") && !string.IsNullOrEmpty(info.ExecutablePath))
                        {
                            ev.Details["ExecutablePath"] = info.ExecutablePath;
                        }
                    }
                }

                // Resolve Parent Process name for process events
                if (ev.EventType == "Process" && ev.Details.ContainsKey("ParentProcessId"))
                {
                    try
                    {
                        int parentPid = Convert.ToInt32(ev.Details["ParentProcessId"]);
                        ProcessLookupInfo parentInfo;
                        if (processMap.TryGetValue(parentPid, out parentInfo))
                        {
                            ev.ActorName = parentInfo.Name;
                            if (!ev.Details.ContainsKey("ParentProcessName"))
                            {
                                ev.Details["ParentProcessName"] = parentInfo.Name;
                            }
                        }
                    }
                    catch {}
                }
            }

            // 3. Sort Chronologically
            timeline.Sort((x, y) => x.Timestamp.CompareTo(y.Timestamp));

            // 4. De‑duplicate events (exact match on key fields and details)
            var seen = new HashSet<string>();
            var deduped = new List<TelemetryEvent>();
            foreach (var ev in timeline)
            {
                var detailsHash = GetDetailsHash(ev);
                var key = $"{ev.Timestamp.Ticks}|{ev.EventType}|{ev.Action}|{ev.Target}|{detailsHash}";
                if (seen.Add(key))
                {
                    deduped.Add(ev);
                }
            }
            return deduped;
        }
// Helper to create deterministic string representation of the Details dictionary
        private static string GetDetailsHash(TelemetryEvent ev)
        {
            if (ev.Details == null || ev.Details.Count == 0) return string.Empty;
            var parts = new List<string>();
            foreach (var kv in ev.Details.OrderBy(k => k.Key))
            {
                parts.Add($"{kv.Key}:{kv.Value}");
            }
            return string.Join(",", parts);
        }
        private class ProcessLookupInfo
        {
            public int Pid { get; set; }
            public string Name { get; set; }
            public string ExecutablePath { get; set; }
            public string Owner { get; set; }
        }
    }
}
