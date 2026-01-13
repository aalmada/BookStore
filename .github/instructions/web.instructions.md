---
applyTo: "src/BookStore.Web/**"
---

# Web path-specific instructions

These instructions apply when editing files under `src/BookStore.Web/` (frontend/web app).

Essentials:

- Use semantic HTML and ARIA attributes; ensure keyboard navigation and adequate contrast.
- Prefer mobile-first responsive design and test common breakpoints.
- Call backend via the client layer and surface clear user-facing error messages.
- Prepare UI text for localization; avoid hard-coded user-visible strings.

Security & performance:

- Do not embed secrets in client-side code. Optimize images and lazy-load heavy assets.

See `.github/prompts/web.md` and `docs/localization-guide.md` for more details.
