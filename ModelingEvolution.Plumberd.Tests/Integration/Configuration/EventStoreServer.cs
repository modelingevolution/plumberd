using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModelingEvolution.Plumberd.Tests.Integration.Configuration
{
    public class EventStoreServer
    {
        //private ClusterVNode _node;
        

        public static async Task<EventStoreServer> Start()
        {
            EventStoreServer s = new EventStoreServer();
            //await s.StartAsync();
            await s.StartInDocker();
            return s;
        }
        public Uri TcpUrl { get; set; }
        public Uri HttpUrl { get; set; }
        public async Task StartInDocker()
        {
            const string eventStoreHostName = "127.0.0.1";
            const int eventStorePubTcpPort = 1113;
            const int eventStorePubHttpPort = 2113;
            await CheckDns(eventStoreHostName);

            TcpUrl = new Uri($"tcp://{eventStoreHostName}:{eventStorePubTcpPort}");
            HttpUrl = new Uri($"http://{eventStoreHostName}:{eventStorePubHttpPort}");
            DockerClient client = new DockerClientConfiguration()
                .CreateClient();

            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters()
            {
                All = true,
                Limit = 10000
            });
            var container = containers.FirstOrDefault(x => x.Names.Any(n =>n.Contains("eventstore-mem")));
            if (container == null)
            {
                
                var exposedPorts = new Dictionary<string, object>
                {
                    { $"{eventStorePubHttpPort}", new { HostPort = "2113" } },
                    { $"{eventStorePubTcpPort}", new { HostPort = "1113" } }
                };
                var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters()
                {
                    Image = "eventstore/eventstore:latest",
                    Env = new List<string>()
                    {
                        "EVENTSTORE_RUN_PROJECTIONS=All",
                        "EVENTSTORE_START_STANDARD_PROJECTIONS=true",
                        "EVENTSTORE_INSECURE=true",
                        "EVENTSTORE_ENABLE_EXTERNAL_TCP=true",
                        "EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP=true",
                        "EVENTSTORE_MEM_DB=true",
                        //"EVENTSTORE_CERTIFICATE_PASSWORD=ca",
                        //"EVENTSTORE_CERTIFICATE_FILE=/cert/eventstore.p12",
                        //"EVENTSTORE_TRUSTED_ROOT_CERTIFICATES_PATH=/cert/ca-certificates/"
                    },
                    Name = "eventstore-mem",
                    HostConfig = new HostConfig()
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>>()
                        {
                            { $"{eventStorePubTcpPort}", new List<PortBinding>() { new PortBinding() { HostPort = $"{eventStorePubTcpPort}", HostIP = "0.0.0.0" } }},
                            { $"{eventStorePubHttpPort}", new List<PortBinding>() { new PortBinding() { HostPort = $"{eventStorePubHttpPort}", HostIP = "0.0.0.0" } }}
                        }
                        
                    },
                    Volumes = new Dictionary<string, EmptyStruct>()
                    {

                    },
                    ExposedPorts = new Dictionary<string, EmptyStruct>()
                    {
                        {$"{eventStorePubHttpPort}", default(EmptyStruct)},
                        {$"{eventStorePubTcpPort}", default(EmptyStruct)}
                    }
                });
                await client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());
            }
            else
            {
                var data = await client.Containers.InspectContainerAsync(container.ID);
                if (data.State.Running)
                {
                    await client.Containers.StopContainerAsync(data.ID, new ContainerStopParameters());
                }

                await client.Containers.StartContainerAsync(data.ID, new ContainerStartParameters());
            }
        }

        private static async Task CheckDns(string eventStoreHostName)
        {
            try
            {
                var result = await Dns.GetHostEntryAsync(eventStoreHostName);
                foreach (var i in result.AddressList)
                {
                    if (i.Equals(IPAddress.Loopback))
                        return;
                }
            }
            catch { }

            throw new Exception(
                    $"To run tests put {eventStoreHostName} to your etc/hosts and modellution's ca certificate to trusted certificate store.");
            
        }
    }
}