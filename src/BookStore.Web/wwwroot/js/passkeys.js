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

// Utility to recursive convert object keys to camelCase
function normalizeKeys(obj) {
    if (Array.isArray(obj)) {
        return obj.map(v => normalizeKeys(v));
    } else if (obj !== null && obj.constructor === Object) {
        return Object.keys(obj).reduce((result, key) => {
            const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
            result[camelKey] = normalizeKeys(obj[key]);
            return result;
        }, {});
    }
    return obj;
}

window.passkey = {
    register: async (optionsJson) => {
        try {
            let parsed = JSON.parse(optionsJson);

            // Handle wrapped response format: { options: {...}, userId: "..." }
            let options = parsed.options || parsed.Options || parsed;

            // Normalize all keys to camelCase (handling PascalCase from server)
            options = normalizeKeys(options);

            let publicKey = options;
            if (window.PublicKeyCredential && window.PublicKeyCredential.parseCreationOptionsFromJSON) {
                publicKey = window.PublicKeyCredential.parseCreationOptionsFromJSON(options);
            } else {
                // Fix options for WebAuthn (Base64Url Strings -> Uint8Array)
                if (publicKey.challenge) {
                    publicKey.challenge = base64UrlToUint8Array(publicKey.challenge);
                }

                if (publicKey.user && publicKey.user.id) {
                    publicKey.user.id = base64UrlToUint8Array(publicKey.user.id);
                }

                if (publicKey.excludeCredentials) {
                    publicKey.excludeCredentials = publicKey.excludeCredentials.map(c => {
                        c.id = base64UrlToUint8Array(c.id);
                        return c;
                    });
                }
            }

            const credential = await navigator.credentials.create({
                publicKey: publicKey
            });

            const response = {
                id: credential.id,
                rawId: bufferToBase64Url(credential.rawId),
                type: credential.type,
                clientExtensionResults: credential.getClientExtensionResults(),
                response: {
                    attestationObject: bufferToBase64Url(credential.response.attestationObject),
                    clientDataJSON: bufferToBase64Url(credential.response.clientDataJSON),
                    userHandle: credential.response.userHandle ? bufferToBase64Url(credential.response.userHandle) : null
                }
            };

            if (credential.response.getTransports) {
                response.response.transports = credential.response.getTransports();
            }

            // Pass extensions/transports if needed
            if (credential.authenticatorAttachment) response.authenticatorAttachment = credential.authenticatorAttachment;

            const result = JSON.stringify(response);
            return result;
        } catch (error) {
            console.error('Passkey registration error', error);
            if (error.name === 'InvalidStateError' || error.name === 'ConstraintError') {
                throw new Error("This authenticator is already registered for this account.");
            }

            if (error.name === 'NotAllowedError') {
                throw new Error("Registration was cancelled or timed out.");
            }

            throw error instanceof Error ? error : new Error(String(error));
        }
    },

    login: async (optionsJson) => {
        try {
            let parsed = JSON.parse(optionsJson);

            // Handle wrapped response format
            let options = parsed.options || parsed.Options || parsed;

            // Normalize all keys to camelCase
            options = normalizeKeys(options);

            let publicKey = options;
            if (window.PublicKeyCredential && window.PublicKeyCredential.parseRequestOptionsFromJSON) {
                publicKey = window.PublicKeyCredential.parseRequestOptionsFromJSON(options);
            } else {
                publicKey.challenge = base64UrlToUint8Array(publicKey.challenge);

                if (publicKey.allowCredentials) {
                    publicKey.allowCredentials = publicKey.allowCredentials.map(c => {
                        c.id = base64UrlToUint8Array(c.id);
                        return c;
                    });
                }
            }

            const credential = await navigator.credentials.get({
                publicKey: publicKey
            });

            const response = {
                id: credential.id,
                rawId: bufferToBase64Url(credential.rawId),
                type: credential.type,
                clientExtensionResults: credential.getClientExtensionResults(),
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
            if (error.name === 'NotAllowedError') {
                throw new Error("Login was cancelled or timed out.");
            }
            throw error instanceof Error ? error : new Error(String(error));
        }
    },

    isSupported: async () => {
        return !!(window.PublicKeyCredential &&
            await window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable());
    }
};
