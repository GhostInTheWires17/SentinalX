using System;
using System.Collections.Generic;

namespace SentinelX.Models
{
    public class TelemetryEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; } // Process, Network, Registry, FileSystem, Service, EventLog
        public string Action { get; set; }    // Snapshot, Created, Terminated, Connected, Modified, Deleted, etc.
        public int? ActorPid { get; set; }
        public string ActorName { get; set; }
        public string User { get; set; }
        public string Target { get; set; }
        public string Status { get; set; }    // Success, Failure, Unknown
        public Dictionary<string, object> Details { get; set; }
        public string RawData { get; set; }

        public TelemetryEvent()
        {
            Timestamp = DateTime.UtcNow;
            EventId = Guid.NewGuid().ToString();
            Details = new Dictionary<string, object>();
            Status = "Success";
        }
    }
}
