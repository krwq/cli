using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Workspaces;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Workspaces.Tests
{
    public class WorkspaceTests
    {
        private readonly string _testProjectRoot = "misc";
        private readonly string _testProjectPath;

        public WorkspaceTests()
        {
            _testProjectPath = Path.Combine(
                ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory()),
                _testProjectRoot,
                "WorkspaceTests_Case001");
        }

        [Fact]
        public async Task CreateFromPath()
        {
            var current = Directory.GetCurrentDirectory();

            var workspace = Workspace.Create(_testProjectPath);
            Assert.NotNull(workspace);

            var projects = workspace.GetProjects();
            Assert.NotNull(projects);
            Assert.NotEmpty(projects);

            var id = workspace.GetProject("ClassLibrary1");
            Assert.NotNull(id);
            Assert.Equal("ClassLibrary1", id.Name);

            var info = await workspace.GetProjectInformationAsync(id);
            Assert.NotNull(info);
            Assert.Equal("ClassLibrary1", info.Name);
            Assert.Equal(2, info.Frameworks.Count());
            Assert.Equal(2, info.Configurations.Count());

            var framework = NuGetFramework.Parse("dnxcore50");
            var configuration = info.Configurations.First();
            var dependencies = await workspace.GetDependenciesAsync(id, framework, configuration);
            Assert.NotEmpty(dependencies);

            var diagnostics = await workspace.GetDependencyDiagnosticsAsync(id, framework, configuration);
            Assert.NotNull(diagnostics);

            var fileReferences = await workspace.GetFileReferencesAsync(id, framework, configuration);
            Assert.NotEmpty(fileReferences);

            var sources = await workspace.GetSourcesAsync(id, framework, configuration);
            Assert.NotEmpty(sources);

            var projectReferences = await workspace.GetProjectReferencesAsync(id, framework, configuration);
            Assert.NotEmpty(projectReferences);
        }
    }
}
