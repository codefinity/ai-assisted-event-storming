import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection, type IRetryPolicy } from '@microsoft/signalr';

export type HubConnectionEvent = 'reconnecting' | 'reconnected' | 'closed';

/**
 * The little the realtime middleware needs from a hub connection. The SignalR implementation is
 * below; tests pass a fake.
 */
export interface HubClient {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke<T>(method: string, ...args: unknown[]): Promise<T>;
  /** Fire and forget: cursors, focus and drag previews. */
  send(method: string, ...args: unknown[]): Promise<void>;
  on(method: string, handler: (message: never) => void): void;
  onConnectionEvent(handler: (event: HubConnectionEvent) => void): void;
  readonly connected: boolean;
}

export interface HubClientOptions {
  url: string;
  accessToken: () => Promise<string>;
}

export type HubClientFactory = (options: HubClientOptions) => HubClient;

/** Retry soon, then settle at every ten seconds, forever: a board left open should come back on its own. */
const retryForever: IRetryPolicy = {
  nextRetryDelayInMilliseconds: ({ previousRetryCount }) => [0, 1000, 2000, 5000][previousRetryCount] ?? 10_000,
};

class SignalRHubClient implements HubClient {
  private readonly connection: HubConnection;
  private starting: Promise<void> | null = null;

  constructor(options: HubClientOptions) {
    this.connection = new HubConnectionBuilder()
      .withUrl(options.url, { accessTokenFactory: options.accessToken })
      .withAutomaticReconnect(retryForever)
      // The connection status in the toolbar already tells people when the connection drops.
      .configureLogging(process.env.NODE_ENV === 'production' ? LogLevel.None : LogLevel.Warning)
      .build();
  }

  get connected() {
    return this.connection.state === HubConnectionState.Connected;
  }

  /** Starting while a start is under way waits for that one, rather than failing. */
  start() {
    if (this.connection.state === HubConnectionState.Disconnected) {
      this.starting = this.connection.start().finally(() => {
        this.starting = null;
      });
    }

    return this.starting ?? Promise.resolve();
  }

  stop() {
    return this.connection.stop();
  }

  invoke<T>(method: string, ...args: unknown[]) {
    return this.connection.invoke<T>(method, ...args);
  }

  send(method: string, ...args: unknown[]) {
    return this.connection.send(method, ...args);
  }

  on(method: string, handler: (message: never) => void) {
    this.connection.on(method, handler);
  }

  onConnectionEvent(handler: (event: HubConnectionEvent) => void) {
    this.connection.onreconnecting(() => handler('reconnecting'));
    this.connection.onreconnected(() => handler('reconnected'));
    this.connection.onclose(() => handler('closed'));
  }
}

export const signalRHubClient: HubClientFactory = (options) => new SignalRHubClient(options);
