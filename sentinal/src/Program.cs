using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SentinelX.Collectors;
using SentinelX.Engine;
using SentinelX.Models;

namespace SentinelX
{
    class Program
    {
        // Placeholder URL for the Reasoning AI server (User can replace this with their real URL)
        private const string DefaultAiUrl = "https://api.sentinelx.security/v1/analyze";

        static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("SentinelX -- Windows Cyber Investigation Agent");
            Console.WriteLine("=================================================");

            int durationSeconds = 10;
            int intervalSeconds = 60;
            string outputPath = "sentinelx_report.json";
            string uploadUrl = DefaultAiUrl;
            bool shouldUpload = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--duration", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out durationSeconds);
                    i++;
                }
                else if (args[i].Equals("--interval", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out intervalSeconds);
                    i++;
                }
                else if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                    i++;
                }
                else if (args[i].Equals("--upload-url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    uploadUrl = args[i + 1];
                    shouldUpload = true;
                    i++;
                }
                else if (args[i].Equals("--upload", StringComparison.OrdinalIgnoreCase))
                {
                    shouldUpload = true;
                }
                else if (args[i].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[i].Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    ShowHelp();
                    return;
                }
            }

            while (true)
            {
                RunCycle(durationSeconds, outputPath, uploadUrl, shouldUpload);
                Console.WriteLine(string.Format("\n[*] Sleeping for {0} seconds before next cycle...", intervalSeconds));
                Thread.Sleep(intervalSeconds * 1000);
            }
        }

        private static void RunCycle(int durationSeconds, string outputPath, string uploadUrl, bool shouldUpload)
        {
            Console.WriteLine(string.Format("[*] Target monitoring duration: {0} seconds", durationSeconds));
            Console.WriteLine(string.Format("[*] Target report output path:  {0}", Path.GetFullPath(outputPath)));
            if (shouldUpload)
            {
                Console.WriteLine(string.Format("[*] Upload AI reasoning URL:   {0}", uploadUrl));
            }
            else
            {
                Console.WriteLine(string.Format("[*] AI Reasoning URL:          {0} (Pass --upload to transmit)", uploadUrl));
            }

            var report = new TelemetryReport();
            
            report.System.Add("Hostname", Environment.MachineName);
            report.System.Add("OsVersion", Environment.OSVersion.ToString());
            report.System.Add("Username", string.Format("{0}\\{1}", Environment.UserDomainName, Environment.UserName));
            report.System.Add("ScanStart", DateTime.UtcNow);

            Console.WriteLine("\n[*] Initializing collectors...");
            var collectors = new List<ICollector>
            {
                new ProcessCollector(),
                new NetworkCollector(),
                new RegistryCollector(),
                new FileSystemCollector(),
                new ServiceCollector(),
                new EventLogCollector(),
                new PowerShellCollector()
            };

            // 1. Collect baseline snapshot
            Console.WriteLine("[*] Collecting baseline telemetry snapshot...");
            var baselineEvents = new List<TelemetryEvent>();
            foreach (var col in collectors)
            {
                try
                {
                    Console.WriteLine(string.Format("    -> Gathering from {0}...", col.Name));
                    var events = col.Collect();
                    baselineEvents.AddRange(events);
                    Console.WriteLine(string.Format("       [+] Collected {0} items.", events.Count));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("       [!] Collector {0} failed: {1}", col.Name, ex.Message));
                }
            }

            // 2. Start real-time monitoring
            Console.WriteLine("\n[*] Launching real-time monitoring hooks...");
            var dynamicEvents = new List<TelemetryEvent>();
            object lockObject = new object();
            
            Action<TelemetryEvent> onEventReceived = delegate(TelemetryEvent ev)
            {
                lock (lockObject)
                {
                    dynamicEvents.Add(ev);
                }
                Console.WriteLine(string.Format("[Event] [{0}] {1} -> {2}", ev.EventType, ev.Action, ev.Target));
            };

            foreach (var col in collectors)
            {
                try
                {
                    col.StartMonitoring(onEventReceived);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("    [!] Failed to start monitoring on {0}: {1}", col.Name, ex.Message));
                }
            }

            Console.WriteLine(string.Format("[*] Monitoring system changes active. Sleeping for {0} seconds...", durationSeconds));
            Thread.Sleep(durationSeconds * 1000);

            // 3. Stop monitoring
            Console.WriteLine("\n[*] Disabling monitoring hooks...");
            foreach (var col in collectors)
            {
                try
                {
                    col.StopMonitoring();
                }
                catch {}
            }

            // 4. Collect post-session snapshot (target)
            Console.WriteLine("[*] Collecting target telemetry snapshot...");
            var targetEvents = new List<TelemetryEvent>();
            foreach (var col in collectors)
            {
                if (col.Name == "ProcessCollector" || col.Name == "ServiceCollector" || col.Name == "RegistryCollector" || col.Name == "PowerShellCollector")
                {
                    try
                    {
                        Console.WriteLine(string.Format("    -> Gathering from {0}...", col.Name));
                        var events = col.Collect();
                        targetEvents.AddRange(events);
                        Console.WriteLine(string.Format("       [+] Collected {0} items.", events.Count));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("       [!] Collector {0} failed: {1}", col.Name, ex.Message));
                    }
                }
            }

            report.System.Add("ScanEnd", DateTime.UtcNow);

            // 5. Correlate and Build Timeline
            Console.WriteLine("\n[*] Correlation & Timeline Builder Engine starting...");
            var rawEvents = new List<TelemetryEvent>();
            rawEvents.AddRange(baselineEvents);
            lock (lockObject)
            {
                rawEvents.AddRange(dynamicEvents);
            }
            rawEvents.AddRange(targetEvents);

            var timelineBuilder = new TimelineBuilder();
            report.Timeline = timelineBuilder.BuildTimeline(rawEvents);
            // Extract distinct PowerShell command lines for report
            report.PowerShellCommands = report.Timeline
                .Where(ev => ev.EventType == "PowerShell")
                .Select(ev => ev.Details.ContainsKey("CommandLine") ? ev.Details["CommandLine"].ToString() : ev.Target)
                .Distinct()
                .ToList();
            Console.WriteLine(string.Format("[+] Correlated & compiled {0} chronological incident events.", report.Timeline.Count));

            // Populate categorized sections in report based on correlated events
            var uniqueUsers = new HashSet<string>();
            foreach (var ev in report.Timeline)
            {
                if (!string.IsNullOrEmpty(ev.User))
                {
                    uniqueUsers.Add(ev.User);
                }

                if (ev.EventType == "Process")
                {
                    report.Processes.Add(ev);
                }
                else if (ev.EventType == "Network")
                {
                    report.Network.Add(ev);
                }
                else if (ev.EventType == "Registry")
                {
                    report.Registry.Add(ev);
                }
                else if (ev.EventType == "FileSystem")
                {
                    report.Files.Add(ev);
                }
                else if (ev.EventType == "Service")
                {
                    report.Services.Add(ev);
                }
            }
            report.Users.AddRange(uniqueUsers);

            // Build Relationships
            Console.WriteLine("[*] Building entity relationship graph...");
            foreach (var ev in report.Timeline)
            {
                if (ev.EventType == "Process" && ev.Details.ContainsKey("ParentProcessId"))
                {
                    int parentPid = Convert.ToInt32(ev.Details["ParentProcessId"]);
                    int pid = ev.Details.ContainsKey("ProcessId") ? Convert.ToInt32(ev.Details["ProcessId"]) : 0;
                    string parentName = ev.ActorName;
                    string childName = ev.Target;

                    if (parentPid > 0 && pid > 0)
                    {
                        var rel = new ProcessRelationship
                        {
                            Type = "Spawn",
                            Source = string.Format("{0} (PID {1})", parentName, parentPid),
                            Target = string.Format("{0} (PID {1})", childName, pid),
                            Details = string.Format("Command Line: {0}", ev.Details.ContainsKey("CommandLine") ? ev.Details["CommandLine"].ToString() : string.Empty)
                        };
                        report.Relationships.Add(rel);
                    }
                }
                else if (ev.EventType == "Network" && ev.ActorPid.HasValue)
                {
                    var rel = new ProcessRelationship
                    {
                        Type = "NetworkConnection",
                        Source = string.Format("{0} (PID {1})", ev.ActorName, ev.ActorPid.Value),
                        Target = ev.Target,
                        Details = string.Format("Protocol: {0}, LocalAddress: {1}", 
                            ev.Details.ContainsKey("Protocol") ? ev.Details["Protocol"].ToString() : string.Empty,
                            ev.Details.ContainsKey("LocalAddress") ? ev.Details["LocalAddress"].ToString() : string.Empty)
                    };
                    report.Relationships.Add(rel);
                }
                else if (ev.EventType == "FileSystem" && ev.ActorPid.HasValue)
                {
                    var rel = new ProcessRelationship
                    {
                        Type = "FileAccess",
                        Source = string.Format("{0} (PID {1})", ev.ActorName, ev.ActorPid.Value),
                        Target = ev.Target,
                        Details = string.Format("Action: {0}", ev.Action)
                    };
                    report.Relationships.Add(rel);
                }
            }

            // 6. Detect System Changes
            Console.WriteLine("[*] Change Detection Engine checking system baseline deviations...");
            var changeDetector = new ChangeDetector();
            report.Changes = changeDetector.DetectChanges(baselineEvents, targetEvents);

            Console.WriteLine(string.Format("    [+] Added Processes:   {0}", report.Changes.AddedProcesses.Count));
            Console.WriteLine(string.Format("    [+] Terminated Processes: {0}", report.Changes.TerminatedProcesses.Count));
            Console.WriteLine(string.Format("    [+] Added Services:    {0}", report.Changes.AddedServices.Count));
            Console.WriteLine(string.Format("    [+] Stopped Services:  {0}", report.Changes.StoppedServices.Count));
            Console.WriteLine(string.Format("    [+] Registry Changes:  {0}", report.Changes.ModifiedRegistryKeys.Count));

            // 7. Advanced Heuristic Analysis and Risk Scoring
            Console.WriteLine("[*] Advanced Heuristics Engine running cyber risk analysis...");
            var riskScorer = new RiskScorer();
            report.RiskAnalysis = riskScorer.Analyze(report);
            Console.WriteLine(string.Format("    [+] Triggered IOCs:    {0}", report.RiskAnalysis.TriggeredIndicators.Count));
            Console.WriteLine(string.Format("    [+] Threat Risk Score: {0}/100", report.RiskAnalysis.TotalRiskScore));
            foreach (var ind in report.RiskAnalysis.TriggeredIndicators)
            {
                Console.WriteLine(string.Format("       - [{0}] {1}: {2} (Impact: +{3})", ind.Severity, ind.Title, ind.Artifact, ind.ScoreImpact));
            }

            // 8. Calculate Statistics
            report.Statistics.Add("TotalTimelineEvents", report.Timeline.Count);
            report.Statistics.Add("UniqueUsersCount", report.Users.Count);
            report.Statistics.Add("ProcessSnapshotCount", report.Processes.Count);
            report.Statistics.Add("NetworkConnectionsCount", report.Network.Count);
            report.Statistics.Add("RegistryStartupsCount", report.Registry.Count);
            report.Statistics.Add("RecentFilesCount", report.Files.Count);
            report.Statistics.Add("ServicesCount", report.Services.Count);
            report.Statistics.Add("RelationshipsCount", report.Relationships.Count);
            report.Statistics.Add("ThreatRiskScore", report.RiskAnalysis.TotalRiskScore);
            report.Statistics.Add("TriggeredIndicatorsCount", report.RiskAnalysis.TriggeredIndicators.Count);

            // 9. Serialize and Write Report
            Console.WriteLine(string.Format("\n[*] Serializing report to JSON using SimpleJson..."));
            string jsonOutput = null;
            try
            {
                jsonOutput = SimpleJson.Serialize(report);
                File.WriteAllText(outputPath, jsonOutput);
                Console.WriteLine("[+] SentinelX Report generated successfully!");
                Console.WriteLine(string.Format("[+] File size: {0} bytes", new FileInfo(outputPath).Length));
                Console.WriteLine(string.Format("[+] Output path: {0}", Path.GetFullPath(outputPath)));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Failed to generate report: {0}", ex.Message));
            }

            // 10. HTTP Upload to AI Reasoning Endpoint
            if (shouldUpload && !string.IsNullOrEmpty(jsonOutput))
            {
                Console.WriteLine("\n[*] Transmitting telemetry package to Reasoning AI...");
                bool uploadSuccess = ReportUploader.UploadReport(uploadUrl, jsonOutput);
                if (!uploadSuccess && uploadUrl == DefaultAiUrl)
                {
                    Console.WriteLine("\n[!] Technical Guidance: The telemetry upload failed because the destination is a placeholder URL.");
                    Console.WriteLine("    To hook up your live reasoning AI, modify the 'DefaultAiUrl' constant in 'Program.cs'");
                    Console.WriteLine("    or execute the agent passing the --upload-url parameter, for example:");
                    Console.WriteLine("    SentinelX.exe --upload --upload-url http://your-reasoning-ai-ip:5000/api/v1/telemetry");
                }
            }
            else
            {
                Console.WriteLine("\n[*] Run with '--upload' or '--upload-url <url>' to transmit this telemetry to your Reasoning AI.");
            }

            Console.WriteLine("\n[*] Exiting. SentinelX scan complete.");
        }

        static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  SentinelX.exe [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --duration <seconds>   Time to monitor active changes (default: 10)");
            Console.WriteLine("  --output <filepath>    Path to write the JSON report (default: sentinelx_report.json)");
            Console.WriteLine("  --upload               Transmission flag to send JSON telemetry report to Reasoning AI");
            Console.WriteLine("  --upload-url <url>     Specific endpoint to HTTP POST telemetry report to");
            Console.WriteLine("  --help, -h             Show this help screen");
        }
    }
}
