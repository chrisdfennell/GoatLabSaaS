// Google reCAPTCHA v3 loader + executor.
//
// Usage from Blazor:
//   await JS.invokeVoidAsync("goatlabRecaptcha.load", siteKey)
//   const token = await JS.invokeAsync("goatlabRecaptcha.execute", "login")
//
// load() is idempotent — second/third calls do nothing if the script has
// already been injected. execute() resolves to null when not configured
// (empty siteKey) so callers can continue without a token in dev.

(function () {
    const state = {
        loaded: false,
        loading: null, // promise while the <script> is being loaded
        siteKey: null,
    };

    function loadScript(siteKey) {
        if (state.loaded && state.siteKey === siteKey) return Promise.resolve();
        if (state.loading) return state.loading;
        state.loading = new Promise(function (resolve, reject) {
            const s = document.createElement("script");
            s.src = "https://www.google.com/recaptcha/api.js?render=" + encodeURIComponent(siteKey);
            s.async = true;
            s.defer = true;
            s.onload = function () {
                state.loaded = true;
                state.siteKey = siteKey;
                if (typeof grecaptcha === "undefined") { resolve(); return; }
                grecaptcha.ready(function () { resolve(); });
            };
            s.onerror = function (e) {
                state.loading = null; // allow a future retry
                reject(e);
            };
            document.head.appendChild(s);
        });
        return state.loading;
    }

    window.goatlabRecaptcha = {
        load: async function (siteKey) {
            if (!siteKey) return; // no-op in dev when key isn't configured
            try { await loadScript(siteKey); } catch (e) {
                // swallow — execute() will return null and the request will
                // either pass (in dev) or fail server-side.
                console.warn("[recaptcha] load failed", e);
            }
        },

        execute: async function (action) {
            if (!state.loaded || !state.siteKey) return null;
            if (typeof grecaptcha === "undefined") return null;
            try {
                return await grecaptcha.execute(state.siteKey, { action: action });
            } catch (e) {
                console.warn("[recaptcha] execute failed", e);
                return null;
            }
        }
    };
})();
