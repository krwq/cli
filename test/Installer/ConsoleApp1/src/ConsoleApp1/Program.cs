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

namespace Test
{
    public class TestServer
    {
        private Dictionary<string, RequestDelegate> _pathsMappings;

        public RequestDelegate PathNotFoundHandler { get; set; }

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
            //context.Response.ContentType = "application/octet-stream";
            //context.Response.SendFileAsync()
            //return context.Response.SendFileAsync("NuGet.config");
            /*StringBuilder sb = new StringBuilder();
            foreach (var file in Directory.EnumerateFiles("."))
            {
                sb.AppendLine(file);
            }
            return context.Response.WriteAsync($"Hello World! {context.Request.Path}\r\n{sb.ToString()}");*/
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

        public void Configure(IApplicationBuilder app)
        {
            app.ServerFeatures.Set<TestServer>(this);
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All
            });
            app.Run(PathHandlerOrDefault);
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var hostBuilder = new WebHostBuilder();
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
            }
        }
    }
}