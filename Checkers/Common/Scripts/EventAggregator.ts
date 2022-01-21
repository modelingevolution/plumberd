import {IBusChannel, IEventBase, PubSubEvent} from "./PubSubEvent";
import DotNetObject = DotNet.DotNetObject;

export interface IBus {
    send(eventType: string, payload:string):void;
}
export interface IEventAggregator {
    getEvent<T>(ctor: { new(): T; }): IEventBase<T>;
}

export class EventAggregatorFactory { 
    public static readonly instance: EventAggregatorFactory = new EventAggregatorFactory();
    private _proxy: DotNetObject;
    public connect(proxy: DotNet.DotNetObject) {
        this._proxy = proxy;
    }
    public create():EventAggregator{
        return new EventAggregator(this._proxy);
    }
}
export class EventAggregator implements IEventAggregator, IBus {
    private static _instance: EventAggregator;

    public static get instance(): IEventAggregator {
        if(!EventAggregator._instance)
            EventAggregator._instance = EventAggregatorFactory.instance.create();
        return EventAggregator._instance;
    }

    private readonly _events: Map<string, any>;
    private readonly _ctors: Map<string, () => any>;
    private _proxy: DotNetObject;

    constructor(proxy:DotNetObject) {
        this._events = new Map<string, any>();
        this._ctors = new Map<string, () => any>();
        this._proxy = proxy;
    }
    
   

    
    public getEvent<T>(ctor: { new(): T; }): IEventBase<T> {

        const ev = new ctor();
        const key = ev.constructor.name;
        if (!this._events.has(key)) {
            const me = this;
            const pubSub = new PubSubEvent<T>(x => {
                me.onEvent(x);
            });
            this._ctors.set(key, () => new ctor());
            this._events.set(key, pubSub);
        }
        return <IEventBase<T>>this._events.get(key);
    }

    public send(eventType: string, eventPayload: string): void {
        if (this._events.has(eventType)) {
            const event = <IBusChannel>this._events.get(eventType);
            const json = JSON.parse(eventPayload);
            const payload = this._ctors.get(eventType)();
            Object.assign(payload, json);
            event.send(payload);
        }
    }

    private onEvent(x: any): void {
        // this should call .net
        // .net should not call me
        if (this._proxy) {
            if(x['isSharable']) {
                const json = JSON.stringify(x);
                const eventType = x.constructor.name;
                //console.log(json);
                this._proxy.invokeMethod("Send", eventType, json);
            }
        }
    }
}