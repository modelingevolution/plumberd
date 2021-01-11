using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class ProjectionSchemaBuilder
    {
        private string _fromStreams;
        private string _streamName;
        private string _projectionName;
        public ProjectionSchemaBuilder FromEventTypes(IEnumerable<string> types)
        {
            StringBuilder query = new StringBuilder();

            query.Append("fromStreams([");
            query.Append(string.Join(',', types.Select(i => $"'$et-{i}'")));
            query.Append("])");
            _fromStreams = query.ToString();
            return this;
        }
        public ProjectionSchemaBuilder FromStreams(params string[] streams)
        {
            StringBuilder query = new StringBuilder();

            query.Append("fromStreams([");
            query.Append(string.Join(',', streams));
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
            if(string.IsNullOrWhiteSpace( _fromStreams))
                throw new ArgumentException("From streams is not set. Have you forgotten to set 'FromEventTypes' or 'FromStreams'?");

            if (string.IsNullOrWhiteSpace(_projectionName))
                throw new ArgumentException("Projection name is not set. Have you forgotten to set 'FromHandler'?");

            if (string.IsNullOrWhiteSpace(_streamName))
                throw new ArgumentException("Stream name not set. Have you forgotten to set 'EmittingStream'?");

            return new ProjectionSchema()
            {
                Script = Script(),
                ProjectionName = _projectionName,
                StreamName = _streamName
            };
        }
        public string Script()
        {
            StringBuilder query = new StringBuilder(_fromStreams);

            query.AppendLine();
            query.Append(".when( { \r\n    $any : function(s,e) { linkTo('");
            query.Append(_streamName);
            query.Append("', e) }\r\n});");

            return query.ToString();
        }
        public ProjectionSchemaBuilder EmittingStream(string streamName)
        {
            this._streamName = streamName;
            return this;
        }
    }
}