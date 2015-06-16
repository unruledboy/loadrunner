using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Org.LoadRunner.Core.Infrastructure;
using Org.LoadRunner.Core.Models;

namespace Org.LoadRunner.Core.Engine
{
    internal class LoadEngine
    {
        public event EventHandler<ProgressEventArgs> Progress;
        public event EventHandler<ThroughputEventArgs> Throughput;

        private Payload load;

        public LoadEngine(Payload load)
        {
            this.load = load;
        }

        public LoadResult Run()
        {
            var startedTime = DateTime.Now;
            var watch = new Stopwatch();
            var result = new LoadResult { Items = new ConcurrentList<ItemResult>(), MinAvgTime = double.MaxValue, MinHitsPerSecond = double.MaxValue, MaxAvgTime = 0, MaxHitsPerSecond = 0 };
            if (load.LoadSize <= 0)
                return result;
            if (load.Requests.Count == 0)
                return result;

            watch.Start();

            try
            {
                ServicePointManager.DefaultConnectionLimit = load.ConcurrentSize;
                var cts = new CancellationTokenSource();
                ParallelOptions parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = load.ConcurrentSize,
                    CancellationToken = cts.Token
                };

                var http = new HttpClient();
                //auth
                string sessionKey;
                var authResult = new Dictionary<string, string>();
                if (load.Auth != null && !string.IsNullOrEmpty(load.Auth.Url))
                {
                    sessionKey = Guid.NewGuid().ToString();
                    var authContent = http.Send(Encoding.GetEncoding(load.Auth.ContentEncoding), load.Auth.Url, load.Auth.Parameters.ToDictionary(p => p.Key, p => p.Value), load.Auth.IsPost, load.Auth.ContentType, load.Auth.AcceptType, sessionKey, null, load.Auth.IsBodyTransport);
                    //todo: transform authContent to authResult
                }
                else
                    sessionKey = string.Empty;

                Parallel.For(0, load.LoadSize, parallelOptions, j =>
                {
                    var itemWatch = Stopwatch.StartNew();
                    var item = new ItemResult { StartedTime = DateTime.Now, Requests = new List<RequestResult>() };

                    for (int i = 0; i < load.Requests.Count; i++)
                    {
                        var requestStartedTime = DateTime.Now;
                        var requestResult = new RequestResult { Id = i, StartedTime = requestStartedTime };
                        try
                        {
                            /*using (WebClient client = new WebClient { Proxy = new WebProxy() })
                            {
                                var content = client.DownloadData(load.Url);

                                item.Bytes = content.Length;
                            }*/
                            var request = load.Requests[i];
                            var content = http.Send(Encoding.GetEncoding(request.ContentEncoding), request.Url, request.Parameters.ToDictionary(p => p.Key, p => p.Value), request.IsPost, request.ContentType, request.AcceptType, sessionKey, authResult, request.IsBodyTransport);
                            requestResult.Bytes = content.Length;
                            requestResult.IsSuccessful = true;
                        }
                        catch (Exception ex)
                        {
                            requestResult.Message = ex.Message;
                        }
                        requestResult.FinishedTime = DateTime.Now;
                        item.Requests.Add(requestResult);
                    }
                    itemWatch.Stop();
                    item.Bytes = item.Requests.Sum(r => r.Bytes);
                    item.FinishedTime = DateTime.Now;
                    item.CompletedTime = itemWatch.ElapsedMilliseconds;
                    if (item.Requests.Any(r => !r.IsSuccessful))
                        result.Failed++;
                    else
                        result.Successful++;
                    result.Items.Add(item);
                    result.Completed++;

                    if (Progress != null)
                    {
                        var progressArgs = new ProgressEventArgs { Load = load, Result = result, Completed = result.Completed };
                        Progress(this, progressArgs);
                        if (progressArgs.Cancelled)
                            cts.Cancel();
                    }

                    if (Throughput != null && load.ThroughputSize > 0 && result.Completed % load.ThroughputSize == 0)
                    {
                        var throughputItems = result.Items.Skip(result.Items.Count > load.ThroughputSize ? result.Items.Count - load.ThroughputSize : 0).Take(load.ThroughputSize);

                        var throughputTotalTime = watch.ElapsedMilliseconds;
                        var throughputAvgTime = throughputTotalTime / result.Completed;

                        var throughputHitsPerSecond = 1000.0 / throughputAvgTime;
                        var throughputTotalBytes = throughputItems.Sum(i => i.Bytes);
                        var throughputBytesPerHit = throughputTotalBytes / load.ThroughputSize;

                        if (throughputAvgTime < result.MinAvgTime)
                            result.MinAvgTime = throughputAvgTime;

                        if (throughputHitsPerSecond < result.MinHitsPerSecond)
                            result.MinHitsPerSecond = throughputHitsPerSecond;

                        if (throughputAvgTime > result.MaxAvgTime)
                            result.MaxAvgTime = throughputAvgTime;

                        if (throughputHitsPerSecond > result.MaxHitsPerSecond)
                            result.MaxHitsPerSecond = throughputHitsPerSecond;

                        Throughput(this, new ThroughputEventArgs { Items = throughputItems, TotalTime = throughputTotalTime, AvgTime = throughputAvgTime, HitsPerSecond = throughputHitsPerSecond, TotalBytes = throughputTotalBytes, BytesPerHit = throughputBytesPerHit });
                    }
                });
            }
            catch (Exception ex)
            {
                result.Message = ex.ToString();
            }

            watch.Stop();
            result.StartedTime = startedTime;
            result.FinishedTime = DateTime.Now;
            result.TotalTime = watch.ElapsedMilliseconds;
            result.AvgTime = watch.ElapsedMilliseconds / load.LoadSize;
            result.HitsPerSecond = 1000.0 / result.AvgTime;
            result.TotalBytes = result.Items.Sum(i => i.Bytes);
            result.BytesPerHit = result.TotalBytes / result.Completed;

            result.Requests = result.Items.Sum(i => i.Requests.Count);
            result.BytesPerRequest = result.TotalBytes / result.Requests;
            result.AvgRequestTime = watch.ElapsedMilliseconds / result.Requests;
            result.RequestPerSecond = 1000.0 / result.AvgRequestTime;

            return result;
        }
    }
}
