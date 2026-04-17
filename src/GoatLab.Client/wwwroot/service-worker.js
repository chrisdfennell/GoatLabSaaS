// GoatLab service worker (development — no caching, but full push handlers
// so push notifications work end-to-end in `dotnet run` too).
self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', event => { });

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
