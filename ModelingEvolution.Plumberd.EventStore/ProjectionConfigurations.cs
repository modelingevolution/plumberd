using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client;

using Microsoft.Extensions.Logging;


namespace ModelingEvolution.Plumberd.EventStore
{
    public class ProjectionConfigurations
    {
        private readonly EventStoreProjectionManagementClient _projectionManager;
        private readonly UserCredentials _userCredentials;
        private readonly IEventStoreSettings _settings;
        private readonly List<IProjectionConfig> _configs;
        private readonly ILogger _log;
        public ProjectionConfigurations(EventStoreProjectionManagementClient projectionManager, 
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
            var projections = await Projections();
            foreach (var schema in schemas)
            {
                await UpdateProjectionSchemaInner(schema, projections);
            }
        }

        private List<ProjectionDetails>? _projections;
        private async Task<List<ProjectionDetails>> Projections()
        {
            if (_projections == null || _projections.Count == 0) 
                _projections = await _projectionManager.ListAllAsync().ToListAsync();
            return _projections;

        }
        public async Task UpdateProjectionSchema(ProjectionSchema schema)
        {
            await UpdateProjectionSchemaInner(schema, await Projections());
        }

        private async Task UpdateProjectionSchemaInner(ProjectionSchema schema, List<ProjectionDetails> projections)
        {
            var projectionName = schema.ProjectionName;
            // we make projection only when we need to.
            if (!projections.Exists(x => x.Name == projectionName))
            {
                var query = schema.Script;
                await _projectionManager.CreateContinuousAsync(projectionName, query, true);
            }
            else
            {
                // We cannot check it yet.
            }
        }
    }
}