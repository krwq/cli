// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;
using System.Diagnostics;

namespace Microsoft.DotNet.Tests.InstallScripts
{
    public class InstallScriptsTests : TestBase
    {
        private static string _shell;
        private static string _installScriptPath;
        
        static InstallScriptsTests()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _shell = "powershell";
                _installScriptPath = Path.Combine(RepoRoot, "scripts", "obtain", "install.ps1");
            }
            else
            {
                _shell = "bash";
                _installScriptPath = Path.Combine(RepoRoot, "scripts", "obtain", "install.sh");
            }
        }
        
        [Fact]
        public void TestDotnetBuild()
        {
            Console.WriteLine($"Default shell: {_shell}");
            Console.WriteLine($"Script to run: {_installScriptPath}");
        }
        
        private static void Install(string additionalArguments)
        {
            var arguments = $"{additionalArguments}";
            var process = Process.Start(_shell, arguments);

            if (!process.WaitForExit(5 * 60 * 1000))
            {
                throw new InvalidOperationException($"Failed to wait for the installation operation to complete.");
            }

            else if (0 != process.ExitCode)
            {
                throw new InvalidOperationException($"The installation operation failed.");
            }
        }
    }
}
