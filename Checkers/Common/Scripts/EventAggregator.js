import { PubSubEvent } from "./PubSubEvent";
export class EventAggregatorFactory {
    connect(proxy) {
        this._proxy = proxy;
    }
    create() {
        return new EventAggregator(this._proxy);
    }
}
EventAggregatorFactory.instance = new EventAggregatorFactory();
export class EventAggregator {
    constructor(proxy) {
        this._events = new Map();
        this._ctors = new Map();
        this._proxy = proxy;
    }
    static get instance() {
        if (!EventAggregator._instance)
            EventAggregator._instance = EventAggregatorFactory.instance.create();
        return EventAggregator._instance;
    }
    getEvent(ctor) {
        const ev = new ctor();
        const key = ev.constructor.name;
        if (!this._events.has(key)) {
            const me = this;
            const pubSub = new PubSubEvent(x => {
                me.onEvent(x);
            });
            this._ctors.set(key, () => new ctor());
            this._events.set(key, pubSub);
        }
        return this._events.get(key);
    }
    send(eventType, eventPayload) {
        if (this._events.has(eventType)) {
            const event = this._events.get(eventType);
            const json = JSON.parse(eventPayload);
            const payload = this._ctors.get(eventType)();
            Object.assign(payload, json);
            event.send(payload);
        }
    }
    onEvent(x) {
        // this should call .net
        // .net should not call me
        if (this._proxy) {
            if (x['isSharable']) {
                const json = JSON.stringify(x);
                const eventType = x.constructor.name;
                //console.log(json);
                this._proxy.invokeMethod("Send", eventType, json);
            }
        }
    }
}
//# sourceMappingURL=EventAggregator.js.map