// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Project = Microsoft.DotNet.Internal.ProjectModel.Project;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigrateSharedOptionsRule : IMigrationRule
    {
        //private ProjectDependencyFinder _dependencyFinder = new ProjectDependencyFinder();

        //private IEnumerable<>

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            //var outputProj = migrationRuleInputs.OutputMSBuildProject;
            //var projectFile = migrationRuleInputs.DefaultProjectContext.ProjectFile.ProjectFilePath;
            
            //foreach (var dep in _dependencyFinder.ResolveProjectDependencies(
            //    migrationSettings.ProjectDirectory,
            //    migrationSettings.ProjectXProjFilePath,
            //    migrationSettings.SolutionFile))
            //{
            //    dep.
            //}

            //var transformResults = projectContext.ProjectFile.Files.SharedPatternsGroup.IncludePatterns
        }
    }
}
