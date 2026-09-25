import { describe, expect, it } from 'vitest';
import type { BoardChangeSet, BoardElement, BoardSnapshot } from '../model';
import {
  boardOpened,
  boardSlice,
  changeSetReceived,
  contentReplaced,
  opAcknowledged,
  opApplied,
  resyncStarted,
  snapshotLoaded,
  type BoardState,
} from './boardSlice';
import type { ContentOp } from './ops';

const reduce = boardSlice.reducer;
const boardId = 'b1';
const ana = { kind: 'account' as const, id: 'ana', name: 'Ana' };

function element(id: string, overrides: Partial<BoardElement> = {}): BoardElement {
  return {
    id,
    type: 'domain-event',
    text: id,
    x: 0,
    y: 0,
    width: 160,
    height: 100,
    pivotal: false,
    color: null,
    version: 1,
    updatedAt: '2026-09-25T10:00:00Z',
    updatedBy: ana,
    ...overrides,
  };
}

function snapshot(revision: number, elements: BoardElement[]): BoardSnapshot {
  return {
    board: {
      id: boardId,
      teamId: 't1',
      name: 'Ordering',
      level: 'big-picture',
      revision,
      elementCount: elements.length,
      createdAt: '2026-09-25T10:00:00Z',
      createdBy: ana,
      updatedAt: '2026-09-25T10:00:00Z',
      updatedBy: ana,
      archivedAt: null,
    },
    permission: 'edit',
    elements,
    connections: [],
  };
}

function changes(revision: number, set: Partial<BoardChangeSet>): BoardChangeSet {
  return { boardId, revision, operationId: null, actor: ana, elements: [], removedElements: [], connections: [], removedConnections: [], ...set };
}

function loaded(revision: number, elements: BoardElement[]): BoardState {
  return reduce(reduce(undefined, boardOpened(boardId)), snapshotLoaded(snapshot(revision, elements)));
}

const shown = (state: BoardState, id: string) => state.elements.entities[id];
const move = (id: string, x: number, y: number): ContentOp => ({ kind: 'move', moves: [{ id, x, y }] });

describe('board content', () => {
  it('shows an operation at once, before the server answers', () => {
    const state = reduce(loaded(3, [element('e1')]), opApplied({ opId: 'op1', op: move('e1', 200, 40) }));
    expect(shown(state, 'e1')).toMatchObject({ x: 200, y: 40 });
    expect(state.pending).toHaveLength(1);
  });

  it('rolls an operation back when the hub refuses it', () => {
    let state = reduce(loaded(3, [element('e1')]), opApplied({ opId: 'op1', op: move('e1', 200, 40) }));
    state = reduce(state, opAcknowledged({ opId: 'op1', ok: false }));
    expect(shown(state, 'e1')).toMatchObject({ x: 0, y: 0 });
    expect(state.pending).toHaveLength(0);
  });

  it('keeps an acknowledged operation until its broadcast brings the committed image', () => {
    let state = reduce(loaded(3, [element('e1')]), opApplied({ opId: 'op1', op: move('e1', 200, 40) }));
    state = reduce(state, opAcknowledged({ opId: 'op1', ok: true }));
    expect(shown(state, 'e1')).toMatchObject({ x: 200, y: 40 });

    state = reduce(state, changeSetReceived(changes(4, { operationId: 'op1', elements: [element('e1', { x: 200, y: 40, version: 2 })] })));
    expect(shown(state, 'e1')).toMatchObject({ x: 200, y: 40, version: 2 });
  });

  it('drops a pending operation when its own broadcast arrives before the ack', () => {
    let state = reduce(loaded(3, [element('e1')]), opApplied({ opId: 'op1', op: move('e1', 200, 40) }));
    state = reduce(state, changeSetReceived(changes(4, { operationId: 'op1', elements: [element('e1', { x: 200, y: 40, version: 2 })] })));
    expect(state.pending).toHaveLength(0);

    state = reduce(state, opAcknowledged({ opId: 'op1', ok: true }));
    expect(shown(state, 'e1')).toMatchObject({ x: 200, y: 40, version: 2 });
  });

  it('keeps showing a pending change on top of someone else’s change to the same element', () => {
    let state = reduce(loaded(3, [element('e1')]), opApplied({ opId: 'op1', op: { kind: 'update', id: 'e1', patch: { text: 'Order Placed' } } }));
    state = reduce(state, changeSetReceived(changes(4, { elements: [element('e1', { x: 500, version: 2 })] })));
    expect(shown(state, 'e1')).toMatchObject({ x: 500, text: 'Order Placed' });
  });

  it('never lets an older change overwrite a newer one', () => {
    let state = loaded(3, [element('e1')]);
    state = reduce(state, changeSetReceived(changes(5, { elements: [element('e1', { x: 50, version: 3 })] })));
    state = reduce(state, changeSetReceived(changes(4, { elements: [element('e1', { x: 40, version: 2 })] })));
    expect(shown(state, 'e1')).toMatchObject({ x: 50 });
  });

  it('removes what someone else deleted, and brings it back if it is added again later', () => {
    let state = loaded(3, [element('e1')]);
    state = reduce(state, changeSetReceived(changes(4, { removedElements: [{ id: 'e1', version: 1 }] })));
    expect(shown(state, 'e1')).toBeUndefined();

    state = reduce(state, changeSetReceived(changes(6, { elements: [element('e1', { version: 1 })] })));
    expect(shown(state, 'e1')).toBeDefined();
  });

  it('holds changes during a resync and applies only those newer than the snapshot', () => {
    let state = loaded(3, [element('e1')]);
    state = reduce(state, resyncStarted());
    state = reduce(state, changeSetReceived(changes(4, { elements: [element('stale', { version: 1 })] })));
    state = reduce(state, changeSetReceived(changes(6, { elements: [element('e1', { x: 90, version: 3 })] })));
    expect(shown(state, 'e1')).toMatchObject({ x: 0 });

    state = reduce(state, snapshotLoaded(snapshot(5, [element('e1', { x: 10, version: 2 })])));
    expect(shown(state, 'stale')).toBeUndefined();
    expect(shown(state, 'e1')).toMatchObject({ x: 90 });
  });

  it('re-applies pending operations on top of a fresh snapshot', () => {
    let state = reduce(loaded(3, [element('e1')]), opApplied({ opId: 'op1', op: move('e1', 200, 40) }));
    state = reduce(state, resyncStarted());
    state = reduce(state, snapshotLoaded(snapshot(7, [element('e1', { x: 5, version: 4 })])));
    expect(shown(state, 'e1')).toMatchObject({ x: 200, y: 40 });
    expect(state.pending).toHaveLength(1);
  });

  it('keeps the identity of elements a resync did not change', () => {
    const before = loaded(3, [element('e1'), element('e2')]);
    const after = reduce(reduce(before, resyncStarted()), snapshotLoaded(snapshot(4, [element('e1'), element('e2', { x: 99, version: 2 })])));
    expect(after.elements.entities.e1).toBe(before.elements.entities.e1);
    expect(after.elements.entities.e2).not.toBe(before.elements.entities.e2);
  });

  it('removes connections locally with the elements they touch', () => {
    let state = loaded(3, [element('e1'), element('e2')]);
    state = reduce(state, changeSetReceived(changes(4, { connections: [{ id: 'c1', from: 'e1', to: 'e2', label: null, version: 1 }] })));
    state = reduce(state, opApplied({ opId: 'op1', op: { kind: 'delete', elementIds: ['e1'], connectionIds: ['c1'] } }));
    expect(state.connections.ids).toEqual([]);

    state = reduce(state, opAcknowledged({ opId: 'op1', ok: false }));
    expect(state.connections.ids).toEqual(['c1']);
    expect(shown(state, 'e1')).toBeDefined();
  });

  it('drops pending work aimed at content that was replaced', () => {
    let state = reduce(loaded(3, [element('e1')]), opApplied({ opId: 'op1', op: move('e1', 200, 40) }));
    state = reduce(state, contentReplaced({ boardId, revision: 9, actor: ana }));
    state = reduce(state, snapshotLoaded(snapshot(9, [element('n1')])));
    expect(state.pending).toHaveLength(0);
    expect(state.elements.ids).toEqual(['n1']);
  });

  it('ignores changes for another board', () => {
    const state = reduce(loaded(3, [element('e1')]), changeSetReceived({ ...changes(9, { removedElements: [{ id: 'e1', version: 1 }] }), boardId: 'other' }));
    expect(shown(state, 'e1')).toBeDefined();
  });
});
