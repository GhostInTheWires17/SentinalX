using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public class PowerShellCollector : ICollector
    {
        public string Name => "PowerShellCollector";

        private const int MaxEntriesPerLog = 30;
        private List<EventLogWatcher> _activeWatchers = new List<EventLogWatcher>();

        public List<TelemetryEvent> Collect()
        {
            var events = new List<TelemetryEvent>();
            try
            {
                var query = new EventLogQuery("Windows PowerShell", PathType.LogName,
                    "*[System[(EventID=400 or EventID=4104)]]");
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    int count = 0;
                    while ((record = reader.ReadEvent()) != null && count < MaxEntriesPerLog)
                    {
                        var ev = ParseEventRecord(record);
                        if (ev != null)
                        {
                            events.Add(ev);
                            count++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[PowerShellCollector] Error reading PowerShell log: {0}", ex.Message));
            }
            return events;
        }

        public void StartMonitoring(Action<TelemetryEvent> onEvent)
        {
            try
            {
                var watcher = new EventLogWatcher(new EventLogQuery("Windows PowerShell", PathType.LogName,
                    "*[System[(EventID=400 or EventID=4104)]]"));
                watcher.EventRecordWritten += (sender, args) =>
                {
                    var ev = ParseEventRecord(args.EventRecord);
                    if (ev != null) onEvent(ev);
                };
                watcher.Enabled = true;
                _activeWatchers.Add(watcher);
                Console.WriteLine("[PowerShellCollector] Real‑time monitoring started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[PowerShellCollector] Monitoring error: {0}", ex.Message));
            }
        }

        public void StopMonitoring()
        {
            foreach (var w in _activeWatchers)
            {
                try { w.Enabled = false; w.Dispose(); } catch { }
            }
            _activeWatchers.Clear();
        }

        private TelemetryEvent ParseEventRecord(EventRecord record)
        {
            try
            {
                var ev = new TelemetryEvent
                {
                    Timestamp = record.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
                    EventType = "PowerShell",
                    Action = record.Id == 400 ? "ScriptBlock" : "Command",
                    Target = record.Properties.Count > 0 ? record.Properties[0].Value?.ToString() ?? "PowerShell" : "PowerShell",
                    Status = "Success"
                };
                ev.User = record.UserId?.ToString();
                ev.ActorName = record.ProviderName;
                ev.Details.Add("EventID", record.Id);
                ev.Details.Add("LogName", record.LogName);
                ev.Details.Add("Message", record.FormatDescription());
                // Attempt to capture ProcessId if available
                try
                {
                    var pidObj = record.Properties["ProcessId"].Value;
                    if (pidObj != null)
                        ev.Details.Add("ProcessId", Convert.ToInt32(pidObj));
                }
                catch { }
                return ev;
            }
            catch
            {
                return null;
            }
        }
    }
}
