'use client';

import { createContext, useContext, type RefObject } from 'react';
import type { NotationIndex } from '../notation/notation';

/** What the parts of the editor share and that is not state: the notation, the canvas, the pointer. */
export interface BoardContextValue {
  index: NotationIndex | undefined;
  canEdit: boolean;
  canvasRef: RefObject<HTMLDivElement | null>;
  /** Where the pointer is on the board while it is over the canvas (world coordinates). */
  pointerRef: RefObject<{ x: number; y: number } | null>;
  /** Pan and zoom so the given elements (or everything, when none are given) fill the view. */
  fitTo(ids?: readonly string[]): void;
  /** Pan so an element is fully in view, keeping the zoom. */
  reveal(id: string): void;
  /** Zoom around the middle of the canvas. */
  zoomBy(factor: number): void;
  /** The middle of what is on screen, in world coordinates. */
  viewCenter(): { x: number; y: number };
  /** Moves keyboard focus to an element. */
  focusElement(id: string): void;
}

export const BoardContext = createContext<BoardContextValue | null>(null);

export function useBoardContext(): BoardContextValue {
  const value = useContext(BoardContext);
  if (!value) throw new Error('useBoardContext must be used inside the board editor.');
  return value;
}
