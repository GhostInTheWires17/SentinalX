using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public class NetworkCollector : ICollector
    {
        public string Name
        {
            get { return "NetworkCollector"; }
        }

        public List<TelemetryEvent> Collect()
        {
            var events = new List<TelemetryEvent>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano",
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
                                var ev = ParseNetstatLine(line);
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
                Console.WriteLine(string.Format("[NetworkCollector] Error running netstat: {0}", ex.Message));
            }
            return events;
        }

        public void StartMonitoring(Action<TelemetryEvent> onEvent)
        {
        }

        public void StopMonitoring()
        {
        }

        private TelemetryEvent ParseNetstatLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            var parts = Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 4) return null;

            string proto = parts[0].ToUpper();
            if (proto != "TCP" && proto != "UDP") return null;

            string localAddr = parts[1];
            string foreignAddr = parts[2];
            string state = string.Empty;
            int pid = 0;

            if (proto == "TCP")
            {
                if (parts.Length >= 5)
                {
                    state = parts[3];
                    int.TryParse(parts[4], out pid);
                }
            }
            else // UDP
            {
                int.TryParse(parts[3], out pid);
            }

            if (pid == 0) return null;

            var ev = new TelemetryEvent
            {
                EventType = "Network",
                Action = "Snapshot",
                ActorPid = pid,
                ActorName = "Unknown", 
                Target = foreignAddr,
                Status = "Success"
            };

            ev.Details.Add("Protocol", proto);
            ev.Details.Add("LocalAddress", localAddr);
            ev.Details.Add("ForeignAddress", foreignAddr);
            ev.Details.Add("State", state);
            ev.Details.Add("ProcessId", pid);

            return ev;
        }
    }
}
