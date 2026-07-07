using System;
using System.Collections.Generic;
using System.ServiceProcess;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public class ServiceCollector : ICollector
    {
        public string Name
        {
            get { return "ServiceCollector"; }
        }

        public List<TelemetryEvent> Collect()
        {
            var events = new List<TelemetryEvent>();
            try
            {
                var services = ServiceController.GetServices();
                foreach (var s in services)
                {
                    try
                    {
                        var ev = new TelemetryEvent
                        {
                            EventType = "Service",
                            Action = "Snapshot",
                            Target = s.ServiceName,
                            Status = "Success"
                        };

                        ev.Details.Add("ServiceName", s.ServiceName);
                        ev.Details.Add("DisplayName", s.DisplayName);
                        ev.Details.Add("Status", s.Status.ToString());
                        ev.Details.Add("ServiceType", s.ServiceType.ToString());
                        ev.Details.Add("CanStop", s.CanStop);

                        events.Add(ev);
                    }
                    catch {}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[ServiceCollector] Error reading Windows Services: {0}", ex.Message));
            }
            return events;
        }

        public void StartMonitoring(Action<TelemetryEvent> onEvent)
        {
        }

        public void StopMonitoring()
        {
        }
    }
}
