using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel.Workspaces
{
    public class ProjectReference
    {
        public NuGetFramework Framework { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string WrappedProjectPath { get; set; }

        // TODO: Add ProjectId here
    }
}