using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModelingEvolution.Plumberd.Binding;
using ModelingEvolution.Plumberd.Metadata;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class ProjectionSchemaBuilder
    {
        private string _fromStreams;
        private string _streamName;
        private string _projectionName;
        private bool _streamNameIsDynamic;
        private Func<string> _whenStatement;
        private IEventStoreSettings _settings;
        public ProjectionSchemaBuilder FromEventTypes(IEnumerable<string> types)
        {
            StringBuilder query = new StringBuilder();

            query.Append("fromStreams([");
            query.Append(string.Join(',', types.Select(i => $"'$et-{i}'")));
            query.Append("])");
            _fromStreams = query.ToString();
            _whenStatement = WhenFromEvents;
            return this;
        }

        private string _propertyName;
        private string _category;

        public ProjectionSchemaBuilder(IEventStoreSettings settings = null)
        {
            _settings = settings;
        }


        /// <summary>
        /// This will create a new stream named: outputCategory-{metadata.propertyName}
        /// with links from events taken from 'FromStream(s)'
        /// </summary>
        /// <param name="propertyName">Name of property in metadata</param>
        /// <param name="outputCategory">Output stream that will be created</param>
        /// <returns></returns>
        public ProjectionSchemaBuilder PartitionByMetadata(string propertyName, string outputCategory)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentNullException(nameof(propertyName));

            if (string.IsNullOrWhiteSpace(outputCategory))
                throw new ArgumentNullException(nameof(outputCategory));

            _propertyName = propertyName;
            _category = ComputeOutputCategory(outputCategory);
            _projectionName = $"{outputCategory}By{_propertyName}";
            _whenStatement = WhenPartitionByMetadata;
            _streamNameIsDynamic = true;
            return this;
        }

        private string ComputeOutputCategory(string outputCategory)
        {
            if (_settings?.ProjectionStreamPrefix != null && !outputCategory.StartsWith(_settings.ProjectionStreamPrefix))
                return _settings.ProjectionStreamPrefix + outputCategory;
            else
                return outputCategory;
        }

        public ProjectionSchemaBuilder PartitionByEventData(string propertyName, string outputCategory)
        {
            if(string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentNullException(nameof(propertyName));

            if (string.IsNullOrWhiteSpace(outputCategory))
                throw new ArgumentNullException(nameof(outputCategory));

            _propertyName = propertyName;
            _category = ComputeOutputCategory(outputCategory);
            _projectionName = $"{outputCategory}By{_propertyName}";
            _whenStatement = WhenPartitionByEventData;
            return this;
        }
        private string WhenPartitionByEventData()
        {
            StringBuilder query = new StringBuilder();

            query.AppendLine(".when( { \r\n    $any : function(s,e) { ");
            query.AppendLine($"var streamName = '{_category}-'+e.body.{_propertyName};");
            query.AppendLine($"linkTo(streamName, e); }}");
            query.Append("});");

            _streamNameIsDynamic = true;
            
            return query.ToString();
        }
        private string WhenPartitionByMetadata()
        {
            StringBuilder query = new StringBuilder();

            query.AppendLine(".when( { \r\n    $any : function(s,e) { ");
            query.AppendLine("var m = JSON.parse(e.metadataRaw);");
            query.AppendLine($"var streamName = '{_category}-'+m.{_propertyName};");
            query.AppendLine($"linkTo(streamName, e); }}");
            query.Append("});");

            _streamNameIsDynamic = true;

            return query.ToString();
        }
        private string WhenFromEvents()
        {
            StringBuilder query = new StringBuilder();
            
            query.Append(".when( { \r\n    $any : function(s,e) { linkTo('");
            query.Append(_streamName);
            query.Append("', e) }\r\n});");

            return query.ToString();
        }

        public ProjectionSchemaBuilder FromStreamsByEventTypeIn<TController>()
        {
            EventHandlerBinder b = new EventHandlerBinder(typeof(TController));
            b.Discover(true);
            var eventTypes = b.Types().SelectMany(x => _settings.RecordNamingConvention(x));
            return FromEventTypes(eventTypes);
        }
        public ProjectionSchemaBuilder FromStreams(params string[] streams)
        {
            StringBuilder query = new StringBuilder();

            query.Append("fromStreams([");
            query.Append(string.Join(',', streams.Select(x=>$"'{x}'")));
            query.Append("])");
            _fromStreams = query.ToString();
            return this;
        }
        public ProjectionSchemaBuilder ForHandler(Type handerType)
        {
            _projectionName = handerType.Name;
            return this;
        }
        public ProjectionSchema Build()
        {
            string script = Script();

            if (string.IsNullOrWhiteSpace(_streamName) && !_streamNameIsDynamic)
                throw new ArgumentException("Stream name not set. Have you forgotten to set 'EmittingStream'?"); 
            
            if (!string.IsNullOrWhiteSpace(script))
            {
                if (string.IsNullOrWhiteSpace(_projectionName))
                    throw new ArgumentException("Projection name is not set. Have you forgotten to set 'FromHandler'?");
            }

            return new ProjectionSchema()
            {
                Script = script,
                ProjectionName = _projectionName,
                StreamName = _streamName
            };
        }
        
        
        public string Script()
        {
            if (_whenStatement == null) return null;

            StringBuilder query = new StringBuilder(_fromStreams);
            query.Append(_whenStatement());

            return query.ToString();
        }
        public ProjectionSchemaBuilder EmittingLinksToStream(string dstStream)
        {
            if(string.IsNullOrWhiteSpace(dstStream))
                throw new ArgumentNullException(nameof(dstStream));

            this._streamName = dstStream;
            return this;
        }

        public ProjectionSchemaBuilder FromEventTypes(params string[] eventTypes)
        {
            return FromEventTypes(eventTypes.AsEnumerable());
        }


    }
}