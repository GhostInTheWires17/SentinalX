using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public class ProcessCollector : ICollector
    {
        public string Name
        {
            get { return "ProcessCollector"; }
        }

        private ManagementEventWatcher _startWatcher;
        private ManagementEventWatcher _stopWatcher;
        private Action<TelemetryEvent> _eventCallback;
        private bool _isMonitoring = false;

        public List<TelemetryEvent> Collect()
        {
            var events = new List<TelemetryEvent>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        var ev = ParseProcessObject(mo, "Snapshot");
                        if (ev != null)
                        {
                            events.Add(ev);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[ProcessCollector] WMI query failed: {0}. Falling back to standard process collection.", ex.Message));
                // Fallback to standard Process class if WMI fails
                try
                {
                    foreach (var p in Process.GetProcesses())
                    {
                        var ev = new TelemetryEvent
                        {
                            EventType = "Process",
                            Action = "Snapshot",
                            ActorPid = p.Id,
                            ActorName = p.ProcessName,
                            Target = p.ProcessName,
                            Status = "Success"
                        };
                        try
                        {
                            ev.Target = p.MainModule.FileName;
                            ev.Details.Add("ExecutablePath", p.MainModule.FileName);
                        }
                        catch
                        {
                            ev.Details.Add("ExecutablePath", "Access Denied");
                        }
                        events.Add(ev);
                    }
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine(string.Format("[ProcessCollector] Error collecting processes: {0}", fallbackEx.Message));
                }
            }
            return events;
        }

        public void StartMonitoring(Action<TelemetryEvent> onEvent)
        {
            if (_isMonitoring) return;
            _eventCallback = onEvent;
            _isMonitoring = true;

            try
            {
                // Watch for process starts
                var startQuery = new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1), "TargetInstance isa 'Win32_Process'");
                _startWatcher = new ManagementEventWatcher(startQuery);
                _startWatcher.EventArrived += StartWatcher_EventArrived;
                _startWatcher.Start();

                // Watch for process stops
                var stopQuery = new WqlEventQuery("__InstanceDeletionEvent", new TimeSpan(0, 0, 1), "TargetInstance isa 'Win32_Process'");
                _stopWatcher = new ManagementEventWatcher(stopQuery);
                _stopWatcher.EventArrived += StopWatcher_EventArrived;
                _stopWatcher.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[ProcessCollector] Real-time monitoring initialization failed: {0}. Process monitoring will rely on snapshots.", ex.Message));
            }
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;
            _isMonitoring = false;

            try
            {
                if (_startWatcher != null)
                {
                    _startWatcher.Stop();
                    _startWatcher.Dispose();
                }
                if (_stopWatcher != null)
                {
                    _stopWatcher.Stop();
                    _stopWatcher.Dispose();
                }
            }
            catch {}
        }

        private void StartWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                var ev = ParseProcessObject(instance, "Created");
                if (ev != null && _eventCallback != null)
                {
                    _eventCallback(ev);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[ProcessCollector] Event error: {0}", ex.Message));
            }
        }

        private void StopWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                var ev = ParseProcessObject(instance, "Terminated");
                if (ev != null && _eventCallback != null)
                {
                    _eventCallback(ev);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[ProcessCollector] Event error: {0}", ex.Message));
            }
        }

        private TelemetryEvent ParseProcessObject(ManagementBaseObject mo, string action)
        {
            try
            {
                var pidVal = mo["ProcessId"];
                var parentPidVal = mo["ParentProcessId"];
                var nameVal = mo["Name"];
                var pathVal = mo["ExecutablePath"];
                var cmdVal = mo["CommandLine"];

                int pid = pidVal != null ? Convert.ToInt32(pidVal) : 0;
                int parentPid = parentPidVal != null ? Convert.ToInt32(parentPidVal) : 0;
                string name = nameVal != null ? nameVal.ToString() : "Unknown";
                string path = pathVal != null ? pathVal.ToString() : "";
                string cmd = cmdVal != null ? cmdVal.ToString() : "";

                var ev = new TelemetryEvent
                {
                    EventType = "Process",
                    Action = action,
                    ActorPid = parentPid,
                    ActorName = "SystemParent", 
                    Target = name,
                    Status = "Success"
                };

                ev.Details.Add("ProcessId", pid);
                ev.Details.Add("ParentProcessId", parentPid);
                ev.Details.Add("Name", name);
                ev.Details.Add("ExecutablePath", path);
                ev.Details.Add("CommandLine", cmd);

                // Get Process Owner
                var activeMo = mo as ManagementObject;
                if (activeMo != null)
                {
                    try
                    {
                        var ownerArgs = new object[] { string.Empty, string.Empty };
                        var result = Convert.ToInt32(activeMo.InvokeMethod("GetOwner", ownerArgs));
                        if (result == 0)
                        {
                            string ownerArgs0 = ownerArgs[0] != null ? ownerArgs[0].ToString() : string.Empty;
                            string ownerArgs1 = ownerArgs[1] != null ? ownerArgs[1].ToString() : string.Empty;

                            string user = string.IsNullOrEmpty(ownerArgs1) 
                                ? ownerArgs0 
                                : string.Format("{0}\\{1}", ownerArgs1, ownerArgs0);
                            ev.User = user;
                            ev.Details.Add("Owner", user);
                        }
                    }
                    catch
                    {
                        ev.User = "System / Unknown";
                    }
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
