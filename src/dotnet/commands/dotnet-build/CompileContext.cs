// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dotnet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.DotNet.Tools.Compiler;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.DotNet.ProjectModel.Compilation;

namespace Microsoft.DotNet.Tools.Build
{
    // todo: Convert CompileContext into a DAG of dependencies: if a node needs recompilation, the entire path up to root needs compilation
    // Knows how to orchestrate compilation for a ProjectContext
    // Collects icnremental safety checks and transitively compiles a project context
    internal class CompileContext
    {
        public static readonly string[] KnownCompilers = { "csc", "vbc", "fsc" };

        private readonly ProjectContext _rootProject;
        private readonly ProjectDependenciesFacade _rootProjectDependencies;
        private readonly IncrementalPreconditions _preconditions;

        public bool IsSafeForIncrementalCompilation => !_preconditions.PreconditionsDetected();

        public BuilderCommandApp Args { get; set; }
        
        public CompileContext(ProjectContext rootProject, BuilderCommandApp args)
        {
            _rootProject = rootProject;

            // Cleaner to clone the args and mutate the clone than have separate CompileContext fields for mutated args
            // and then reasoning which ones to get from args and which ones from fields.
            Args = (BuilderCommandApp)args.ShallowCopy();

            // Set up dependencies
            _rootProjectDependencies = new ProjectDependenciesFacade(_rootProject, Args.ConfigValue);

            // gather preconditions
            _preconditions = GatherIncrementalPreconditions();
        }

        public bool Compile(bool incremental)
        {
            CreateOutputDirectories();

            return CompileDependencies(incremental) && CompileRootProject(incremental);
        }

        private bool CompileRootProject(bool incremental)
        {
            try
            {
                if (incremental && !NeedsRebuilding(_rootProject, _rootProjectDependencies))
                {
                    return true;
                }

                var success = InvokeCompileOnRootProject();

                PrintSummary(success);

                return success;
            }
            finally
            {
                StampProjectWithSDKVersion(_rootProject);
            }
        }

        private bool CompileDependencies(bool incremental)
        {
            if (Args.ShouldSkipDependencies)
            {
                return true;
            }

            foreach (var dependency in Sort(_rootProjectDependencies))
            {
                var dependencyProjectContext = ProjectContext.Create(dependency.Path, dependency.Framework, new[] { _rootProject.RuntimeIdentifier });

                try
                {
                    if (incremental && !NeedsRebuilding(dependencyProjectContext, new ProjectDependenciesFacade(dependencyProjectContext, Args.ConfigValue)))
                    {
                        continue;
                    }

                    if (!InvokeCompileOnDependency(dependency))
                    {
                        return false;
                    }
                }
                finally
                {
                    StampProjectWithSDKVersion(dependencyProjectContext);
                }
            }

            return true;
        }

        private bool NeedsRebuilding(ProjectContext project, ProjectDependenciesFacade dependencies)
        {
            if (CLIChangedSinceLastCompilation(project))
            {
                Reporter.Verbose.WriteLine($"Project {project.GetDisplayName()} will be compiled because the version or bitness of the CLI changed since the last build");
                return true;
            }

            var compilerIO = GetCompileIO(project, dependencies);

            // rebuild if empty inputs / outputs
            if (!(compilerIO.Outputs.Any() && compilerIO.Inputs.Any()))
            {
                Reporter.Output.WriteLine($"Project {project.GetDisplayName()} will be compiled because it either has empty inputs or outputs");
                return true;
            }

            //rebuild if missing inputs / outputs
            if (AnyMissingIO(project, compilerIO.Outputs, "outputs") || AnyMissingIO(project, compilerIO.Inputs, "inputs"))
            {
                return true;
            }

            // find the output with the earliest write time
            var minOutputPath = compilerIO.Outputs.First();
            var minDateUtc = File.GetLastWriteTimeUtc(minOutputPath);

            foreach (var outputPath in compilerIO.Outputs)
            {
                if (File.GetLastWriteTimeUtc(outputPath) >= minDateUtc)
                {
                    continue;
                }

                minDateUtc = File.GetLastWriteTimeUtc(outputPath);
                minOutputPath = outputPath;
            }

            // find inputs that are older than the earliest output
            var newInputs = compilerIO.Inputs.FindAll(p => File.GetLastWriteTimeUtc(p) >= minDateUtc);

            if (!newInputs.Any())
            {
                Reporter.Output.WriteLine($"Project {project.GetDisplayName()} was previously compiled. Skipping compilation.");
                return false;
            }

            Reporter.Output.WriteLine($"Project {project.GetDisplayName()} will be compiled because some of its inputs were newer than its oldest output.");
            Reporter.Verbose.WriteLine();
            Reporter.Verbose.WriteLine($" Oldest output item:");
            Reporter.Verbose.WriteLine($"  {minDateUtc.ToLocalTime()}: {minOutputPath}");
            Reporter.Verbose.WriteLine();

            Reporter.Verbose.WriteLine($" Inputs newer than the oldest output item:");

            foreach (var newInput in newInputs)
            {
                Reporter.Verbose.WriteLine($"  {File.GetLastWriteTime(newInput)}: {newInput}");
            }

            Reporter.Verbose.WriteLine();

            return true;
        }

        private static bool AnyMissingIO(ProjectContext project, IEnumerable<string> items, string itemsType)
        {
            var missingItems = items.Where(i => !File.Exists(i)).ToList();

            if (!missingItems.Any())
            {
                return false;
            }

            Reporter.Verbose.WriteLine($"Project {project.GetDisplayName()} will be compiled because expected {itemsType} are missing.");

            foreach (var missing in missingItems)
            {
                Reporter.Verbose.WriteLine($" {missing}");
            }

            Reporter.Verbose.WriteLine(); ;

            return true;
        }

        private bool CLIChangedSinceLastCompilation(ProjectContext project)
        {
            var currentVersionFile = DotnetFiles.VersionFile;
            var versionFileFromLastCompile = project.GetSDKVersionFile(Args.ConfigValue, Args.BuildBasePathValue, Args.OutputValue);

            if (!File.Exists(currentVersionFile))
            {
                // this CLI does not have a version file; cannot tell if CLI changed
                return false;
            }

            if (!File.Exists(versionFileFromLastCompile))
            {
                // this is the first compilation; cannot tell if CLI changed
                return false;
            }

            var currentContent = ComputeCurrentVersionFileData();

            var versionsAreEqual = string.Equals(currentContent, File.ReadAllText(versionFileFromLastCompile), StringComparison.OrdinalIgnoreCase);

            return !versionsAreEqual;
        }

        private void StampProjectWithSDKVersion(ProjectContext project)
        {
            if (File.Exists(DotnetFiles.VersionFile))
            {
                var projectVersionFile = project.GetSDKVersionFile(Args.ConfigValue, Args.BuildBasePathValue, Args.OutputValue);
                var parentDirectory = Path.GetDirectoryName(projectVersionFile);

                if (!Directory.Exists(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                string content = ComputeCurrentVersionFileData();

                File.WriteAllText(projectVersionFile, content);
            }
            else
            {
                Reporter.Verbose.WriteLine($"Project {project.GetDisplayName()} was not stamped with a CLI version because the version file does not exist: {DotnetFiles.VersionFile}");
            }
        }

        private static string ComputeCurrentVersionFileData()
        {
            var content = File.ReadAllText(DotnetFiles.VersionFile);
            content += Environment.NewLine;
            content += PlatformServices.Default.Runtime.GetRuntimeIdentifier();
            return content;
        }

        private void PrintSummary(bool success)
        {
            // todo: Ideally it's the builder's responsibility for adding the time elapsed. That way we avoid cross cutting display concerns between compile and build for printing time elapsed
            if (success)
            {
                Reporter.Output.Write(" " + _preconditions.LogMessage());
                Reporter.Output.WriteLine();
            }

            Reporter.Output.WriteLine();
        }

        private void CreateOutputDirectories()
        {
            if (!string.IsNullOrEmpty(Args.OutputValue))
            {
                Directory.CreateDirectory(Args.OutputValue);
            }
            if (!string.IsNullOrEmpty(Args.BuildBasePathValue))
            {
                Directory.CreateDirectory(Args.BuildBasePathValue);
            }
        }

        private IncrementalPreconditions GatherIncrementalPreconditions()
        {
            var preconditions = new IncrementalPreconditions(Args.ShouldPrintIncrementalPreconditions);

            if (Args.ShouldNotUseIncrementality)
            {
                preconditions.AddForceUnsafePrecondition();
            }

            var projectsToCheck = GetProjectsToCheck();

            foreach (var project in projectsToCheck)
            {
                CollectScriptPreconditions(project, preconditions);
                CollectCompilerNamePreconditions(project, preconditions);
                CollectCheckPathProbingPreconditions(project, preconditions);
            }

            return preconditions;
        }

        // check the entire project tree that needs to be compiled, duplicated for each framework
        private List<ProjectContext> GetProjectsToCheck()
        {
            if (Args.ShouldSkipDependencies)
            {
                return new List<ProjectContext>(1) { _rootProject };
            }

            // include initial root project
            var contextsToCheck = new List<ProjectContext>(1 + _rootProjectDependencies.ProjectDependenciesWithSources.Count) { _rootProject };

            // convert ProjectDescription to ProjectContext
            var dependencyContexts = _rootProjectDependencies.ProjectDependenciesWithSources.Select
                (keyValuePair => ProjectContext.Create(keyValuePair.Value.Path, keyValuePair.Value.Framework));

            contextsToCheck.AddRange(dependencyContexts);


            return contextsToCheck;
        }

        private void CollectCheckPathProbingPreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            var pathCommands = CompilerUtil.GetCommandsInvokedByCompile(project)
                .Select(commandName => Command.CreateDotNet(commandName, Enumerable.Empty<string>(), project.TargetFramework))
                .Where(c => c.ResolutionStrategy.Equals(CommandResolutionStrategy.Path));

            foreach (var pathCommand in pathCommands)
            {
                preconditions.AddPathProbingPrecondition(project.ProjectName(), pathCommand.CommandName);
            }
        }

        private void CollectCompilerNamePreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            if (project.ProjectFile != null)
            {
                var projectCompiler = project.ProjectFile.CompilerName;

                if (!KnownCompilers.Any(knownCompiler => knownCompiler.Equals(projectCompiler, StringComparison.Ordinal)))
                {
                    preconditions.AddUnknownCompilerPrecondition(project.ProjectName(), projectCompiler);
                }
            }
        }

        private void CollectScriptPreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            if (project.ProjectFile != null)
            {
                var preCompileScripts = project.ProjectFile.Scripts.GetOrEmpty(ScriptNames.PreCompile);
                var postCompileScripts = project.ProjectFile.Scripts.GetOrEmpty(ScriptNames.PostCompile);

                if (preCompileScripts.Any())
                {
                    preconditions.AddPrePostScriptPrecondition(project.ProjectName(), ScriptNames.PreCompile);
                }

                if (postCompileScripts.Any())
                {
                    preconditions.AddPrePostScriptPrecondition(project.ProjectName(), ScriptNames.PostCompile);
                }
            }
        }

        private bool InvokeCompileOnDependency(ProjectDescription projectDependency)
        {
            var args = new List<string>();

            args.Add("--framework");
            args.Add($"{projectDependency.Framework}");

            args.Add("--configuration");
            args.Add(Args.ConfigValue);
            args.Add(projectDependency.Project.ProjectDirectory);

            if (!string.IsNullOrWhiteSpace(Args.RuntimeValue))
            {
                args.Add("--runtime");
                args.Add(Args.RuntimeValue);
            }

            if (!string.IsNullOrEmpty(Args.VersionSuffixValue))
            {
                args.Add("--version-suffix");
                args.Add(Args.VersionSuffixValue);
            }

            if (!string.IsNullOrWhiteSpace(Args.BuildBasePathValue))
            {
                args.Add("--build-base-path");
                args.Add(Args.BuildBasePathValue);
            }

            var compileResult = CompileCommand.Run(args.ToArray());

            return compileResult == 0;
        }

        private bool InvokeCompileOnRootProject()
        {
            // todo: add methods to CompilerCommandApp to generate the arg string?
            var args = new List<string>();
            args.Add("--framework");
            args.Add(_rootProject.TargetFramework.ToString());
            args.Add("--configuration");
            args.Add(Args.ConfigValue);

            if (!string.IsNullOrWhiteSpace(Args.RuntimeValue))
            {
                args.Add("--runtime");
                args.Add(Args.RuntimeValue);
            }

            if (!string.IsNullOrEmpty(Args.OutputValue))
            {
                args.Add("--output");
                args.Add(Args.OutputValue);
            }

            if (!string.IsNullOrEmpty(Args.VersionSuffixValue))
            {
                args.Add("--version-suffix");
                args.Add(Args.VersionSuffixValue);
            }

            if (!string.IsNullOrEmpty(Args.BuildBasePathValue))
            {
                args.Add("--build-base-path");
                args.Add(Args.BuildBasePathValue);
            }

            //native args
            if (Args.IsNativeValue)
            {
                args.Add("--native");
            }

            if (Args.IsCppModeValue)
            {
                args.Add("--cpp");
            }

            if (!string.IsNullOrWhiteSpace(Args.CppCompilerFlagsValue))
            {
                args.Add("--cppcompilerflags");
                args.Add(Args.CppCompilerFlagsValue);
            }

            if (!string.IsNullOrWhiteSpace(Args.ArchValue))
            {
                args.Add("--arch");
                args.Add(Args.ArchValue);
            }

            foreach (var ilcArg in Args.IlcArgsValue)
            {
                args.Add("--ilcarg");
                args.Add(ilcArg);
            }

            if (!string.IsNullOrWhiteSpace(Args.IlcPathValue))
            {
                args.Add("--ilcpath");
                args.Add(Args.IlcPathValue);
            }

            if (!string.IsNullOrWhiteSpace(Args.IlcSdkPathValue))
            {
                args.Add("--ilcsdkpath");
                args.Add(Args.IlcSdkPathValue);
            }

            args.Add(_rootProject.ProjectDirectory);

            var compileResult = CompileCommand.Run(args.ToArray());

            var succeeded = compileResult == 0;

            if (succeeded)
            {
                MakeRunnable();
            }

            return succeeded;
        }

        private void CopyCompilationOutput(OutputPaths outputPaths)
        {
            var dest = outputPaths.RuntimeOutputPath;
            var source = outputPaths.CompilationOutputPath;

            // No need to copy if dest and source are the same
            if(string.Equals(dest, source, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var file in outputPaths.CompilationFiles.All())
            {
                var destFileName = file.Replace(source, dest);
                var directoryName = Path.GetDirectoryName(destFileName);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                File.Copy(file, destFileName, true);
            }
        }

        private void MakeRunnable()
        {
            foreach (var runtimeContext in CreateRuntimeContexts())
            {
                var outputPaths = runtimeContext.GetOutputPaths(Args.ConfigValue, Args.BuildBasePathValue, Args.OutputValue);
                var libraryExporter = runtimeContext.CreateExporter(Args.ConfigValue, Args.BuildBasePathValue);

                CopyCompilationOutput(outputPaths);

                var executable = new Executable(runtimeContext, outputPaths, libraryExporter, Args.ConfigValue);
                executable.MakeCompilationOutputRunnable();
            }
        }
        
        public IEnumerable<ProjectContext> CreateRuntimeContexts()
        {
            var allRuntimeContexts = _rootProject.CreateAllRuntimeContexts(Args.ConfigValue);
            if (!string.IsNullOrEmpty(Args.RuntimeValue))
            {
                return ProjectContext.FilterProjectContextsByRuntime(allRuntimeContexts, Args.RuntimeValue);
            }

            return allRuntimeContexts;
        }

        private static IEnumerable<ProjectDescription> Sort(ProjectDependenciesFacade dependencies)
        {
            var outputs = new List<ProjectDescription>();

            foreach (var pair in dependencies.Dependencies)
            {
                Sort(pair.Value, dependencies, outputs);
            }

            return outputs.Distinct(new ProjectComparer());
        }

        private static void Sort(LibraryExport node, ProjectDependenciesFacade dependencies, IList<ProjectDescription> outputs)
        {
            // Sorts projects in dependency order so that we only build them once per chain
            ProjectDescription projectDependency;
            foreach (var dependency in node.Library.Dependencies)
            {
                // Sort the children
                Sort(dependencies.Dependencies[dependency.Name], dependencies, outputs);
            }

            // Add this node to the list if it is a project
            if (dependencies.ProjectDependenciesWithSources.TryGetValue(node.Library.Identity.Name, out projectDependency))
            {
                // Add to the list of projects to build
                outputs.Add(projectDependency);
            }
        }

        private class ProjectComparer : IEqualityComparer<ProjectDescription>
        {
            public bool Equals(ProjectDescription x, ProjectDescription y) => string.Equals(x.Identity.Name, y.Identity.Name, StringComparison.Ordinal);
            public int GetHashCode(ProjectDescription obj) => obj.Identity.Name.GetHashCode();
        }

        public struct CompilerIO
        {
            public readonly List<string> Inputs;
            public readonly List<string> Outputs;

            public CompilerIO(List<string> inputs, List<string> outputs)
            {
                Inputs = inputs;
                Outputs = outputs;
            }
        }

        // computes all the inputs and outputs that would be used in the compilation of a project
        // ensures that all paths are files
        // ensures no missing inputs
        public CompilerIO GetCompileIO(ProjectContext project, ProjectDependenciesFacade dependencies)
        {
            var buildConfiguration = Args.ConfigValue;
            var buildBasePath = Args.BuildBasePathValue;
            var outputPath = Args.OutputValue;
            var isRootProject = project == _rootProject;

            var compilerIO = new CompilerIO(new List<string>(), new List<string>());
            var calculator = project.GetOutputPaths(buildConfiguration, buildBasePath, outputPath);
            var binariesOutputPath = calculator.CompilationOutputPath;

            // input: project.json
            compilerIO.Inputs.Add(project.ProjectFile.ProjectFilePath);

            // input: lock file; find when dependencies change
            AddLockFile(project, compilerIO);

            // input: source files
            compilerIO.Inputs.AddRange(CompilerUtil.GetCompilationSources(project));

            // todo: Factor out dependency resolution between Build and Compile. Ideally Build injects the dependencies into Compile
            // input: dependencies
            AddDependencies(dependencies, compilerIO);

            var allOutputPath = new HashSet<string>(calculator.CompilationFiles.All());
            foreach (var runtimeContext in CreateRuntimeContexts())
            {
                foreach (var path in runtimeContext.GetOutputPaths(buildConfiguration, buildBasePath, outputPath).RuntimeFiles.All())
                {
                    allOutputPath.Add(path);
                }
            }

            // output: compiler outputs
            foreach (var path in allOutputPath)
            {
                compilerIO.Outputs.Add(path);
            }

            // input compilation options files
            AddCompilationOptions(project, buildConfiguration, compilerIO);

            // input / output: resources with culture
            AddNonCultureResources(project, calculator.IntermediateOutputDirectoryPath, compilerIO);

            // input / output: resources without culture
            AddCultureResources(project, binariesOutputPath, compilerIO);

            return compilerIO;
        }

        private static void AddLockFile(ProjectContext project, CompilerIO compilerIO)
        {
            if (project.LockFile == null)
            {
                var errorMessage = $"Project {project.ProjectName()} does not have a lock file.";
                Reporter.Error.WriteLine(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            compilerIO.Inputs.Add(project.LockFile.LockFilePath);

            if (project.LockFile.ExportFile != null)
            {
                compilerIO.Inputs.Add(project.LockFile.ExportFile.ExportFilePath);
            }
        }

        private static void AddDependencies(ProjectDependenciesFacade dependencies, CompilerIO compilerIO)
        {
            // add dependency sources that need compilation
            compilerIO.Inputs.AddRange(dependencies.ProjectDependenciesWithSources.Values.SelectMany(p => p.Project.Files.SourceFiles));

            // non project dependencies get captured by changes in the lock file
        }

        private static void AddCompilationOptions(ProjectContext project, string config, CompilerIO compilerIO)
        {
            var compilerOptions = project.ResolveCompilationOptions(config);

            // input: key file
            if (compilerOptions.KeyFile != null)
            {
                compilerIO.Inputs.Add(compilerOptions.KeyFile);
            }
        }

        private static void AddNonCultureResources(ProjectContext project, string intermediaryOutputPath, CompilerIO compilerIO)
        {
            foreach (var resourceIO in CompilerUtil.GetNonCultureResources(project.ProjectFile, intermediaryOutputPath))
            {
                compilerIO.Inputs.Add(resourceIO.InputFile);

                if (resourceIO.OutputFile != null)
                {
                    compilerIO.Outputs.Add(resourceIO.OutputFile);
                }
            }
        }

        private static void AddCultureResources(ProjectContext project, string outputPath, CompilerIO compilerIO)
        {
            foreach (var cultureResourceIO in CompilerUtil.GetCultureResources(project.ProjectFile, outputPath))
            {
                compilerIO.Inputs.AddRange(cultureResourceIO.InputFileToMetadata.Keys);

                if (cultureResourceIO.OutputFile != null)
                {
                    compilerIO.Outputs.Add(cultureResourceIO.OutputFile);
                }
            }
        }
    }

}
