using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Org.LoadRunner.Core.Engine;
using Org.LoadRunner.Core.Infrastructure;
using Org.LoadRunner.Core.Logs;
using Org.LoadRunner.Core.Models;
using Org.LoadRunner.Core.Network;

namespace Org.LoadRunner.UI
{
    internal class TaskRunner
    {
        private const string ReportFile = "report.txt";
        private readonly IList<ILog> _logs = new List<ILog> { new ConsoleLog(), new TextLog(ReportFile) };
        private XDocument _loadSet;
        private DistributedEngine _engine;

        internal void Start()
        {
            var taskFile = "Tasks.xml";
            if (File.Exists(taskFile))
            {
                _loadSet = XDocument.Load(taskFile);
                StartServer();
                RunTasks();
            }
            else
                AddLog(string.Format("Task file missing: {0}", taskFile));
        }

        internal void Stop()
        {
            if (_engine != null)
                _engine.StopListening();
        }

        private void StartServer()
        {
            var key = "ServerPrefixes";
            var prefixes = _loadSet.Root.Attributes().Any(a => a.Name == key) ? _loadSet.Root.Attribute(key).Value : string.Empty;
            if (!string.IsNullOrEmpty(prefixes))
            {
                _engine = new DistributedEngine();
                _engine.BatchLoad += (s, e) =>
                    {
                        AddLog(string.Format("Remote batch load requested from: {0}", e.RequestorAddress));
                        e.Load.Name += string.Format(" (distributed from {0})", e.RequestorAddress);
                        TestLoad(e.Load);
                    };
                _engine.DistributedProgress += (s, e) =>
                    {
                        AddLog(string.Format("Remote batch load from: {0} exception: ", e.Url, e.Message));
                    };
                var result = _engine.Start(prefixes.Split(Runtime.PartSplitter));
                if (result)
                    AddLog("HTTP Server is listening...");
                else
                    AddLog("HTTP Server could not start");
            }
        }

        internal void AddDistributedTask(BatchPayload batchPayload)
        {
            if (_engine != null && _engine.IsStarted)
            {
                var servers = _loadSet.Root.Element("RemoteServers").Descendants("Server").Where(s => Convert.ToBoolean(s.Attribute("IsEnabled").Value)).Select(s => s.Value).ToArray();
                servers.ForEach(s =>
                    {
                        AddLog(string.Format("distributing batch load to {0}", s));
                    });
                _engine.Distribute(batchPayload, servers);
            }
        }

        private void RunTasks()
        {
            _loadSet.Descendants("Task").Where(t => Convert.ToBoolean(t.Attribute("IsEnabled").Value)).ForEach(t =>
            {
                var loadSize = Convert.ToInt32(t.Attribute("LoadSize").Value);
                var throughputSize = loadSize / 10;
                var useBatchSet = Convert.ToBoolean(t.Attribute("UseBatchSet").Value);
                var isDistributed = t.Attributes().Any(a => a.Name == "IsDistributed") ? Convert.ToBoolean(t.Attribute("IsDistributed").Value) : false;
                int[] batches;
                if (useBatchSet)
                    batches = _loadSet.Root.Element("Batches").Descendants("Batch").Select(b => Convert.ToInt32(b.Value)).ToArray();
                else
                    batches = t.Descendants("Batch").Select(b => Convert.ToInt32(b.Value)).ToArray();

                var requests = new List<HttpRequest>();
                t.Descendants("Request").Where(r => Convert.ToBoolean(r.Attribute("IsEnabled").Value)).ForEach(r =>
                    {
                        var request = t.Element("Request");
                        var requestUrl = request.Attribute("Url").Value;
                        var requestParameters = request.Descendants("Parameter").Select(p => new KeyValuePair<string, string>(p.Attribute("Key").Value, p.Value)).ToList();
                        var requestContentEncoding = request.Attribute("ContentEncoding").Value;
                        var requestContentType = request.Attribute("ContentType").Value.Parse<ContentTypes>();
                        var requestAcceptType = request.Attribute("AcceptType").Value.Parse<ContentTypes>();
                        var requestIsPost = Convert.ToBoolean(request.Attribute("IsPost").Value);
                        var requestIsBodyTransport = Convert.ToBoolean(request.Attribute("IsBodyTransport").Value);
                        requests.Add(new HttpRequest { Url = requestUrl, Parameters = requestParameters, ContentEncoding = requestContentEncoding, ContentType = requestContentType, AcceptType = requestAcceptType, IsPost = requestIsPost, IsBodyTransport = requestIsBodyTransport });
                    });

                var auth = t.Element("Auth");
                var authRequest = new HttpRequest
                {
                    Url = string.Empty,
                    Parameters = new List<KeyValuePair<string, string>>(),
                    ContentEncoding = Encoding.Default.BodyName,
                    ContentType = ContentTypes.None,
                    AcceptType = ContentTypes.None,
                    IsPost = false,
                    IsBodyTransport = false
                };
                if (auth != null)
                {
                    authRequest.Url = auth.Attributes().Any(a => a.Name == "Url") ? auth.Attribute("Url").Value : string.Empty;
                    if (!string.IsNullOrEmpty(authRequest.Url))
                    {
                        authRequest.Parameters = auth.Descendants("Parameter").Select(p => new KeyValuePair<string, string>(p.Attribute("Key").Value, p.Value)).ToList();
                        authRequest.ContentEncoding = auth.Attribute("ContentEncoding").Value;
                        authRequest.ContentType = auth.Attribute("ContentType").Value.Parse<ContentTypes>();
                        authRequest.AcceptType = auth.Attribute("AcceptType").Value.Parse<ContentTypes>();
                        authRequest.IsPost = Convert.ToBoolean(auth.Attribute("IsPost").Value);
                        authRequest.IsBodyTransport = Convert.ToBoolean(auth.Attribute("IsBodyTransport").Value);
                    }
                }

                var name = t.Attribute("Name").Value;
                var batchPayload = new BatchPayload { Name = name, LoadSize = loadSize, Requests = requests, Auth = authRequest, ThroughputSize = throughputSize, BatchSizes = batches };

                if (isDistributed)
                    AddDistributedTask(batchPayload);
                TestLoad(batchPayload);
            });
        }

        private void AddLog(string content)
        {
            _logs.ForEach(l =>
                {
                    l.Add(content);
                });
        }

        internal void TestLoad(BatchPayload batchPayload)
        {
            if (batchPayload.LoadSize <= 0)
            {
                AddLog("no load specified");
                return;
            }
            if (batchPayload.Requests.Count == 0)
            {
                AddLog("no request specified");
                return;
            }
            if (batchPayload.ThroughputSize <= 0)
                batchPayload.ThroughputSize = 1;

            var grandResults = new List<KeyValuePair<Payload, LoadResult>>();

            var grandReport = new StringBuilder();
            grandReport.AppendLine(new string('*', 80));
            grandReport.AppendLine(string.Format("task: {0}", batchPayload.Name));
            grandReport.AppendLine(string.Format("load: {0}", batchPayload.LoadSize));
            grandReport.AppendLine(string.Format("grand started: {0}", DateTime.Now));
            grandReport.AppendLine(new string('-', 50));
            AddLog(grandReport.ToString());

            batchPayload.BatchSizes.ForEach(batchSize =>
                {
                    var load = new Payload
                    {
                        LoadSize = batchPayload.LoadSize,
                        Requests = batchPayload.Requests,
                        Auth = batchPayload.Auth,
                        ConcurrentSize = batchSize,
                        ThroughputSize = batchPayload.ThroughputSize
                    };

                    AddLog("concurrent: " + load.ConcurrentSize);
                    AddLog("started: " + DateTime.Now.ToString());

                    AddLog("requests: ");
                    load.Requests.ForEach(r =>
                        {
                            AddLog(string.Format("\t{0}", r.Url));
                        });

                    var engine = new LoadEngine(load);
                    engine.Progress += (s, e) =>
                        {
                            if (e.Completed % e.Load.ThroughputSize == 0)
                                AddLog(string.Format("current: {0} / {1}", e.Completed, load.LoadSize));
                        };
                    engine.Throughput += (s, e) =>
                        {
                            AddLog("throughput:");
                            AddLog(string.Format("\tavg: {0} ms/hit, {1} hits/s", e.AvgTime, Math.Round(e.HitsPerSecond, 3)));
                            AddLog(string.Format("\ttotal: {0} KB, {1} KB/hit", e.TotalBytes.ToKB(), e.BytesPerHit.ToKB()));
                        };

                    var result = engine.Run();
                    grandResults.Add(new KeyValuePair<Payload, LoadResult>(load, result));

                    var report = new StringBuilder();
                    report.AppendLine(string.Format("concurrent: {0}", load.ConcurrentSize));
                    report.AppendLine(string.Format("started: {0}", result.StartedTime.ToString()));
                    report.AppendLine(string.Format("finished: {0}", result.FinishedTime.ToString()));
                    report.AppendLine(string.Format("elapsed: {0}", TimeSpan.FromMilliseconds(result.TotalTime)));
                    report.AppendLine(string.Format("completed: {0}", result.Completed));
                    report.AppendLine(string.Format("successful: {0}", result.Successful));
                    report.AppendLine(string.Format("failed: {0}", result.Failed));
                    report.AppendLine("hits:");
                    report.AppendLine(string.Format("\tavg: {0} ms/hit, {1} hits/s", result.AvgTime, Math.Round(result.HitsPerSecond, 3)));
                    report.AppendLine(string.Format("\tmin: {0} ms/hit, {1} hits/s", result.MinAvgTime, Math.Round(result.MinHitsPerSecond, 3)));
                    report.AppendLine(string.Format("\tmax: {0} ms/hit, {1} hits/s", result.MaxAvgTime, Math.Round(result.MaxHitsPerSecond, 3)));
                    report.AppendLine(string.Format("\ttotal: {0} KB, {1} KB/hit", result.TotalBytes.ToKB(), result.BytesPerHit.ToKB()));
                    report.AppendLine("requests: ");
                    var successfulRequests = result.Items.Sum(i => i.Requests.Count(r => r.IsSuccessful));
                    var failedRequests = result.Requests - successfulRequests;
                    report.AppendLine(string.Format("\tcompleted: {0}, successful: {1}, failed: {2}", result.Requests, successfulRequests, failedRequests));
                    report.AppendLine(string.Format("\tavg: {0} ms/request, {1} requests/s, {2} KB/request", result.AvgRequestTime, Math.Round(result.RequestPerSecond, 3), result.BytesPerRequest.ToKB()));
                    report.AppendLine(new string('-', 50));

                    AddLog(report.ToString());
                });

            grandReport = new StringBuilder();
            grandReport.AppendLine(string.Format("grand finished: {0}", DateTime.Now));
            var totalTime = grandResults.Sum(r => r.Value.TotalTime);
            grandReport.AppendLine(string.Format("grand elapsed: {0}", TimeSpan.FromMilliseconds(totalTime)));
            var grandCompleted = grandResults.Sum(r => r.Value.Completed);
            grandReport.AppendLine(string.Format("grand completed: {0}", grandCompleted));
            grandReport.AppendLine(string.Format("grand successful: {0}", grandResults.Sum(r => r.Value.Successful)));
            grandReport.AppendLine(string.Format("grand failed: {0}", grandResults.Sum(r => r.Value.Failed)));
            grandReport.AppendLine("grand hits:");
            var avgTime = grandResults.Average(r => r.Value.FinishedTime.Subtract(r.Value.StartedTime).TotalMilliseconds / r.Value.Completed);
            grandReport.AppendLine(string.Format("\tavg: {0} ms/hit, {1} hits/s", Math.Round(avgTime, 1), Math.Round(1000.0 / avgTime, 3)));

            var minAvgTime = grandResults.Min(r => r.Value.MinAvgTime);
            var minHitsPerSecond = grandResults.Min(r => r.Value.MinHitsPerSecond);
            grandReport.AppendLine(string.Format("\tmin: {0} ms/hit, {1} hits/s", minAvgTime, Math.Round(minHitsPerSecond, 3)));

            var maxAvgTime = grandResults.Max(r => r.Value.MaxAvgTime);
            var maxHitsPerSecond = grandResults.Max(r => r.Value.MaxHitsPerSecond);
            grandReport.AppendLine(string.Format("\tmax: {0} ms/hit, {1} hits/s", maxAvgTime, Math.Round(maxHitsPerSecond, 3)));

            var totalKB = grandResults.Sum(r => r.Value.TotalBytes / 1024);
            grandReport.AppendLine(string.Format("\ttotal: {0} KB, {1} KB/hit", totalKB, totalKB / grandCompleted));

            grandReport.AppendLine("grand requests:");
            var grandRequests = grandResults.Sum(r => r.Value.Requests);
            var grandSuccessfulRequests = grandResults.Sum(r => r.Value.Items.Sum(i => i.Requests.Count(e => e.IsSuccessful)));
            var grandFailedRequests = grandRequests - grandSuccessfulRequests;
            grandReport.AppendLine(string.Format("\tcompleted: {0}, successful: {1}, failed: {2}", grandRequests, grandSuccessfulRequests, grandFailedRequests));
            var grandAvgRequestTime = totalTime / grandRequests;
            grandReport.AppendLine(string.Format("\tavg: {0} ms/request, {1} requests/s, {2} KB/request", Math.Round(grandAvgRequestTime, 1), Math.Round(1000.0 / grandAvgRequestTime, 3), totalKB / grandRequests));

            grandReport.AppendLine(new string('-', 50));
            grandReport.AppendLine("Concurrent Users\tAvg Time(ms)/Hit\tHits/s\tTotal Througput(KB)\tAvg Throughput(KB)");
            grandResults.ForEach(r =>
                {
                    grandReport.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}KB", r.Key.ConcurrentSize, r.Value.AvgTime, Math.Round(r.Value.HitsPerSecond, 3), r.Value.TotalBytes.ToKB(), r.Value.BytesPerHit.ToKB()));
                });

            AddLog(grandReport.ToString());
        }
    }
}
