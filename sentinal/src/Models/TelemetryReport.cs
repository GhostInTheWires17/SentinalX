using System;
using System.Collections.Generic;

namespace SentinelX.Models
{
    public class TelemetryReport
    {
        public Dictionary<string, object> System { get; set; }
        public List<TelemetryEvent> Timeline { get; set; }
        public List<string> Users { get; set; }
        public List<TelemetryEvent> Processes { get; set; }
        public List<TelemetryEvent> Files { get; set; }
        public List<TelemetryEvent> Registry { get; set; }
        public List<TelemetryEvent> Services { get; set; }
        public List<TelemetryEvent> Network { get; set; }
        public List<TelemetryEvent> ScheduledTasks { get; set; }
        public SystemChangeReport Changes { get; set; }
        public List<ProcessRelationship> Relationships { get; set; }
        public Dictionary<string, object> Statistics { get; set; }
        public RiskScoreReport RiskAnalysis { get; set; }
        public List<string> PowerShellCommands { get; set; }

        public TelemetryReport()
        {
            System = new Dictionary<string, object>();
            Timeline = new List<TelemetryEvent>();
            Users = new List<string>();
            Processes = new List<TelemetryEvent>();
            Files = new List<TelemetryEvent>();
            Registry = new List<TelemetryEvent>();
            Services = new List<TelemetryEvent>();
            Network = new List<TelemetryEvent>();
            ScheduledTasks = new List<TelemetryEvent>();
            Changes = new SystemChangeReport();
            Relationships = new List<ProcessRelationship>();
            Statistics = new Dictionary<string, object>();
            RiskAnalysis = new RiskScoreReport();
            PowerShellCommands = new List<string>();
        }
    }

    public class SystemChangeReport
    {
        public List<string> AddedProcesses { get; set; }
        public List<string> TerminatedProcesses { get; set; }
        public List<string> AddedServices { get; set; }
        public List<string> StoppedServices { get; set; }
        public List<string> ModifiedRegistryKeys { get; set; }
        public List<string> AddedScheduledTasks { get; set; }
        public List<string> RemovedScheduledTasks { get; set; }
        public List<string> PowerShellCommands { get; set; }

        public SystemChangeReport()
        {
            AddedProcesses = new List<string>();
            TerminatedProcesses = new List<string>();
            AddedServices = new List<string>();
            StoppedServices = new List<string>();
            ModifiedRegistryKeys = new List<string>();
            AddedScheduledTasks = new List<string>();
            RemovedScheduledTasks = new List<string>();
            PowerShellCommands = new List<string>();
        }
    }

    public class ProcessRelationship
    {
        public string Type { get; set; } 
        public string Source { get; set; } 
        public string Target { get; set; } 
        public string Details { get; set; }
    }

    public class RiskIndicator
    {
        public string Severity { get; set; } 
        public string Title { get; set; }
        public string Description { get; set; }
        public string Artifact { get; set; }
        public int ScoreImpact { get; set; }
    }

    public class RiskScoreReport
    {
        public int TotalRiskScore { get; set; }
        public List<RiskIndicator> TriggeredIndicators { get; set; }

        public RiskScoreReport()
        {
            TriggeredIndicators = new List<RiskIndicator>();
            TotalRiskScore = 0;
        }
    }
}
