using System.Collections.Generic;

namespace Microsoft.Extensions.ProjectModel.Workspaces
{
    internal class DependencyInfo
    {
        public DependencyInfo(IList<DiagnosticMessage> diagnostics,
                              IList<DependencyDescription> dependencies,
                              IList<ProjectReference> projectReferences,
                              IList<string> runtimeAssemblyReferences,
                              IList<string> exportedSourceFiles)
        {
            Diagnostics = diagnostics;
            Dependencies = dependencies;
            FileReferences = runtimeAssemblyReferences;
            ProjectReferences = projectReferences;
            ExportedSourcesFiles = exportedSourceFiles;
        }

        public IList<DiagnosticMessage> Diagnostics { get; }

        public IList<DependencyDescription> Dependencies { get; }

        public IList<string> FileReferences { get; }

        public IList<ProjectReference> ProjectReferences { get; }

        public IList<string> ExportedSourcesFiles { get; }
        //public Dictionary<string, byte[]> RawReferences { get; set; } = new Dictionary<string, byte[]>();
    }
}
