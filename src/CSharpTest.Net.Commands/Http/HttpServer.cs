#region Copyright 2011-2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Threading;

namespace CSharpTest.Net.Http
{
    /// <summary>
    /// Hosts an HttpListener on a dedicated set of worker threads, providing a clean shutdown
    /// on dispose.
    /// </summary>
    public class HttpServer : IDisposable
    {
        [ThreadStatic]
        private static HttpListenerContext _threadContext;

        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;
        private readonly Thread[] _workers;
        private readonly ManualResetEvent _stop, _ready;
        private Queue<HttpListenerContext> _queue;

        /// <summary>
        /// Constructs the HttpServer with a fixed thread-pool size.
        /// </summary>
        public HttpServer(int maxThreads)
        {
            _workers = new Thread[maxThreads];
            _queue = new Queue<HttpListenerContext>();
            _stop = new ManualResetEvent(false);
            _ready = new ManualResetEvent(false);
            _listener = new HttpListener();
            _listenerThread = new Thread(HandleRequests);
        }

        /// <summary>
        /// Returns a thread-static HttpListenerContext of the current http request, or null if there is none.
        /// </summary>
        public static HttpListenerContext Context {
            get { return _threadContext; }
        }

        /// <summary>
        /// Exposes a WaitHandle that can be used to signal other threads that the HTTP server is shutting down.
        /// </summary>
        public WaitHandle ShutdownEvent {
            get { return _stop; }
        }

        /// <summary>
        /// Returns the path being used to host this server instance, for example return "/root/" when prefixes = "http://*:8080/root"
        /// </summary>
        public string ApplicationPath { get; private set; }

        /// <summary>
        /// Raised when an unhandled exception occurs, you can use HttpServer.Context to respond to the http client if needed.
        /// </summary>
        public event EventHandler<ErrorEventArgs> OnError;

        /// <summary>
        /// Performs the processing of the request on one of the worker threads
        /// </summary>
        public event EventHandler<HttpContextEventArgs> ProcessRequest;

        /// <summary>
        /// Starts the HttpServer listening on the prefixes supplied.  use "http://+:80/" to match any host identifier not already mapped, 
        /// or "http://*:80/" to match all host identifiers.
        /// </summary>
        /// <param name="prefixes">see http://msdn.microsoft.com/en-us/library/system.net.httplistenerprefixcollection.add.aspx</param>
        public void Start(string[] prefixes)
        {
            string baseUri = null;
            foreach (var prefix in prefixes)
            {
                _listener.Prefixes.Add(prefix);

                string tmp = prefix.Replace("://*", "://hostname").Replace("://+", "://hostname");
                if (baseUri == null)
                    baseUri = new Uri(tmp).AbsolutePath;
                else if (baseUri != new Uri(tmp).AbsolutePath)
                    throw new ArgumentException("Must use the same path for all prefixes.", "prefixes");
            }
            ApplicationPath = baseUri;
            try
            {
                _listener.Start();
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode == 5)
                {
                    StringWriter swMsg = new StringWriter();
                    swMsg.WriteLine(ex.Message);

                    WindowsIdentity user = WindowsIdentity.GetCurrent();
                    swMsg.WriteLine("Use the following command(s) to grant access:");
                    foreach (var prefix in prefixes)
                    {
                        swMsg.WriteLine("  netsh http add urlacl url={0} \"user={1}\" listen=yes",
                                        prefix, user == null ? "NT AUTHORITY\\Everyone" : user.Name);
                    }
                    throw new UnauthorizedAccessException(swMsg.ToString(), ex);
                }
                throw;
            }

            _listenerThread.Start();

            for (int i = 0; i < _workers.Length; i++)
            {
                _workers[i] = new Thread(Worker);
                _workers[i].Start();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        { Stop(); }

        /// <summary>
        /// Stops the HTTP server and all worker threads.
        /// </summary>
        public void Stop()
        {
            _stop.Set();
            try { _listenerThread.Join(); }
            catch { }
            foreach (Thread worker in _workers)
            {
                try { worker.Join(); }
                catch { }
            }
            try { _listener.Stop(); }
            catch { }
        }

        private void HandleRequests()
        {
            try
            {
                while (_listener.IsListening)
                {
                    var context = _listener.BeginGetContext(ContextReady, null);

                    if (0 == WaitHandle.WaitAny(new[] {_stop, context.AsyncWaitHandle}))
                        return;
                }
            }
            catch { _stop.Set(); }
        }

        private void ContextReady(IAsyncResult ar)
        {
            try
            {
                lock (_queue)
                {
                    _queue.Enqueue(_listener.EndGetContext(ar));
                    _ready.Set();
                }
            }
            catch { return; }
        }

        private void Worker()
        {
            WaitHandle[] wait = new[] { _ready, _stop };
            while (0 == WaitHandle.WaitAny(wait))
            {
                HttpListenerContext context;
                lock (_queue)
                {
                    if (_queue.Count > 0)
                        context = _queue.Dequeue();
                    else
                    {
                        _ready.Reset();
                        continue;
                    }
                }

                try
                {
                    _threadContext = context;
                    ProcessRequest(this, new HttpContextEventArgs(this, context));
                }
                catch (Exception ex)
                {
                    EventHandler<ErrorEventArgs> e = OnError;
                    if (e != null)
                        try { e(context, new ErrorEventArgs(ex)); } catch { }
                }
                finally
                {
                    _threadContext = null;
                }
            }
        }
    }

    /// <summary>
    /// The event type used for the processing event to access the HttpListenerContext and the
    /// HttpServer instance.
    /// </summary>
    public sealed class HttpContextEventArgs : EventArgs
    {
        internal HttpContextEventArgs(HttpServer host, HttpListenerContext context)
        {
            Host = host;
            Context = context;
        }

        /// <summary> The HttpServer recieving the request </summary>
        public HttpServer Host { get; private set; }
        /// <summary> The HttpListenerContext for this request </summary>
        public HttpListenerContext Context { get; private set; }
    }
}
