using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client;

using Microsoft.Extensions.Logging;


namespace ModelingEvolution.Plumberd.EventStore
{
    [Flags]
    public enum StartupProjection
    {
        None = 0,
        Streams = 0x1,
        ByCategory = 0x1 << 1,
        ByEventType = 0x1 << 2,
        ByCorrelationId = 0x1 << 3,
        StreamByCategory = 0x1 << 4,
        All = Streams | ByCategory | ByEventType | ByCorrelationId | StreamByCategory,
        Required = ByEventType,
        Optimal = ByEventType | Streams,
    }
    public class ProjectionConfigurations
    {
        private readonly EventStoreProjectionManagementClient _projectionManager;
        private readonly UserCredentials _userCredentials;
        private readonly IEventStoreSettings _settings;
        private readonly List<IProjectionConfig> _configs;
        private readonly ILogger _log;
        public StartupProjection StartupProjection { get; init; }
        public ProjectionConfigurations(EventStoreProjectionManagementClient projectionManager,
            UserCredentials userCredentials,
            IEventStoreSettings settings, StartupProjection startupProjection)
        {
            _projectionManager = projectionManager;
            _userCredentials = userCredentials;
            _settings = settings;
            _configs = new List<IProjectionConfig>();
            _log = settings.LoggerFactory.CreateLogger<ProjectionConfigurations>();
            StartupProjection = startupProjection;
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
                await UpdateProjectionSchemaInner(schema, projections.TryGetValue(schema.ProjectionName, out var p) ? p : null);
            }
            if(StartupProjection == StartupProjection.None) return;

            await ConfigureProjectionState(StartupProjection.ByCategory, "$by_category");
            await ConfigureProjectionState(StartupProjection.ByCorrelationId, "$by_correlation_id");
            await ConfigureProjectionState(StartupProjection.ByEventType, "$by_event_type");
            await ConfigureProjectionState(StartupProjection.StreamByCategory, "$stream_by_category");
            await ConfigureProjectionState(StartupProjection.Streams, "$streams");

        }

        private async Task ConfigureProjectionState(StartupProjection startupProjection, string name)
        {
            if ((StartupProjection & startupProjection) == startupProjection)
                await EnableProjection(name);
            else await DisableProjection(name);
        }
        private async Task EnableProjection(string name)
        {
            var projections = await Projections();
            if (projections[name].Status == "Running") return;
            await _projectionManager.EnableAsync(name);
            _log.LogInformation("Projection {ProjectionName} was enabled", name);
        }
        private async Task DisableProjection(string name)
        {
            var projections = await Projections();
            if (projections[name].Status != "Running") return;
            await _projectionManager.DisableAsync(name);
            _log.LogInformation("Projection {ProjectionName} was disabled", name);
        }

        private Dictionary<string,ProjectionDetails>? _projections;
        private async Task<Dictionary<string,ProjectionDetails>> Projections()
        {
            if (_projections == null || _projections.Count == 0)
                _projections = await _projectionManager.ListAllAsync().ToDictionaryAsync(x => x.Name);
            return _projections;

        }
        public async Task UpdateProjectionSchema(ProjectionSchema schema)
        {
            await UpdateProjectionSchemaInner(schema, (await Projections()).TryGetValue(schema.ProjectionName, out var p) ? p : null);
        }

        private async Task UpdateProjectionSchemaInner(ProjectionSchema schema, ProjectionDetails projections)
        {
            var projectionName = schema.ProjectionName;
            // we make projection only when we need to.
            if (projections == null)
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