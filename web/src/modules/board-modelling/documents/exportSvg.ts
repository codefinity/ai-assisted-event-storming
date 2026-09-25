import { arrowBetween } from '../canvas/ConnectionLayer';
import type { BoardConnection, BoardElement } from '../model';
import { layerOf, typeOf, type NotationIndex } from '../notation/notation';
import { boundsOf } from '../board/selectors';

const padding = 48;
const fontSize = 14;
/** An estimate of one character's width in the export font; SVG has no text wrapping of its own. */
const charWidth = 7.4;

function escapeXml(value: string): string {
  return value.replace(/[<>&"']/g, (char) => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', '"': '&quot;', "'": '&apos;' })[char]!);
}

export function wrap(text: string, width: number, maxLines: number): string[] {
  const perLine = Math.max(4, Math.floor(width / charWidth));
  const lines: string[] = [];
  for (const paragraph of text.split(/\r?\n/)) {
    let line = '';
    for (const word of paragraph.split(/\s+/).filter(Boolean)) {
      const candidate = line ? `${line} ${word}` : word;
      if (candidate.length <= perLine) {
        line = candidate;
      } else {
        if (line) lines.push(line);
        line = word.length > perLine ? `${word.slice(0, perLine - 1)}…` : word;
      }
    }
    lines.push(line);
  }

  if (lines.length > maxLines) {
    const kept = lines.slice(0, maxLines);
    kept[maxLines - 1] = `${kept[maxLines - 1]!.slice(0, perLine - 1)}…`;
    return kept;
  }

  return lines;
}

function textBlock(lines: string[], x: number, y: number, weight: number): string {
  return `<text x="${x}" y="${y}" font-size="${fontSize}" font-weight="${weight}">${lines
    .map((line, index) => `<tspan x="${x}" dy="${index === 0 ? 0 : fontSize * 1.3}">${escapeXml(line)}</tspan>`)
    .join('')}</text>`;
}

/** The board as a standalone SVG: lanes, then boundaries, arrows, stickies - the order they are drawn on screen. */
export function boardToSvg(elements: readonly BoardElement[], connections: readonly BoardConnection[], index: NotationIndex | undefined, title: string): string {
  const bounds = boundsOf(elements) ?? { x: 0, y: 0, width: 400, height: 200 };
  const x0 = bounds.x - padding;
  const y0 = bounds.y - padding;
  const width = bounds.width + padding * 2;
  const height = bounds.height + padding * 2;
  const byId = new Map(elements.map((element) => [element.id, element]));
  const ordered = [...elements].sort((a, b) => layerOf(typeOf(index, a.type)) - layerOf(typeOf(index, b.type)));

  const parts: string[] = [];
  for (const element of ordered) {
    const type = typeOf(index, element.type);
    const fill = element.color ?? type.color;
    const label = element.text || type.name;
    if (type.renderer === 'lane') {
      parts.push(
        `<g><rect x="${element.x}" y="${element.y}" width="${element.width}" height="${element.height}" fill="${fill}" fill-opacity="0.45"/>`,
        textBlock(wrap(label, 130, 3), element.x + 12, element.y + 24, 600),
        '</g>',
      );
    } else if (type.renderer === 'area') {
      parts.push(
        `<g><rect x="${element.x}" y="${element.y}" width="${element.width}" height="${element.height}" rx="12" fill="${fill}" fill-opacity="0.35" stroke="#000" stroke-opacity="0.28" stroke-width="2" stroke-dasharray="8 6"/>`,
        textBlock(wrap(label, element.width - 24, 1), element.x + 14, element.y + 26, 600),
        '</g>',
      );
    }
  }

  for (const connection of connections) {
    const from = byId.get(connection.from);
    const to = byId.get(connection.to);
    if (!from || !to) continue;
    const { x1, y1, x2, y2 } = arrowBetween(from, to);
    parts.push(`<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="#495057" stroke-width="2" marker-end="url(#arrow)"/>`);
    if (connection.label) {
      parts.push(`<text x="${(x1 + x2) / 2}" y="${(y1 + y2) / 2 - 6}" font-size="12" text-anchor="middle" fill="#343a40">${escapeXml(connection.label)}</text>`);
    }
  }

  for (const element of ordered) {
    const type = typeOf(index, element.type);
    if (type.renderer === 'lane' || type.renderer === 'area') continue;
    const fill = element.color ?? type.color;
    const inset = element.pivotal ? 16 : 10;
    parts.push(
      `<g><rect x="${element.x}" y="${element.y}" width="${element.width}" height="${element.height}" rx="3" fill="${fill}"/>`,
      element.pivotal ? `<rect x="${element.x}" y="${element.y}" width="6" height="${element.height}" fill="#000" fill-opacity="0.3"/>` : '',
      `<text x="${element.x + inset}" y="${element.y + 18}" font-size="10" font-weight="600" fill-opacity="0.55" letter-spacing="0.2">${escapeXml(type.name.toUpperCase())}</text>`,
      textBlock(wrap(element.text, element.width - inset - 10, Math.max(1, Math.floor((element.height - 34) / (fontSize * 1.3)))), element.x + inset, element.y + 38, 500),
      '</g>',
    );
  }

  return [
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="${x0} ${y0} ${width} ${height}" font-family="system-ui, -apple-system, 'Segoe UI', Roboto, Arial, sans-serif" fill="#1b1b1f">`,
    `<title>${escapeXml(title)}</title>`,
    '<defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="#495057"/></marker></defs>',
    `<rect x="${x0}" y="${y0}" width="${width}" height="${height}" fill="#f7f7f5"/>`,
    ...parts,
    '</svg>',
  ].join('\n');
}
