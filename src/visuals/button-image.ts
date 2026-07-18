/** SVG data-URL helpers for Stream Deck key artwork. */

function svgDataUrl(svg: string): string {
  return `data:image/svg+xml;charset=utf8,${encodeURIComponent(svg)}`;
}

function escapeXml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function darken(hex: string, amount: number): string {
  const raw = hex.replace("#", "");
  if (raw.length !== 6) return "#222222";
  const n = Number.parseInt(raw, 16);
  const r = Math.max(0, Math.round(((n >> 16) & 0xff) * (1 - amount)));
  const g = Math.max(0, Math.round(((n >> 8) & 0xff) * (1 - amount)));
  const b = Math.max(0, Math.round((n & 0xff) * (1 - amount)));
  return `#${((r << 16) | (g << 8) | b).toString(16).padStart(6, "0")}`;
}

export type ButtonFace = {
  /** Background fill color (#RRGGBB). */
  color: string;
  /** Short label drawn centered (optional; title is usually set separately). */
  glyph?: string;
  /** Bright ring / brighter fill when the assigned mode/profile is active. */
  active?: boolean;
  /** Dim inactive appearance. */
  dim?: boolean;
};

/** Colored key face with optional active ring. */
export function buttonImage(face: ButtonFace): string {
  const base = face.color.startsWith("#") ? face.color : `#${face.color}`;
  const fill = face.dim ? darken(base, 0.55) : face.active ? base : darken(base, 0.25);
  const ring = face.active ? "#FFFFFF" : "rgba(255,255,255,0.22)";
  const ringWidth = face.active ? 5 : 2;
  const glyph = face.glyph ? escapeXml(face.glyph) : "";
  const glyphSize = glyph.length <= 3 ? 22 : glyph.length <= 5 ? 16 : 13;

  const svg = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">
  <defs>
    <radialGradient id="g" cx="35%" cy="30%" r="75%">
      <stop offset="0%" stop-color="#ffffff" stop-opacity="0.22"/>
      <stop offset="100%" stop-color="#000000" stop-opacity="0.35"/>
    </radialGradient>
  </defs>
  <rect width="144" height="144" rx="28" fill="${fill}"/>
  <rect width="144" height="144" rx="28" fill="url(#g)"/>
  <rect x="8" y="8" width="128" height="128" rx="22" fill="none" stroke="${ring}" stroke-width="${ringWidth}"/>
  ${
    glyph
      ? `<text x="72" y="78" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="${glyphSize}" font-weight="700" fill="#ffffff">${glyph}</text>`
      : ""
  }
</svg>`;

  return svgDataUrl(svg);
}

export const ANC_COLORS: Record<number, string> = {
  0: "#45454A", // OFF
  1: "#1E9B50", // ANC
  2: "#2F7DCF", // Talk Through
  3: "#C88A1E", // Ambient
};

export const ANC_GLYPHS: Record<number, string> = {
  0: "OFF",
  1: "ANC",
  2: "TT",
  3: "AMB",
};

export function shortTitle(text: string, max = 10): string {
  const t = text.trim();
  if (t.length <= max) return t;
  return `${t.slice(0, max - 1)}…`;
}
