// GoatLab offline write queue.
// When the app tries to POST/PUT/DELETE and the browser is offline, we enqueue
// the request in IndexedDB. When `online` fires again we replay queued ops in
// insertion order. Survives reloads and browser restarts.
(function () {
    const DB_NAME = 'goatlab-offline';
    const STORE   = 'queue';
    const VERSION = 1;

    function open() {
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, VERSION);
            req.onupgradeneeded = () => {
                const db = req.result;
                if (!db.objectStoreNames.contains(STORE)) {
                    db.createObjectStore(STORE, { keyPath: 'id', autoIncrement: true });
                }
            };
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
    }

    async function put(op) {
        const db = await open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readwrite');
            const req = tx.objectStore(STORE).add(op);
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
    }

    async function getAll() {
        const db = await open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readonly');
            const req = tx.objectStore(STORE).getAll();
            req.onsuccess = () => resolve(req.result || []);
            req.onerror = () => reject(req.error);
        });
    }

    async function remove(id) {
        const db = await open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readwrite');
            const req = tx.objectStore(STORE).delete(id);
            req.onsuccess = () => resolve();
            req.onerror = () => reject(req.error);
        });
    }

    async function count() {
        const db = await open();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readonly');
            const req = tx.objectStore(STORE).count();
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
    }

    async function enqueue(method, url, body) {
        const id = await put({
            method,
            url,
            body: body == null ? null : JSON.stringify(body),
            enqueuedAt: new Date().toISOString()
        });
        notifyCount();
        return id;
    }

    // Flush: replay each op. On 4xx/5xx the op is removed too — we don't want
    // a poisoned record blocking the queue forever. Errors are logged.
    async function flush() {
        const ops = await getAll();
        let ok = 0;
        let failed = 0;
        for (const op of ops) {
            try {
                const resp = await fetch(op.url, {
                    method: op.method,
                    headers: op.body ? { 'Content-Type': 'application/json' } : {},
                    body: op.body ?? undefined
                });
                // Remove regardless; if server rejected we don't keep retrying blindly.
                await remove(op.id);
                if (resp.ok) ok++; else failed++;
            } catch (err) {
                // Network still unreachable; keep the op and stop — next online event will retry.
                console.warn('offline-queue: flush stopped, still offline', err);
                break;
            }
        }
        notifyCount();
        return { flushed: ok, failed };
    }

    const countListeners = [];
    async function notifyCount() {
        try {
            const n = await count();
            countListeners.forEach(cb => { try { cb(n); } catch { } });
        } catch { }
    }

    // Auto-flush on reconnect
    window.addEventListener('online', () => { flush(); });

    window.goatOfflineQueue = {
        enqueue,
        count,
        flush,
        getAll,
        clear: async function () {
            const ops = await getAll();
            for (const op of ops) await remove(op.id);
            notifyCount();
        },
        registerCountChanged: function (dotnetRef) {
            const cb = async (n) => { try { await dotnetRef.invokeMethodAsync('OnQueueCountChanged', n); } catch { } };
            countListeners.push(cb);
            // Fire once to prime the UI
            notifyCount();
            return countListeners.length - 1;
        }
    };
})();
