using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Projections;
using EventStore.ClientAPI.SystemData;
using Microsoft.Extensions.Logging;


namespace ModelingEvolution.Plumberd.EventStore
{
    public class ProjectionConfigurations
    {
        private readonly ProjectionsManager _projectionManager;
        private readonly UserCredentials _userCredentials;
        private readonly IEventStoreSettings _settings;
        private readonly List<IProjectionConfig> _configs;
        private readonly ILogger _log;
        public ProjectionConfigurations(ProjectionsManager projectionManager, 
            UserCredentials userCredentials, 
            IEventStoreSettings settings)
        {
            _projectionManager = projectionManager;
            _userCredentials = userCredentials;
            _settings = settings;
            _configs = new List<IProjectionConfig>();
            _log = settings.LoggerFactory.CreateLogger<ProjectionConfigurations>();
        }
        public void Register(IProjectionConfig c)
        {
            _configs.Add(c);
        }
        public void Register(IEnumerable<IProjectionConfig> configs)
        {
            _configs.AddRange(configs);
        }

        public async Task UpdateIfRequired()
        {
            var schemas = _configs.Select(x => x.Schema(_settings)).ToArray();
            var projections = await _projectionManager.ListContinuousAsync(_userCredentials);
            foreach (var schema in schemas)
            {
                await UpdateProjectionSchemaInner(schema, projections);
            }
        }

        public async Task UpdateProjectionSchema(ProjectionSchema schema)
        {
            var projections = await _projectionManager.ListContinuousAsync(_userCredentials);
            await UpdateProjectionSchemaInner(schema, projections);
        }

        private async Task UpdateProjectionSchemaInner(ProjectionSchema schema, List<ProjectionDetails> projections)
        {
            var projectionName = schema.ProjectionName;
            // we make projection only when we need to.
            if (!projections.Exists(x => x.Name == projectionName))
            {
                var query = schema.Script;
                await _projectionManager.CreateContinuousAsync(projectionName, query, false, _userCredentials);
            }
            else
            {
                var query = schema.Script;
                var config = await _projectionManager.GetConfigAsync(projectionName, _userCredentials);

                var currentQuery = await _projectionManager.GetQueryAsync(projectionName, _userCredentials);

                if (query != currentQuery || !config.EmitEnabled)
                {
                    _log.LogInformation("Updating continues projection definition and config: {projectionName}",
                        projectionName);
                    await _projectionManager.UpdateQueryAsync(projectionName, query, true, _userCredentials);
                }
            }
        }
    }
}