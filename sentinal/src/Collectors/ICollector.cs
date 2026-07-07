using System;
using System.Collections.Generic;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public interface ICollector
    {
        string Name { get; }
        List<TelemetryEvent> Collect();
        void StartMonitoring(Action<TelemetryEvent> onEvent);
        void StopMonitoring();
    }
}
