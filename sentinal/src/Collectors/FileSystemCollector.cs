using System;
using System.Collections.Generic;
using System.IO;
using SentinelX.Models;

namespace SentinelX.Collectors
{
    public class FileSystemCollector : ICollector
    {
        public string Name
        {
            get { return "FileSystemCollector"; }
        }

        private List<FileSystemWatcher> _watchers;
        private Action<TelemetryEvent> _eventCallback;
        private bool _isMonitoring = false;

        public List<TelemetryEvent> Collect()
        {
            var events = new List<TelemetryEvent>();
            try
            {
                var pathsToScan = new List<string>
                {
                    Path.GetTempPath(),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                };

                foreach (var path in pathsToScan)
                {
                    if (!Directory.Exists(path)) continue;

                    var dirInfo = new DirectoryInfo(path);
                    var files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);

                    var recentFiles = new List<FileInfo>(files);
                    recentFiles.Sort((x, y) => y.LastWriteTime.CompareTo(x.LastWriteTime));

                    int count = 0;
                    foreach (var file in recentFiles)
                    {
                        if (count++ >= 20) break;

                        var ev = new TelemetryEvent
                        {
                            EventType = "FileSystem",
                            Action = "Snapshot",
                            Target = file.FullName,
                            Timestamp = file.LastWriteTime.ToUniversalTime(),
                            Status = "Success"
                        };
                        ev.Details.Add("FileName", file.Name);
                        ev.Details.Add("Length", file.Length);
                        ev.Details.Add("Extension", file.Extension);
                        ev.Details.Add("LastWriteTime", file.LastWriteTime);

                        events.Add(ev);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[FileSystemCollector] Error scanning files: {0}", ex.Message));
            }
            return events;
        }

        public void StartMonitoring(Action<TelemetryEvent> onEvent)
        {
            if (_isMonitoring) return;
            _eventCallback = onEvent;
            _watchers = new List<FileSystemWatcher>();
            _isMonitoring = true;

            var pathsToWatch = new List<string>
            {
                Path.GetTempPath(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };

            foreach (var path in pathsToWatch)
            {
                try
                {
                    if (!Directory.Exists(path)) continue;

                    var watcher = new FileSystemWatcher
                    {
                        Path = path,
                        Filter = "*.*",
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.DirectoryName,
                        IncludeSubdirectories = false
                    };

                    watcher.Created += Watcher_Event;
                    watcher.Changed += Watcher_Event;
                    watcher.Deleted += Watcher_Event;
                    watcher.Renamed += Watcher_RenamedEvent;

                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[FileSystemCollector] Failed to watch path '{0}': {1}", path, ex.Message));
                }
            }
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;
            _isMonitoring = false;

            if (_watchers != null)
            {
                foreach (var watcher in _watchers)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                    catch {}
                }
                _watchers.Clear();
            }
        }

        private void Watcher_Event(object sender, FileSystemEventArgs e)
        {
            if (_eventCallback == null) return;

            var ev = new TelemetryEvent
            {
                EventType = "FileSystem",
                Action = e.ChangeType.ToString(),
                Target = e.FullPath,
                Status = "Success"
            };
            ev.Details.Add("FileName", e.Name);
            ev.Details.Add("FullPath", e.FullPath);

            _eventCallback(ev);
        }

        private void Watcher_RenamedEvent(object sender, RenamedEventArgs e)
        {
            if (_eventCallback == null) return;

            var ev = new TelemetryEvent
            {
                EventType = "FileSystem",
                Action = "Renamed",
                Target = e.FullPath,
                Status = "Success"
            };
            ev.Details.Add("FileName", e.Name);
            ev.Details.Add("FullPath", e.FullPath);
            ev.Details.Add("OldFileName", e.OldName);
            ev.Details.Add("OldFullPath", e.OldFullPath);

            _eventCallback(ev);
        }
    }
}
