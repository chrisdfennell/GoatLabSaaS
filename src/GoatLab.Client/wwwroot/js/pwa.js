// GoatLab PWA helpers: online/offline notifications + installable-app prompt.
(function () {
    let deferredInstallPrompt = null;
    const listeners = { online: [], install: [] };

    window.addEventListener('beforeinstallprompt', e => {
        e.preventDefault();
        deferredInstallPrompt = e;
        listeners.install.forEach(cb => { try { cb(true); } catch { } });
    });

    window.addEventListener('appinstalled', () => {
        deferredInstallPrompt = null;
        listeners.install.forEach(cb => { try { cb(false); } catch { } });
    });

    window.addEventListener('online', () => listeners.online.forEach(cb => { try { cb(true); } catch { } }));
    window.addEventListener('offline', () => listeners.online.forEach(cb => { try { cb(false); } catch { } }));

    window.goatPwa = {
        isOnline: () => navigator.onLine,
        canInstall: () => deferredInstallPrompt !== null,
        isStandalone: () =>
            window.matchMedia('(display-mode: standalone)').matches ||
            window.navigator.standalone === true,
        promptInstall: async function () {
            if (!deferredInstallPrompt) return 'unavailable';
            deferredInstallPrompt.prompt();
            const choice = await deferredInstallPrompt.userChoice;
            deferredInstallPrompt = null;
            listeners.install.forEach(cb => { try { cb(false); } catch { } });
            return choice.outcome; // 'accepted' | 'dismissed'
        },
        registerOnlineChanged: function (dotnetRef) {
            const cb = (online) => dotnetRef.invokeMethodAsync('OnOnlineChanged', online);
            listeners.online.push(cb);
            return listeners.online.length - 1;
        },
        registerInstallAvailableChanged: function (dotnetRef) {
            const cb = (available) => dotnetRef.invokeMethodAsync('OnInstallAvailableChanged', available);
            listeners.install.push(cb);
            if (deferredInstallPrompt) cb(true);
            return listeners.install.length - 1;
        }
    };
})();
