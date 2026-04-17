// GoatLab service worker (published)
// Strategy:
//   - App shell / _framework / static assets: cache-first (fetched once, reused offline)
//   - GET /api/* : stale-while-revalidate (serve cache instantly, refresh in background)
//   - Non-GET /api/* : network-only; if offline, respond with 503 so the app shows a toast
//   - Navigation requests: network, fallback to cached index.html (SPA offline boot)
const SHELL_CACHE = 'goatlab-shell-v3';
const API_CACHE   = 'goatlab-api-v3';

const SHELL_ASSETS = [
    '/',
    '/index.html',
    '/css/app.css',
    '/manifest.webmanifest',
    '/images/icon-192.png',
    '/images/icon-512.png'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(SHELL_CACHE).then(cache => cache.addAll(SHELL_ASSETS))
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(keys.filter(k => k !== SHELL_CACHE && k !== API_CACHE).map(k => caches.delete(k)));
        await self.clients.claim();
    })());
});

function isApi(url)  { return url.pathname.startsWith('/api/'); }
function isNav(req)  { return req.mode === 'navigate'; }
function isShellAsset(url) {
    return url.pathname.startsWith('/_framework/')
        || url.pathname.startsWith('/_content/')
        || url.pathname.startsWith('/css/')
        || url.pathname.startsWith('/js/')
        || url.pathname.startsWith('/images/')
        || url.pathname.endsWith('.webmanifest')
        || url.pathname === '/'
        || url.pathname === '/index.html';
}

async function staleWhileRevalidate(request) {
    const cache = await caches.open(API_CACHE);
    const cached = await cache.match(request);
    const networkPromise = fetch(request)
        .then(res => {
            if (res.ok) cache.put(request, res.clone());
            return res;
        })
        .catch(() => null);
    // Serve cache immediately if present; else wait for network
    return cached || (await networkPromise) || new Response(
        JSON.stringify({ error: 'offline', message: 'You are offline and no cached copy is available.' }),
        { status: 503, headers: { 'Content-Type': 'application/json' } }
    );
}

async function cacheFirst(request) {
    const cache = await caches.open(SHELL_CACHE);
    const cached = await cache.match(request);
    if (cached) return cached;
    try {
        const res = await fetch(request);
        if (res.ok) cache.put(request, res.clone());
        return res;
    } catch {
        return cached || Response.error();
    }
}

async function navigationHandler(request) {
    try {
        return await fetch(request);
    } catch {
        const cache = await caches.open(SHELL_CACHE);
        return (await cache.match('/index.html')) || (await cache.match('/')) || Response.error();
    }
}

self.addEventListener('fetch', event => {
    const req = event.request;
    const url = new URL(req.url);

    if (url.origin !== self.location.origin) return; // ignore cross-origin (CDNs etc.)

    if (isNav(req)) {
        event.respondWith(navigationHandler(req));
        return;
    }

    if (isApi(url)) {
        if (req.method !== 'GET') {
            // writes must hit the network — surface the failure to the UI
            event.respondWith(fetch(req).catch(() => new Response(
                JSON.stringify({ error: 'offline', message: 'Cannot save while offline.' }),
                { status: 503, headers: { 'Content-Type': 'application/json' } }
            )));
            return;
        }
        event.respondWith(staleWhileRevalidate(req));
        return;
    }

    if (req.method === 'GET' && isShellAsset(url)) {
        event.respondWith(cacheFirst(req));
    }
});

// Web Push: server payloads are JSON shaped { title, body, url, tag? }.
// Falls back to a generic notification if the payload is missing/non-JSON
// (e.g. test pings).
self.addEventListener('push', event => {
    let data = { title: 'GoatLab', body: 'You have a new alert.', url: '/alerts' };
    if (event.data) {
        try { data = Object.assign(data, event.data.json()); }
        catch { try { data.body = event.data.text(); } catch { /* swallow */ } }
    }
    event.waitUntil(self.registration.showNotification(data.title, {
        body: data.body,
        icon: '/images/icon-192.png',
        badge: '/images/icon-192.png',
        tag: data.tag || 'goatlab-alert',
        data: { url: data.url },
    }));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = (event.notification.data && event.notification.data.url) || '/alerts';
    event.waitUntil((async () => {
        const all = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const c of all) {
            if (c.url.includes(target) && 'focus' in c) return c.focus();
        }
        if (self.clients.openWindow) return self.clients.openWindow(target);
    })());
});
