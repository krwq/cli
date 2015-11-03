using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel.Workspaces
{
    public class Workspace
    {
        private readonly Dictionary<string, ProjectId> _projectIds =
            new Dictionary<string, ProjectId>();

        private readonly WorkspaceCache _cache =
            new WorkspaceCache();

        private Workspace(GlobalSettings globalSettings)
        {
            // Collect all projects
            foreach (var searchPath in globalSettings.ProjectSearchPaths)
            {
                var actualPath = Path.Combine(globalSettings.DirectoryPath, searchPath);
                if (!Directory.Exists(actualPath))
                {
                    continue;
                }

                foreach (var dir in Directory.GetDirectories(actualPath))
                {
                    var id = ProjectId.CreateFromPath(dir, _cache);
                    if (id != null)
                    {
                        _projectIds[id.Name] = id;
                    }
                }
            }
        }

        public IEnumerable<ProjectId> GetProjects()
        {
            return _projectIds.Values;
        }

        public ProjectId GetProject(string projectName)
        {
            ProjectId id;
            if (_projectIds.TryGetValue(projectName, out id))
            {
                return id;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Create a workspace from a given path.
        /// 
        /// The give path could be a global.json file path or a directory contains a 
        /// global.json file. 
        /// </summary>
        /// <param name="path">The path to the workspace.</param>
        /// <returns>Returns workspace instance. If the global.json is missing returns null.</returns>
        public static Workspace Create(string path)
        {
            if (Directory.Exists(path))
            {
                return CreateFromGlobalJson(Path.Combine(path, GlobalSettings.FileName));
            }
            else
            {
                return CreateFromGlobalJson(path);
            }
        }

        /// <summary>
        /// Create a workspace from a global.json.
        /// 
        /// The given path must be a global.json, otherwise null is returned.
        /// </summary>
        /// <param name="filepath">The path to the global.json.</param>
        /// <returns>Returns workspace instance. If a global.json is missing returns null.</returns>
        public static Workspace CreateFromGlobalJson(string filepath)
        {
            if (File.Exists(filepath) &&
                string.Equals(Path.GetFileName(filepath), GlobalSettings.FileName, StringComparison.OrdinalIgnoreCase))
            {
                GlobalSettings globalSettings;

                if (GlobalSettings.TryGetGlobalSettings(filepath, out globalSettings))
                {
                    return new Workspace(globalSettings);
                }
            }

            return null;
        }

        public async Task<ProjectInformation> GetProjectInformationAsync(ProjectId id)
        {
            var project = await _cache.GetProjectAsync(id);
            if (project == null)
            {
                return null;
            }
            else
            {
                return new ProjectInformation(project);
            }
        }

        public async Task<IEnumerable<ProjectReference>> GetProjectReferencesAsync(
            ProjectId id,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(id, framework, configuration);
            if (dependencyInfo == null)
            {
                return Enumerable.Empty<ProjectReference>();
            }

            return dependencyInfo.ProjectReferences;
        }

        public async Task<IEnumerable<DependencyDescription>> GetDependenciesAsync(
            ProjectId id,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(id, framework, configuration);
            if (dependencyInfo == null)
            {
                return Enumerable.Empty<DependencyDescription>();
            }

            return dependencyInfo.Dependencies;
        }

        public async Task<IEnumerable<DiagnosticMessage>> GetDependencyDiagnosticsAsync(
            ProjectId id, 
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(id, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            return dependencyInfo.Diagnostics;
        }

        public async Task<IEnumerable<string>> GetFileReferencesAsync(
            ProjectId id,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(id, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            return dependencyInfo.FileReferences;
        }

        public Task<IDictionary<string, byte[]>> GetRawReferencesAsync(ProjectId project, NuGetFramework framework, string configuration)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<string>> GetSourcesAsync(ProjectId id, NuGetFramework framework, string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(id, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            var project = await _cache.GetProjectAsync(id);
            var sources = new List<string>(project.Files.SourceFiles);
            sources.AddRange(dependencyInfo.ExportedSourcesFiles);

            return sources;
        }
    }
}
