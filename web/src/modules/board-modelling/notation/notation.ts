import type { BoardLevel } from '../model';

/** One kind of element, exactly as the registry describes it. Nothing about a type is hardcoded here. */
export interface ElementType {
  id: string;
  name: string;
  category: 'sticky' | 'structure';
  /** How it is drawn: "sticky", "lane" or "area". Unknown renderers fall back to a sticky. */
  renderer: string;
  color: string;
  textColor: string;
  icon: string;
  defaultSize: { width: number; height: number };
  levels: BoardLevel[];
  shortcut: string | null;
  maxTextLength: number;
  canBePivotal: boolean;
  layoutRole: 'item' | 'lane' | 'boundary';
  description: string;
  whenToUse: string;
  writingRule: string | null;
  examples: string[];
}

export interface LevelInfo {
  id: BoardLevel;
  name: string;
  description: string;
  /** The type ids offered in the palette at this level, in order. */
  palette: string[];
}

export interface Notation {
  types: ElementType[];
  levels: LevelInfo[];
}

/** Lookups built once per notation. */
export interface NotationIndex {
  notation: Notation;
  byId: ReadonlyMap<string, ElementType>;
  byShortcut: ReadonlyMap<string, ElementType>;
  levels: ReadonlyMap<BoardLevel, LevelInfo>;
}

const cache = new WeakMap<Notation, NotationIndex>();

export function indexNotation(notation: Notation): NotationIndex {
  let index = cache.get(notation);
  if (!index) {
    index = {
      notation,
      byId: new Map(notation.types.map((type) => [type.id, type])),
      byShortcut: new Map(
        notation.types.filter((type) => type.shortcut).map((type) => [type.shortcut!.toLowerCase(), type]),
      ),
      levels: new Map(notation.levels.map((level) => [level.id, level])),
    };
    cache.set(notation, index);
  }

  return index;
}

/** The types to offer first at a level; every other type stays available under "More". */
export function paletteFor(index: NotationIndex, level: BoardLevel): { primary: ElementType[]; more: ElementType[] } {
  const ids = index.levels.get(level)?.palette ?? index.notation.types.map((type) => type.id);
  const primary = ids.map((id) => index.byId.get(id)).filter((type): type is ElementType => type !== undefined);
  const more = index.notation.types.filter((type) => !ids.includes(type.id));
  return { primary, more };
}

/** Structures (lanes, boundaries) sit behind stickies. Higher draws later. */
export function layerOf(type: ElementType | undefined): number {
  switch (type?.renderer) {
    case 'lane':
      return 0;
    case 'area':
      return 1;
    default:
      return 2;
  }
}

/** A readable fallback for an element whose type the registry no longer knows. */
export const unknownType: ElementType = {
  id: 'unknown',
  name: 'Unknown element',
  category: 'sticky',
  renderer: 'sticky',
  color: '#E9ECEF',
  textColor: '#1B1B1F',
  icon: 'box',
  defaultSize: { width: 160, height: 100 },
  levels: [],
  shortcut: null,
  maxTextLength: 1000,
  canBePivotal: false,
  layoutRole: 'item',
  description: 'This element has a type the notation no longer defines.',
  whenToUse: '',
  writingRule: null,
  examples: [],
};

export function typeOf(index: NotationIndex | undefined, id: string): ElementType {
  return index?.byId.get(id) ?? unknownType;
}
