/**
 * Loads a Google Font dynamically by upserting a <link> tag in <head>.
 * Called whenever the active tenant changes its font family setting.
 *
 * @param {string} fontFamily - The Google Fonts family name (e.g. "Inter", "Playfair Display").
 */
export function loadGoogleFont(fontFamily) {
    if (!fontFamily || fontFamily.trim() === '') return;

    const id = 'tenant-font';
    const url = `https://fonts.googleapis.com/css2?family=${encodeURIComponent(fontFamily)}:wght@300;400;500;600;700&display=swap`;

    let link = document.getElementById(id);

    if (!link) {
        link = document.createElement('link');
        link.id = id;
        link.rel = 'stylesheet';
        document.head.appendChild(link);
    }

    if (link.href !== url) {
        link.href = url;
    }
}
