// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectContext
    {
        private string[] _runtimeFallbacks;

        public ProjectContextIdentity Identity { get; }

        public GlobalSettings GlobalSettings { get; }

        public ProjectDescription RootProject { get; }

        public NuGetFramework TargetFramework { get; }

        public LibraryDescription PlatformLibrary { get; }

        public bool IsPortable { get; }

        public string RuntimeIdentifier { get; }

        public Project ProjectFile => RootProject?.Project;

        public LockFile LockFile { get; }

        public string RootDirectory => GlobalSettings?.DirectoryPath;

        public string ProjectDirectory => ProjectFile.ProjectDirectory;

        public string PackagesDirectory { get; }

        public LibraryManager LibraryManager { get; }

        internal ProjectContext(
            GlobalSettings globalSettings,
            ProjectDescription rootProject,
            LibraryDescription platformLibrary,
            NuGetFramework targetFramework,
            bool isPortable,
            string runtimeIdentifier,
            string packagesDirectory,
            LibraryManager libraryManager,
            LockFile lockfile)
        {
            Identity = new ProjectContextIdentity(rootProject?.Path, targetFramework);
            GlobalSettings = globalSettings;
            RootProject = rootProject;
            PlatformLibrary = platformLibrary;
            TargetFramework = targetFramework;
            RuntimeIdentifier = runtimeIdentifier;
            PackagesDirectory = packagesDirectory;
            LibraryManager = libraryManager;
            LockFile = lockfile;
            IsPortable = isPortable;
        }

        public LibraryExporter CreateExporter(string configuration, string buildBasePath = null)
        {
            if (IsPortable && RuntimeIdentifier != null && _runtimeFallbacks == null)
            {
                var graph = RuntimeGraphCollector.Collect(LibraryManager.GetLibraries());
                _runtimeFallbacks = graph.ExpandRuntime(RuntimeIdentifier).ToArray();
            }
            return new LibraryExporter(RootProject,
                LibraryManager,
                configuration,
                RuntimeIdentifier,
                _runtimeFallbacks,
                buildBasePath,
                RootDirectory);
        }

        /// <summary>
        /// Creates a project context for the project located at <paramref name="projectPath"/>,
        /// specifically in the context of the framework specified in <paramref name="framework"/>
        /// </summary>
        public static ProjectContext Create(string projectPath, NuGetFramework framework)
        {
            return Create(projectPath, framework, Enumerable.Empty<string>());
        }

        /// <summary>
        /// Creates a project context for the project located at <paramref name="projectPath"/>,
        /// specifically in the context of the framework specified in <paramref name="framework"/>
        /// and the candidate runtime identifiers specified in <param name="runtimeIdentifiers"/>
        /// </summary>
        public static ProjectContext Create(string projectPath, NuGetFramework framework, IEnumerable<string> runtimeIdentifiers)
        {
            if (projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }
            return new ProjectContextBuilder()
                        .WithProjectDirectory(projectPath)
                        .WithTargetFramework(framework)
                        .WithRuntimeIdentifiers(runtimeIdentifiers)
                        .Build();
        }

        public static ProjectContextBuilder CreateBuilder(string projectPath, NuGetFramework framework)
        {
            if (projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.GetDirectoryName(projectPath);
            }
            return new ProjectContextBuilder()
                        .WithProjectDirectory(projectPath)
                        .WithTargetFramework(framework);
        }
        
        /// <summary>
        /// Creates a project context for each framework located in the project at <paramref name="projectPath"/>
        /// </summary>
        public static IEnumerable<ProjectContext> CreateContextForEachFramework(string projectPath, ProjectReaderSettings settings = null, IEnumerable<string> runtimeIdentifiers = null)
        {
            if (!projectPath.EndsWith(Project.FileName))
            {
                projectPath = Path.Combine(projectPath, Project.FileName);
            }
            var project = ProjectReader.GetProject(projectPath, settings);

            foreach (var framework in project.GetTargetFrameworks())
            {
                yield return new ProjectContextBuilder()
                                .WithProject(project)
                                .WithTargetFramework(framework.FrameworkName)
                                .WithReaderSettings(settings)
                                .WithRuntimeIdentifiers(runtimeIdentifiers ?? Enumerable.Empty<string>())
                                .Build();
            }
        }

        /// <summary>
        /// Creates a project context for each target located in the project at <paramref name="projectPath"/>
        /// </summary>
        public static IEnumerable<ProjectContext> CreateContextForEachTarget(string projectPath, ProjectReaderSettings settings = null)
        {
            var project = ProjectReader.GetProject(projectPath);

            return new ProjectContextBuilder()
                        .WithReaderSettings(settings)
                        .WithProject(project)
                        .BuildAllTargets();
        }

        public static bool IsRidCompatibleWith(string rid, string otherRid)
        {
            if (otherRid == null)
            {
                return false;
            }

            if (rid == otherRid)
            {
                return true;
            }

            if (rid.EndsWith("x86") != otherRid.EndsWith("x86"))
            {
                return false;
            }

            if (rid.StartsWith("win10-"))
            {
                return otherRid.StartsWith("win8-") || otherRid.StartsWith("win7-");
            }

            if (rid.StartsWith("win8-"))
            {
                return otherRid.StartsWith("win7-");
            }

            return false;
        }

        /// <summary>
        /// Creates a project context based on current platform
        /// </summary>
        /// <param name="buildConfiguration">build configuration</param>
        /// <returns></returns>

        public ProjectContext CreateRuntimeContextForCurrentPlatform(string buildConfiguration)
        {
            if (IsPortable)
            {
                return this;
            }

            string currentRid = PlatformServices.Default.Runtime.GetRuntimeIdentifier();

            List<ProjectContext> contexts = CreateAllRuntimeContexts(buildConfiguration).ToList();
            foreach (var runtimeContext in contexts)
            {
                if (runtimeContext.RuntimeIdentifier == currentRid)
                {
                    return runtimeContext;
                }
            }

            foreach (var runtimeContext in contexts)
            {
                if (IsRidCompatibleWith(currentRid, runtimeContext.RuntimeIdentifier))
                {
                    return runtimeContext;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a list of project contexts representing runtime based on a given context
        /// </summary>
        /// <param name="buildConfiguration">build configuration</param>
        /// <returns>List of project contexts - one for each runtime identifier and one for runtime independent IL code (RuntimeIdentifier == null)</returns>

        public IEnumerable<ProjectContext> CreateAllRuntimeContexts(string buildConfiguration)
        {
            if (!ProjectFile.HasRuntimeOutput(buildConfiguration))
            {
                return new[] { this };
            }

            IEnumerable<ProjectContext> allContexts = CreateContextForEachTarget(ProjectFile.ProjectFilePath);
            IEnumerable<ProjectContext> runtimeContextsForTarget = FilterProjectContextsByFramework(allContexts, TargetFramework);

            return runtimeContextsForTarget;
        }

        public static IEnumerable<ProjectContext> FilterProjectContextsByRuntime(IEnumerable<ProjectContext> contexts, string rid)
        {
            return contexts.Where(t => rid.Equals(t.RuntimeIdentifier)).ToList();
        }

        public static IEnumerable<ProjectContext> FilterProjectContextsByFramework(IEnumerable<ProjectContext> contexts, NuGetFramework framework)
        {
            return contexts.Where(t => framework.Equals(t.TargetFramework)).ToList();
        }

        public OutputPaths GetOutputPaths(string configuration, string buidBasePath = null, string outputPath = null)
        {
            return OutputPathsCalculator.GetOutputPaths(ProjectFile,
                TargetFramework,
                RuntimeIdentifier,
                configuration,
                RootDirectory,
                buidBasePath,
                outputPath);
        }
    }
}
