import { describe, expect, it } from 'vitest';
import { element, notation } from '@/test/fixtures';
import { indexNotation } from '../notation/notation';
import { boardToSvg, wrap } from './exportSvg';

describe('SVG export', () => {
  const index = indexNotation(notation);

  it('draws every element, lanes first, and escapes text', () => {
    const svg = boardToSvg(
      [element('s', { text: 'Pay <now> & "later"', x: 40, y: 40 }), element('l', { type: 'lane', text: 'Customer', width: 1600, height: 240 })],
      [{ id: 'c', from: 's', to: 'l', label: 'then', version: 1 }],
      index,
      'Ordering & more',
    );
    expect(svg.startsWith('<svg xmlns="http://www.w3.org/2000/svg"')).toBe(true);
    expect(svg).toContain('Pay &lt;now&gt; &amp;');
    expect(svg).toContain('&quot;later&quot;');
    expect(svg).toContain('<title>Ordering &amp; more</title>');
    expect(svg.indexOf('Customer')).toBeLessThan(svg.indexOf('Pay &lt;now'));
    expect(svg).toContain('marker-end="url(#arrow)"');
    expect(new DOMParser().parseFromString(svg, 'image/svg+xml').querySelector('parsererror')).toBeNull();
  });

  it('wraps text to the sticky and ends a cut line with an ellipsis', () => {
    const lines = wrap('Order placed by the customer on the website after checkout', 100, 2);
    expect(lines).toHaveLength(2);
    expect(lines[1]!.endsWith('…')).toBe(true);
  });
});
