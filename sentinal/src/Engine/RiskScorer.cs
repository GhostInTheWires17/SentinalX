using System;
using System.Collections.Generic;
using SentinelX.Models;

namespace SentinelX.Engine
{
    public class RiskScorer
    {
        public RiskScoreReport Analyze(TelemetryReport report)
        {
            var riskReport = new RiskScoreReport();
            var indicators = riskReport.TriggeredIndicators;

            // Track login failures to detect brute force
            int logonFailures = 0;

            foreach (var ev in report.Timeline)
            {
                // 1. Process heuristic checks
                if (ev.EventType == "Process")
                {
                    string name = ev.Target.ToLower();
                    string cmdLine = ev.Details.ContainsKey("CommandLine") ? ev.Details["CommandLine"].ToString().ToLower() : string.Empty;
                    string path = ev.Details.ContainsKey("ExecutablePath") ? ev.Details["ExecutablePath"].ToString().ToLower() : string.Empty;
                    string parentName = ev.Details.ContainsKey("ParentProcessName") ? ev.Details["ParentProcessName"].ToString().ToLower() : string.Empty;

                    // Suspicious PowerShell commands
                    if (name.Contains("powershell") || name.Contains("pwsh"))
                    {
                        if (cmdLine.Contains("-enc") || cmdLine.Contains("-encodedcommand") || cmdLine.Contains("bypass") || cmdLine.Contains("downloadstring") || cmdLine.Contains("iex") || cmdLine.Contains("invoke-expression"))
                        {
                            indicators.Add(new RiskIndicator
                            {
                                Severity = "High",
                                Title = "Suspicious PowerShell Command Execution",
                                Description = "PowerShell was executed with parameters commonly used to bypass execution policies, execute encoded code, or download remote payloads.",
                                Artifact = string.Format("{0} -> {1}", ev.ActorName, ev.Target),
                                ScoreImpact = 35
                            });
                        }
                    }

                    // Shadow copy deletion (Ransomware behavior)
                    if (name.Contains("vssadmin") && (cmdLine.Contains("delete") && cmdLine.Contains("shadows")))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Severity = "Critical",
                            Title = "Volume Shadow Copy Deletion",
                            Description = "Vssadmin was used to delete Volume Shadow Copies. This is a common pre-cursor to ransomware encryption to prevent file recovery.",
                            Artifact = cmdLine,
                            ScoreImpact = 60
                        });
                    }

                    // Discovery tools
                    if (name == "whoami.exe" || name == "nltest.exe" || name == "quser.exe" || name == "qwinsta.exe" || name == "net.exe" && (cmdLine.Contains("user") || cmdLine.Contains("group") || cmdLine.Contains("localgroup")))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Severity = "Medium",
                            Title = "System Discovery Activity",
                            Description = "Standard Windows discovery commands were executed to enumerate users, domain settings, or active sessions.",
                            Artifact = cmdLine,
                            ScoreImpact = 15
                        });
                    }

                    // Execution from temporary directories
                    if (path.Contains("\\temp\\") || path.Contains("\\appdata\\local\\temp\\") || path.Contains("\\users\\public\\"))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Severity = "Medium",
                            Title = "Execution from Suspicious Directory",
                            Description = "A process executable was launched from a temporary or public directory, which is common for initial access payloads.",
                            Artifact = path,
                            ScoreImpact = 20
                        });
                    }

                    // Web server spawning command line (Web shell indicator)
                    if ((parentName.Contains("w3wp.exe") || parentName.Contains("tomcat") || parentName.Contains("nginx") || parentName.Contains("httpd")) &&
                        (name.Contains("cmd.exe") || name.Contains("powershell.exe")))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Severity = "Critical",
                            Title = "Shell Spawned by Web Application Server",
                            Description = "An interactive command shell was spawned by a web server process. This is a strong indicator of an active web shell exploitation.",
                            Artifact = string.Format("{0} spawned {1}", ev.ActorName, ev.Target),
                            ScoreImpact = 80
                        });
                    }
                }

                // 2. Network connection checks
                else if (ev.EventType == "Network")
                {
                    string actor = ev.ActorName.ToLower();
                    string target = ev.Target.ToLower();

                    // Shell process opening a network connection (Reverse Shell indicator)
                    if (actor.Contains("cmd.exe") || actor.Contains("powershell.exe") || actor.Contains("bash.exe"))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Severity = "High",
                            Title = "Command Shell Outbound Network Connection",
                            Description = "A command shell process initiated an outbound network connection, which is characteristic of a reverse shell C2 connection.",
                            Artifact = string.Format("Process: {0}, Remote: {1}", ev.ActorName, ev.Target),
                            ScoreImpact = 45
                        });
                    }
                }

                // 3. Registry startup persistence changes
                else if (ev.EventType == "Registry" && ev.Action == "Snapshot")
                {
                    string valData = ev.Details.ContainsKey("ValueData") ? ev.Details["ValueData"].ToString().ToLower() : string.Empty;
                    if (valData.Contains(".bat") || valData.Contains(".vbs") || valData.Contains(".ps1") || valData.Contains("wscript") || valData.Contains("cscript") || valData.Contains("temp"))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Severity = "High",
                            Title = "Suspicious Autostart Registry Configuration",
                            Description = "A registry run/startup entry was identified pointing to a script or temporary folder. This is a common persistence technique.",
                            Artifact = ev.Target,
                            ScoreImpact = 25
                        });
                    }
                }

                // 4. Logon Failure event logs
                else if (ev.EventType == "EventLog" && ev.Action == "UserLogonFailureLog")
                {
                    logonFailures++;
                }
            }

            // Brute force logon checks
            if (logonFailures >= 5)
            {
                indicators.Add(new RiskIndicator
                {
                    Severity = "Medium",
                    Title = "Multiple Failed Logons Detected",
                    Description = string.Format("Identified {0} logon failures during the monitoring session, suggesting potential brute force login attempts.", logonFailures),
                    Artifact = "Event Log Event ID 4625",
                    ScoreImpact = 15
                });
            }

            // Deduplicate indicators to avoid noise
            var uniqueIndicators = new List<RiskIndicator>();
            var keys = new HashSet<string>();
            foreach (var ind in indicators)
            {
                string key = string.Format("{0}_{1}_{2}", ind.Title, ind.Severity, ind.Artifact);
                if (!keys.Contains(key))
                {
                    keys.Add(key);
                    uniqueIndicators.Add(ind);
                }
            }
            riskReport.TriggeredIndicators = uniqueIndicators;

            // Calculate overall risk score (capped at 100)
            int score = 0;
            foreach (var ind in uniqueIndicators)
            {
                score += ind.ScoreImpact;
            }
            riskReport.TotalRiskScore = Math.Min(score, 100);

            return riskReport;
        }
    }
}
