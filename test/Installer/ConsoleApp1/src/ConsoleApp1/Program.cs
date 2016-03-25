// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.Tasks;
using System;

namespace Test
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All
            });

            app.Run(context =>
            {
                return context.Response.WriteAsync("Hello World!");
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
            hostBuilder.UseStartup<Startup>();

            using (IWebHost host = hostBuilder.Build())
            {
                host.Start();
                Console.WriteLine("server started. press a key to stop");
                Console.ReadLine();
            }

            Console.WriteLine("server stopped. press a key to close app");
            Console.ReadLine();
        }
    }
}