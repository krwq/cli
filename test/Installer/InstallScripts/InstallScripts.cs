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
        public static void Main()
        {
            Console.WriteLine("Dummy Entrypoint.");
        }

        [Fact]
        public void TestDotnetBuild()
        {
            //
        }
    }
}
