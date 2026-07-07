using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public class ScheduledTaskCollector : ICollector
    {
        public string Name
        {
            get { return "ScheduledTaskCollector"; }
        }

        public List<TelemetryEvent> Collect()
        {
            var events = new List<TelemetryEvent>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/query /FO CSV",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        using (var reader = proc.StandardOutput)
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                var ev = ParseTaskLine(line);
                                if (ev != null)
                                {
                                    events.Add(ev);
                                }
                            }
                        }
                        proc.WaitForExit();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[ScheduledTaskCollector] Error running schtasks: {0}", ex.Message));
            }
            return events;
        }

        public void StartMonitoring(Action<TelemetryEvent> onEvent)
        {
        }

        public void StopMonitoring()
        {
        }

        private TelemetryEvent ParseTaskLine(string line)
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith("\"TaskName\"")) return null;

            // Simple CSV parsing (handling quotes)
            var matches = Regex.Matches(line, @"(?:^|,)(?:\x22([^\x22]*)\x22|([^,]*))");
            var parts = new List<string>();
            foreach (Match m in matches)
            {
                string val = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                parts.Add(val);
            }

            if (parts.Count < 3) return null;

            string taskName = parts[0];
            string nextRun = parts[1];
            string status = parts[2];

            if (string.IsNullOrEmpty(taskName)) return null;

            var ev = new TelemetryEvent
            {
                EventType = "ScheduledTask",
                Action = "Snapshot",
                Target = taskName,
                Status = "Success"
            };

            ev.Details.Add("TaskName", taskName);
            ev.Details.Add("NextRunTime", nextRun);
            ev.Details.Add("Status", status);

            return ev;
        }
    }
}
