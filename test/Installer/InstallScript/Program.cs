// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace Microsoft.DotNet.InstallScripts.Tests
{
    public class PackTests
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
