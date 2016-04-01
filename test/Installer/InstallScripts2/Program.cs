// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.Tasks;
using System;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Test
{
    public class Counter<T>
    {
        public Dictionary<T, int> Counts { get; private set; }

        public Counter()
        {
            Counts = new Dictionary<T, int>();
        }

        public int this[T key]
        {
            get
            {
                int ret;
                if (Counts.TryGetValue(key, out ret))
                {
                    return ret;
                }
                else
                {
                    return 0;
                }
            }
        }

        public void Increment(T key)
        {
            Counts[key] = this[key] + 1;
        }
    }

    public class TestServer : IDisposable
    {
        private Dictionary<string, RequestDelegate> _pathsMappings;
        public Counter<string> RequestCounts { get; private set; }
        public int PageNotFoundHits { get; private set; }
        private IWebHost _host;

        public RequestDelegate PathNotFoundHandler { get; set; }
        public string Url { get; private set; }

        public TestServer()
        {
            _pathsMappings = new Dictionary<string, RequestDelegate>();
            RequestCounts = new Counter<string>();
            PathNotFoundHandler = DefaultPathNotFoundHandler;
        }

        public RequestDelegate this[string path]
        {
            get
            {
                RequestDelegate requestHandler;
                _pathsMappings.TryGetValue(NormalizeServerPath(path), out requestHandler);
                return requestHandler;
            }
            set
            {
                _pathsMappings[NormalizeServerPath(path)] = value;
            }
        }

        public Task DefaultPathNotFoundHandler(HttpContext context)
        {
            context.Response.StatusCode = 404;
            return context.Response.WriteAsync($"404 Path {context.Request.Path} not found!");
        }

        private Task PathHandlerOrDefault(HttpContext context)
        {
            if (_pathsMappings == null)
            {
                return MappingsNotSetHandler(context);
            }

            string path = NormalizeServerPath(context.Request.Path);
            RequestCounts.Increment(path);

            RequestDelegate handler;
            if (_pathsMappings.TryGetValue(path, out handler))
            {
                return handler(context);
            }
            else
            {
                PageNotFoundHits++;
                return PathNotFoundHandler(context);
            }
        }

        public static RequestDelegate SendFile(string filePath)
        {
            return (context) =>
            {
                context.Response.ContentType = "application/octet-stream";
                return context.Response.SendFileAsync(filePath);
            };
        }

        public static RequestDelegate SendText(string text)
        {
            return context => context.Response.WriteAsync(text);
        }

        private Task MappingsNotSetHandler(HttpContext context)
        {
            context.Response.StatusCode = 500;
            return context.Response.WriteAsync($"500 Server path mappings are set to null!");
        }

        public void Configure(IApplicationBuilder app)
        {
            app.ServerFeatures.Set<TestServer>(this);
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All
            });

            app.Run(PathHandlerOrDefault);
        }

        private static string NormalizeServerPath(string path)
        {
            path = path.ToLower();
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            return path;
        }

        public static TestServer Create()
        {
            Exception lastException = null;
            // Try few times just in case different process takes it in between the call
            for (int creationAttempt = 0; creationAttempt < 10; creationAttempt++)
            {
                IWebHost host = null;
                try
                {
                    TestServer ret = null;

                    var hostBuilder = new WebHostBuilder();
                    hostBuilder.UseServer("Microsoft.AspNetCore.Server.Kestrel");
                    hostBuilder.UseContentRoot(Directory.GetCurrentDirectory());
                    hostBuilder.UseDefaultConfiguration();
                    var cb = new ConfigurationBuilder();
                    hostBuilder.UseConfiguration(cb.Build());
                    hostBuilder.UseStartup<TestServer>();
                    string url;
                    lock (_freePortLock)
                    {
                        url = $"http://localhost:{GetFreePort()}";
                        hostBuilder.UseUrls(url);

                        host = hostBuilder.Build();
                        host.Start();
                    }

                    ret = host.ServerFeatures.Get<TestServer>();
                    ret._host = host;
                    ret.Url = url;

                    return ret;
                }
                catch (Exception e)
                {
                    lastException = e;
                    host?.Dispose();
                }
            }

            throw lastException;
        }

        public static TestServer Create(Dictionary<string, RequestDelegate> mappings)
        {
            TestServer ret = Create();
            foreach (var mapping in mappings)
            {
                ret[mapping.Key] = mapping.Value;
            }

            return ret;
        }

        public void Dispose()
        {
            _host?.Dispose();
            _host = null;
        }

        private static object _freePortLock = new object();
        private static int GetFreePort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }

    public class InstallScriptsTests : TestBase
    {
        [Fact]
        public void Test()
        {
            using (TestServer s = TestServer.Create())
            {
                s[""] = TestServer.SendText("<html><head/><body>Welcome on my server</body></html>");
                s["/dupa"] = TestServer.SendText("hello Seattle");
                s["/nuget"] = TestServer.SendFile("NuGet.Config");

                Console.WriteLine($"Created server {s.Url}");
                Console.WriteLine("press ENTER to stop the server");
                Console.ReadLine();
                foreach (var reqCount in s.RequestCounts.Counts)
                {
                    Console.WriteLine($"hit {reqCount.Key} {reqCount.Value} time(s)");
                }
                Console.WriteLine($"Number of 404: {s.PageNotFoundHits}");
            }
        }
    }
}