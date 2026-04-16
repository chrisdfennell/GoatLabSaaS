// GoatLab Browser Notifications
window.goatNotify = (function () {
    return {
        isSupported: function () {
            return typeof Notification !== 'undefined';
        },

        permission: function () {
            if (typeof Notification === 'undefined') return 'unsupported';
            return Notification.permission; // 'default', 'granted', 'denied'
        },

        request: async function () {
            if (typeof Notification === 'undefined') return 'unsupported';
            try {
                const result = await Notification.requestPermission();
                return result; // 'granted', 'denied', 'default'
            } catch (e) {
                return 'denied';
            }
        },

        show: function (title, body, tag, url) {
            if (typeof Notification === 'undefined') return false;
            if (Notification.permission !== 'granted') return false;
            try {
                const n = new Notification(title, {
                    body: body,
                    tag: tag || 'goatlab',
                    icon: '/images/icon-192.png',
                    badge: '/images/icon-192.png'
                });
                if (url) {
                    n.onclick = () => { window.focus(); window.location.href = url; n.close(); };
                }
                return true;
            } catch {
                return false;
            }
        }
    };
})();
