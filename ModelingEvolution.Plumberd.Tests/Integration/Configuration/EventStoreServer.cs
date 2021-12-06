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
        public async Task StartInDocker()
        {
            //Uri uri = new Uri("npipe://./pipe/docker_engine");

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
                    { "2113", new { HostPort = "2113" } },
                    { "1113", new { HostPort = "1113" } }
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
                        "EVENTSTORE_CERTIFICATE_PASSWORD=ca",
                        "EVENTSTORE_CERTIFICATE_FILE=/cert/eventstore.p12",
                        "EVENTSTORE_TRUSTED_ROOT_CERTIFICATES_PATH=/cert/ca-certificates/"
                    },
                    Name = "eventstore-mem",
                    HostConfig = new HostConfig()
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>>()
                        {
                            { "1113", new List<PortBinding>() { new PortBinding() { HostPort = "1113", HostIP = "0.0.0.0" } }},
                            { "2113", new List<PortBinding>() { new PortBinding() { HostPort = "2113", HostIP = "0.0.0.0" } }}
                        }
                        
                    },
                    ExposedPorts = new Dictionary<string, EmptyStruct>()
                    {
                        {"2113", default(EmptyStruct)},
                        {"1113", default(EmptyStruct)}
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
    }
}