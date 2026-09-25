import { describe, expect, it } from 'vitest';
import { boardClosed, boardOpened, opApplied } from '@/modules/board-modelling/board/boardSlice';
import { moveElements, updateElement } from '@/modules/board-modelling/editor/commands';
import { dragEnded, dragMoved, dragStarted, editStarted, pointerMoved } from '@/modules/board-modelling/editor/editorSlice';
import { ana, boardId, element, FakeHub, settle, snapshotOf, testStore } from '@/test/fixtures';

async function openBoard(hub = new FakeHub(), snapshot = () => snapshotOf([element('e1'), element('e2', { x: 300 })])) {
  const { store } = testStore({ hub, snapshot });
  store.dispatch(boardOpened(boardId));
  await settle();
  return { store, hub };
}

const changes = (revision: number, set: Record<string, unknown>) => ({
  boardId,
  revision,
  operationId: null,
  actor: ana,
  elements: [],
  removedElements: [],
  connections: [],
  removedConnections: [],
  ...set,
});

describe('realtime middleware', () => {
  it('joins the board, loads the snapshot and goes live', async () => {
    const { store, hub } = await openBoard();
    const state = store.getState();
    expect(hub.starts).toBe(1);
    expect(state.realtime.status).toBe('live');
    expect(state.board.phase).toBe('ready');
    expect(state.board.elements.ids).toEqual(['e1', 'e2']);
    expect(state.presence.you?.connectionId).toBe('me-1');
    expect(state.presence.participants.ids).toEqual(['bo-1']);
  });

  it('sends an operation to the matching hub method with the operation id', async () => {
    const { store, hub } = await openBoard();
    store.dispatch(moveElements([{ id: 'e1', x: 40, y: 20 }]));
    await settle();

    const [call] = hub.invocations;
    expect(call?.method).toBe('MoveElements');
    const request = call?.args[0] as { boardId: string; operationId: string; moves: unknown[] };
    expect(request.boardId).toBe(boardId);
    expect(request.moves).toEqual([{ elementId: 'e1', x: 40, y: 20 }]);
    expect(request.operationId).toBe(store.getState().board.pending[0]?.opId);
  });

  it('rolls back and explains when the hub refuses a change', async () => {
    const { store, hub } = await openBoard();
    store.dispatch(updateElement('e1', { text: 'Order Placed' }));
    await settle();
    expect(store.getState().board.elements.entities.e1?.text).toBe('Order Placed');

    hub.answer(false, [{ kind: 'conflict', code: 'board-archived', message: 'The board is archived.', fix: 'Restore it first.' }]);
    await settle();

    const state = store.getState();
    expect(state.board.elements.entities.e1?.text).toBe('e1');
    expect(state.notices.items).toEqual([expect.objectContaining({ tone: 'error', text: 'The board is archived.', fix: 'Restore it first.' })]);
  });

  it('applies what others change, as the server broadcasts it', async () => {
    const { store, hub } = await openBoard();
    hub.emit('boardChanged', changes(6, { elements: [element('e3', { text: 'Payment Taken', version: 1 })], removedElements: [{ id: 'e2', version: 1 }] }));
    expect(store.getState().board.elements.ids).toEqual(['e1', 'e3']);
  });

  it('resends unanswered operations after a reconnect, on top of the fresh snapshot', async () => {
    let revision = 5;
    const hub = new FakeHub();
    const { store } = await openBoard(hub, () => snapshotOf([element('e1'), element('e2', { x: 300, text: 'Changed while away' })], revision));
    store.dispatch(moveElements([{ id: 'e1', x: 90, y: 0 }]));
    await settle();
    expect(hub.invocations).toHaveLength(1);

    hub.drop();
    await settle();
    expect(store.getState().realtime.status).toBe('reconnecting');
    expect(store.getState().board.pending).toHaveLength(1);
    expect(store.getState().presence.you).toBeNull();

    revision = 9;
    hub.reconnect();
    await settle();

    const state = store.getState();
    expect(state.realtime.status).toBe('live');
    expect(state.board.floor).toBe(9);
    expect(state.board.elements.entities.e2?.text).toBe('Changed while away');
    expect(state.board.elements.entities.e1?.x).toBe(90);
    expect(hub.invocations.map((call) => call.method)).toEqual(['MoveElements']);
    expect((hub.invocations[0]?.args[0] as { operationId: string }).operationId).toBe(state.board.pending[0]?.opId);
  });

  it('reloads when someone replaces the whole board', async () => {
    let elements = [element('e1')];
    const hub = new FakeHub();
    const { store } = await openBoard(hub, () => snapshotOf(elements, elements.length === 1 ? 5 : 12));
    elements = [element('n1'), element('n2')];
    hub.emit('boardReplaced', { boardId, revision: 12, actor: ana });
    await settle();
    expect(store.getState().board.elements.ids).toEqual(['n1', 'n2']);
  });

  it('shares the cursor, the editing focus and drag previews', async () => {
    const { store, hub } = await openBoard();
    store.dispatch(pointerMoved({ x: 10, y: 20 }));
    store.dispatch(editStarted('e1'));
    store.dispatch(dragStarted(['e2']));
    store.dispatch(dragMoved({ dx: 5, dy: 0 }));
    store.dispatch(dragEnded());

    expect(hub.sent).toEqual([
      { method: 'MoveCursor', args: [boardId, 10, 20] },
      { method: 'SetEditingFocus', args: [boardId, 'e1'] },
      { method: 'ShareDragPreview', args: [boardId, [{ elementId: 'e2', x: 305, y: 0 }]] },
      { method: 'ShareDragPreview', args: [boardId, []] },
    ]);
  });

  it('shows where others are dragging until their move is committed', async () => {
    const { store, hub } = await openBoard();
    hub.emit('dragPreviewed', { boardId, connectionId: 'bo-1', moves: [{ elementId: 'e1', x: 50, y: 60 }] });
    expect(store.getState().presence.drags['bo-1']).toHaveLength(1);

    hub.emit('boardChanged', changes(6, { elements: [element('e1', { x: 50, y: 60, version: 2 })] }));
    expect(store.getState().presence.drags['bo-1']).toBeUndefined();
  });

  it('holds operations made before the board is live and sends them once it is', async () => {
    const hub = new FakeHub();
    const { store } = testStore({ hub });
    store.dispatch(boardOpened(boardId));
    store.dispatch(opApplied({ opId: 'early', op: { kind: 'move', moves: [{ id: 'e1', x: 1, y: 1 }] } }));
    expect(hub.invocations).toHaveLength(0);

    await settle();
    expect(hub.invocations.map((call) => (call.args[0] as { operationId: string }).operationId)).toEqual(['early']);
  });

  it('leaves the board and lets the connection go when the board is closed', async () => {
    const { store, hub } = await openBoard();
    store.dispatch(boardClosed());
    expect(hub.sent.at(-1)).toEqual({ method: 'LeaveBoard', args: [] });
    expect(store.getState().realtime.status).toBe('offline');
  });
});
