using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using EventStore.ClientAPI.Embedded;
using EventStore.Common.Options;
using EventStore.Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Hosting;
using ModelingEvolution.Plumberd.EventStore;
using Serilog;

namespace ModelingEvolution.Plumberd.Tests
{
    public class EventStoreServer
    {
        private ClusterVNode _node;
        private IHost _host;

        public static async Task<EventStoreServer> Start()
        {
            EventStoreServer s = new EventStoreServer();
            //await s.StartAsync();
            await s.StartInDocker();
            return s;
        }
        public async Task StartInDocker()
        {
            Uri uri = new Uri("npipe://./pipe/docker_engine");

            DockerClient client = new DockerClientConfiguration(uri, defaultTimeout: TimeSpan.FromSeconds(5))
                .CreateClient();

            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters()
            {
                All = true,
                Limit = 10000
            });
            var container = containers.FirstOrDefault(x => x.Names.Any(n => n == "/eventstore-mem"));
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
        private async Task StartAsync()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            var nodeBuilder = EmbeddedVNodeBuilder
                .AsSingleNode()
                .EnableExternalTCP()
                .EnableLoggingOfHttpRequests()
                .OnDefaultEndpoints()
                .StartStandardProjections()
                .RunInMemory();
            
            this._node = nodeBuilder.Build();
            this._host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging => logging.AddSerilog())
                .ConfigureWebHostDefaults(builder =>
                    builder.UseKestrel(server => 
                            server.Listen(IPAddress.Loopback, 2113, 
                            listenOptions => listenOptions.Use(next => new ClearTextHttpMultiplexingMiddleware(next).OnConnectAsync)
                            ))
                        .ConfigureServices(services => _node.Startup.ConfigureServices(services))
                        .Configure(_node.Startup.Configure)
                    ).Build();
                
            await _node.StartAsync(true);
            await _host.StartAsync();
            await Task.Delay(30000);
        }
    }
public class ClearTextHttpMultiplexingMiddleware
{
    private ConnectionDelegate _next;
    //HTTP/2 prior knowledge-mode connection preface
    private static readonly byte[] _http2Preface = { 0x50, 0x52, 0x49, 0x20, 0x2a, 0x20, 0x48, 0x54, 0x54, 0x50, 0x2f, 0x32, 0x2e, 0x30, 0x0d, 0x0a, 0x0d, 0x0a, 0x53, 0x4d, 0x0d, 0x0a, 0x0d, 0x0a }; //PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n

    public ClearTextHttpMultiplexingMiddleware(ConnectionDelegate next)
    {
        _next = next;
    }

    private static async Task<bool> HasHttp2Preface(PipeReader input)
    {
        while (true)
        {
            var result = await input.ReadAsync().ConfigureAwait(false);
            try
            {
                int pos = 0;
                foreach (var x in result.Buffer)
                {
                    for (var i = 0; i < x.Span.Length && pos < _http2Preface.Length; i++)
                    {
                        if (_http2Preface[pos] != x.Span[i])
                        {
                            return false;
                        }

                        pos++;
                    }

                    if (pos >= _http2Preface.Length)
                    {
                        return true;
                    }
                }

                if (result.IsCompleted) return false;
            }
            finally
            {
                input.AdvanceTo(result.Buffer.Start);
            }
        }
    }

    private static void SetProtocols(object target, HttpProtocols protocols)
    {
        var field = target.GetType().GetField("_protocols", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new RuntimeBinderException("Couldn't bind to Kestrel's _protocols field");
        field.SetValue(target, protocols);
    }

    public async Task OnConnectAsync(ConnectionContext context)
    {
        var hasHttp2Preface = await HasHttp2Preface(context.Transport.Input).ConfigureAwait(false);
        SetProtocols(_next.Target, hasHttp2Preface ? HttpProtocols.Http2 : HttpProtocols.Http1);
        await _next(context).ConfigureAwait(false);
    }
}
}