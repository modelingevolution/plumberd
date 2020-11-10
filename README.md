![](logo.png)


## INTRODUCTION

Plumberd is a lightweight library to make working with Event driven apps easy. It unifies abstractions and thus combines Event Drive Architecture (messageing) and Event Sourcing (persistance) together.
It's makes it super easy for everyone familiar with ASP Controllers or WCF to jump into CQRS and EventSourcing. All the plumbing is automated. 
Current version is aimed to support your **Handlers** (CommandHandlers, EventHandlers) to work on server-side or client-side without changing the logic. 
This becomes important especially because **Blazor** and WebAssembly is getting more traction. 
Supported plumbing:

* EventStore [Done]
* gRPC [comming soon]
* SignalR [in next release]

## GETTING STARTED

**Prepare your environment** - run EventStore. You can run it in docker (or however you like):

```bash
docker run --name esdb-node -it -p 2113:2113 -p 1113:1113 eventstore/eventstore:latest --insecure --run-projections=All
```

You can find more on EventStore from documentation [there](https://developers.eventstore.com/server/20.6/server/installation/docker.html).

**Install nugets**
```bash
dotnet add package Plumberd
dotnet add package Plumberd.EventStore
```

**Write your command-handlers, projections, processors and other handlers**:


```C#
public class MyCreazyCommandHandler 
{
	// Command Handler Method Signature
	public async Task When(Guid id, MyCreazyCommand cmd) 
	{
		// do some stuff
	}
}

[StreamName("Creazy")]  					// Category - optional.
public class MyCreazyCommand : Command { }  // you can implement ICommand instead.
```
and add bit of **configuration**:
```C#
await new PlumberBuilder()
	.WithDefaultEventStore(x => x.InSecure())
	.Build()
	.RegisterController(new MyCreazyCommandHandler())  // or RegisterController<MyCreazyCommandHandler>();
	.StartAsync();

```
### Handlers or Controllers

CommandHandlers, EventHandlers are just processing units. They contain methods that correspond to certain signatures. 
You may have pure command-handers and event-handlers. Or you can flex your design and have controllers with same methods. It's all up to you. 
Plumberd supports out of the box quite a few signatures. 
Let's see a more complex example:


```C#
public class HappyController
{
	public async IEnumerable<IEvent> When(Guid id, MyCreazyCommand cmd) 
	{
		yield return MyCreazyEvent();
	}
}

[MyProjectionConfiguration]
public class MyProjection
{
	public async Task Given(IMetadata m, MyCreazyEvent e) { }
	public async MyDerivedEvent Given(IMetadata m, MyCreazyEvent e) { }
	/* ... */
}

[MyProcessorConfiguration]
public class MyProcessor
{
	public async IAsyncEnumerable<ICommand> When(IMetadata m, MyCreazyEvent2 e) { }
	public async Task<(Guid, MyCreazyCommand3)> When(IMetadata m, MyCreazyEvent2 e) { }
	/* ... */
}
```

Configuration can be done in attributes or in Fluent API:

```C#
public class MyProcessorAttribute : ProcessingUnitConfigAttribute
{
    public MyProcessorAttribute()
    {
        SubscribesFromBeginning = false;	// Subscribe to stream from beginning or from now.
        IsEventEmitEnabled = true;    // Methods can return events or (Guid,TEvent)
        IsCommandEmitEnabled = true;  // Methods can return commands or (Guid, TCommand)
        IsPersistent = true;		  // Cursor is saved in EventStore are persistant subscription
        BindingFlags = BindingFlags.ProcessEvents |		// A flag that is used to narrow the scope of binding in 'Controllers'
                                  BindingFlags.ReturnEvents |
                                  BindingFlags.ReturnCommands;
        }
    }
}
```
**DONE** You ready to run the app. F5!
## Dependencies

Using the library requires using 3 interfaces:
* ICommand (all your commands need to implement it)
* IEvent (all your events need to implement it)
* IMatadata (most likely you won't derive from it.)

The interface is very simple requireing you only to have **Id** property of type **Guid**.

## Projections

Processing events in good-enough order is very important. That's why out of the box Plumberd is:

* Creating projection for every Controller (except one method ones). 
* The projection emits a new stream that links to orignal events.
* This way you can track what events where processed by projection. 
* CorrelationId and CausationId are appended automatically. 

## Metadata

Metadata in EventStore is like a headers in WebAPI. Plumberd makes it easy to write your own Enricher, that can append new properties to metadata. 


# ROADMAP

Current version is **PRE-Alfa**. 

**pre-Alfa**
In this version we are experimenting with new features as fast as possible. It's well known that eventsourcing libraries are to complex and do not fit every case. 
We want to check if we can came up with fix number of features that satisfy many usecases. To make it happen, we need to iterate fast and experiment with 
the set of features. 
From QA perspective this means that few unit-tests are there, however you will find integration-tests and api-tests (if we can achive something though expressing it using lib.).

**Alfa**
In this version we might introduce new breaking features. The library might be unstable and is not suitable to production environment - unless you know what you do ;)
Alfa testing will spread testing upon interested people in closed team.

**Beta** version is scheduled on at the end of 2020 or early 2021. 
In this version we'll be encouraging everybody to test the library and give feedback. Some breaking changes might be still introduces.

**Release candidate** will be published as soon as we are satisfied with the scope of features.
In this stage will focus on performance. 

**Release 1.0** is scheduled at the 1-quater 2021.

## PRE-ALFA PROPOSITIONS:

- Integrate scope factory.
- Allow the enrichers can get arguments from container. (scoped)
- Support 3 argument signature When(Guid, TCommand, IMetadata) in CommandHandler
- Introduce curcuit breaker. Through enricher / or though correlation-projection.
- Create a test project with aggregates.
- Create integration with Kafka
- Create integration with Carter
- Refactor library to use decorator pattern when handling records (although this might be done separetly?)
- Integrate security through metadata-stream
- Make enrichers to save info about the verion to metadata-stream.
- Create the concept of consistent stream migration and event-version. 