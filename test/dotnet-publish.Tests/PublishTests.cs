﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;
using System;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishTests : TestBase
    {
        private readonly string _testProjectsRoot;
        private readonly Func<string, string, string> _getProjectJson = ProjectUtils.GetProjectJson;

        public PublishTests()
        {
            _testProjectsRoot = Path.Combine(RepoRoot, "TestAssets", "TestProjects");
        }

        private static readonly dynamic[] CrossPublishTestData = new[]
        {
            new 
            { 
                Rid="centos.7-x64",
                HostExtension="", 
                ExpectedArtifacts=new string[] { "libhostfxr.so", "libcoreclr.so", "libhostpolicy.so" } 
            }, 
            new 
            { 
                Rid="rhel.7.2-x64",
                HostExtension="", 
                ExpectedArtifacts=new string[] { "libhostfxr.so", "libcoreclr.so", "libhostpolicy.so" } 
            }, 
            new 
            { 
                Rid="ubuntu.14.04-x64",
                HostExtension="", 
                ExpectedArtifacts=new string[] { "libhostfxr.so", "libcoreclr.so", "libhostpolicy.so" } 
            }, 
            new 
            { 
                Rid="win7-x64",
                HostExtension=".exe", 
                ExpectedArtifacts=new string[] { "hostfxr.dll", "coreclr.dll", "hostpolicy.dll" } 
            }, 
            new 
            { 
                Rid="osx.10.11-x64",
                HostExtension="", 
                ExpectedArtifacts=new string[] { "libhostfxr.dylib", "libcoreclr.dylib", "libhostpolicy.dylib" } 
            }
        };

        public static IEnumerable<object[]> PublishOptions
        {
            get
            {
                return new[]
                {
                    new object[] { "1", "", "", "", "" },
                    new object[] { "2", "netcoreapp1.0", "", "", "" },
                    new object[] { "3", "", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier(), "", "" },
                    new object[] { "4", "", "", "Release", "" },
                    new object[] { "5", "", "", "", "some/dir"},
                    new object[] { "6", "", "", "", "some/dir/with spaces" },
                    new object[] { "7", "netcoreapp1.0", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier(), "Debug", "some/dir" },
                };
            }
        }

        [Theory]
        [MemberData("PublishOptions")]
        public void PublishOptionsTest(string testIdentifier, string framework, string runtime, string config, string outputDir)
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary", identifier: testIdentifier)
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            string testRoot = _getProjectJson(instance.TestRoot, "TestApp");

            outputDir = string.IsNullOrEmpty(outputDir) ? "" : Path.Combine(instance.TestRoot, outputDir);
            var publishCommand = new PublishCommand(testRoot, output: outputDir);
            publishCommand.Execute().Should().Pass();

            // verify the output executable generated
            var publishedDir = publishCommand.GetOutputDirectory();
            var outputExe = publishCommand.GetOutputExecutable();
            var outputPdb = Path.ChangeExtension(outputExe, "pdb");

            // lets make sure that the output exe is runnable
            var outputExePath = Path.Combine(publishedDir.FullName, publishCommand.GetOutputExecutable());
            var command = new TestCommand(outputExePath);
            command.Execute("").Should().ExitWith(100);

            // the pdb should also be published
            publishedDir.Should().HaveFile(outputPdb);
        }

        [Fact]
        public void ProjectWithContentsTest()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithContents")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var testProject = _getProjectJson(instance.TestRoot, "TestAppWithContents");
            var publishCommand = new PublishCommand(testProject);

            publishCommand.Execute().Should().Pass();
            publishCommand.GetOutputDirectory().Should().HaveFile("testcontentfile.txt");
        }

        [Fact]
        public void FailWhenNoRestoreTest()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary");

            string testProject = _getProjectJson(instance.TestRoot, "TestApp");
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void CrossPublishingHasExpectedArtifacts()
        {
            var testNugetCache = "packages_cross_publish_test";
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine("PortableTests"));
                
            var testProject = Path.Combine(instance.TestRoot, "StandaloneApp", "project.json");

            var restoreCommand = new RestoreCommand();

            restoreCommand.WorkingDirectory = Path.GetDirectoryName(testProject);
            restoreCommand.Environment["NUGET_PACKAGES"] = testNugetCache;
            restoreCommand.Execute().Should().Pass();

            foreach (var testData in CrossPublishTestData)
            {
                var buildCommand = new BuildCommand(testProject, runtime: testData.Rid);

                buildCommand.WorkingDirectory = Path.GetDirectoryName(testProject);
                buildCommand.Environment["NUGET_PACKAGES"] = testNugetCache;
                buildCommand.Execute().Should().Pass();

                var publishCommand = new PublishCommand(testProject, runtime: testData.Rid, noBuild: true);
                publishCommand.Environment["NUGET_PACKAGES"] = testNugetCache;
                publishCommand.WorkingDirectory = Path.GetDirectoryName(testProject);
                publishCommand.Execute().Should().Pass();

                var publishedDir = publishCommand.GetOutputDirectory();
                publishedDir.Should().HaveFile("StandaloneApp"+ testData.HostExtension);

                foreach (var artifact in testData.ExpectedArtifacts)
                {
                    publishedDir.Should().HaveFile(artifact);
                }
            }
        }

        [Fact]
        public void PublishesWhenPrebuildWithBuildBasePath()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary")
                                                     .WithLockFiles();

            string basePath = Path.Combine(instance.TestRoot, "build");
            string testProject = _getProjectJson(instance.TestRoot, "TestApp");
            var buildCommand = new BuildCommand(testProject, buildBasePath: basePath);
            buildCommand.Execute().Should().Pass();

            var publishCommand = new PublishCommand(testProject, buildBasePath: basePath, noBuild: true);
            publishCommand.Execute().Should().Pass();
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/2536")]
        public void PublishedLibraryShouldOutputDependenciesAndNoHost()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine("TestAppWithLibrary"))
                                                     .WithLockFiles();

            var testProject = _getProjectJson(instance.TestRoot, "TestLibrary");
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibrary.exe");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibrary.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibrary.pdb");
            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("System.Runtime.dll");
        }

        [WindowsOnlyFact(Skip="https://github.com/dotnet/cli/issues/2536")]
        public void TestLibraryBindingRedirectGeneration()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestBindingRedirectGeneration")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var lesserTestProject = _getProjectJson(instance.TestRoot, "TestLibraryLesser");

            var publishCommand = new PublishCommand(lesserTestProject, "net451");
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.pdb");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll.config");
            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibraryLesser.deps.json");

            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("Newtonsoft.Json.dll");
            publishCommand.GetOutputDirectory().Delete(true);

            publishCommand = new PublishCommand(lesserTestProject, "netstandard1.5", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier());
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.pdb");
            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibraryLesser.dll.config");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.deps.json");

            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("Newtonsoft.Json.dll");
        }

        [Fact]
        public void RefsPublishTest()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppCompilationContext")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var testProject = _getProjectJson(instance.TestRoot, "TestApp");
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestApp.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibrary.dll");

            var refsDirectory = new DirectoryInfo(Path.Combine(publishCommand.GetOutputDirectory().FullName, "refs"));
            // Should have compilation time assemblies
            refsDirectory.Should().HaveFile("System.IO.dll");
            // Libraries in which lib==ref should be deduped
            refsDirectory.Should().NotHaveFile("TestLibrary.dll");
        }

        [Fact]
        public void CompilationFailedTest()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("CompileFail")
                                                     .WithLockFiles();

            var testProject = _getProjectJson(instance.TestRoot, "CompileFail");
            var publishCommand = new PublishCommand(testProject);

            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void PublishFailsWhenProjectNotBuiltAndNoBuildFlagSet()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppCompilationContext")
                                                     .WithLockFiles();

            var testProject = _getProjectJson(instance.TestRoot, "TestApp");
            var publishCommand = new PublishCommand(testProject, noBuild: true);

            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void PublishSucceedsWhenProjectPreviouslyCompiledAndNoBuildFlagSet()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppCompilationContext")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var testProject = _getProjectJson(instance.TestRoot, "TestApp");
            var publishCommand = new PublishCommand(testProject, noBuild: true);

            publishCommand.Execute().Should().Pass();
        }

        [Fact]
        public void PublishScriptsRun()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithScripts")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var testProject = _getProjectJson(instance.TestRoot, "TestAppWithScripts");

            var publishCommand = new PublishCommand(testProject);
            var result = publishCommand.ExecuteWithCapturedOutput();

            result.Should().HaveStdOutMatching("\nprepublish_output( \\?[^%]+\\?){5}.+\npostpublish_output( \\?[^%]+\\?){5}", RegexOptions.Singleline);
            result.Should().Pass();
        }

        public void PublishAppWithOutputAssemblyName()
        {
            TestInstance instance =
                TestAssetsManager
                    .CreateTestInstance("AppWithOutputAssemblyName")
                    .WithLockFiles()
                    .WithBuildArtifacts();

            var testRoot = _getProjectJson(instance.TestRoot, "AppWithOutputAssemblyName");
            var publishCommand = new PublishCommand(testRoot, output: testRoot);
            publishCommand.Execute().Should().Pass();

            var publishedDir = publishCommand.GetOutputDirectory();
            var extension = publishCommand.GetExecutableExtension();
            var outputExe = "MyApp" + extension;
            publishedDir.Should().HaveFiles(new[] { "MyApp.dll", outputExe });
            publishedDir.Should().NotHaveFile("AppWithOutputAssemblyName" + extension);
            publishedDir.Should().NotHaveFile("AppWithOutputAssemblyName.dll");

            var command = new TestCommand(Path.Combine(publishedDir.FullName, outputExe));
            command.Execute("").Should().ExitWith(0);
        }

        [Fact]
        public void PublishFailsWhenProjectRootIsEmpty()
        {
            using (var dir = new DisposableDirectory(Temp))
            {
                var command = new TestCommand("dotnet");
                command.Execute($"publish {dir.Path}").Should().Fail();
            }
        }

        [Fact]
        public void PublishFailsWhenProjectJsonDoesNotExist()
        {
            using (var dir = new DisposableDirectory(Temp))
            {
                var command = new TestCommand("dotnet");
                string temp = Path.Combine(dir.Path, "project.json");
                command.Execute($"publish {temp}").Should().Fail();
            }
        }
    }
}
