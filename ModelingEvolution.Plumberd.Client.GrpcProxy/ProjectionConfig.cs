using System;
using System.Collections.Generic;
using System.Text;

namespace ModelingEvolution.Plumberd.EventStore
{
    public class StreamNameBuilder
    {
        private readonly IDictionary<string, Func<string>> _translations;

        public StreamNameBuilder(IDictionary<string, Func<string>> translations)
        {
            _translations = translations;
        }
        public string FromCategoryInProjection(string category)
        {
            string tmp = category.StartsWith('/') ? category : $"/{category}";
            foreach (var i in _translations)
            {
                var sub = $"{{{i.Key}}}";
                if (tmp.Contains(sub))
                    tmp = tmp.Replace(sub, i.Value(), StringComparison.OrdinalIgnoreCase);
            }

            return tmp;
        }

        
    }
}