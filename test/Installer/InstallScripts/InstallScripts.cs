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

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class InstallScriptsTests : TestBase
    {
        private static string _shell;
        
        static InstallScriptsTests()
        {
            _shell = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell" : "bash";
        }
        
        [Fact]
        public void TestDotnetBuild()
        {
            Console.WriteLine($"Default shell: {_shell}");
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
