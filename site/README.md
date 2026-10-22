# Better AirPlay website

Vite, React, Tailwind v4 and shadcn (base-nova). Deployed to GitHub Pages by `.github/workflows/pages.yml`.

```sh
bun install
bun run dev      # http://localhost:5173
bun run lint     # oxlint with @shadcn/lint: no arbitrary values, raw colors, unknown classes or restyled components outside src/components/ui
bun run build
```

The hero scene (`src/components/hero-scene.tsx`) is drawn in plain CSS, so the unknown-classes rule is off for that file only.

## Link previews

`index.html` holds the page metadata. Its canonical URL, `og:url` and `og:image` are built from `VITE_SITE_URL` in `.env`.
Change that value if the site moves to a custom domain.

`public/og.png` is a screenshot of `og.html`, which renders `src/components/social-preview.tsx`. The build skips `og.html`.
To update the image, run the dev server, open http://localhost:5173/og.html in a 1200×630 window and save a screenshot
at 1× scale as `public/og.png`. With Chromium:

```sh
chromium --headless --hide-scrollbars --force-device-scale-factor=1 --window-size=1200,630 \
  --virtual-time-budget=4000 --screenshot=public/og.png http://localhost:5173/og.html
```
