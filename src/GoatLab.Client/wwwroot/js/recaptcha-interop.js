// Google reCAPTCHA v3 loader + executor.
//
// Usage from Blazor:
//   await JS.invokeVoidAsync("goatlabRecaptcha.load", siteKey)
//   const token = await JS.invokeAsync("goatlabRecaptcha.execute", "login")
//
// load() is idempotent — second/third calls do nothing if the script has
// already been injected. execute() resolves to null when not configured
// (empty siteKey) so callers can continue without a token in dev.
//
// The execute() function intentionally awaits an in-flight load() rather
// than returning null immediately — that race used to lock out mobile
// users on slow networks who hit Submit before the reCAPTCHA script
// finished downloading.

(function () {
    var state = {
        loaded: false,
        loading: null, // promise while the <script> is being loaded
        siteKey: null,
    };

    var EXECUTE_LOAD_TIMEOUT_MS = 6000;

    function loadScript(siteKey) {
        if (state.loaded && state.siteKey === siteKey) return Promise.resolve();
        if (state.loading) return state.loading;
        state.loading = new Promise(function (resolve, reject) {
            var s = document.createElement("script");
            s.src = "https://www.google.com/recaptcha/api.js?render=" + encodeURIComponent(siteKey);
            s.async = true;
            s.defer = true;
            s.onload = function () {
                state.siteKey = siteKey;
                if (typeof grecaptcha === "undefined") {
                    state.loaded = true;
                    resolve();
                    return;
                }
                grecaptcha.ready(function () {
                    state.loaded = true;
                    resolve();
                });
            };
            s.onerror = function (e) {
                state.loading = null; // allow a future retry
                reject(e);
            };
            document.head.appendChild(s);
        });
        return state.loading;
    }

    function withTimeout(promise, ms) {
        return new Promise(function (resolve, reject) {
            var t = setTimeout(function () { reject(new Error("recaptcha load timeout")); }, ms);
            promise.then(function (v) { clearTimeout(t); resolve(v); },
                         function (e) { clearTimeout(t); reject(e); });
        });
    }

    window.goatlabRecaptcha = {
        load: async function (siteKey) {
            if (!siteKey) return; // no-op in dev when key isn't configured
            try { await loadScript(siteKey); } catch (e) {
                console.warn("[recaptcha] load failed", e);
            }
        },

        execute: async function (action) {
            // If load is in flight (mobile typically — slow network race),
            // wait up to EXECUTE_LOAD_TIMEOUT_MS for it before giving up.
            // Without this, hitting Submit before grecaptcha finished
            // loading silently produced a null token and the server
            // rejected the request as "missing token".
            if (!state.loaded && state.loading) {
                try { await withTimeout(state.loading, EXECUTE_LOAD_TIMEOUT_MS); }
                catch (e) {
                    console.warn("[recaptcha] execute() gave up waiting on load", e);
                    return null;
                }
            }
            if (!state.loaded || !state.siteKey) return null;
            if (typeof grecaptcha === "undefined") return null;
            try {
                return await grecaptcha.execute(state.siteKey, { action: action });
            } catch (e) {
                console.warn("[recaptcha] execute failed", e);
                return null;
            }
        },

        // Diagnostic helper — exposed so console.log(window.goatlabRecaptcha.state())
        // gives a quick snapshot of why a token might be missing.
        state: function () {
            return {
                loaded: state.loaded,
                loading: !!state.loading,
                siteKey: state.siteKey ? state.siteKey.slice(0, 8) + "…" : null,
                hasGrecaptcha: typeof grecaptcha !== "undefined",
            };
        }
    };
})();
