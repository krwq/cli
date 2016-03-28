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
    public class TestServer : IDisposable
    {
        private Dictionary<string, RequestDelegate> _pathsMappings;
        private IWebHost _host;

        public RequestDelegate PathNotFoundHandler { get; set; }
        public string Url { get; private set; }

        public TestServer()
        {
            _pathsMappings = new Dictionary<string, RequestDelegate>();
            PathNotFoundHandler = DefaultPathNotFoundHandler;
        }

        public RequestDelegate this[string path]
        {
            get
            {
                path = path.ToLower();
                RequestDelegate requestHandler;
                _pathsMappings.TryGetValue(path, out requestHandler);
                return requestHandler;
            }
            set
            {
                path = path.ToLower();
                _pathsMappings[path] = value;
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

            return (this[context.Request.Path] ?? PathNotFoundHandler)(context);
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

        public static TestServer Create()
        {
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
                    string url = $"https://localhost:{GetFreePort()}";
                    hostBuilder.UseUrls(url);

                    host = hostBuilder.Build();
                    host.Start();
                    ret = host.ServerFeatures.Get<TestServer>();
                    ret._host = host;
                    ret.Url = url;

                    return ret;
                }
                catch { host?.Dispose(); /* choke all exceptions and retry */ }
            }

            throw new Exception("Could not create a server.");
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

        private static int GetFreePort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            using (TestServer s = TestServer.Create())
            {
                s["/dupa"] = TestServer.SendText("hello Seattle");
                s["/nuget"] = TestServer.SendFile("NuGet.Config");
                Console.WriteLine($"Created server {s.Url}");
                Console.WriteLine("press ENTER to stop the server");
                Console.ReadLine();
            }
            /*var hostBuilder = new WebHostBuilder();
            hostBuilder.UseServer("Microsoft.AspNetCore.Server.Kestrel");
            hostBuilder.UseContentRoot(Directory.GetCurrentDirectory());
            hostBuilder.UseDefaultConfiguration(args);
            var cb = new ConfigurationBuilder();
            hostBuilder.UseConfiguration(cb.Build());
            hostBuilder.UseStartup<TestServer>();
            hostBuilder.UseUrls("https://localhost:9999");

            using (IWebHost host = hostBuilder.Build())
            {
                host.Start();

                TestServer server = host.ServerFeatures.Get<TestServer>();
                server["/dupa"] = TestServer.SendText("hello Seattle");
                server["/nuget"] = TestServer.SendFile("NuGet.Config");
                Console.WriteLine("server changed. press ENTER to stop");
                Console.ReadLine();
                //Console.WriteLine($"Last visited path is {test.Path}");
            }*/
        }
    }
}