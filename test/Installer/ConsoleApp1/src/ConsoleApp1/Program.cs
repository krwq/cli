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

namespace Test
{
    public class Startup
    {
        public string Path;
        public void Configure(IApplicationBuilder app)
        {
            //app.Properties.
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All
            });

            app.Run(context =>
            {
                Path = context.Request.Path;
                //context.Response.ContentType = "application/octet-stream";
                //context.Response.SendFileAsync()
                return context.Response.SendFileAsync("NuGet.config");
                /*StringBuilder sb = new StringBuilder();
                foreach (var file in Directory.EnumerateFiles("."))
                {
                    sb.AppendLine(file);
                }
                return context.Response.WriteAsync($"Hello World! {context.Request.Path}\r\n{sb.ToString()}");*/
            });
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
            hostBuilder.UseStartup<Startup>();
            hostBuilder.UseUrls("https://localhost:9999");

            using (IWebHost host = hostBuilder.Build())
            {
                host.Start();
                //var test = (Startup)host.Services.GetService(typeof(Startup));
                Console.WriteLine("server started. press ENTER to stop");
                Console.ReadLine();
                //Console.WriteLine($"Last visited path is {test.Path}");
            }
        }
    }
}