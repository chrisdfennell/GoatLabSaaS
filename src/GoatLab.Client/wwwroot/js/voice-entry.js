// GoatLab Voice Entry — Web Speech API wrapper
window.goatVoice = (function () {
    let recognition = null;
    let dotNetRef = null;
    let active = false;

    function getRecognition() {
        const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!Recognition) return null;
        const r = new Recognition();
        r.continuous = false;
        r.interimResults = false;
        r.lang = 'en-US';
        return r;
    }

    return {
        isSupported: function () {
            return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
        },

        start: function (dotNetObjRef) {
            if (active) return false;
            recognition = getRecognition();
            if (!recognition) return false;

            dotNetRef = dotNetObjRef;
            active = true;

            recognition.onresult = (event) => {
                const transcript = event.results[0][0].transcript;
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnVoiceTranscript', transcript);
                }
            };
            recognition.onerror = (event) => {
                active = false;
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnVoiceError', String(event.error));
            };
            recognition.onend = () => {
                active = false;
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnVoiceEnd');
            };
            recognition.start();
            return true;
        },

        stop: function () {
            if (recognition && active) { try { recognition.stop(); } catch { } }
            active = false;
        },

        // Parse a number from a spoken phrase (e.g. "4.5", "four and a half", "log seven")
        parseNumber: function (text) {
            if (!text) return null;
            const lower = text.toLowerCase();
            // direct numeric
            const numMatch = lower.match(/-?\d+(\.\d+)?/);
            if (numMatch) return parseFloat(numMatch[0]);
            // written numbers
            const words = {
                zero: 0, one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8, nine: 9, ten: 10,
                eleven: 11, twelve: 12, thirteen: 13, fourteen: 14, fifteen: 15, sixteen: 16, seventeen: 17, eighteen: 18, nineteen: 19,
                twenty: 20, thirty: 30, forty: 40, fifty: 50, sixty: 60, seventy: 70, eighty: 80, ninety: 90, hundred: 100
            };
            let total = 0, hit = false;
            for (const tok of lower.split(/\s+/)) {
                if (tok in words) { total += words[tok]; hit = true; }
            }
            // "half" handling
            if (lower.includes('and a half') || lower.includes('point five')) { total += 0.5; hit = true; }
            return hit ? total : null;
        }
    };
})();
