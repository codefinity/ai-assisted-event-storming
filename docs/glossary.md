# Glossary

These words mean the same thing in the code, the UI, the API and these docs. Type ids are the keys
of the element-type registry (`server/src/driven-adapters/EventStorming.ElementTypes.Json/element-types.json`).

## Element types

The registry defines these. Nothing about them is hard-coded in the server or the web app: names,
colors, icons, sizes, shortcuts, levels and guidance all come from the registry.

| Name | Type id | Meaning | Write it as | Shortcut |
|---|---|---|---|---|
| **Domain Event** | `domain-event` | Something that happened that domain experts care about. The backbone of the timeline. | Past tense: "Order Placed" | `E` |
| **Command** | `command` | A decision or intention that causes events. | Imperative: "Place Order" | `C` |
| **Actor** | `actor` | A person, role or group that issues a command. | Noun: "Customer" | `A` |
| **Policy** | `policy` | Reactive logic between an event and a command, automated or human. | "Whenever X, then Y" | `P` |
| **Read Model** | `read-model` | Information an actor needs in order to decide. | Noun phrase: "Menu with prices" | `R` |
| **External System** | `external-system` | A system outside our control that receives commands or emits events. | Name: "Payment Provider" | `X` |
| **Aggregate / Constraint** | `aggregate` | The thing that accepts or rejects a command, enforces rules, and produces events. | Noun: "Order" | `G` |
| **Hot Spot** | `hot-spot` | A question, risk, problem or disagreement. Mark it now; resolve it later. | Question or statement | `H` |
| **Opportunity** | `opportunity` | An idea for improvement. | Statement | `O` |
| **Swimlane** | `swimlane` | A structure: a horizontal band separating parallel flows, actors or departments. | Its label | `L` |
| **Boundary** | `boundary` | A structure: a labelled area around the elements of one candidate bounded context. | The context's name | `B` |

Every type has an icon and a name on the sticky as well as a color, so meaning never depends on
color alone.

## Board structures

| Term | In the model | Meaning |
|---|---|---|
| **Timeline** | the board's x-axis | The board reads left to right in time. Position is the order; there is no separate sequence field. |
| **Pivotal Event** | `pivotal: true` on a type whose registry entry allows it (Domain Event) | A turning point between phases. Drawn with a heavy left edge and a dashed divider across the board. |
| **Swimlane** | element of type `swimlane` | See above. Elements sit in a lane by position; the Board Document can place them with `"swimlane": "<key>"`. |
| **Boundary** | element of type `boundary` | See above. Deliberately not called "bounded context" in code, so it is never confused with this tool's own bounded contexts. |
| **Connection** | `Connection` | An optional directed arrow between two elements. The UI says "arrow". |

## Levels

| Level | Id | Purpose | Palette |
|---|---|---|---|
| **Big Picture** | `big-picture` | Explore a whole business line with a large, mixed group: hot spots, pivotal events and emerging boundaries. | Domain Event, Actor, External System, Hot Spot, Opportunity, Swimlane, Boundary |
| **Process Modelling** | `process-modelling` | Model one process with the grammar Actor + Read Model → Command → System → Event → Policy → Command. | Domain Event, Command, Actor, Policy, Read Model, External System, Hot Spot, Swimlane |
| **Software Design** | `software-design` | Design the implementation: aggregates, the commands they accept, the events they emit. | Domain Event, Command, Actor, Policy, Read Model, External System, Aggregate, Hot Spot, Boundary |

A level decides what the palette offers first. Every type can still be placed on any board
("More types" in the palette).

## Product vocabulary

| Term | Context | Meaning |
|---|---|---|
| **Account** | Identity | A person who can sign in with email and password. |
| **Session** | Identity | A signed-in period: a 15-minute access token plus a rotating refresh token in an HttpOnly cookie. |
| **Team**, **Member**, **Role** | Teams | Who may see and change which boards. Roles: **Owner** (everything, including members and API keys), **Editor** (boards), **Viewer** (read only). |
| **Invitation** | Teams | A link anyone can use once, or an email invitation only that address can accept. Expires after 7 days. |
| **Board** | Board Modelling | A shared canvas at one level, owned by a team. It can be **archived** (read-only, hidden) and **restored**. |
| **Element** | Board Modelling | Anything on a board: stickies and structures. The UI says "sticky" for sticky-category elements. |
| **Element Type**, **Registry**, **Palette**, **Legend** | Board Modelling | The notation, defined as data. |
| **Revision** | Board Modelling | A per-board counter, raised by every change. Clients use it to put changes in order and to resync. |
| **Version** | Board Modelling | A per-element counter, raised by every change to that element. The public API's `expectedVersion` uses it. |
| **Board Document** | Public Integration | The published JSON format for export, import and bulk creation, with a JSON Schema. |
| **Participant**, **Presence**, **Cursor**, **Editing indicator**, **Drag preview** | Collaboration | Who is on a board and what they are doing right now. All of it is in memory and never stored. |
| **API Key**, **Scope** (`read`, `write`) | Public Integration | How other systems and LLM agents authenticate. `write` implies `read`. |
| **Idempotency Key** | Public Integration | A client-chosen id on a write, so retrying it is safe. |
| **Operation id** | Board Modelling / web app | The web app's id for one change, echoed in the broadcast so the sender recognises its own change. |
