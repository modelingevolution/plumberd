using System;

namespace ModelingEvolution.Plumberd.RelationDataModeling
{
    public class Record : IRecord
    {
        public Guid Id { get; set; }

        public Record()
        {
            Id = Guid.NewGuid();
        }
    }
}
