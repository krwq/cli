﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using System.Diagnostics;
using FluentAssertions;

namespace Microsoft.DotNet.Tests.ArgumentForwarding
{
    public class ArgumentForwardingTests : TestBase
    {
        private static readonly string s_reflectorExeName = "reflector" + Constants.ExeSuffix;

        private string ReflectorPath { get; set; }

        public static void Main()
        {
            Console.WriteLine("Dummy Entrypoint.");
        }
       
        public ArgumentForwardingTests()
        {
            // This test has a dependency on an argument reflector
            // Make sure it's been binplaced properly
            FindAndEnsureReflectorPresent();
        }

        private void FindAndEnsureReflectorPresent()
        {
            ReflectorPath = Path.Combine(AppContext.BaseDirectory, s_reflectorExeName);

            File.Exists(ReflectorPath).Should().BeTrue();
        }

        /// <summary>
        /// Tests argument forwarding in Command.Create
        /// This is a critical scenario for the driver.
        /// </summary>
        /// <param name="testUserArgument"></param>
        [Theory]
        [InlineData(@"""abc"" d e")]
        [InlineData(@"""abc""      d e")]
        [InlineData("\"abc\"\t\td\te")]
        [InlineData(@"a\\b d""e f""g h")]
        [InlineData(@"\ \\ \\\")]
        [InlineData(@"a\""b c d")]
        [InlineData(@"a\\""b c d")]
        [InlineData(@"a\\\""b c d")]
        [InlineData(@"a\\\\""b c d")]
        [InlineData(@"a\\\\""b c d")]
        [InlineData(@"a\\\\""b c"" d e")]
        [InlineData(@"a""b c""d e""f g""h i""j k""l")]
        [InlineData(@"a b c""def")]
        [InlineData(@"""\a\"" \\""\\\ b c")]
        [InlineData(@"a\""b \\ cd ""\e f\"" \\""\\\")]
        public void TestArgumentForwarding(string testUserArgument)
        {
            // Get Baseline Argument Evaluation via Reflector
            var rawEvaluatedArgument = RawEvaluateArgumentString(testUserArgument);

            // Escape and Re-Evaluate the rawEvaluatedArgument
            var escapedEvaluatedRawArgument = EscapeAndEvaluateArgumentString(rawEvaluatedArgument);

            rawEvaluatedArgument.Length.Should().Be(escapedEvaluatedRawArgument.Length);

            for (int i=0; i<rawEvaluatedArgument.Length; ++i)
            {
                var rawArg = rawEvaluatedArgument[i];
                var escapedArg = escapedEvaluatedRawArgument[i];

                rawArg.Should().Be(escapedArg);
            }
        }

        /// <summary>
        /// EscapeAndEvaluateArgumentString returns a representation of string[] args
        /// when rawEvaluatedArgument is passed as an argument to a process using
        /// Command.Create(). Ideally this should escape the argument such that 
        /// the output is == rawEvaluatedArgument.
        /// </summary>
        /// <param name="rawEvaluatedArgument">A string[] representing string[] args as already evaluated by a process</param>
        /// <returns></returns>
        private string[] EscapeAndEvaluateArgumentString(string[] rawEvaluatedArgument)
        {
            var commandResult = Command.Create(ReflectorPath, rawEvaluatedArgument)
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            commandResult.ExitCode.Should().Be(0);

            return ParseReflectorOutput(commandResult.StdOut);
        }

        /// <summary>
        /// Parse the output of the reflector into a string array.
        /// Reflector output is simply string[] args written to 
        /// one string separated by commas.
        /// </summary>
        /// <param name="reflectorOutput"></param>
        /// <returns></returns>
        private string[] ParseReflectorOutput(string reflectorOutput)
        {
            return reflectorOutput.Split(',');
        }

        /// <summary>
        /// RawEvaluateArgumentString returns a representation of string[] args
        /// when testUserArgument is provided (unmodified) as arguments to a c#
        /// process.
        /// </summary>
        /// <param name="testUserArgument">A test argument representing what a "user" would provide to a process</param>
        /// <returns>A string[] representing string[] args with the provided testUserArgument</returns>
        private string[] RawEvaluateArgumentString(string testUserArgument)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = s_reflectorExeName,
                    Arguments = testUserArgument,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            var stdOut = proc.StandardOutput.ReadToEnd();

            Assert.Equal(0, proc.ExitCode);

            return ParseReflectorOutput(stdOut);
        }
    }
}