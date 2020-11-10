using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.Tests.TestingModel
{
    [StreamName("Foo")]
    public class FooCommand : Command
    {
        public string Name { get; set; }

        public FooCommand()
        {
            Name = "Hello world!";
        }
    }

}