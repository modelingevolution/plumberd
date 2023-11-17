using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ModelingEvolution.Plumberd.Tests.Models;
using Xunit;
#pragma warning disable 1998

namespace ModelingEvolution.Plumberd.Tests
{
    public class TransitionUnitTests
    {
        [Fact]
        public async Task WhenReturnsEnumerable()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            var events = sut.Execute(new Command1());

            events.Should().HaveCount(1);
            events[0].Should().BeOfType<Event1>();
        }

        [Fact]
        public void Given()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            sut.Rehydrate(new[] { new Event1() });

            sut.GetState().Name.Should().Be("Foo");
        }
        [Fact]
        public void GivenMany()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            sut.Rehydrate(new[] { new Event1(), new Event1(), new Event1() });

            sut.GetState().Name.Should().Be("Foo");
        }

        [Fact]
        public void WhenReturnsEvent()
        {
            ComplexTransitionUnit sut = new ComplexTransitionUnit();

            var events = sut.Execute(new Command2());

            events.Should().HaveCount(1);
            events[0].Should().BeOfType<Event2>();
        }

    }
    class LazyLogProvider : ILoggerFactory
    {
        IServiceProvider _provider;

        class ConsoleLogger : ILogger, IDisposable
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                return this;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                var line = formatter(state, exception);
                Console.WriteLine(line);
            }

            public void Dispose()
            {
            }
        }

        public LazyLogProvider(IServiceProvider provider)
        {
            _provider = provider;
        }

        public void Dispose()
        {

        }

        public void AddProvider(ILoggerProvider provider)
        {

        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ConsoleLogger();
        }
    }
}