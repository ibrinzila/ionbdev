// Codapter C# MVC - Client-side utilities

/**
 * JSON-RPC helper for making API calls.
 */
const CodapterRpc = (() => {
    let _id = 1;
    const _baseUrl = '/api/rpc';

    async function call(method, params) {
        const response = await fetch(_baseUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                jsonrpc: '2.0',
                id: _id++,
                method,
                params: params || {}
            })
        });

        const data = await response.json();
        if (data.error) {
            throw new RpcError(data.error.code, data.error.message, data.error.data);
        }
        return data.result;
    }

    async function batch(requests) {
        const body = requests.map(r => ({
            jsonrpc: '2.0',
            id: _id++,
            method: r.method,
            params: r.params || {}
        }));

        const response = await fetch(`${_baseUrl}/batch`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        return await response.json();
    }

    class RpcError extends Error {
        constructor(code, message, data) {
            super(message);
            this.code = code;
            this.data = data;
        }
    }

    return { call, batch, RpcError };
})();

/**
 * WebSocket NDJSON-RPC client.
 */
class NdjsonRpcClient {
    constructor(url) {
        this._url = url || `ws://${location.host}/ws/rpc`;
        this._ws = null;
        this._id = 1;
        this._pending = new Map();
        this._notificationHandlers = [];
    }

    connect() {
        return new Promise((resolve, reject) => {
            this._ws = new WebSocket(this._url);

            this._ws.onopen = () => resolve();
            this._ws.onerror = (err) => reject(err);

            this._ws.onmessage = (event) => {
                const lines = event.data.split('\n').filter(l => l.trim());
                for (const line of lines) {
                    try {
                        const msg = JSON.parse(line);
                        if (msg.id && this._pending.has(msg.id)) {
                            const { resolve, reject } = this._pending.get(msg.id);
                            this._pending.delete(msg.id);
                            if (msg.error) reject(new Error(msg.error.message));
                            else resolve(msg.result);
                        } else if (msg.method) {
                            this._notificationHandlers.forEach(h => h(msg));
                        }
                    } catch (e) {
                        console.warn('Failed to parse NDJSON line:', line);
                    }
                }
            };

            this._ws.onclose = () => {
                for (const [, { reject }] of this._pending) {
                    reject(new Error('WebSocket closed'));
                }
                this._pending.clear();
            };
        });
    }

    call(method, params) {
        return new Promise((resolve, reject) => {
            const id = this._id++;
            this._pending.set(id, { resolve, reject });
            const msg = JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n';
            this._ws.send(msg);
        });
    }

    onNotification(handler) {
        this._notificationHandlers.push(handler);
        return () => {
            const idx = this._notificationHandlers.indexOf(handler);
            if (idx >= 0) this._notificationHandlers.splice(idx, 1);
        };
    }

    close() {
        if (this._ws) this._ws.close();
    }
}
