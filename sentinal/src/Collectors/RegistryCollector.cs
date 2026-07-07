using System;
using System.Collections.Generic;
using Microsoft.Win32;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public class RegistryCollector : ICollector
    {
        public string Name
        {
            get { return "RegistryCollector"; }
        }

        private static readonly string[] StartupKeys = new[]
        {
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce"
        };

        public List<TelemetryEvent> Collect()
        {
            var events = new List<TelemetryEvent>();

            CollectFromHive(Registry.CurrentUser, "HKCU", events);
            CollectFromHive(Registry.LocalMachine, "HKLM", events);

            return events;
        }

        public void StartMonitoring(Action<TelemetryEvent> onEvent)
        {
        }

        public void StopMonitoring()
        {
        }

        private void CollectFromHive(RegistryKey hive, string hiveName, List<TelemetryEvent> events)
        {
            foreach (var subKeyPath in StartupKeys)
            {
                try
                {
                    using (var key = hive.OpenSubKey(subKeyPath, false))
                    {
                        if (key != null)
                        {
                            foreach (var valueName in key.GetValueNames())
                            {
                                try
                                {
                                    var valueData = key.GetValue(valueName);
                                    string dataStr = valueData != null ? valueData.ToString() : string.Empty;
                                    string fullPath = string.Format("{0}\\{1}\\{2}", hiveName, subKeyPath, valueName);

                                    var ev = new TelemetryEvent
                                    {
                                        EventType = "Registry",
                                        Action = "Snapshot",
                                        Target = fullPath,
                                        Status = "Success"
                                    };

                                    ev.Details.Add("Hive", hiveName);
                                    ev.Details.Add("KeyPath", subKeyPath);
                                    ev.Details.Add("ValueName", valueName);
                                    ev.Details.Add("ValueData", dataStr);

                                    events.Add(ev);
                                }
                                catch {}
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[RegistryCollector] Error reading key {0}\\{1}: {2}", hiveName, subKeyPath, ex.Message));
                }
            }
        }
    }
}
