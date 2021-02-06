using System;

namespace ModelingEvolution.Plumberd.EventStore
{
    public interface IProjectionConfig
    {
        ProjectionSchema Schema(IEventStoreSettings settings);
    }
    public abstract class ProjectionConfig : IProjectionConfig
    {
        private readonly Lazy<ProjectionSchema> _schema;
        private IEventStoreSettings _settings;
        public ProjectionSchema Schema(IEventStoreSettings settings)
        {
            _settings = settings;
            return _schema.Value;
        }
        protected abstract void Configure(ProjectionSchemaBuilder builder);
        

        protected ProjectionConfig()
        {
            _schema = new Lazy<ProjectionSchema>(OnBuildSchema);
        }

        private ProjectionSchema OnBuildSchema()
        {
            ProjectionSchemaBuilder b = new ProjectionSchemaBuilder(_settings);
            Configure(b);
            return b.Build();
        }
    }
}