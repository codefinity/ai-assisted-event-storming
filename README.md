# EventStorming

A web-based, real-time, collaborative [EventStorming](https://www.eventstorming.com/) board. Teams model
business processes together on an infinite board of stickies (Domain Events, Commands, Actors, Policies,
Read Models, External Systems, Aggregates, Hot Spots, Opportunities), with swimlanes, boundaries,
pivotal events and arrows, at three levels: Big Picture, Process Modelling and Software Design.
Other systems and AI agents build boards through a public REST API or an MCP server, and their
changes appear live.

- **Collaborate live:** see who is on the board, their cursors, what they are editing and what they are dragging. Changes merge deterministically, and the board resyncs cleanly after a dropped connection.
- **Model fast:**
  - a type's letter adds a sticky where the pointer is; `Tab` continues the timeline;
  - double-click to quick-add; drag from the palette;
  - multi-select, copy/paste (including between boards, or lines of text as events), and per-person undo/redo;
  - search and filter, a legend, and keyboard shortcuts (`?`).
- **Teams:** accounts, teams with Owner / Editor / Viewer roles, and invitations by link or email. A dashboard to create, rename, duplicate, archive, restore and permanently delete boards.
- **Import and export** a board as JSON (the Board Document), or export it as an SVG image.
- **Public API** (`/api/v1`):
  - API keys with scopes;
  - one-call import with automatic timeline layout;
  - idempotency keys, per-key rate limits, and RFC 9457 errors that name the field and the fix.
- **MCP server** (`/mcp`): AI agents (Claude, VS Code, Cursor and other MCP clients) draw and edit boards with tools such as `create_board` and `add_to_board`, with the modelling guide as a resource and ready-made prompts.

## Contents

- [Quick start](#quick-start)
- [Developing locally](#developing-locally)
- [Using the MCP server](#using-the-mcp-server)
- [Tests](#tests)
- [Architecture](#architecture)
- [Folder structure](#folder-structure)
- [How to add an element type](#how-to-add-an-element-type)
- [How to add a use case (slice)](#how-to-add-a-use-case-slice)
- [Configuration](#configuration)
- [Documentation](#documentation)
- [Limitations](#limitations)

## Quick start

You need Docker with Compose.

```sh
docker compose up --build            # MongoDB, Mailpit, the API and the web app
docker compose run --rm api seed     # optional: a demo account, team and sample board
```

| What | Where |
|---|---|
| Web app | http://localhost:3000 |
| API reference (Scalar) | http://localhost:5080/docs |
| Public API index | http://localhost:5080/api/v1 |
| MCP server (for agents; needs an API key) | http://localhost:5080/mcp |
| Mailpit (invitation emails) | http://localhost:8025 |

The seed creates `demo@eventstorming.local` with the password `eventstorming` (set `SEED_DEMO_PASSWORD`
to choose another), a "Demo team", and the "Online food ordering" Big Picture board.

No secrets are stored in the repository. In Development the API generates a JWT signing key into the
`keys` volume on first start. Copy `.env.example` to `.env` to change ports or anything else. For
example, if port 8025 is taken, set `MAILPIT_UI_PORT=8026`.

## Developing locally

Run MongoDB and Mailpit in Docker, and the API and web app on the host:

```sh
docker compose up -d mongo mailpit

# API on http://localhost:5080 (needs the .NET 10 SDK)
dotnet run --project server/src/driving-adapters/EventStorming.Host --launch-profile http

# Web app on http://localhost:3000 (needs Node 22.13+ or 24+)
cd web
npm install
npm run dev
```

Seed the local database with `./scripts/seed.sh` (or `./scripts/seed.ps1` on Windows). The scripts use
the Docker stack if it is running, and the local API otherwise. MongoDB is published on host port
**27018**, since a local installation often takes 27017.

## Using the MCP server

The API includes a [Model Context Protocol](https://modelcontextprotocol.io/) server. With it, AI
assistants and agents can draw and edit EventStorming boards: Claude Code, Claude Desktop, VS Code
(Copilot), Cursor, or your own code. You ask in plain language ("run a Big Picture EventStorming of
online food ordering"), and the agent draws a real board in your team.

What the agent draws is validated and laid out automatically. It appears live for everyone who has
the board open, and people can keep editing it in the web app.

| | |
|---|---|
| Endpoint | `http://localhost:5080/mcp` (Streamable HTTP, stateless) |
| Authentication | A team API key: `Authorization: Bearer es_…` |
| What it can reach | The boards of the key's team, nothing else |
| Permissions | `read` scope to read; `write` scope ("Can change boards") to draw |
| Rate limit | 300 requests per minute per key, shared with `/api/v1` |

### 1. Start the stack and get an API key

1. Start everything, and optionally seed the demo data:

   ```sh
   docker compose up --build
   docker compose run --rm api seed
   ```

2. Open http://localhost:3000 and sign in. The demo account is `demo@eventstorming.local` / `eventstorming`.
3. Open the team (for example "Demo team") and choose **Members & API keys**. Only the team's Owners see the API keys section.
4. Under **API keys**:
   - type a name the agent's changes will be attributed to, e.g. `Claude`;
   - tick **Can change boards** (without it the agent can read but not draw);
   - optionally pick an expiry date;
   - choose **Create key**.
5. Copy the key (`es_…`) straight away: it is shown once, and only a hash is stored.

To cut an agent off later, press **Revoke** next to its key. It stops working immediately.

### 2. Connect your client

Replace `es_…` with your key. If the API runs somewhere else, replace `http://localhost:5080` too.

**Claude Code**

```sh
claude mcp add --transport http eventstorming http://localhost:5080/mcp \
  --header "Authorization: Bearer es_…"
```

- Add `--scope user` to use it in every project.
- `claude mcp list` or `/mcp` inside Claude Code shows whether it connected.
- To share the setup with a team through a project's `.mcp.json`, don't commit the key. Reference an environment variable instead: `"Authorization": "Bearer ${EVENTSTORMING_API_KEY}"`.

**VS Code** (Copilot agent mode). Create `.vscode/mcp.json`; VS Code asks for the key once and keeps it out of the file:

```json
{
  "inputs": [
    { "type": "promptString", "id": "eventstorming-key", "description": "EventStorming API key", "password": true }
  ],
  "servers": {
    "eventstorming": {
      "type": "http",
      "url": "http://localhost:5080/mcp",
      "headers": { "Authorization": "Bearer ${input:eventstorming-key}" }
    }
  }
}
```

**Cursor**, in `~/.cursor/mcp.json` (or `.cursor/mcp.json` in a project):

```json
{
  "mcpServers": {
    "eventstorming": {
      "url": "http://localhost:5080/mcp",
      "headers": { "Authorization": "Bearer es_…" }
    }
  }
}
```

**Claude Desktop** and other clients that only start local (stdio) servers need a bridge to a remote
server, such as [`mcp-remote`](https://www.npmjs.com/package/mcp-remote) (needs Node.js). Add this to
`claude_desktop_config.json` and restart the app. The file lives at
`%APPDATA%\Claude\claude_desktop_config.json` on Windows, or
`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS:

```json
{
  "mcpServers": {
    "eventstorming": {
      "command": "npx",
      "args": ["-y", "mcp-remote", "http://localhost:5080/mcp", "--header", "Authorization:${EVENTSTORMING_AUTH}"],
      "env": { "EVENTSTORMING_AUTH": "Bearer es_…" }
    }
  }
}
```

**MCP Inspector**, to try the tools by hand: run `npx @modelcontextprotocol/inspector`. Then choose
the Streamable HTTP transport, enter the endpoint URL, and add an `Authorization` header with
`Bearer es_…`.

**Any other client** works if it speaks Streamable HTTP and can send a custom header. From your own
.NET code, with the official `ModelContextProtocol` package (the integration tests connect this way):

```csharp
await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("http://localhost:5080/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp,
    AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer es_…" },
}));

var result = await client.CallToolAsync("list_boards", new Dictionary<string, object?>());
```

### 3. Check that it works

Without any client, curl can list the tools. The server answers as a server-sent event (`data: {…}`):

```sh
curl -s http://localhost:5080/mcp \
  -H "Authorization: Bearer es_…" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -H "MCP-Protocol-Version: 2025-11-25" \
  -d '{ "jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {} }'
```

You should see the 11 tools below. A `401` means the key is missing, mistyped or revoked.

### 4. Draw a board

Ask the agent in your own words. It calls the tools itself. For example:

> Use the eventstorming tools to run a Big Picture EventStorming of online food ordering: customers,
> restaurants and couriers. Mark the pivotal events, add hot spots for anything unclear, and give me
> the link to the board.

A well-behaved agent then does the following:

1. It calls `list_element_types` to learn the notation: which types exist, what each means, and how to write it.
2. It calls `create_board` once, with the whole story as elements in timeline order.
3. It answers with the link, e.g. `http://localhost:3000/boards/01a0…`. Open it: the board is laid out with swimlanes, pivotal-event dividers, hot spots under the events they question, and arrows.

Then keep the conversation going; the agent edits the same board:

> Add what happens when a customer asks for a refund, in the Customer and Support lanes.

> Rename "Order Placd" to "Order Placed" and mark "Payment Taken" as pivotal.

> Move the hot spots about delivery times next to "Order Delivered".

If the board is open in your browser while the agent works, you see each change appear. A notice
names the key: "“Claude” (an API key) is changing this board".

**Ready-made prompts.** The server offers two prompts that walk the agent through a proper session:

| Prompt | Arguments | What it does |
|---|---|---|
| `big_picture` | `domain`, optional `focus` | Explores a whole domain as a Big Picture board. |
| `process_modelling` | `process`, optional `boardId` | Models one process with the Actor → Command → Event → Policy grammar, on a new board or continuing an existing one. |

In Claude Code they appear as slash commands, e.g. `/mcp__eventstorming__big_picture`. Other clients
list them in their prompt or command menu.

**Resources.** Clients that support MCP resources can attach these:

| Resource | Content |
|---|---|
| `eventstorming://guide` | The modelling guide: notation, format, a worked example, common mistakes |
| `eventstorming://element-types` | The notation as JSON |
| `eventstorming://boards/{boardId}` | One board's elements and arrows |

### Tools

| Tool | Arguments | What it does |
|---|---|---|
| `list_element_types` | — | The notation: every element type (meaning, when to use it, how to write it, whether it can be pivotal) and each level's palette. |
| `list_boards` | `includeArchived?`, `cursor?` | The team's boards with ids, levels, sizes and links. |
| `get_board` | `boardId` | Every element in timeline order (id, type, text, position, size, pivotal, version, and the ids of its swimlane and boundary) and every arrow. |
| `create_board` | `name`, `level`, `elements?`, `connections?` | Draws a new board in one call and lays it out. |
| `add_to_board` | `boardId`, `elements`, `connections?` | Continues a board after its last sticky; up to 500 elements per call. |
| `update_element` | `boardId`, `elementId`, `text?`, `type?`, `position?`, `size?`, `pivotal?`, `color?`, `expectedVersion?` | Changes one element; only the fields given change. |
| `move_elements` | `boardId`, `moves` (`elementId`, `x`, `y`) | Moves elements in one change. |
| `connect_elements` | `boardId`, `from`, `to`, `label?` | Draws an arrow between two elements on the board. |
| `delete_elements` | `boardId`, `elementIds` | Deletes elements and the arrows touching them. |
| `delete_connections` | `boardId`, `connectionIds` | Deletes arrows. |
| `replace_board_content` | `boardId`, `elements`, `connections?`, `name?` | Redraws a whole board from scratch. |

`level` is `big-picture`, `process-modelling` or `software-design`. The first three tools only read;
clients may ask you to confirm the delete and replace tools, which are marked destructive.

Elements use the same shape as the [Board Document](docs/llm-guide.md). An agent's `create_board` call
looks like this:

```json
{
  "name": "Online food ordering",
  "level": "big-picture",
  "elements": [
    { "key": "customer", "type": "swimlane", "text": "Customer" },
    { "key": "placed", "type": "domain-event", "text": "Order Placed", "swimlane": "customer", "pivotal": true },
    { "key": "late", "type": "hot-spot", "text": "What if the kitchen is busy?", "anchor": "placed" },
    { "key": "paid", "type": "domain-event", "text": "Payment Taken", "swimlane": "customer" }
  ],
  "connections": [ { "from": "placed", "to": "paid" } ]
}
```

The layout follows a few rules:
- **Array order is the timeline:** elements are placed left to right in the order listed, one column each, across all swimlanes.
- **`anchor`** stacks an element below another (e.g. a hot spot under its event).
- **`swimlane`** and **`boundary`** place an element in a lane or a boundary box. They name a `key` in the same call, or the id of an element already on the board.
- **Positions are optional:** leave them out and the server lays everything out.
- **`add_to_board`** continues after the last sticky. A lane or boundary that is too small grows to hold the new elements, and the result lists it under `resized`.
- **Results** map every `key` to the element id it was given, for later edits.

### When something goes wrong

If a tool call breaks a rule, the board is not changed. The agent gets a normal tool result with
`isError: true` that lists every problem with the field, a code and the fix:

```
create_board was refused with 2 problems. Nothing was changed. Fix them all and call create_board again:
- elements[1].type: 'hotspot' is not an element type. [unknown-element-type] Fix: Use one of: domain-event, command, actor, …
- elements[1].anchor: No element in this request has key 'plced', and no element on the board has that id. [unknown-reference] Fix: Did you mean 'placed'? …
```

Agents usually fix the call and retry on their own. Problems at the connection level show up as client errors instead:

| Symptom | Cause | Fix |
|---|---|---|
| `401 Unauthorized`, or the client cannot connect | No key, a mistyped key, or a revoked or expired key | Check the `Authorization: Bearer es_…` header; create a new key if needed. |
| Tools are refused with `[forbidden]` | The key has only the `read` scope | Create a key with **Can change boards** ticked. |
| `[not-found]` for a board you can see in the web app | The board belongs to another team than the key | Use a key of the board's team. |
| `[board-archived]` | The board is archived | Restore it on the team's dashboard. |
| `429 Too Many Requests` | More than 300 requests in a minute with this key | Wait for the `Retry-After` seconds; prefer one `create_board` or `add_to_board` over many small calls. |
| Links in results point to the wrong host | `WebApp__BaseUrl` (`WEB_ORIGIN` in Compose) is not where people open the web app | Set it to the web app's public address. |
| Connection refused | The API is not running, or listens on another port | `docker compose ps`; the API is on `API_PORT` (default 5080). |

### Running it for others

- **Serve the API over HTTPS** when agents connect from other machines: the API key travels in a header.
- **One key per agent or integration**, so each change is attributed to it, and each can be revoked on its own.
- **The endpoint is not for browsers:** it has no CORS policy.
- **The server is stateless.** It keeps no sessions and never calls back into the client (no sampling or elicitation), so any MCP client that can send a header works.

The full reference is in [`docs/mcp.md`](docs/mcp.md); ADR [11](docs/adr/0011-mcp-server-as-a-driving-adapter.md) explains the design.

## Tests

| Suite | What it covers | Run |
|---|---|---|
| Behaviour specs (Reqnroll + xUnit v3) | Every use case against in-memory fakes, in Given/When/Then | `dotnet test --project server/tests/EventStorming.Specs` |
| Architecture (ArchUnitNET) | Rings, context independence, slice shape, no tactical DDD, composition | `dotnet test --project server/tests/EventStorming.Architecture.Tests` |
| MongoDB integration (Testcontainers) | Every store adapter against a real replica set | `dotnet test --project server/tests/EventStorming.Persistence.MongoDb.IntegrationTests` |
| Host integration (WebApplicationFactory + Testcontainers) | HTTP, SignalR and MCP end to end: sessions, errors, the public API, live changes between two people, an agent drawing through the official MCP client, schema drift | `dotnet test --project server/tests/EventStorming.Host.IntegrationTests` |
| Web (Vitest + React Testing Library) | Sync model, realtime middleware (fake hub), commands and undo, clipboard, export, the editor from the keyboard, render counts, module boundaries | `cd web && npm test` |
| End to end (Playwright) | Two people sign up, share a team and a board, and see each other's changes live | `cd tests/e2e && npm install && npx playwright install chromium && npm test` (with the stack running) |

The integration suites start their own MongoDB containers, so Docker must be running. The end-to-end
test signs up two new accounts per run; the API allows 10 sign-ins or sign-ups per minute per client
address.

After a deliberate change to the Board Document, regenerate the published schema with
`UPDATE_PUBLISHED_SCHEMA=1 dotnet test --project server/tests/EventStorming.Host.IntegrationTests`,
and review the diff to `docs/schemas/board-document.v1.schema.json`.

## Architecture

Hexagonal, organised as vertical slices, with strategic DDD only: bounded contexts, a context map and
anti-corruption layers, but no aggregates, value objects or repositories. The decisions and their
reasons are in [`docs/adr`](docs/adr).

### Bounded contexts

```mermaid
flowchart LR
  ID["Identity<br/><i>generic</i>"]
  TM["Teams<br/><i>supporting</i>"]
  BM["Board Modelling<br/><b>core</b>"]
  CO["Collaboration<br/><i>supporting</i>"]
  PI["Public Integration<br/><i>supporting</i>"]
  EXT["External systems & AI agents"]
  ID -- "Conformist: account id and name from the token" --> TM
  TM -- "Customer–Supplier, ACL: team role → board permission" --> BM
  BM -- "Published Language: board change sets" --> CO
  BM -- "Customer–Supplier: PI calls BM use cases" --> PI
  PI == "Open Host Service: /api/v1 and /mcp, Board Document v1, JSON Schema" ==> EXT
```

| Context | Owns |
|---|---|
| **Identity** | Accounts, passwords, sessions (access and rotating refresh tokens) |
| **Teams** | Teams, members, roles, invitations |
| **Board Modelling** (core) | Boards, elements, connections, the element-type registry, the Board Document, auto-layout |
| **Collaboration** | Presence: participants, cursors, editing focus, drag previews (in memory) |
| **Public Integration** | API keys and scopes, idempotency records |

Each context is one core assembly with no references to the other contexts. What one context needs
from another is a port it owns, implemented by an adapter. For example, Board Modelling asks
`IBoardAccess` for a `BoardPermission`, and the MongoDB adapter answers from team roles and API-key
scopes: that adapter is the anti-corruption layer.

### How a change flows

```mermaid
sequenceDiagram
  participant A as Ana's browser
  participant Hub as SignalR hub
  participant UC as Board Modelling use case
  participant DB as MongoDB
  participant Feed as Board feed
  participant B as Bo's browser
  A->>A: apply optimistically (pending op)
  A->>Hub: MoveElements(operationId)
  Hub->>UC: MoveElementsCommand
  UC->>DB: update elements, bump board revision (transaction)
  UC->>Feed: change set (revision, full post-images)
  Hub-->>A: ack
  Feed-->>A: boardChanged (own operationId: pending op settled)
  Feed-->>B: boardChanged (applied if newer than what B has)
```

The public API and the MCP server call the same use cases, so their changes reach open boards the same way.

## Folder structure

```
server/
  src/
    application/                 the cores: one assembly per bounded context
      EventStorming.SharedKernel   Failure, Actor, IClock, paging
      EventStorming.Identity       Model/  Shared/  Slices/<UseCase>/
      EventStorming.Teams
      EventStorming.BoardModelling also Layout/ (auto-layout) and Drafting/ (Board Document planning)
      EventStorming.Collaboration
      EventStorming.PublicIntegration
    driving-adapters/
      EventStorming.Api.Rest       /api/app (web app) and /api/v1 (public), Problem Details, idempotency
      EventStorming.Realtime.SignalR  the board hub and the feed relay
      EventStorming.Mcp            the MCP server: drawing tools, resources and prompts for agents
      EventStorming.Host           composition root, configuration, seeding
    driven-adapters/
      EventStorming.Persistence.MongoDb  stores per slice, the ACL, indexes
      EventStorming.Security       password hashing, JWTs, token and key secrets
      EventStorming.Email.Smtp     invitation emails
      EventStorming.ElementTypes.Json  the element-type registry (element-types.json)
      EventStorming.Presence.InMemory  who is on which board
      EventStorming.Broadcasting(.Transport)  change sets and presence onto the board feed
  tests/                         specs, architecture, MongoDB integration, host integration
web/
  src/
    app/                         Next.js routes; they compose modules
    modules/
      identity/  teams/  public-integration/
      board-modelling/           board (sync model), editor, canvas, palette, legend, search,
                                 history, clipboard, shortcuts, documents, catalog, notation
      collaboration/             realtime (middleware, hub client), presence
    shared/                      API base, problems, UI primitives
    store/                       the Redux store
tests/e2e/                       Playwright
docs/                            API reference, LLM guide, glossary, ADRs, schema, examples
scripts/                         seed.sh, seed.ps1
```

## How to add an element type

Element types are data. Adding one changes no server or web code.

1. Add an entry to `server/src/driven-adapters/EventStorming.ElementTypes.Json/element-types.json`:

   ```json
   {
     "id": "question",
     "name": "Question",
     "category": "sticky",
     "renderer": "sticky",
     "color": "#FFD8A8",
     "textColor": "#1B1B1F",
     "icon": "help",
     "defaultSize": { "width": 160, "height": 100 },
     "levels": ["big-picture"],
     "shortcut": "Q",
     "maxTextLength": 300,
     "canBePivotal": false,
     "layoutRole": "item",
     "description": "Something the group wants to ask an expert.",
     "whenToUse": "When nobody in the room knows the answer.",
     "writingRule": "A question: \"Who approves refunds?\"",
     "examples": ["Who approves refunds?"]
   }
   ```

   - `id`: kebab-case and unique.
   - `shortcut`: unique.
   - `levels`: the levels whose palette offers the type (every type can still be placed anywhere).
   - `renderer`: `sticky`, `lane` or `area`.
   - `layoutRole`: `item`, `lane` or `boundary`, which decides how auto-layout treats it.
   - `icon`: a name from `web/src/shared/ui/Icon.tsx`; an unknown name draws a square.
2. Restart the API. The registry is validated at startup, and a mistake stops the API with a message naming the type and the problem. To use a file outside the build, point `ElementTypes__Path` at it.
3. That's all. The palette, legend, shortcuts, help overlay, validation, the JSON Schema's `enum` and the public API pick it up. The specs use their own registry fake, so they are unaffected.

## How to add a use case (slice)

Take "revoke an invitation" in Teams as the model (`server/src/application/EventStorming.Teams/Slices/RevokeInvitation`):

1. **Contract**: `Slices/<Name>/<Name>.cs` holds:
   - the command or query record (it carries the `Actor`);
   - a `<Name>Result` implementing `IUseCaseResult` with `Succeeded` / `Failed` factories;
   - the handler interface `I<Name>CommandHandler`;
   - the store port `I<Name>Store`, with only the reads and writes this use case needs.
2. **Handler**: `Slices/<Name>/<Name>Handler.cs` holds the sealed handler, plus a FluentValidation validator if the input needs one. Validator failures carry error codes and a `.WithFix(...)`. The handler:
   - checks authorization (through the context's access port);
   - applies the rules and calls the store;
   - broadcasts if content changed;
   - returns failures, never throws for expected outcomes.
3. **Register** the handler (and validator) in the context's `*ServiceExtensions`.
4. **Store adapter**: implement the port in the MongoDB adapter (e.g. `Persistence.MongoDb/Teams/TeamsStores.cs`) and register it in `MongoDbServiceExtensions`.
5. **Driving adapter**: map an endpoint in `Api.Rest` (or a hub method), translating the request into the command and `Problems.From(result, http)` for failures.
6. **Tests**: a scenario in `server/tests/EventStorming.Specs/Features/<Context>/` with step definitions using the in-memory fakes, and a store test in the MongoDB integration suite. The architecture tests check the slice's shape automatically.

## Configuration

The API reads `appsettings.json`, then environment variables (`Section__Key`).

| Setting | Default | Meaning |
|---|---|---|
| `Mongo__ConnectionString` | `mongodb://localhost:27018/?replicaSet=rs0&directConnection=true` | A replica set is required (transactions). |
| `Mongo__Database` | `eventstorming` | |
| `Jwt__SigningKey` | empty | Base64, at least 32 bytes. Required in Production; generated into `Keys__Directory` otherwise. |
| `Keys__Directory` | `<content root>/.keys` | Where a development signing key is kept. |
| `Cors__AllowedOrigins__0` | `http://localhost:3000` | The web app's origin (credentials allowed). |
| `Cors__PublicApiOrigins__0` | none | Browser origins allowed to call `/api/v1`. |
| `RestApi__PublicApiPermitsPerMinute` | 300 | Requests per API key per minute. |
| `RestApi__SignInPermitsPerMinute` | 10 | Sign-ins and sign-ups per client address per minute. |
| `RestApi__SecureCookies` | `true` (`false` in Development) | The `Secure` flag on the refresh cookie. |
| `Smtp__Host`, `Smtp__Port`, `Smtp__From` | | Invitation emails; Compose uses Mailpit. |
| `WebApp__BaseUrl` | `http://localhost:3000` | Used in invitation links. |
| `ElementTypes__Path` | embedded registry | Another `element-types.json` to load. |

The web app has one setting, `NEXT_PUBLIC_API_URL` (default `http://localhost:5080`). It is baked in at build time; see `web/.env.example`.

## Documentation

- [`docs/api.md`](docs/api.md): the public API: authentication, conventions, every endpoint with examples, errors, idempotency, rate limits, versioning.
- [`docs/mcp.md`](docs/mcp.md): the MCP server: connecting Claude Code, VS Code, Cursor and other clients, the tools, resources and prompts.
- [`docs/llm-guide.md`](docs/llm-guide.md): for LLM agents: the notation, the Board Document, a worked example, common mistakes. Also served at `/docs/llm-guide.md` and as the MCP resource `eventstorming://guide`.
- [`docs/glossary.md`](docs/glossary.md): the ubiquitous language.
- [`docs/adr/`](docs/adr): architecture decision records.
- [`docs/schemas/board-document.v1.schema.json`](docs/schemas/board-document.v1.schema.json): the Board Document's JSON Schema.
- [`docs/examples/food-ordering.board.json`](docs/examples/food-ordering.board.json): a complete Big Picture board.
- [`docs/phase-0-plan.md`](docs/phase-0-plan.md): the original design and the reasoning behind it.

## Limitations

- **One API instance.** Presence and the board feed are in memory; scaling out needs a SignalR backplane (ADR 4).
- **Last writer wins per field.** There is no character-level merge when two people type into the same sticky at once; the editing indicator makes it visible.
- **Arrows are straight** and their labels can only be set through the API or a Board Document.
- **Export** is JSON and SVG; there is no PNG export.
