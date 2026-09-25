import { describe, expect, it } from 'vitest';
import { notation } from '@/test/fixtures';
import { indexNotation } from '../notation/notation';
import { parsePayload, payloadFromText, placePayload, type ClipboardPayload } from './clipboard';

const payload: ClipboardPayload = {
  format: 'eventstorming/elements@1',
  elements: [
    { id: 'a', type: 'domain-event', text: 'Order Placed', x: 100, y: 100, width: 160, height: 100, pivotal: false, color: null },
    { id: 'b', type: 'domain-event', text: 'Order Paid', x: 300, y: 100, width: 160, height: 100, pivotal: false, color: null },
  ],
  connections: [
    { id: 'c', from: 'a', to: 'b', label: null },
    { id: 'outside', from: 'a', to: 'somewhere-else', label: null },
  ],
};

describe('clipboard', () => {
  it('gives pasted elements new ids and keeps their arrows between them', () => {
    const placed = placePayload(payload, { x: 1000, y: 500 });
    const ids = placed.elements.map((element) => element.id);
    expect(ids).not.toContain('a');
    expect(new Set(ids).size).toBe(2);
    expect(placed.elements.map((element) => [element.x, element.y])).toEqual([
      [1000, 500],
      [1200, 500],
    ]);
    expect(placed.connections).toHaveLength(1);
    expect(placed.connections[0]).toMatchObject({ from: ids[0], to: ids[1] });
  });

  it('offsets repeated pastes of the same thing', () => {
    const first = placePayload(payload, null);
    const second = placePayload(payload, null);
    expect(second.elements[0]!.x - first.elements[0]!.x).toBe(24);
  });

  it('reads only its own format back', () => {
    expect(parsePayload(JSON.stringify(payload))).toEqual(payload);
    expect(parsePayload('{"format":"other"}')).toBeNull();
    expect(parsePayload('Order Placed')).toBeNull();
  });

  it('turns lines of plain text into a row of stickies of the given type', () => {
    const type = indexNotation(notation).byId.get('domain-event')!;
    const fromText = payloadFromText('Order Placed\n\n  Order Paid  \r\nOrder Shipped', type)!;
    expect(fromText.elements.map((element) => element.text)).toEqual(['Order Placed', 'Order Paid', 'Order Shipped']);
    expect(fromText.elements.every((element) => element.type === 'domain-event')).toBe(true);
    expect(fromText.elements[1]!.x).toBeGreaterThan(fromText.elements[0]!.x + fromText.elements[0]!.width);
    expect(payloadFromText('   \n ', type)).toBeNull();
  });
});
