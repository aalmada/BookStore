const CART_KEY = 'bookstore:anonymous-cart';
const MAX_QUANTITY = 10;

function getCart() {
    try {
        const stored = localStorage.getItem(CART_KEY);
        const parsed = stored ? JSON.parse(stored) : [];
        return Array.isArray(parsed) ? parsed : [];
    } catch {
        return [];
    }
}

function setCart(items) {
    localStorage.setItem(CART_KEY, JSON.stringify(items));
}

function normalizeQuantity(quantity) {
    const parsed = Number(quantity);
    if (!Number.isFinite(parsed)) {
        return 1;
    }

    return Math.min(Math.max(Math.trunc(parsed), 1), MAX_QUANTITY);
}

window.anonymousCart = {
    addItem(bookId, quantity) {
        const items = getCart();
        const normalizedQuantity = normalizeQuantity(quantity);
        const existing = items.find(item => item.bookId === bookId);

        if (existing) {
            existing.quantity = Math.min(existing.quantity + normalizedQuantity, MAX_QUANTITY);
        } else {
            items.push({ bookId, quantity: normalizedQuantity });
        }

        setCart(items);
        return items;
    },

    removeItem(bookId) {
        const items = getCart().filter(item => item.bookId !== bookId);
        setCart(items);
        return items;
    },

    updateItem(bookId, quantity) {
        const items = getCart();
        const existing = items.find(item => item.bookId === bookId);

        if (existing) {
            existing.quantity = normalizeQuantity(quantity);
            setCart(items);
        }

        return items;
    },

    clear() {
        localStorage.removeItem(CART_KEY);
    },

    getItems() {
        return getCart();
    },

    getCount() {
        return getCart().reduce((sum, item) => sum + normalizeQuantity(item.quantity), 0);
    }
};
