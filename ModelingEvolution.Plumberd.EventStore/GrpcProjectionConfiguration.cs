using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client;
using Microsoft.Extensions.Logging;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class GrpcProjectionConfigurations
    {
        private readonly EventStoreProjectionManagementClient _projectionManager;
        private readonly UserCredentials _userCredentials;
        private readonly IEventStoreSettings _settings;
        private readonly List<IProjectionConfig> _configs;
        private static ILogger Log = Modellution.Logging.LogFactory.GetLogger<ProjectionConfigurations>();

        public GrpcProjectionConfigurations(EventStoreProjectionManagementClient projectionManager,
            UserCredentials userCredentials,
            IEventStoreSettings settings)
        {
            _projectionManager = projectionManager;
            _userCredentials = userCredentials;
            _settings = settings;
            _configs = new List<IProjectionConfig>();

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
            var projections = await _projectionManager.ListContinuousAsync(_userCredentials).ToListAsync();
            foreach (var schema in schemas)
            {
                await UpdateProjectionSchemaInner(schema, projections);
            }
        }

        public async Task UpdateProjectionSchema(ProjectionSchema schema)
        {
            var projections = await _projectionManager.ListContinuousAsync(_userCredentials).ToListAsync();
            await UpdateProjectionSchemaInner(schema, projections);
        }

        private async Task UpdateProjectionSchemaInner(ProjectionSchema schema, List<ProjectionDetails> projections)
        {
            var projectionName = schema.ProjectionName;
            // we make projection only when we need to.
            if (!projections.Exists(x => x.Name == projectionName))
            {
                var set = new EventStoreClientSettings();
                
                var n = new EventStoreProjectionManagementClient(set);
                var query = schema.Script;
                await _projectionManager.CreateContinuousAsync(projectionName, query, false, _userCredentials);
            }
            else
            {
                var query = schema.Script;
            }
        }
    }
    }
