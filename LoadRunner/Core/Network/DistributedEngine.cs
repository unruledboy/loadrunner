using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Org.LoadRunner.Core.Infrastructure;
using Org.LoadRunner.Core.Models;

namespace Org.LoadRunner.Core.Network
{
    internal class DistributedEngine
    {
        public event EventHandler<BatchLoadEventArgs> BatchLoad;
        public event EventHandler<DistributedLoadEventArgs> DistributedProgress;

        private int _requestCounter;
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private bool _isStarted;

        public bool Start(string[] prefixes)
        {
            if (!HttpListener.IsSupported)
                return false;
            if (_isStarted)
                return true;

            var listener = new HttpListener();

            prefixes.ForEach(p =>
                {
                    listener.Prefixes.Add(p);
                });

            listener.Start();
            var state = new HttpListenerCallbackState(listener);
            ThreadPool.QueueUserWorkItem(s =>
            {
                var callbackState = state;

                while (callbackState.Listener.IsListening)
                {
                    callbackState.Listener.BeginGetContext(e =>
                    {
                        HttpListenerContext context = null;

                        var requestNumber = Interlocked.Increment(ref _requestCounter);

                        try
                        {
                            context = callbackState.Listener.EndGetContext(e);
                        }
                        catch (Exception)
                        {
                            return;
                        }
                        finally
                        {
                            callbackState.ListenForNextRequest.Set();
                        }

                        if (context == null) return;

                        var request = context.Request;
                        var responseContent = "unruledboy rocks ;-)";

                        if (request.HasEntityBody)
                        {
                            using (var sr = new StreamReader(request.InputStream, request.ContentEncoding))
                            {
                                string requestData = sr.ReadToEnd();

                                if (BatchLoad != null)
                                {
                                    var batchPayload = requestData.Deserialize<BatchPayload>();
                                    BatchLoad(this, new BatchLoadEventArgs { Load = batchPayload, RequestorAddress = request.UserHostAddress });
                                }
                            }
                        }

                        try
                        {
                            using (var response = context.Response)
                            {
                                var buffer = Encoding.UTF8.GetBytes(responseContent);
                                response.ContentLength64 = buffer.LongLength;
                                response.OutputStream.Write(buffer, 0, buffer.Length);
                                response.Close();
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }, callbackState);
                    var result = WaitHandle.WaitAny(new WaitHandle[] { callbackState.ListenForNextRequest, _stopEvent });

                    if (result == 1)
                    {
                        callbackState.Listener.Stop();
                        break;
                    }
                }
            }, state);

            _isStarted = true;

            return true;
        }

        public void StopListening()
        {
            _stopEvent.Set();
            _isStarted = false;
        }

        public bool IsStarted
        {
            get { return _isStarted; }
        }

        public void Distribute(BatchPayload batchPayload, string[] endpoints)
        {
            var result = batchPayload.Serialize();
            var content = Encoding.UTF8.GetBytes(result);
            endpoints.ForEach(e =>
                {
                    try
                    {
                        new WebClient().UploadData(e, content);
                    }
                    catch (Exception ex)
                    {
                        if (DistributedProgress != null)
                            DistributedProgress(this, new DistributedLoadEventArgs { Load = batchPayload, Url = e, Message = ex.Message });
                    }
                });
        }
    }
}
