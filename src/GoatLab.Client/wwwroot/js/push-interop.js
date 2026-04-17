// Web Push interop for Blazor. Mirrors the shape of webauthn-interop.js —
// each method is a thin wrapper around the browser's PushManager API and
// returns plain values so the Blazor side can stay typed.
window.goatPush = {
    isSupported: function () {
        return ('serviceWorker' in navigator) && ('PushManager' in window);
    },

    permission: function () {
        return (typeof Notification !== 'undefined') ? Notification.permission : 'denied';
    },

    // Returns the endpoint of the current subscription, or null.
    currentEndpoint: async function () {
        if (!this.isSupported()) return null;
        const reg = await navigator.serviceWorker.getRegistration();
        if (!reg) return null;
        const sub = await reg.pushManager.getSubscription();
        return sub ? sub.endpoint : null;
    },

    // Prompt for permission (if needed) and create a push subscription using
    // the server-provided VAPID public key. Returns the subscription as a
    // JSON string (.NET parses it). Throws on user denial.
    subscribe: async function (vapidPublicKey) {
        if (!this.isSupported()) throw new Error('push-not-supported');
        const reg = await navigator.serviceWorker.ready;

        if (Notification.permission === 'default') {
            const result = await Notification.requestPermission();
            if (result !== 'granted') return null;
        }
        if (Notification.permission !== 'granted') return null;

        let sub = await reg.pushManager.getSubscription();
        if (!sub) {
            sub = await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
            });
        }
        return JSON.stringify(sub);
    },

    unsubscribe: async function () {
        if (!this.isSupported()) return false;
        const reg = await navigator.serviceWorker.getRegistration();
        if (!reg) return false;
        const sub = await reg.pushManager.getSubscription();
        if (!sub) return false;
        return await sub.unsubscribe();
    },
};

function urlBase64ToUint8Array(base64) {
    const padding = '='.repeat((4 - base64.length % 4) % 4);
    const b64 = (base64 + padding).replace(/-/g, '+').replace(/_/g, '/');
    const raw = atob(b64);
    const out = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; ++i) out[i] = raw.charCodeAt(i);
    return out;
}
