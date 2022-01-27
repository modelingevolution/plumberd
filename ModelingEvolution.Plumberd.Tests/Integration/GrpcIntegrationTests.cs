using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.EventStore.GrpcProxy;
using ModelingEvolution.Plumberd.EventStore;
using ModelingEvolution.Plumberd.GrpcProxy;
using ModelingEvolution.Plumberd.Tests.Integration.Configuration;
using ModelingEvolution.Plumberd.Tests.Models;
using NSubstitute;
using Microsoft.Extensions.Logging;

using Xunit;
using Xunit.Abstractions;

namespace ModelingEvolution.Plumberd.Tests.Integration
{
    public class GrpcIntegrationTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private EventStoreServer server;

        public GrpcIntegrationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }
        [Trait("Category", "Integration")]
        [Fact]
         public async Task InvokeRead()
        {
            Guid id = Guid.NewGuid();
            var c = new SimpleCommand();

            var plumber = await CreatePlumber();
            this._commandHandler = new SimpleCommandHandler();
            plumber.RegisterController(_commandHandler);
            await plumber.StartAsync();
            var proxy = CreateGrpcWebApp(plumber);

            await plumber.DefaultCommandInvoker.Execute(id, c);

            var responseStream = ResponseStream();
            await proxy.ReadStream(Request("x",true,false, "SimpleEvent"), 
                responseStream, 
                ServerContext(Guid.NewGuid(),"John"));

            await Task.Delay(2000);
            await responseStream.Received().WriteAsync(Arg.Any<ReadRsp>());
        }
        internal const string HttpContextKey = "__HttpContext";
        private ServerCallContext _serverContextMock;
        private ServerCallContext ServerContext(Guid? userId, string name, bool isAuth = true)
        {
            if (_serverContextMock == null)
            {
                _serverContextMock = Substitute.For<ServerCallContext>();
                Dictionary<object,object> userStatus = new Dictionary<object, object>();
                HttpContext context = Substitute.For<HttpContext>();

                var identity = new GenericIdentity(name);
                if(userId.HasValue)
                    identity.AddClaim(new Claim("sub", userId.ToString()));
                ClaimsPrincipal user = new ClaimsPrincipal(identity);
                
                context.User.Returns(user);
                userStatus.Add(HttpContextKey, context);
                _serverContextMock.UserState.Returns(userStatus);
            }
            return _serverContextMock;
        }

        private IServerStreamWriter<ReadRsp> _responseStream;
        private SimpleCommandHandler _commandHandler;

        private IServerStreamWriter<ReadRsp> ResponseStream()
        {
            if (_responseStream == null)
            {
                _responseStream = NSubstitute.Substitute.For<IServerStreamWriter<ReadRsp>>();
            }

            return _responseStream;
        }

        private ReadReq Request(string name, bool fromBeginning, bool isPersistant, params string[] types)
        {
            ReadReq r = new ReadReq();
            r.FromBeginning = fromBeginning;
            r.IsPersistent = isPersistant;
            r.EventTypeSchema = new EventTypeProjectionSchema() { Name = name, EventTypes = { types }};
            return r;
        }

        public EventStoreProxy CreateGrpcWebApp(IPlumberRuntime plumberRuntime)
        {
            ServiceCollection collection = new ServiceCollection();

            collection.AddSingleton(plumberRuntime);
            collection.AddSingleton(plumberRuntime.DefaultEventStore);
            collection.AddSingleton(plumberRuntime.DefaultCommandInvoker);
            collection.AddScoped<EventStoreProxy>();

            TypeRegister tr = new TypeRegister();
            tr.Index(typeof(FooCommand).Assembly.GetTypes().Where(x => typeof(IRecord).IsAssignableFrom(x))
                .ToArray());
            collection.AddSingleton(tr);

            var result = collection.BuildServiceProvider(true);

            return result.GetRequiredService<EventStoreProxy>();
        }

        private async Task<IPlumberRuntime> CreatePlumber()
        {
            this.server = await EventStoreServer.Start();
            await Task.Delay(2000);

            PlumberBuilder b = new PlumberBuilder()
                .WithTcpEventStore(x => x.InSecure());
            
            var plumber = b.Build();
            return plumber;
        }
    }
}
