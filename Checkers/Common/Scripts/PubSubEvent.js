export class PubSubEvent {
    constructor(system) {
        this.handlers = [];
        this.system = system;
        //console.log('PubSub<>');
    }
    send(payload) {
        const data = payload;
        this.handlers.slice(0).forEach(h => h(data));
    }
    on(handler) {
        this.handlers.push(handler);
    }
    off(handler) {
        this.handlers = this.handlers.filter(h => h !== handler);
    }
    raise(data) {
        this.handlers.slice(0).forEach(h => h(data));
        if (this.system)
            this.system(data);
    }
    expose() {
        return this;
    }
}
//# sourceMappingURL=PubSubEvent.js.map