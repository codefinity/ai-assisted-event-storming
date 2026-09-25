import { describe, expect, it } from 'vitest';
import { notation } from '@/test/fixtures';
import { indexNotation, layerOf, paletteFor, typeOf, unknownType } from './notation';

describe('notation', () => {
  const index = indexNotation(notation);

  it('offers each level its own palette, in the registry’s order, with the rest under More', () => {
    const { primary, more } = paletteFor(index, 'process-modelling');
    expect(primary.map((type) => type.id)).toEqual(['domain-event', 'command', 'hot-spot']);
    expect(more.map((type) => type.id)).toEqual(['lane', 'area']);
  });

  it('finds types by their shortcut letter, whatever the case', () => {
    expect(index.byShortcut.get('e')?.id).toBe('domain-event');
    expect(index.byShortcut.get('l')?.id).toBe('lane');
  });

  it('draws lanes behind boundaries behind stickies, by renderer', () => {
    expect(layerOf(index.byId.get('lane'))).toBeLessThan(layerOf(index.byId.get('area')));
    expect(layerOf(index.byId.get('area'))).toBeLessThan(layerOf(index.byId.get('hot-spot')));
  });

  it('falls back to a readable type for one the registry no longer has', () => {
    expect(typeOf(index, 'retired-type')).toBe(unknownType);
  });

  it('builds the index once per notation', () => {
    expect(indexNotation(notation)).toBe(index);
  });
});
