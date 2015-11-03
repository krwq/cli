using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ProjectModel.Caching;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel.Workspaces
{
    internal class WorkspaceCache
    {
        public WorkspaceCache()
        {
            CacheContextAccessor = new CacheContextAccessor();
            Cache = new Cache(CacheContextAccessor);
            NamedCacheDependencyProvider = new NamedCacheDependencyProvider();
        }

        public ICacheContextAccessor CacheContextAccessor { get; }

        public ICache Cache { get; }

        public INamedCacheDependencyProvider NamedCacheDependencyProvider { get; }

        public Task<Project> GetProjectAsync(ProjectId id)
        {
            return Task.Run(() =>
            {
                return (Project)Cache.Get(Tuple.Create(typeof(Project), id), ctx =>
                {
                    //_logger.LogVerbose($"Reading project {projectHandle} => {projectPath}");

                    TriggerDependency<Project>(id);
                    MonitorDependency(ctx, new FileWriteTimeCacheDependency(Path.Combine(id.FullPath, Project.FileName)));

                    Project project;
                    if (!ProjectReader.TryGetProject(id.FullPath, out project))
                    {
                        return null;
                    }
                    else
                    {
                        return project;
                    }
                });
            });
        }

        public async Task<ProjectContext> GetProjectContext(ProjectId id, NuGetFramework framework)
        {
            var project = await GetProjectAsync(id);
            if (project == null)
            {
                return null;
            }

            var projectContext = await Task.Run(() =>
            {
                return Cache.Get(Tuple.Create(typeof(ProjectContext), id, framework), ctx =>
                {
                    //_logger.LogVerbose($"Reading project context {projectHandle} => {project.ProjectDirectory}");
                    TriggerDependency<ProjectContext>(id, framework);
                    MonitorDependency<Project>(ctx, id);

                    var builder = new ProjectContextBuilder()
                        .WithProject(project)
                        .WithTargetFramework(framework);

                    return builder.Build();
                }) as ProjectContext;
            });

            return projectContext;
        }

        public async Task<DependencyInfo> GetDependencyInfo(ProjectId id, NuGetFramework framework, string configuration)
        {
            var projectContext = await GetProjectContext(id, framework);
            if (projectContext == null)
            {
                return null;
            }

            var cacheKey = Tuple.Create(typeof(DependencyInfo), id, framework, configuration);
            return Cache.Get(cacheKey, ctx =>
            {
                //_logger.LogVerbose($"Resolving dependencies for project {projectHandle}/{framework}");

                TriggerDependency<DependencyInfo>(id, framework, configuration);
                MonitorDependency<ProjectContext>(ctx, id, framework);

                var resolver = new DependencyInfoResolver(projectContext, configuration);
                return resolver.Resolve();
            }) as DependencyInfo;
        }

        private void TriggerDependency<T>(ProjectId id)
        {
            NamedCacheDependencyProvider.Trigger(GetCacheDependencyName<T>(id));
        }

        private void TriggerDependency<T>(ProjectId id, NuGetFramework framework)
        {
            NamedCacheDependencyProvider.Trigger(GetCacheDependencyName<T>(id, framework));
        }

        private void TriggerDependency<T>(ProjectId id, NuGetFramework framework, string configuration)
        {
            NamedCacheDependencyProvider.Trigger(GetCacheDependencyName<T>(id, framework, configuration));
        }

        private void MonitorDependency(CacheContext ctx, ICacheDependency dependency)
        {
            ctx.Monitor(dependency);
        }

        private void MonitorDependency<T>(CacheContext ctx, ProjectId id)
        {
            MonitorDependency(ctx,
                NamedCacheDependencyProvider.GetNamedDependency(GetCacheDependencyName<T>(id)));
        }

        private void MonitorDependency<T>(CacheContext ctx, ProjectId id, NuGetFramework framework)
        {
            MonitorDependency(ctx,
                NamedCacheDependencyProvider.GetNamedDependency(GetCacheDependencyName<T>(id, framework)));
        }

        private string GetCacheDependencyName<T>(ProjectId id)
        {
            return $"DependencyName_of_{nameof(ProjectId)}_{id.FullPath}_for_{typeof(T).Name}";
        }

        private string GetCacheDependencyName<T>(ProjectId id, NuGetFramework framework)
        {
            return $"DependencyName_of_{nameof(ProjectId)}_{id.FullPath}_for_{typeof(T).Name}_{framework.GetShortFolderName()}";
        }

        private string GetCacheDependencyName<T>(ProjectId id, NuGetFramework framework, string configuration)
        {
            return $"DependencyName_of_{nameof(ProjectId)}_{id.FullPath}_for_{typeof(T).Name}_{framework.GetShortFolderName()}_{configuration}";
        }
    }
}
