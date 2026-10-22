# Better AirPlay website

Vite, React, Tailwind v4 and shadcn (base-nova). Deployed to GitHub Pages by `.github/workflows/pages.yml`.

```sh
bun install
bun run dev      # http://localhost:5173
bun run lint     # oxlint with @shadcn/lint: no arbitrary values, raw colors, unknown classes or restyled components outside src/components/ui
bun run build
```

The hero scene (`src/components/hero-scene.tsx`) is drawn in plain CSS, so the unknown-classes rule is off for that file only.
