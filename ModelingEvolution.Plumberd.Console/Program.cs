using System;
using System.Threading.Tasks;
using ModelingEvolution.Plumberd.Tests;

namespace ModelingEvolution.Plumberd.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //UnitTest1 c = new UnitTest1();
            //await c.Check();
            EventStoreServer s = new EventStoreServer();
            await s.StartInDocker();
        }
    }
}
