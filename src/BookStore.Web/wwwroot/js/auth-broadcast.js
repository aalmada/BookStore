// BroadcastChannel for cross-tab authentication synchronization
// This enables instant logout/login synchronization across multiple browser tabs

const AUTH_CHANNEL_NAME = 'bookstore-auth';

class AuthBroadcast {
    constructor() {
        // Check if BroadcastChannel is supported
        if (typeof BroadcastChannel === 'undefined') {
            console.warn('BroadcastChannel API not supported in this browser');
            this.channel = null;
            return;
        }

        // Create the broadcast channel
        this.channel = new BroadcastChannel(AUTH_CHANNEL_NAME);

        // Listen for messages from other tabs
        this.channel.onmessage = (event) => {
            this.handleMessage(event.data);
        };

        console.log('AuthBroadcast initialized');
    }

    /**
     * Handle incoming messages from other tabs
     */
    handleMessage(data) {
        if (!data || !data.type) {
            return;
        }

        switch (data.type) {
            case 'logout':
                console.log('Logout event received from another tab');
                // Dispatch custom event for Blazor to handle reactively
                window.dispatchEvent(new CustomEvent('auth-state-changed', {
                    detail: { type: 'logout', timestamp: data.timestamp }
                }));
                // Also redirect to login for smooth UX
                window.location.href = '/login';
                break;

            case 'login':
                console.log('Login event received from another tab');
                // Dispatch custom event for Blazor to handle reactively
                window.dispatchEvent(new CustomEvent('auth-state-changed', {
                    detail: { type: 'login', timestamp: data.timestamp }
                }));
                // Force auth state refresh without full page reload
                // The event listener in Blazor will trigger GetAuthenticationStateAsync
                break;

            default:
                console.warn('Unknown auth broadcast message type:', data.type);
        }
    }

    /**
     * Notify other tabs that the user has logged out
     */
    notifyLogout() {
        if (!this.channel) {
            console.warn('BroadcastChannel not available, skipping logout notification');
            return;
        }

        console.log('Broadcasting logout to other tabs');
        this.channel.postMessage({ type: 'logout', timestamp: Date.now() });
    }

    /**
     * Notify other tabs that the user has logged in
     */
    notifyLogin() {
        if (!this.channel) {
            console.warn('BroadcastChannel not available, skipping login notification');
            return;
        }

        console.log('Broadcasting login to other tabs');
        this.channel.postMessage({ type: 'login', timestamp: Date.now() });
    }

    /**
     * Close the broadcast channel (cleanup)
     */
    close() {
        if (this.channel) {
            this.channel.close();
            console.log('AuthBroadcast closed');
        }
    }
}

// Create global instance
window.authBroadcast = new AuthBroadcast();

// Cleanup on page unload
window.addEventListener('beforeunload', () => {
    if (window.authBroadcast) {
        window.authBroadcast.close();
    }
});
