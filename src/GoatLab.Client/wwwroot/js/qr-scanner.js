// GoatLab QR / barcode scanner — wraps html5-qrcode
window.goatQr = (function () {
    let scanner = null;
    let dotNetRef = null;

    return {
        start: async function (elementId, dotNetObjRef) {
            dotNetRef = dotNetObjRef;
            if (typeof Html5Qrcode === 'undefined') {
                throw new Error('html5-qrcode library not loaded');
            }
            if (scanner) { await this.stop(); }

            scanner = new Html5Qrcode(elementId);
            const config = { fps: 10, qrbox: { width: 280, height: 280 } };

            try {
                await scanner.start(
                    { facingMode: "environment" },
                    config,
                    (decodedText, decodedResult) => {
                        if (dotNetRef) {
                            dotNetRef.invokeMethodAsync('OnCodeDetected', decodedText);
                        }
                    },
                    // ignore per-frame failures silently
                    (_errorMsg) => { /* no-op */ }
                );
            } catch (err) {
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnScanError', String(err));
                throw err;
            }
            return true;
        },

        stop: async function () {
            if (scanner) {
                try { await scanner.stop(); } catch { /* already stopped */ }
                try { scanner.clear(); } catch { /* ignore */ }
                scanner = null;
            }
            dotNetRef = null;
        },

        hasCamera: async function () {
            if (typeof Html5Qrcode === 'undefined') return false;
            try {
                const devices = await Html5Qrcode.getCameras();
                return devices && devices.length > 0;
            } catch {
                return false;
            }
        }
    };
})();
