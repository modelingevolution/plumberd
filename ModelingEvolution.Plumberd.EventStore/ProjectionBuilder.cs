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
        private WhenStatement _whenStatement;
        public ProjectionSchemaBuilder FromEventTypes(IEnumerable<string> types)
        {
            StringBuilder query = new StringBuilder();

            query.Append("fromStreams([");
            query.Append(string.Join(',', types.Select(i => $"'$et-{i}'")));
            query.Append("])");
            _fromStreams = query.ToString();
            _whenStatement = FromEventsWhen;
            return this;
        }

        private static string FromEventsWhen(string streamName)
        {
            StringBuilder query = new StringBuilder();
            
            query.Append(".when( { \r\n    $any : function(s,e) { linkTo('");
            query.Append(streamName);
            query.Append("', e) }\r\n});");

            return query.ToString();
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
            string script = Script();

            if (string.IsNullOrWhiteSpace(_streamName))
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
        
        public ProjectionSchemaBuilder When(WhenStatement whenStatement)
        {
            _whenStatement = whenStatement;
            return this;
        }
        public string Script()
        {
            if (_whenStatement == null) return null;

            StringBuilder query = new StringBuilder(_fromStreams);
            query.Append(_whenStatement(_streamName));

            return query.ToString();
        }
        public ProjectionSchemaBuilder EmittingStream(string streamName)
        {
            this._streamName = streamName;
            return this;
        }

        public ProjectionSchemaBuilder FromEventTypes(params string[] eventTypes)
        {
            return FromEventTypes(eventTypes.AsEnumerable());
        }

        public delegate string WhenStatement(string outputSteamName);
    }
}