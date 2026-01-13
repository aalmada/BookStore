# Web — concise instructions

Apply when editing frontend (`src/BookStore.Web/**`). Keep UI & accessibility top of mind.

Key points:

- Use semantic HTML and ARIA where needed; ensure keyboard navigation and contrast.
- Prefer mobile-first responsive design; test common breakpoints.
- Call backend via client layer; handle failures with clear user messages.
- Prepare UI text for localization; avoid hard-coded user-visible strings.
- Do not embed secrets in client code; optimize images and lazy-load heavy assets.

See `docs/localization-guide.md` and `docs/aspire-guide.md` for Dev/Env notes.
Security & best practices
