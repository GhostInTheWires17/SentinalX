using System;
using System.Collections.Generic;
using System.Diagnostics;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public class EventLogCollector : ICollector
    {
        public string Name
        {
            get { return "EventLogCollector"; }
        }

        private const int MaxEntriesPerLog = 30;

        public List<TelemetryEvent> Collect()
        {
            var events = new List<TelemetryEvent>();
            
            ReadLog("System", events);
            ReadLog("Application", events);
            ReadLog("Windows PowerShell", events);

            try
            {
                ReadLog("Security", events);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[EventLogCollector] Warning: Could not read Security logs (requires Admin privileges): {0}", ex.Message));
            }

            return events;
        }

        public void StartMonitoring(Action<TelemetryEvent> onEvent)
        {
            try
            {
                RegisterLogWatcher("System", onEvent);
                RegisterLogWatcher("Application", onEvent);
                RegisterLogWatcher("Windows PowerShell", onEvent);
                try
                {
                    RegisterLogWatcher("Security", onEvent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[EventLogCollector] Monitoring: Could not register Security log watcher: {0}", ex.Message));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[EventLogCollector] Monitoring: Failed to register event log watch: {0}", ex.Message));
            }
        }

        private List<EventLog> _activeLogs = new List<EventLog>();

        public void StopMonitoring()
        {
            foreach (var log in _activeLogs)
            {
                try
                {
                    log.EnableRaisingEvents = false;
                    log.Dispose();
                }
                catch {}
            }
            _activeLogs.Clear();
        }

        private void RegisterLogWatcher(string logName, Action<TelemetryEvent> onEvent)
        {
            var log = new EventLog(logName);
            log.EntryWritten += (sender, e) =>
            {
                try
                {
                    var ev = ParseEventEntry(e.Entry, logName, "Realtime");
                    if (ev != null)
                    {
                        onEvent(ev);
                    }
                }
                catch {}
            };
            log.EnableRaisingEvents = true;
            _activeLogs.Add(log);
        }

        private void ReadLog(string logName, List<TelemetryEvent> events)
        {
            try
            {
                if (!EventLog.Exists(logName)) return;

                using (var log = new EventLog(logName))
                {
                    var entries = log.Entries;
                    int count = entries.Count;
                    int readCount = 0;

                    for (int i = count - 1; i >= 0 && readCount < MaxEntriesPerLog; i--)
                    {
                        try
                        {
                            var entry = entries[i];
                            var ev = ParseEventEntry(entry, logName, "Snapshot");
                            if (ev != null)
                            {
                                events.Add(ev);
                                readCount++;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[EventLogCollector] Error reading log '{0}': {1}", logName, ex.Message));
            }
        }

        private TelemetryEvent ParseEventEntry(EventLogEntry entry, string logName, string source)
        {
            try
            {
                var ev = new TelemetryEvent
                {
                    Timestamp = entry.TimeGenerated.ToUniversalTime(),
                    EventType = "EventLog",
                    Action = source,
                    Target = string.Format("{0}/{1}", logName, entry.InstanceId),
                    Status = "Success"
                };

                ev.User = entry.UserName;
                ev.ActorName = entry.Source;

                ev.Details.Add("LogName", logName);
                ev.Details.Add("Source", entry.Source);
                ev.Details.Add("InstanceId", entry.InstanceId);
                ev.Details.Add("EntryType", entry.EntryType.ToString());
                ev.Details.Add("Message", entry.Message);

                if (entry.InstanceId == 4688)
                {
                    ev.Action = "ProcessCreateLog";
                }
                else if (entry.InstanceId == 4624)
                {
                    ev.Action = "UserLogonLog";
                }
                else if (entry.InstanceId == 4625)
                {
                    ev.Action = "UserLogonFailureLog";
                    ev.Status = "Failure";
                }

                return ev;
            }
            catch
            {
                return null;
            }
        }
    }
}
