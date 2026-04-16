// WebAuthn JS interop helpers for Blazor. Bridges the gap between the Fido2
// server JSON format and the browser's navigator.credentials API which expects
// ArrayBuffer, not base64url strings.

window.webAuthn = {
    // Convert a server-side CredentialCreateOptions JSON → browser create() call
    // → return the raw attestation response as a plain object safe for JSON roundtrip.
    register: async function (optionsJson) {
        const options = JSON.parse(optionsJson);

        // The browser needs ArrayBuffers, not base64url strings.
        options.challenge = base64urlToBuffer(options.challenge);
        options.user.id = base64urlToBuffer(options.user.id);
        if (options.excludeCredentials) {
            options.excludeCredentials = options.excludeCredentials.map(c => ({
                ...c,
                id: base64urlToBuffer(c.id),
            }));
        }

        const credential = await navigator.credentials.create({ publicKey: options });
        return {
            id: credential.id,
            rawId: bufferToBase64url(credential.rawId),
            type: credential.type,
            response: {
                attestationObject: bufferToBase64url(credential.response.attestationObject),
                clientDataJSON: bufferToBase64url(credential.response.clientDataJSON),
            },
            extensions: credential.getClientExtensionResults(),
        };
    },

    // Convert a server-side AssertionOptions JSON → browser get() call
    // → return the raw assertion response as a plain object.
    authenticate: async function (optionsJson) {
        const options = JSON.parse(optionsJson);
        options.challenge = base64urlToBuffer(options.challenge);
        if (options.allowCredentials) {
            options.allowCredentials = options.allowCredentials.map(c => ({
                ...c,
                id: base64urlToBuffer(c.id),
            }));
        }

        const assertion = await navigator.credentials.get({ publicKey: options });
        return {
            id: assertion.id,
            rawId: bufferToBase64url(assertion.rawId),
            type: assertion.type,
            response: {
                authenticatorData: bufferToBase64url(assertion.response.authenticatorData),
                clientDataJSON: bufferToBase64url(assertion.response.clientDataJSON),
                signature: bufferToBase64url(assertion.response.signature),
                userHandle: assertion.response.userHandle
                    ? bufferToBase64url(assertion.response.userHandle)
                    : null,
            },
            extensions: assertion.getClientExtensionResults(),
        };
    },

    isAvailable: function () {
        return window.PublicKeyCredential !== undefined;
    },
};

function base64urlToBuffer(base64url) {
    const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
    const pad = base64.length % 4 === 0 ? '' : '='.repeat(4 - (base64.length % 4));
    const binary = atob(base64 + pad);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return bytes.buffer;
}

function bufferToBase64url(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) binary += String.fromCharCode(bytes[i]);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
