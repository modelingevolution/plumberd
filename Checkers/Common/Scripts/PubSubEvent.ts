

export interface IEventBase<T> {
    on(handler: { (data?: T): void }): void;
    off(handler: { (data?: T): void }): void;
    raise(data?: T):void;
}
export class PubSubEvent<T> implements IEventBase<T>, IBusChannel {
    private handlers: { (data?: T): void; }[] = [];
    private system: { (data?: T): void; };

    public constructor(system: { (data?: T): void }) {
        this.system = system;
        //console.log('PubSub<>');
    }

    public send(payload: any) {
        const data = <T>payload;
        this.handlers.slice(0).forEach(h => h(data));
    }

    public on(handler: { (data?: T): void }): void {
        this.handlers.push(handler);
    }

    public off(handler: { (data?: T): void }): void {
        this.handlers = this.handlers.filter(h => h !== handler);
    }

    public raise(data?: T) {
        this.handlers.slice(0).forEach(h => h(data));
        if(this.system)
            this.system(data);
    }

    public expose(): IEventBase<T> {
        return this;
    }
}
export interface IBusChannel {
    send(payload: any);
}

