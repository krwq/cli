// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Xunit.Abstractions;
using Xunit;

namespace Microsoft.DotNet.InstallScripts.Tests
{
    public class InstallScriptsTests
    {
        private static readonly string _shell;
        private static readonly string _installScriptPath;
        private static readonly string _repoRoot;
        
        private readonly ITestOutputHelper output;
        
        public InstallScriptsTests(ITestOutputHelper output)
        {
            this.output = output;
        }
        
        static string FindRepoRoot()
        {
            string directory = AppContext.BaseDirectory;
            while (!Directory.Exists(Path.Combine(directory, ".git")) && directory != null)
            {
                directory = Directory.GetParent(directory).FullName;
            }

            if (directory == null)
            {
                throw new Exception("Cannot find the git repository root");
            }
            
            return directory;
        }
        
        static InstallScriptsTests()
        {
            _repoRoot = FindRepoRoot();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _shell = "powershell";
                _installScriptPath = Path.Combine(_repoRoot, "scripts", "obtain", "install.ps1");
            }
            else
            {
                _shell = "bash";
                _installScriptPath = Path.Combine(_repoRoot, "scripts", "obtain", "install.sh");
            }
        }
        
        private Process InstallEx(string additionalArguments)
        {
            var arguments = $"-File \"{_installScriptPath}\" -Verbose {additionalArguments}";
            Process ret = new Process();
            ret.StartInfo.FileName = _shell;
            ret.StartInfo.Arguments = arguments;
            ret.StartInfo.UseShellExecute = false;
            
            return ret;;
        }
        
        private void Install(string additionalArguments)
        {
            var process = InstallEx(additionalArguments);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            output.WriteLine($"Calling {_shell} {process.StartInfo.Arguments}");
            process.Start();
            
            bool finishedGracefully = process.WaitForExit(10 * 1000);
            
            output.WriteLine("Process stdout:");
            output.WriteLine(process.StandardOutput.ReadToEnd());
            output.WriteLine("Process stderr:");
            output.WriteLine(process.StandardError.ReadToEnd());
            
            Assert.True(finishedGracefully, "Failed to wait for the installation operation to complete.");
            Assert.Equal(0, process.ExitCode);
        }
        
        [Fact]
        public void InstallScriptExists()
        {
            Assert.True(File.Exists(_installScriptPath));
        }
        
        [Fact]
        public void Test()
        {
            using (TestServer s = TestServer.Create())
            {
                string versionFile = "beta/dnvm/latest.win.x64.version";
                s[versionFile] = TestServer.SendText("123\r\n1.2.3");
                Console.WriteLine($"Server: {s.Url}. press enter");
                Console.ReadLine();
                //TestServer.SendFile("NuGet.Config");
                Install($"-DryRun -AzureFeed {s.Url}");
                Assert.Equal(1, s.RequestCounts[versionFile]);
                Assert.Equal(0, s.PageNotFoundHits);
                Assert.True(false);
            }
        }
    }
}
