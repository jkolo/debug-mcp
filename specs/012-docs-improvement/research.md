# Research: Documentation Improvement

## 1. Docusaurus Mermaid Plugin

**Decision**: Use `@docusaurus/theme-mermaid` (official Docusaurus plugin).

**Configuration**:
```typescript
// docusaurus.config.ts
export default {
  markdown: { mermaid: true },
  themes: ['@docusaurus/theme-mermaid'],  // top-level, NOT inside themeConfig
  themeConfig: {
    mermaid: {
      theme: { light: 'neutral', dark: 'dark' },
    },
  },
};
```

**Key findings**:
- Dark/light mode switching is automatic — configure via `mermaid.theme` with `light` and `dark` keys
- `themes` array MUST be at config root level (common mistake: nesting inside `themeConfig`)
- Installation: `npm install --save @docusaurus/theme-mermaid`
- Use standard ` ```mermaid ` code blocks in markdown — no special syntax needed

**Alternatives considered**:
- Raw SVG images: rejected (not maintainable as code, can't auto-theme)
- D2 diagrams: rejected (no Docusaurus plugin, requires external rendering)

---

## 2. Asciinema Player in Docusaurus

**Decision**: Use `asciinema-player` npm package with React `<BrowserOnly>` wrapper component.

**Implementation pattern**:
```tsx
// src/components/AsciinemaPlayer.tsx
import BrowserOnly from '@docusaurus/BrowserOnly';
import 'asciinema-player/dist/bundle/asciinema-player.css';

export default function AsciinemaPlayer({ src, ...opts }) {
  return (
    <BrowserOnly fallback={<div>Loading recording...</div>}>
      {() => {
        const Player = require('asciinema-player');
        const ref = useRef(null);
        useEffect(() => {
          Player.create(src, ref.current, opts);
        }, [src]);
        return <div ref={ref} />;
      }}
    </BrowserOnly>
  );
}
```

**Key findings**:
- Must use `BrowserOnly` wrapper to prevent SSR build failures
- Must use `AsciinemaPlayer.create()` API, not custom HTML element
- Must import CSS: `asciinema-player/dist/bundle/asciinema-player.css`
- Local `.cast` files go in `static/casts/`, referenced as `/casts/demo.cast`
- Key props: `autoPlay`, `speed`, `idleTimeLimit`, `theme`, `fit`, `poster` (preview frame via `"npt:0:03"`)
- Installation: `npm install asciinema-player`

**Alternatives considered**:
- iframe embed from asciinema.org: rejected (dependency on external service, FR-004 requires self-hosting)
- GIF recordings: rejected (not interactive, large file size, no copy-paste)

---

## 3. Asciinema CLI Recording

**Decision**: Install via pacman (Arch Linux), record with `asciinema rec`.

**Recording workflow**:
```bash
# Install
sudo pacman -S asciinema

# Record
asciinema rec static/casts/demo.cast --idle-time-limit 2 --title "Demo Session"

# Replay to verify
asciinema play static/casts/demo.cast
```

**Key findings**:
- asciicast v2 format: newline-delimited JSON (header + `[time, type, data]` events)
- Typical size: 5-50 KB for 30s, 50-500 KB for 5 min — fine for repo storage
- `--idle-time-limit` compresses pauses (essential for documentation recordings)
- Exit recording with `Ctrl+D` or `exit`
- Files are plain text, diffable, and git-friendly

**Alternatives considered**:
- VHS (charmbracelet): generates GIF from script — rejected (not interactive player)
- termtosvg: abandoned project — rejected
