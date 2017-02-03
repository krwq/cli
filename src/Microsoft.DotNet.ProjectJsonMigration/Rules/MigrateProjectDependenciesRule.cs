// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using Microsoft.DotNet.Internal.ProjectModel.Files;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigrateProjectDependenciesRule : IMigrationRule
    {
        private readonly ITransformApplicator _transformApplicator;
        private readonly ProjectDependencyFinder _projectDependencyFinder;
        private string _projectDirectory;

        public MigrateProjectDependenciesRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
            _projectDependencyFinder = new ProjectDependencyFinder();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            _projectDirectory = migrationSettings.ProjectDirectory;

            var migratedXProjDependencyPaths = MigrateXProjProjectDependencies(migrationRuleInputs);
            var migratedXProjDependencyNames = new HashSet<string>(
                migratedXProjDependencyPaths.Select(p =>
                    Path.GetFileNameWithoutExtension(PathUtility.GetPathWithDirectorySeparator(p))));
            MigrateProjectJsonProjectDependencies(
                migrationRuleInputs.ProjectContexts, 
                migratedXProjDependencyNames,
                migrationSettings.SolutionFile,
                migrationRuleInputs.OutputMSBuildProject);
        }

        private IEnumerable<string> MigrateXProjProjectDependencies(MigrationRuleInputs migrationRuleInputs)
        {
            var csprojReferenceItems = _projectDependencyFinder.ResolveXProjProjectDependencies(migrationRuleInputs.ProjectXproj);

            if (!csprojReferenceItems.Any())
            {
                return Enumerable.Empty<string>();
            }

            var csprojTransformedReferences = new List<ProjectItemElement>();

            foreach (var csprojReferenceItem in csprojReferenceItems)
            {
                var conditionChain = csprojReferenceItem.ConditionChain();
                var condition = string.Join(" and ", conditionChain);

                var referenceInclude = string.Join(";", csprojReferenceItem.Includes()
                    .Where(include => 
                        string.Equals(Path.GetExtension(include), ".csproj", StringComparison.OrdinalIgnoreCase)));

                var transformItem = ProjectDependencyStringTransform.Transform(referenceInclude);
                transformItem.Condition = condition; 

                csprojTransformedReferences.Add(transformItem);
            }

            MigrationTrace.Instance.WriteLine(String.Format(LocalizableStrings.MigratingCountXprojToCsprojReferences, nameof(MigrateProjectDependenciesRule), csprojTransformedReferences.Count()));

            foreach (var csprojTransformedReference in csprojTransformedReferences)
            {
                _transformApplicator.Execute(csprojTransformedReference, migrationRuleInputs.CommonItemGroup, true);
            }

            return csprojTransformedReferences.SelectMany(r => r.Includes());
        }

        public void MigrateProjectJsonProjectDependencies(
            IEnumerable<ProjectContext> projectContexts,
            HashSet<string> migratedXProjDependencyNames,
            SlnFile solutionFile,
            ProjectRootElement outputMSBuildProject)
        {
            if(projectContexts.Any())
            {
                MigrateProjectJsonProjectDependency(
                    projectContexts.First().ProjectFile,
                    null,
                    migratedXProjDependencyNames,
                    solutionFile,
                    outputMSBuildProject);
            }

            foreach (var projectContext in projectContexts)
            {
                MigrateProjectJsonProjectDependency(
                    projectContext.ProjectFile,
                    projectContext.TargetFramework,
                    migratedXProjDependencyNames,
                    solutionFile,
                    outputMSBuildProject);
            }
        }

        private IEnumerable<string> RemoveBuiltinIncludeSharedPatterns(IEnumerable<string> includePatterns)
        {
            return includePatterns.Where((pattern) => pattern != @"compiler\shared\**\*.cs");
        }

        private static string EnsureRootedPath(string path, string defaultRootDirectory)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(defaultRootDirectory, path);
        }

        private static string RebaseRelativePath(string path, string oldBaseDirectory, string newBaseDirectory)
        {
            string fullPath = EnsureRootedPath(path, oldBaseDirectory);
            return PathUtility.GetRelativePath(PathUtility.EnsureTrailingSlash(newBaseDirectory), fullPath);
        }

        private static string NormalizeSlashesForMsbuild(string path)
        {
            return path.Replace('/', '\\');
        }

        private static IEnumerable<string> PreparePathsForMsbuild(IEnumerable<string> paths, string oldBaseDirectory, string newBaseDirectory)
        {
            return paths.Select((path) => RebaseRelativePath(path, oldBaseDirectory, newBaseDirectory))
                        .Select((path) => NormalizeSlashesForMsbuild(path));
        }

        private void MigrateSharedKey(Project project, NuGetFramework framework, List<ProjectDependency> dependencies, ProjectRootElement outputMSBuildProject)
        {
            var itemGroup = new Lazy<ProjectItemGroupElement>(() => outputMSBuildProject.AddItemGroup());

            foreach (var projectDependencyPath in dependencies)
            {
                var projectDependency = ProjectReader.GetProject(projectDependencyPath.ProjectFilePath);
                var sharedPatterns = projectDependency.Files.SharedPatternsGroup;

                var depRoot = new FileInfo(projectDependencyPath.ProjectFilePath).Directory.FullName;
                string[] includePatterns = PreparePathsForMsbuild(
                                               RemoveBuiltinIncludeSharedPatterns(sharedPatterns.IncludePatterns),
                                               depRoot,
                                               project.ProjectDirectory)
                                           .ToArray();
                string[] excludePatterns = PreparePathsForMsbuild(
                                               sharedPatterns.ExcludePatterns,
                                               depRoot,
                                               project.ProjectDirectory)
                                           .ToArray();

                if (includePatterns.Length > 0)
                {
                    var t = new AddItemTransform<bool>("Compile", includePatterns, excludePatterns, (cond) => true);
                    t.WithMetadata("Link", "$([MSBuild]::MakeRelative('$(MSBuildThisFileDirectory)', '%(FullPath)'))");
                    if (framework != null)
                    {
                        t.WithMetadata("Condition", $" '$(TargetFramework)' == '{framework.GetShortFolderName()}' ", expressedAsAttribute: true);
                    }

                    _transformApplicator.Execute(t.ConditionallyTransform(source: true),
                                                 itemGroup.Value,
                                                 mergeExisting: true);
                }
            }
        }

        private void MigrateProjectJsonProjectDependency(
            Project project,
            NuGetFramework framework,
            HashSet<string> migratedXProjDependencyNames,
            SlnFile solutionFile,
            ProjectRootElement outputMSBuildProject)
        {
            var projectDependencies = _projectDependencyFinder.ResolveAllProjectDependenciesForFramework(
                    new ProjectDependency(project.Name, project.ProjectFilePath),
                    framework,
                    migratedXProjDependencyNames,
                    solutionFile)
                    .ToList();

            var projectDependencyTransformResults = 
                projectDependencies.Select(p => ProjectDependencyTransform.Transform(p));
            
            if (projectDependencyTransformResults.Any())
            {
                AddProjectDependenciesToNewItemGroup(
                    outputMSBuildProject.AddItemGroup(), 
                    projectDependencyTransformResults, 
                    framework);
            }

            MigrateSharedKey(project, framework, projectDependencies, outputMSBuildProject);
        }

        private void AddProjectDependenciesToNewItemGroup(
            ProjectItemGroupElement itemGroup, 
            IEnumerable<ProjectItemElement> projectDependencyTransformResults,
            NuGetFramework targetFramework)
        {
            if (targetFramework != null)
            {
                itemGroup.Condition = targetFramework.GetMSBuildCondition();
            }

            foreach (var projectDependencyTransformResult in projectDependencyTransformResults)
            {
                _transformApplicator.Execute(projectDependencyTransformResult, itemGroup, true);
            }
        }
        
        private AddItemTransform<ProjectDependency> ProjectDependencyTransform => 
            GetProjectDependencyTransfrom();

        private Func<AddItemTransform<ProjectDependency>> GetProjectDependencyTransfrom => 
            () => new AddItemTransform<ProjectDependency>(
                "ProjectReference",
                dep => 
                {
                    var projectDir = Path.GetDirectoryName(dep.ProjectFilePath);
                    var migratedProjectFileName = Path.GetFileName(projectDir) + ".csproj";
                    var relativeProjectDir = PathUtility.GetRelativePath(_projectDirectory + "/", projectDir);

                    return Path.Combine(relativeProjectDir, migratedProjectFileName);
                },
                dep => "",
                dep => true);

        private AddItemTransform<string> ProjectDependencyStringTransform => new AddItemTransform<string>(
            "ProjectReference",
            path => path,
            path => "",
            path => true);
    }
}
