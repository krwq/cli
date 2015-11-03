using System;
using System.IO;

namespace Microsoft.Extensions.ProjectModel.Workspaces
{
    public class ProjectId
    {
        private readonly WorkspaceCache _cache;

        private ProjectId(string path, WorkspaceCache cache)
        {
            FullPath = path;
            Name = Path.GetFileName(path);

            _cache = cache;
        }

        public string Name { get; }

        public string FullPath { get; }

        public override bool Equals(object obj)
        {
            var id = obj as ProjectId;
            return id != null &&
                   string.Equals(id.FullPath, this.FullPath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return FullPath.GetHashCode();
        }

        internal static ProjectId CreateFromPath(string projectDirectory, WorkspaceCache cache)
        {
            if (File.Exists(Path.Combine(projectDirectory, Project.FileName)))
            {
                return new ProjectId(Path.GetFullPath(projectDirectory), cache);
            }

            return null;
        }
    }
}