import type { HubClient, HubConnectionEvent } from '@/modules/collaboration/realtime/hubClient';
import type { BoardElement, BoardSnapshot } from '@/modules/board-modelling/model';
import type { ElementType, Notation } from '@/modules/board-modelling/notation/notation';
import { makeStore, type StoreOptions } from '@/store/store';

export const ana = { kind: 'account' as const, id: '00000000-0000-4000-8000-00000000a0a0', name: 'Ana' };

function type(overrides: Partial<ElementType> & Pick<ElementType, 'id' | 'name' | 'color'>): ElementType {
  return {
    category: 'sticky',
    renderer: 'sticky',
    textColor: '#1B1B1F',
    icon: 'zap',
    defaultSize: { width: 160, height: 100 },
    levels: ['big-picture', 'process-modelling', 'software-design'],
    shortcut: null,
    maxTextLength: 200,
    canBePivotal: false,
    layoutRole: 'item',
    description: `${overrides.name} description.`,
    whenToUse: `When to use ${overrides.name}.`,
    writingRule: null,
    examples: [],
    ...overrides,
  };
}

/** Shaped like the server's registry, but deliberately not identical: the UI must not assume the real one. */
export const notation: Notation = {
  types: [
    type({ id: 'domain-event', name: 'Domain Event', color: '#FFA94D', shortcut: 'E', canBePivotal: true, writingRule: 'Past tense.' }),
    type({ id: 'hot-spot', name: 'Hot Spot', color: '#F783AC', shortcut: 'H', icon: 'alert' }),
    type({ id: 'command', name: 'Command', color: '#74C0FC', shortcut: 'C', icon: 'send', levels: ['process-modelling', 'software-design'] }),
    type({ id: 'lane', name: 'Swimlane', color: '#F1F3F5', shortcut: 'L', renderer: 'lane', category: 'structure', layoutRole: 'lane', defaultSize: { width: 1600, height: 240 } }),
    type({ id: 'area', name: 'Boundary', color: '#E7F5FF', shortcut: 'B', renderer: 'area', category: 'structure', layoutRole: 'boundary', defaultSize: { width: 720, height: 480 } }),
  ],
  levels: [
    { id: 'big-picture', name: 'Big Picture', description: 'The whole domain.', palette: ['domain-event', 'hot-spot', 'lane', 'area'] },
    { id: 'process-modelling', name: 'Process Modelling', description: 'One process.', palette: ['domain-event', 'command', 'hot-spot'] },
    { id: 'software-design', name: 'Software Design', description: 'The software.', palette: ['domain-event', 'command'] },
  ],
};

export const boardId = '0b0b0b0b-0000-4000-8000-000000000001';
export const teamId = '0b0b0b0b-0000-4000-8000-0000000000aa';

export function element(id: string, overrides: Partial<BoardElement> = {}): BoardElement {
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

export function snapshotOf(elements: BoardElement[], revision = 5, overrides: Partial<BoardSnapshot> = {}): BoardSnapshot {
  return {
    board: {
      id: boardId,
      teamId,
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
    ...overrides,
  };
}

interface Invocation {
  method: string;
  args: unknown[];
  resolve: (value: unknown) => void;
  reject: (error: unknown) => void;
}

/** A board hub in memory: records what is sent, answers joins, and lets a test answer or drop the rest. */
export class FakeHub implements HubClient {
  connected = false;
  starts = 0;
  readonly invocations: Invocation[] = [];
  readonly sent: Array<{ method: string; args: unknown[] }> = [];
  /** Answer editing calls at once with success. */
  autoAck = false;
  private readonly handlers = new Map<string, (message: never) => void>();
  private connectionEvents: ((event: HubConnectionEvent) => void) | null = null;
  private joinCount = 0;

  async start() {
    this.starts++;
    this.connected = true;
  }

  async stop() {
    this.connected = false;
  }

  invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    return new Promise<T>((resolve, reject) => {
      if (method === 'JoinBoard') {
        this.joinCount++;
        resolve({ ok: true, you: this.participant(`me-${this.joinCount}`), participants: [this.participant('bo-1', 'Bo')], failures: [] } as T);
        return;
      }

      if (this.autoAck) {
        const request = args[0] as { operationId?: string };
        resolve({ ok: true, operationId: request?.operationId ?? null, revision: 0, failures: [] } as T);
        return;
      }

      this.invocations.push({ method, args, resolve: resolve as (value: unknown) => void, reject });
    });
  }

  async send(method: string, ...args: unknown[]) {
    this.sent.push({ method, args });
  }

  on(method: string, handler: (message: never) => void) {
    this.handlers.set(method, handler);
  }

  onConnectionEvent(handler: (event: HubConnectionEvent) => void) {
    this.connectionEvents = handler;
  }

  /** The server broadcasts a message to this client. */
  emit(method: string, message: unknown) {
    this.handlers.get(method)?.(message as never);
  }

  /** Answers the oldest unanswered editing call. */
  answer(ok: boolean, failures: unknown[] = []) {
    const invocation = this.invocations.shift();
    if (!invocation) throw new Error('No call is waiting for an answer.');
    const request = invocation.args[0] as { operationId?: string };
    invocation.resolve({ ok, operationId: request.operationId ?? null, revision: 0, failures });
    return invocation;
  }

  /** The connection drops: calls in flight fail, and SignalR starts reconnecting. */
  drop() {
    this.connected = false;
    for (const invocation of this.invocations.splice(0)) invocation.reject(new Error('Invocation canceled due to the underlying connection being closed.'));
    this.connectionEvents?.('reconnecting');
  }

  reconnect() {
    this.connected = true;
    this.connectionEvents?.('reconnected');
  }

  participant(connectionId: string, displayName = 'Me') {
    return { connectionId, accountId: `${displayName}-account`, displayName, color: '#1D4ED8', editingElementId: null };
  }
}

export const signedIn = {
  status: 'signed-in' as const,
  accessToken: 'token',
  expiresAt: Date.now() + 3_600_000,
  account: { id: ana.id, email: 'ana@example.com', displayName: 'Ana' },
};

export function testStore(options: { hub?: FakeHub; snapshot?: () => BoardSnapshot } & Omit<StoreOptions, 'hubFactory' | 'loadSnapshot'> = {}) {
  const hub = options.hub ?? new FakeHub();
  const snapshot = options.snapshot ?? (() => snapshotOf([element('e1'), element('e2', { x: 300 })]));
  const store = makeStore({
    hubFactory: () => hub,
    loadSnapshot: async () => snapshot(),
    preloadedState: { session: signedIn, ...options.preloadedState },
  });
  return { store, hub };
}

/** Lets pending promise callbacks (hub answers, snapshot loads) run. */
export async function settle(rounds = 5) {
  for (let round = 0; round < rounds; round++) await new Promise((resolve) => setTimeout(resolve, 0));
}
