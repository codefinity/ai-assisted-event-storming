import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Profiler, type ReactNode } from 'react';
import { Provider } from 'react-redux';
import { describe, expect, it, vi } from 'vitest';
import { boardId, element, FakeHub, notation, snapshotOf, testStore } from '@/test/fixtures';
import { changeSetReceived, boardOpened } from '../board/boardSlice';
import { ElementNode } from '../canvas/ElementNode';
import { boardsApi } from '../catalog/boardsApi';
import { indexNotation } from '../notation/notation';
import { BoardContext, type BoardContextValue } from './BoardContext';
import { BoardEditor } from './BoardEditor';

vi.mock('next/link', () => ({
  default: ({ href, children, ...rest }: { href: string; children: ReactNode }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

function renderEditor(elements = [element('e1', { text: 'Order Placed' }), element('e2', { text: 'Payment Taken', x: 300 })]) {
  const hub = new FakeHub();
  const { store } = testStore({ hub, snapshot: () => snapshotOf(elements) });
  store.dispatch(boardsApi.util.upsertQueryData('getNotation', undefined, notation));
  const view = render(
    <Provider store={store}>
      <BoardEditor boardId={boardId} />
    </Provider>,
  );
  return { store, hub, view };
}

const stickies = () => document.querySelectorAll('[data-element-id]');

describe('board editor', () => {
  it('opens the board and draws its elements with accessible names', async () => {
    renderEditor();
    expect(await screen.findByRole('button', { name: 'Domain Event: Order Placed' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Domain Event: Payment Taken' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Ordering' })).toBeInTheDocument();
  });

  it('offers the palette the registry defines for the board’s level', async () => {
    renderEditor();
    const palette = await screen.findByRole('navigation', { name: 'Element types' });
    const names = within(palette)
      .getAllByRole('button', { pressed: false })
      .map((button) => button.querySelector('.palette-name')?.textContent)
      .filter(Boolean);
    expect(names).toEqual(['Domain Event', 'Hot Spot', 'Swimlane', 'Boundary']);
    expect(within(palette).getByRole('button', { name: /1 more types/ })).toBeInTheDocument();
  });

  it('adds a sticky from the keyboard: its letter, the text, Enter', async () => {
    const user = userEvent.setup();
    const { hub } = renderEditor();
    await screen.findByRole('button', { name: 'Domain Event: Order Placed' });

    await user.keyboard('h');
    const editor = screen.getByRole('textbox', { name: 'Text of the new Hot Spot' });
    expect(editor).toHaveFocus();
    await user.type(editor, 'Who pays for a refund?{Enter}');

    expect(screen.getByRole('button', { name: 'Hot Spot: Who pays for a refund?' })).toBeInTheDocument();
    const call = hub.invocations.find((invocation) => invocation.method === 'AddElements');
    expect(call?.args[0]).toMatchObject({ boardId, elements: [expect.objectContaining({ type: 'hot-spot', text: 'Who pays for a refund?' })] });
  });

  it('carries on along the timeline with Tab while typing', async () => {
    const user = userEvent.setup();
    renderEditor([]);
    await screen.findByText(/Press/);
    await user.keyboard('e');
    await user.keyboard('Order Placed{Tab}Order Paid{Enter}');
    const placed = screen.getByRole('button', { name: 'Domain Event: Order Placed' });
    const paid = screen.getByRole('button', { name: 'Domain Event: Order Paid' });
    const x = (node: HTMLElement) => Number(/translate\((-?[\d.]+)px/.exec(node.style.transform)?.[1]);
    expect(x(paid)).toBeGreaterThan(x(placed));
  });

  it('edits, deletes and undoes with the keyboard', async () => {
    const user = userEvent.setup();
    const { hub } = renderEditor();
    const sticky = await screen.findByRole('button', { name: 'Domain Event: Order Placed' });
    await user.click(sticky);
    expect(sticky).toHaveAttribute('aria-pressed', 'true');

    await user.keyboard('{Enter}');
    const editor = screen.getByRole('textbox', { name: 'Text of this Domain Event' });
    await user.clear(editor);
    await user.type(editor, 'Order Submitted{Enter}');
    expect(screen.getByRole('button', { name: 'Domain Event: Order Submitted' })).toBeInTheDocument();

    await user.keyboard('{Delete}');
    expect(screen.queryByRole('button', { name: 'Domain Event: Order Submitted' })).not.toBeInTheDocument();

    await user.keyboard('{Control>}z{/Control}');
    expect(screen.getByRole('button', { name: 'Domain Event: Order Submitted' })).toBeInTheDocument();
    expect(hub.invocations.map((invocation) => invocation.method)).toEqual(['UpdateElement', 'DeleteElements', 'AddElements']);
  });

  it('walks the timeline with Tab, left to right', async () => {
    const user = userEvent.setup();
    renderEditor([element('late', { text: 'Late', x: 900 }), element('early', { text: 'Early', x: 10 }), element('middle', { text: 'Middle', x: 400 })]);
    const early = await screen.findByRole('button', { name: 'Domain Event: Early' });
    expect(early).toHaveAttribute('tabindex', '0');
    early.focus();
    await user.keyboard('{Tab}');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Domain Event: Middle' })).toHaveFocus());
  });

  it('dims everything the search does not match', async () => {
    const user = userEvent.setup();
    renderEditor();
    await screen.findByRole('button', { name: 'Domain Event: Order Placed' });
    await user.click(screen.getByRole('button', { name: 'Search and filter' }));
    await user.type(screen.getByRole('searchbox', { name: 'Search this board' }), 'payment');
    expect(screen.getByRole('status', { name: '' }).textContent ?? '').toMatch(/1 of 2 elements match/);
    expect(screen.getByRole('button', { name: 'Domain Event: Order Placed' })).toHaveClass('el-dimmed');
    expect(screen.getByRole('button', { name: 'Domain Event: Payment Taken' })).not.toHaveClass('el-dimmed');
  });

  it('opens the shortcut list with ?', async () => {
    const user = userEvent.setup();
    renderEditor();
    await screen.findByRole('button', { name: 'Domain Event: Order Placed' });
    await user.keyboard('?');
    expect(await screen.findByRole('heading', { name: 'Keyboard shortcuts' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: /Undo your last change/ })).toBeInTheDocument();
  });

  it('shows a view-only board without the palette or history', async () => {
    const hub = new FakeHub();
    const { store } = testStore({ hub, snapshot: () => snapshotOf([element('e1')], 5, { permission: 'view' }) });
    store.dispatch(boardsApi.util.upsertQueryData('getNotation', undefined, notation));
    render(
      <Provider store={store}>
        <BoardEditor boardId={boardId} />
      </Provider>,
    );
    expect(await screen.findByText('View only')).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Element types' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Undo' })).not.toBeInTheDocument();
  });
});

describe('rendering cost', () => {
  it('re-renders only the element that changed, however many there are', async () => {
    const elements = Array.from({ length: 300 }, (_, index) => element(`e${index}`, { x: index * 10 }));
    const { store } = testStore({ snapshot: () => snapshotOf(elements) });
    store.dispatch(boardOpened(boardId));
    await waitFor(() => expect(store.getState().board.phase).toBe('ready'));

    const renders = new Map<string, number>();
    const context: BoardContextValue = {
      index: indexNotation(notation),
      canEdit: true,
      canvasRef: { current: null },
      pointerRef: { current: null },
      fitTo: () => undefined,
      reveal: () => undefined,
      zoomBy: () => undefined,
      viewCenter: () => ({ x: 0, y: 0 }),
      focusElement: () => undefined,
    };
    render(
      <Provider store={store}>
        <BoardContext.Provider value={context}>
          {elements.map((item) => (
            <Profiler key={item.id} id={item.id} onRender={(id) => renders.set(id, (renders.get(id) ?? 0) + 1)}>
              <ElementNode id={item.id} />
            </Profiler>
          ))}
        </BoardContext.Provider>
      </Provider>,
    );
    expect(stickies()).toHaveLength(300);
    renders.clear();

    act(() => {
      store.dispatch(
        changeSetReceived({
          boardId,
          revision: 99,
          operationId: null,
          actor: elements[0]!.updatedBy,
          elements: [element('e42', { x: 5000, version: 2 })],
          removedElements: [],
          connections: [],
          removedConnections: [],
        }),
      );
    });

    expect([...renders.keys()]).toEqual(['e42']);
  });
});
