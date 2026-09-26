# 11. An MCP server as another driving adapter, in the API process

- **Status:** accepted
- **Date:** 2026-09-26

## Context

External systems should be able to draw EventStorming diagrams. More and more of them are AI agents
that speak the Model Context Protocol. The public REST API already offers everything, but an agent
has to be taught the API first. MCP tools come with their descriptions and schemas, and clients
discover them on connect.

The drawing must land on real boards, with the same validation, layout, authorization and live
updates as everything else.

## Decision

- **A new driving adapter, `EventStorming.Mcp`**, built on the official C# SDK (`ModelContextProtocol.AspNetCore`).
  - Its tools call Board Modelling's use cases through their handler interfaces, like the REST API and the SignalR hub do.
  - It references no other adapter. The host passes it the claims-to-`Actor` mapping (`Actors.From`) and the web app's address.
- **Hosted in the API process, at `/mcp`**, over Streamable HTTP in stateless mode.
  - The board feed and presence are in memory (ADR 4), so only this process can make an agent's drawing appear live on open boards.
  - A separate stdio server would need its own broadcasting. Clients that only run stdio servers connect through a bridge such as `mcp-remote`.
- **Authentication and limits come from the host:** team API keys (the `api-read` policy) and the public API's per-key rate limit.
  - Drawing needs the `write` scope. The use cases enforce it through `IBoardAccess`, not the adapter.
- **The tools model the job, not the endpoints.** There are 11 of them:
  - `create_board` draws a whole diagram in one call, from the Board Document's element shape;
  - `add_to_board`, `update_element`, `move_elements`, `connect_elements`, `delete_elements`, `delete_connections` and `replace_board_content` edit it;
  - `list_element_types`, `list_boards` and `get_board` read.
  - Annotations mark read-only and destructive tools.
- **Refusals are tool results, not protocol errors.** They set `isError` and list every problem with its field, code and fix, so a model can correct itself and retry. Nothing changes when a tool is refused.
- **Schemas say what may be null.** The tools are registered one by one with a schema transform, so required fields and list items are described as non-null. Numbers are plain numbers.
- **Resources and prompts:**
  - resources: the modelling guide (`eventstorming://guide`), the notation, and any board by id;
  - prompts: `big_picture` and `process_modelling`;
  - server instructions summarise how to draw well.

## Consequences

- An agent can draw a complete, laid-out board in one tool call, and people watching see it appear.
- Layout had to handle agents that add to a board bit by bit. New elements now continue after the last sticky, and existing swimlanes and boundaries grow to hold them (ADR 9). The REST bulk endpoint benefits too.
- There are now three driving adapters over the same use cases. A change of rules in the core reaches all of them at once.
- The MCP endpoint shares the API instance's limits. Scaling out needs the same backplane as the hub (ADR 4).
