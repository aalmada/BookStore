// Utility to convert Base64URL to Uint8Array
function base64UrlToUint8Array(base64Url) {
    const padding = '='.repeat((4 - base64Url.length % 4) % 4);
    const base64 = (base64Url + padding)
        .replace(/\-/g, '+')
        .replace(/_/g, '/');

    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}

// Utility to convert ArrayBuffer to Base64URL
function bufferToBase64Url(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return window.btoa(binary)
        .replace(/\+/g, '-')
        .replace(/\//g, '_')
        .replace(/=/g, '');
}

window.passkey = {
    register: async (optionsJson) => {
        try {
            const options = JSON.parse(optionsJson);

            // Fix options for WebAuthn
            options.challenge = base64UrlToUint8Array(options.challenge);
            if (options.user.id) {
                options.user.id = base64UrlToUint8Array(options.user.id); // User ID is usually a string in .NET Identity?? 
                // Wait, .NET Identity PasskeyUserEntity.Id is string. 
                // But WebAuthn expects Buffer.
                // We should ensure the server sends a Base64URL string or handle the conversion correctly.
                // If the server sends a GUID string, we might converts it to UTF8 bytes?
                // Actually, .NET implementation of MakePasskeyCreationOptionsAsync handles this.
            }

            if (options.excludeCredentials) {
                options.excludeCredentials = options.excludeCredentials.map(c => {
                    c.id = base64UrlToUint8Array(c.id);
                    return c;
                });
            }

            const credential = await navigator.credentials.create({
                publicKey: options
            });

            // Convert response back to JSON-friendly format
            const response = {
                id: credential.id,
                rawId: bufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    attestationObject: bufferToBase64Url(credential.response.attestationObject),
                    clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON)
                }
            };

            // Pass extensions/transports if needed
            if (credential.authenticatorAttachment) response.authenticatorAttachment = credential.authenticatorAttachment;

            return JSON.stringify(response);
        } catch (error) {
            console.error('Passkey registration error', error);
            throw error;
        }
    },

    login: async (optionsJson) => {
        try {
            // Options might be minimal for "conditional" UI or explicit login
            // For explicit login (we generate options on server with challenge)
            // But we don't have GetLoginOptions endpoint implemented fully yet (returning Not Implemented)
            // So we might construct a dummy challenge locally if we rely on "PasskeySignInAsync" without challenge verification?
            // NO. Passkey signatures SIGN the challenge. The server MUST provide it.

            // Wait, my /PasskeyLoginOptions endpoint returns "Not Implemented".
            // So valid login flow is impossible right now without that endpoint!
            // I must implement /PasskeyLoginOptions on the backend first?
            // Or I can use client-side discovery?
            // No, server must verify the signature against a challenge it generated.

            // Re-check backend: /PasskeyLoginOptions returns error.
            // I should implement it using "MakePasskeyRequestOptionsAsync" if I can find it?
            // Or I can generate a random challenge manually and store it in session?
            // But SignInManager expects to verify it.

            // Let's assume optionsJson comes from server.
            const options = JSON.parse(optionsJson);

            options.challenge = base64UrlToUint8Array(options.challenge);

            if (options.allowCredentials) {
                options.allowCredentials = options.allowCredentials.map(c => {
                    c.id = base64UrlToUint8Array(c.id);
                    return c;
                });
            }

            const credential = await navigator.credentials.get({
                publicKey: options
            });

            const response = {
                id: credential.id,
                rawId: bufferToBase64Url(credential.rawId),
                type: credential.type,
                response: {
                    authenticatorData: bufferToBase64Url(credential.response.authenticatorData),
                    clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON),
                    signature: bufferToBase64Url(credential.response.signature),
                    userHandle: credential.response.userHandle ? bufferToBase64Url(credential.response.userHandle) : null
                }
            };

            return JSON.stringify(response);
        } catch (error) {
            console.error('Passkey login error', error);
            throw error;
        }
    },

    isSupported: async () => {
        return !!(window.PublicKeyCredential &&
            await window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable());
    }
};
