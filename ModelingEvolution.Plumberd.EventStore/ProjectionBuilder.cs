﻿using System;
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
        private string _propertyName;
        private string _category;
        private IEventStoreSettings _settings;

        public ProjectionSchemaBuilder FromEventTypes(IEnumerable<string> types)
        {
            FromStreams(types.Select(type => $"$et-{type}"));
            _whenStatement = WhenFromEvents;
            return this;
        }
        
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
        public ProjectionSchemaBuilder PartitionByMetadataDate(string outputCategory, string? sufix = null)
        {
            if (string.IsNullOrWhiteSpace(outputCategory))
                throw new ArgumentNullException(nameof(outputCategory));

            _category = ComputeOutputCategory(outputCategory);
            _projectionName = $"{outputCategory}ByDay{sufix}";

            string ComputeWhen()
            {
                StringBuilder query = new StringBuilder();

                query.AppendLine(".when( { \r\n    $any : function(s,e) { ");
                query.AppendLine("const m = JSON.parse(e.metadataRaw);");
                query.AppendLine("const dateSufix = m.Created.split('T')[0].replace(/-/g, '');");
                query.Append($"const streamName = '{_category}-' + dateSufix");
                if (sufix != null) query.Append($" + {sufix}");
                query.AppendLine(";");
                query.AppendLine($"linkTo(streamName, e); }}");
                query.Append("});");
                return query.ToString();
            }

            _whenStatement = ComputeWhen;
            _streamNameIsDynamic = true;
            return this;
        }
        public ProjectionSchemaBuilder PartitionByStreamId(string outputCategory)
        {
            if (string.IsNullOrWhiteSpace(outputCategory))
                throw new ArgumentNullException(nameof(outputCategory));

            _category = ComputeOutputCategory(outputCategory);
            _projectionName ??= _projectionName ?? $"{outputCategory}By{_propertyName}";
            _whenStatement = WhenPartitionByStreamId;
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
            query.AppendLine($"const streamName = '{_category}-'+e.body.{_propertyName};");
            query.AppendLine($"linkTo(streamName, e); }}");
            query.Append("});");

            _streamNameIsDynamic = true;
            
            return query.ToString();
        }
        private string WhenPartitionByStreamId()
        {
            StringBuilder query = new StringBuilder();

            query.AppendLine(".when( { \r\n    $any : function(s,e) { ");
            query.AppendLine("const m = JSON.parse(e.metadataRaw);");
            query.AppendLine($"const streamName = '{_category}-'+m.{_propertyName};");
            query.AppendLine($"linkTo(streamName, e); }}");
            query.Append("});");

            _streamNameIsDynamic = true;

            return query.ToString();
        }
        private string WhenPartitionByMetadata()
        {
            StringBuilder query = new StringBuilder();

            query.AppendLine(".when( { \r\n    $any : function(s,e) { ");
            query.AppendLine("const m = JSON.parse(e.metadataRaw);");
            query.AppendLine($"const streamName = '{_category}-'+m.{_propertyName};");
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
        public ProjectionSchemaBuilder FromCategories(params string[] streams)
        {
            return FromStreams(streams.Select(x => $"$ce-{x}"));
        }
        public ProjectionSchemaBuilder FromStreams(params string[] streams)
        {
            return FromStreams(streams.AsEnumerable());
        }
        public ProjectionSchemaBuilder FromStreams(IEnumerable<string> streams)
        {
            StringBuilder query = new StringBuilder();

            query.Append("fromStreams([");
            query.Append(string.Join(',', streams.Select(x => $"'{x}'")));
            query.Append("])");
            _fromStreams = query.ToString();
            return this;
        }
        public ProjectionSchemaBuilder ForHandler(Type handerType)
        {
            _projectionName = handerType.Name;
            return this;
        }
        public ProjectionSchemaBuilder ForView(string viewName)
        {
            _projectionName = viewName;
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