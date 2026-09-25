import { describe, expect, it } from 'vitest';
import { boardOpened, changeSetReceived } from '../board/boardSlice';
import { ana, boardId, element, FakeHub, notation, settle, snapshotOf, testStore } from '@/test/fixtures';
import { indexNotation } from '../notation/notation';
import { changeType, connectElements, createElements, deleteConnection, deleteElements, moveElements, newElementOf, redo, undo, updateElement } from './commands';

const index = indexNotation(notation);

async function editing(elements = [element('e1', { pivotal: true }), element('e2', { x: 300 })]) {
  const hub = new FakeHub();
  hub.autoAck = true;
  const { store } = testStore({ hub, snapshot: () => snapshotOf(elements) });
  store.dispatch(boardOpened(boardId));
  await settle();
  return { store, hub, shown: () => store.getState().board.elements.entities };
}

describe('editing commands and per-person undo', () => {
  it('undoes and redoes an added element', async () => {
    const { store, shown } = await editing();
    const added = newElementOf(index.byId.get('domain-event')!, { x: 500, y: 50 }, 'Order Placed');
    store.dispatch(createElements([added]));
    expect(shown()[added.id]).toMatchObject({ text: 'Order Placed', x: 420, y: 0 });
    expect(store.getState().editor.selected).toEqual({ [added.id]: true });

    store.dispatch(undo());
    expect(shown()[added.id]).toBeUndefined();

    store.dispatch(redo());
    expect(shown()[added.id]).toMatchObject({ text: 'Order Placed' });
  });

  it('undoes only the fields that changed', async () => {
    const { store, hub, shown } = await editing();
    store.dispatch(updateElement('e1', { text: 'Order Placed', x: 0 }));
    await settle();
    const request = hub.invocations.length === 0 ? null : hub.invocations[0];
    expect(request).toBeNull();
    expect(store.getState().history.past.at(-1)).toMatchObject({ redo: { kind: 'update', patch: { text: 'Order Placed' } }, undo: { kind: 'update', patch: { text: 'e1' } } });

    store.dispatch(undo());
    expect(shown().e1?.text).toBe('e1');
  });

  it('records nothing for a change that changes nothing', async () => {
    const { store } = await editing();
    store.dispatch(updateElement('e1', { text: 'e1' }));
    store.dispatch(moveElements([{ id: 'e1', x: 0, y: 0 }]));
    expect(store.getState().history.past).toHaveLength(0);
    expect(store.getState().board.pending).toHaveLength(0);
  });

  it('sends a size as a pair even when only one side changed', async () => {
    const { store } = await editing();
    store.dispatch(updateElement('e1', { width: 200 }));
    expect(store.getState().board.pending.at(-1)?.op).toEqual({ kind: 'update', id: 'e1', patch: { width: 200, height: 100 } });
  });

  it('deletes the arrows with the elements, and brings both back on undo', async () => {
    const { store, shown } = await editing();
    store.dispatch(connectElements('e1', 'e2'));
    const [connectionId] = store.getState().board.connections.ids;
    expect(connectionId).toBeDefined();

    store.dispatch(deleteElements(['e1']));
    expect(shown().e1).toBeUndefined();
    expect(store.getState().board.connections.ids).toEqual([]);

    store.dispatch(undo());
    expect(shown().e1).toMatchObject({ pivotal: true });
    expect(store.getState().board.connections.ids).toEqual([connectionId]);
  });

  it('does not connect an element to itself or connect the same pair twice', async () => {
    const { store } = await editing();
    store.dispatch(connectElements('e1', 'e1'));
    store.dispatch(connectElements('e1', 'e2'));
    store.dispatch(connectElements('e1', 'e2'));
    expect(store.getState().board.connections.ids).toHaveLength(1);
  });

  it('brings a deleted arrow back on undo', async () => {
    const { store } = await editing();
    store.dispatch(connectElements('e1', 'e2'));
    const [connectionId] = store.getState().board.connections.ids;
    store.dispatch(deleteConnection(connectionId!));
    expect(store.getState().board.connections.ids).toEqual([]);
    store.dispatch(undo());
    expect(store.getState().board.connections.ids).toEqual([connectionId]);
  });

  it('takes the pivotal mark off when the new type cannot be pivotal', async () => {
    const { store, shown } = await editing();
    store.dispatch(changeType('e1', index.byId.get('hot-spot')!));
    expect(shown().e1).toMatchObject({ type: 'hot-spot', pivotal: false });
    store.dispatch(undo());
    expect(shown().e1).toMatchObject({ type: 'domain-event', pivotal: true });
  });

  it('undo reverts my change, not a later change someone else made to another field', async () => {
    const { store, shown } = await editing();
    store.dispatch(updateElement('e1', { text: 'Order Placed' }));
    await settle();
    store.dispatch(changeSetReceived({ boardId, revision: 9, operationId: null, actor: ana, elements: [element('e1', { text: 'Order Placed', x: 700, version: 3, pivotal: true })], removedElements: [], connections: [], removedConnections: [] }));

    store.dispatch(undo());
    expect(shown().e1).toMatchObject({ text: 'e1', x: 700 });
  });

  it('does nothing on a board the person cannot edit', async () => {
    const hub = new FakeHub();
    const { store } = testStore({ hub, snapshot: () => snapshotOf([element('e1')], 5, { permission: 'view' }) });
    store.dispatch(boardOpened(boardId));
    await settle();
    store.dispatch(moveElements([{ id: 'e1', x: 40, y: 40 }]));
    store.dispatch(deleteElements(['e1']));
    expect(store.getState().board.pending).toHaveLength(0);
    expect(store.getState().board.elements.entities.e1).toBeDefined();
  });
});
