# MCP server

EventStorming includes a [Model Context Protocol](https://modelcontextprotocol.io/) server, so AI agents
and other MCP clients can draw and read EventStorming boards. Whatever an agent draws lands on a
real team board: it is validated and laid out like any other change, and appears live for everyone
who has the board open.

- **Endpoint:** `http://localhost:5080/mcp` in the local stack (Streamable HTTP, stateless).
- **Authentication:** a team API key, sent as `Authorization: Bearer es_…`.
- **Scope:** the key's team only. Reading needs the `read` scope; drawing needs `write`.
- **Rate limit:** shared with the public API, 300 requests per minute per key.

## Contents

1. [Get an API key](#get-an-api-key)
2. [Connect a client](#connect-a-client)
3. [Tools](#tools)
4. [Resources and prompts](#resources-and-prompts)
5. [How drawing works](#how-drawing-works)
6. [When a tool is refused](#when-a-tool-is-refused)
7. [Security](#security)

## Get an API key

A team Owner creates keys in the web app: open the team, choose **Members & API keys**, then
**API keys**. Tick **Can change boards** for a key that draws. The full key is shown once; copy it.

A key sees only its own team's boards. Revoke it in the same place to cut an agent off immediately.

## Connect a client

Examples for common clients. Replace `es_…` with your key, and the URL if the API runs elsewhere.

**Claude Code**

```sh
claude mcp add --transport http eventstorming http://localhost:5080/mcp \
  --header "Authorization: Bearer es_…"
```

**VS Code** (`.vscode/mcp.json`), which asks for the key once and stores it securely:

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

**Cursor** (`~/.cursor/mcp.json`):

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

**Clients that only run local (stdio) servers**, such as Claude Desktop, can reach it through a
bridge like [`mcp-remote`](https://www.npmjs.com/package/mcp-remote):

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

**Trying it by hand:** the [MCP Inspector](https://github.com/modelcontextprotocol/inspector)
(`npx @modelcontextprotocol/inspector`) connects with transport "Streamable HTTP", the URL above,
and an `Authorization` header.

Then ask, for example: *"Run a Big Picture EventStorming of online food ordering and draw it."*

## Tools

| Tool | Changes the board? | What it does |
|---|---|---|
| `list_element_types` | no | The notation: every element type (what it means, when to use it, how to write it, whether it can be pivotal) and the three levels with their palettes. Agents should call it first. |
| `list_boards` | no | The team's boards with their ids, levels, sizes and links. |
| `get_board` | no | A board's elements in timeline order (id, type, text, position, size, pivotal, version, and the swimlane and boundary each sits in) and its arrows. |
| `create_board` | adds | Draws a new board in one call from a name, a level, elements and arrows, and lays it out. |
| `add_to_board` | adds | Continues a board: new elements and arrows, laid out after what is there. Up to 500 elements per call. |
| `update_element` | changes | Changes one element's text, type, position, size, pivotal mark or color, optionally only if still at an expected version. |
| `move_elements` | changes | Moves elements to new positions in one change. |
| `connect_elements` | adds | Draws an arrow between two elements already on the board. |
| `delete_elements` | deletes | Deletes elements, with the arrows that touch them. |
| `delete_connections` | deletes | Deletes arrows. |
| `replace_board_content` | replaces | Redraws a whole board from scratch, in one change. |

Each tool's description and input schema say exactly what it takes. Clients show the annotations:
read-only for the first three, and destructive for the delete and replace tools. Results carry both
a short summary with the board's link, and structured JSON.

## Resources and prompts

| Resource | Content |
|---|---|
| `eventstorming://guide` | The modelling guide: the notation, the Board Document format the tools use, a worked example, common mistakes ([`llm-guide.md`](llm-guide.md)). |
| `eventstorming://element-types` | The same notation `list_element_types` returns. |
| `eventstorming://boards/{boardId}` | A board, as `get_board` returns it. |

| Prompt | Arguments | Starts |
|---|---|---|
| `big_picture` | `domain`, optional `focus` | A Big Picture EventStorming of a domain, drawn on a new board. |
| `process_modelling` | `process`, optional `boardId` | A Process Modelling session for one process, on a new board or continuing an existing one. |

The server also sends instructions when a client connects, summarising how to draw well.

## How drawing works

Agents describe the story; the server draws it. The element format is the Board Document's, so
[`llm-guide.md`](llm-guide.md) applies unchanged.

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

Those are `create_board`'s arguments. The layout follows these rules:

- **The array is the timeline.** Elements go left to right in the order listed, one column each, across all swimlanes.
- **Anchored elements stack below their anchor** in the same column.
- **Swimlanes** are horizontal bands; elements naming a lane sit in it.
- **Boundaries** become boxes around the elements that name them.
- **Pivotal events** get extra space before them.
- **Positions are optional.** Give one only to pin an element somewhere.

`add_to_board` continues after the last sticky already on the board. New elements may name
swimlanes and boundaries by the ids `get_board` shows. A swimlane or boundary that is too small grows
to hold them, and the result lists it under `resized`.

The result of `create_board` and `add_to_board` maps each `key` to the id the element was given, for
later edits.

## When a tool is refused

A refusal is a normal tool result with `isError: true`. **Nothing was changed**, and the result lists
every problem at once, each with the field, a code and a fix:

```
create_board was refused with 2 problems. Nothing was changed. Fix them all and call create_board again:
- elements[1].type: 'hotspot' is not an element type. [unknown-element-type] Fix: Use one of: domain-event, command, actor, policy, read-model, external-system, aggregate, hot-spot, opportunity, swimlane, boundary. See GET /api/v1/element-types.
- elements[1].anchor: No element in this request has key 'plced', and no element on the board has that id. [unknown-reference] Fix: Did you mean 'placed'? Use the key of an element defined in this request, or the id of an existing element.
```

The structured result carries the same problems as JSON (`problems[]` with `field`, `code`, `kind`,
`message`, `fix`). Models correct themselves from this without help.

## Security

- The endpoint accepts only team API keys, over the same authentication as `/api/v1`. Requests without a key get `401`.
- Every tool runs the same use case as the web app, so the same authorization applies: a read-only key can read but not draw, and nobody reaches another team's boards.
- Changes are attributed to the key's name. People on the board see "“name” (an API key) is changing this board".
- Browsers cannot call the endpoint; it has no CORS policy, since it is for agents, not web pages.
- The server is stateless: it keeps no sessions, and makes no requests back to the client (no sampling or elicitation).
