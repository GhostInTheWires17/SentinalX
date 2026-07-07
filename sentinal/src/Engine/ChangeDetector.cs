using System;
using System.Collections.Generic;
using SentinelX.Models;

namespace SentinelX.Engine
{
    public class ChangeDetector
    {
        public SystemChangeReport DetectChanges(List<TelemetryEvent> baseline, List<TelemetryEvent> target)
        {
            var report = new SystemChangeReport();

            // 1. Process comparison
            var baselineProcesses = new Dictionary<string, TelemetryEvent>();
            var targetProcesses = new Dictionary<string, TelemetryEvent>();

            foreach (var ev in baseline)
            {
                if (ev.EventType == "Process" && ev.Details.ContainsKey("ProcessId"))
                {
                    string key = string.Format("{0}_{1}", ev.Details["ProcessId"], ev.Target);
                    baselineProcesses[key] = ev;
                }
            }

            foreach (var ev in target)
            {
                if (ev.EventType == "Process" && ev.Details.ContainsKey("ProcessId"))
                {
                    string key = string.Format("{0}_{1}", ev.Details["ProcessId"], ev.Target);
                    targetProcesses[key] = ev;
                }
            }

            foreach (var key in targetProcesses.Keys)
            {
                if (!baselineProcesses.ContainsKey(key))
                {
                    var proc = targetProcesses[key];
                    report.AddedProcesses.Add(string.Format("PID {0}: {1} ({2})", proc.Details["ProcessId"], proc.Target, proc.Details["CommandLine"]));
                }
            }

            foreach (var key in baselineProcesses.Keys)
            {
                if (!targetProcesses.ContainsKey(key))
                {
                    var proc = baselineProcesses[key];
                    report.TerminatedProcesses.Add(string.Format("PID {0}: {1}", proc.Details["ProcessId"], proc.Target));
                }
            }

            // 2. Service comparison
            var baselineServices = new Dictionary<string, string>();
            var targetServices = new Dictionary<string, string>();

            foreach (var ev in baseline)
            {
                if (ev.EventType == "Service" && ev.Details.ContainsKey("ServiceName"))
                {
                    string name = ev.Details["ServiceName"].ToString();
                    string status = ev.Details.ContainsKey("Status") ? ev.Details["Status"].ToString() : string.Empty;
                    baselineServices[name] = status;
                }
            }

            foreach (var ev in target)
            {
                if (ev.EventType == "Service" && ev.Details.ContainsKey("ServiceName"))
                {
                    string name = ev.Details["ServiceName"].ToString();
                    string status = ev.Details.ContainsKey("Status") ? ev.Details["Status"].ToString() : string.Empty;
                    targetServices[name] = status;
                }
            }

            foreach (var name in targetServices.Keys)
            {
                if (!baselineServices.ContainsKey(name))
                {
                    report.AddedServices.Add(string.Format("{0} (Status: {1})", name, targetServices[name]));
                }
                else if (baselineServices[name] == "Running" && targetServices[name] == "Stopped")
                {
                    report.StoppedServices.Add(string.Format("{0} (Transitioned: Running -> Stopped)", name));
                }
            }

            // 3. Registry startup changes
            var baselineReg = new Dictionary<string, string>();
            var targetReg = new Dictionary<string, string>();

            foreach (var ev in baseline)
            {
                if (ev.EventType == "Registry")
                {
                    string path = ev.Target;
                    string data = ev.Details.ContainsKey("ValueData") ? ev.Details["ValueData"].ToString() : string.Empty;
                    baselineReg[path] = data;
                }
            }

            foreach (var ev in target)
            {
                if (ev.EventType == "Registry")
                {
                    string path = ev.Target;
                    string data = ev.Details.ContainsKey("ValueData") ? ev.Details["ValueData"].ToString() : string.Empty;
                    targetReg[path] = data;
                }
            }

            foreach (var path in targetReg.Keys)
            {
                if (!baselineReg.ContainsKey(path))
                {
                    report.ModifiedRegistryKeys.Add(string.Format("[Added] {0} = {1}", path, targetReg[path]));
                }
                else if (baselineReg[path] != targetReg[path])
                {
                    report.ModifiedRegistryKeys.Add(string.Format("[Modified] {0}: Changed from '{1}' to '{2}'", path, baselineReg[path], targetReg[path]));
                }
            }

            // 4. Scheduled task comparison
            var baselineTasks = new Dictionary<string, string>();
            var targetTasks = new Dictionary<string, string>();

            foreach (var ev in baseline)
            {
                if (ev.EventType == "ScheduledTask" && ev.Details.ContainsKey("TaskName"))
                {
                    string name = ev.Details["TaskName"].ToString();
                    string status = ev.Details.ContainsKey("Status") ? ev.Details["Status"].ToString() : string.Empty;
                    baselineTasks[name] = status;
                }
            }

            foreach (var ev in target)
            {
                if (ev.EventType == "ScheduledTask" && ev.Details.ContainsKey("TaskName"))
                {
                    string name = ev.Details["TaskName"].ToString();
                    string status = ev.Details.ContainsKey("Status") ? ev.Details["Status"].ToString() : string.Empty;
                    targetTasks[name] = status;
                }
            }

            foreach (var name in targetTasks.Keys)
            {
                if (!baselineTasks.ContainsKey(name))
                {
                    report.AddedScheduledTasks.Add(string.Format("{0} (Status: {1})", name, targetTasks[name]));
                }
            }

            foreach (var name in baselineTasks.Keys)
            {
                if (!targetTasks.ContainsKey(name))
                {
                    report.RemovedScheduledTasks.Add(name);
                }
            }

            return report;
        }
    }
}
