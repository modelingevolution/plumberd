using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Projections;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.UserManagement;
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Tests.Integration.Configuration;
using Newtonsoft.Json;


namespace ModelingEvolution.Plumberd.Cli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            bool interactive = args.Length == 0;

            do
            {
                if (interactive)
                    args = Console.ReadLine().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    if (args.Length == 1 && args[0] == "run-docker")
                    {
                        EventStoreServer s = new EventStoreServer();
                        await s.StartInDocker();
                    }
                    else if ((args.Length == 2) && args[0] == "read-raw")
                    {
                        string streamName = args[1];
                        var client = await Connect();
                        var slice = await client.Connection.ReadStreamEventsForwardAsync(streamName, 0, 1000, false,
                            client.UserCredentials);
                        foreach (var i in slice.Events)
                        {
                            Console.Out.WriteLine("EventType: " + i.Event.EventType);
                            Console.Out.WriteLine("EventNumber: " + i.Event.EventNumber);
                            Console.Out.WriteLine("Data: " + Encoding.UTF8.GetString(i.Event.Data));
                            Console.Out.WriteLine("Metadata: " + Encoding.UTF8.GetString(i.Event.Metadata));
                        }
                    }
                    else if ((args.Length == 1 || args.Length == 2) && args[0] == "read-metadata")
                    {
                        var client = await Connect();

                        var stream = args.Length == 2 ? args[2] : "$settings";

                        var result = await client.Connection.GetStreamMetadataAsync(stream, client.UserCredentials);

                        Console.WriteLine(result.StreamMetadata.AsJsonString());
                    }
                    else if ((args.Length == 1) && args[0] == "create-user")
                    {
                        var client = await Connect();
                        await client.UsersManager.CreateUserAsync("test", "fullname", new[] {"testing"}, "pwd",
                            client.UserCredentials);
                    }
                    else if ((args.Length == 1) && args[0] == "set-stream-acl")
                    {
                        var client = await Connect();
                        string stream = "Test-14b9bd25-5480-1e20-3ec2-c2f24bbe163c";
                        var oldMetadata =
                            await client.Connection.GetStreamMetadataAsync(stream, client.UserCredentials);

                        var metadata = StreamMetadata.Build()
                            .SetReadRole("$admins")
                            .SetWriteRole("$admins").Build();
                        await client.Connection.SetStreamMetadataAsync(stream, oldMetadata.MetastreamVersion, metadata);
                    }
                    else if ((args.Length == 1 || args.Length == 2) && args[0] == "read")
                    {
                        var client = await Connect();
                        string stream = args.Length == 1 ? "Test-14b9bd25-5480-1e20-3ec2-c2f24bbe163c" : args[1];
                        var slice = await client.Connection.ReadStreamEventsForwardAsync(stream, StreamPosition.Start,
                            1000, true, client.User1);
                        foreach (var i in slice.Events)
                            Console.Out.WriteLine(Encoding.UTF8.GetString(i.Event.Data));
                    }
                    else if ((args.Length == 1) && args[0] == "write")
                    {
                        var client = await Connect();
                        string stream = "Test-14b9bd25-5480-1e20-3ec2-c2f24bbe163c";
                        await client.Connection.AppendToStreamAsync(stream, ExpectedVersion.Any, client.User1,
                            new EventData(Guid.NewGuid(),
                                "tested", true,
                                Encoding.UTF8.GetBytes("{ \"Name\":\"foo\" }"),
                                Encoding.UTF8.GetBytes("{ \"Meta\":\"Metafoo\"}")));
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }
            } while (interactive);
        }

        private static async Task<Clients> Connect()
        {
            var credentials = new UserCredentials("admin", "changeit");
            var httpUri = new Uri("https://eventstore:5113");
            var tcpUrl = new Uri("tcp://127.0.0.1:6113");

            var tcpSettings = ConnectionSettings.Create()
                .DisableServerCertificateValidation()
                .UseDebugLogger()
                .EnableVerboseLogging()
                .KeepReconnecting()
                .KeepRetrying()
                .LimitReconnectionsTo(1000)
                .LimitRetriesForOperationTo(100)
                .WithConnectionTimeoutOf(TimeSpan.FromSeconds(5))
                .SetDefaultUserCredentials(credentials);

            //tcpSettings = tcpSettings.DisableTls();

            tcpSettings = tcpSettings.DisableServerCertificateValidation();


            ProjectionsManager projectionsManager = new ProjectionsManager(new ConsoleLogger(), new DnsEndPoint(httpUri.Host, httpUri.Port),
                TimeSpan.FromSeconds(10));
            UsersManager um = new UsersManager(new ConsoleLogger(), new DnsEndPoint(httpUri.Host, httpUri.Port),
                 TimeSpan.FromSeconds(10));
            IEventStoreConnection connection = EventStoreConnection.Create(tcpSettings.Build(), tcpUrl);
            await connection.ConnectAsync();
            return new Clients(connection, um, projectionsManager, credentials);
        }
    }

    class Clients
    {
        public IEventStoreConnection Connection {get;}
        public UsersManager UsersManager { get; }
        public ProjectionsManager ProjectionsManager { get; }
        public UserCredentials UserCredentials { get; }
        public UserCredentials User1 { get; }
        public Clients(IEventStoreConnection connection, UsersManager usersManager, ProjectionsManager projectionsManager, UserCredentials userCredentials)
        {
            Connection = connection;
            UsersManager = usersManager;
            ProjectionsManager = projectionsManager;
            UserCredentials = userCredentials;
            User1 = new UserCredentials("test", "pwd");
        }
    }
}
