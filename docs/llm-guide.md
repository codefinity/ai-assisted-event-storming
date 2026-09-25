# Guide for LLM agents

You can read and build EventStorming boards through the public API. This guide gives you the
vocabulary, the one JSON format you need (the **Board Document**), a complete worked example, and the
mistakes to avoid. The full reference is [`api.md`](api.md); the JSON Schema is at
`GET /api/v1/schemas/board-document.json`.

## The short version

1. `GET /api/v1/element-types`: learn the element types and each level's palette. Use only type ids from here.
2. Write the model as a **Board Document**: elements in timeline order, left to right, with `key`s; arrows between keys.
3. `POST /api/v1/boards/import` to create a board from it. Leave positions out: the server lays everything out.
4. Read the errors if it fails. Each one names the field (`pointer`) and says how to fix it (`fix`). Fix all of them and resend.
5. To add to an existing board, `POST /api/v1/boards/{boardId}/elements/bulk` with the same element format.
6. Send an `Idempotency-Key` header (a fresh UUID per logical request) with every write, so retrying after a timeout never duplicates work.

Authenticate every call with `Authorization: Bearer es_…` (a team API key with the `write` scope to change things).

## What EventStorming is

EventStorming models a business process as a **timeline of Domain Events**: things that happened,
in the past tense, in the words the business uses. The board reads left to right in time. Around the
events you add who decided (Actors), what they decided (Commands), what reacts (Policies), what they
looked at (Read Models), what we do not control (External Systems), and open questions (Hot Spots).

## The notation

Always take the list from `GET /api/v1/element-types`: types are data, and a server may define more
or fewer. These are the defaults.

| Type id | What it is | Write it as | Examples |
|---|---|---|---|
| `domain-event` | Something that happened that a domain expert cares about. The backbone of every board: start here. | Past tense, business language. Not "Place order", not "OrderCreatedEvent". | "Order Placed", "Payment Declined", "Courier Assigned" |
| `command` | A decision or intention that causes a Domain Event, issued by an Actor or a Policy. Put it just before the event it causes. | Imperative | "Place Order", "Cancel Subscription" |
| `actor` | A person, role or group who decides, usually by issuing a Command. | A role, not a person's name | "Customer", "Restaurant manager" |
| `policy` | The reaction to an event: whenever something happens, then do something. | "Whenever X, then Y" | "Whenever payment is declined, notify the customer" |
| `read-model` | The information an Actor needs to decide. | What is shown | "Menu with prices and delivery time" |
| `external-system` | A system we do not control: a third party, a legacy system, another department's software. | Its name | "Payment provider", "SMS gateway" |
| `aggregate` | What accepts or rejects a Command, enforcing the rules, and emits the resulting events. | A noun, optionally with its key rule | "Order - cannot be changed once dispatched" |
| `hot-spot` | A question, risk, problem or disagreement. The more, the better. | A question or problem statement | "What if the restaurant is closed?" |
| `opportunity` | An idea for making things better, often the answer to a Hot Spot. | A proposal | "Show delivery time on the menu" |
| `swimlane` | A horizontal band separating parallel flows, per actor, department or sub-process. | What the lane is for | "Customer", "Kitchen" |
| `boundary` | An area around the elements that belong to one candidate bounded context. | The context's name | "Ordering", "Delivery" |

A **pivotal** Domain Event (`"pivotal": true`) is a turning point between phases of the story, such
as "Order Placed" or "Order Picked Up". Only `domain-event` can be pivotal. Mark just a few per board.

### Levels

A board has one level, fixed when it is created. The level decides the palette; every type is still allowed.

| Level | Use it to | Typical content |
|---|---|---|
| `big-picture` | Explore a whole business line. | Many Domain Events, Hot Spots, pivotal events, Actors, External Systems, swimlanes, boundaries |
| `process-modelling` | Model one process step by step. | The grammar: **Actor** looks at a **Read Model**, issues a **Command**; a system accepts it and a **Domain Event** happens; a **Policy** reacts with the next **Command** |
| `software-design` | Design the software. | **Aggregates** between the Commands they accept and the Domain Events they emit, **boundaries** for contexts |

## The Board Document

```json
{
  "version": 1,
  "board": { "name": "…", "level": "big-picture" },
  "elements": [ { "key": "…", "type": "…", "text": "…" } ],
  "connections": [ { "from": "<key>", "to": "<key>" } ]
}
```

Each element:

| Field | Required | Meaning |
|---|---|---|
| `type` | yes | A type id from `/element-types`. |
| `text` | no | What the sticky says, up to the type's `maxTextLength` (see `/element-types`; 80 to 300 characters). For swimlanes and boundaries, the label. Keep stickies short. |
| `key` | no, but use it | Your name for the element, unique in the document, so others can refer to it. Keep it short and readable: `order-placed`. |
| `swimlane` | no | The key of a `swimlane` element this element sits in. |
| `boundary` | no | The key of a `boundary` element this element belongs to. |
| `anchor` | no | The key of an element to stack this one **below**, in the same column. Use it for Hot Spots, Opportunities, External Systems and Actors that belong to one event. |
| `pivotal` | no | `true` for a pivotal Domain Event. |
| `position` | no | `{ "x": 400, "y": 40 }`, the top-left corner. **Leave it out** and the element is placed for you. |
| `size` | no | `{ "width": 160, "height": 100 }`. Leave it out for the type's default size. |
| `color` | no | `#RRGGBB` to override the type's color. Rarely needed. |

Each connection is `{ "from": "<key>", "to": "<key>", "label": "optional" }`: an arrow. `from` and
`to` may also be ids of elements already on the board.

### How the layout works

You describe the story; the server draws it.

- **Array order is the timeline.** Each element without an `anchor` gets its own column, left to right, in the order you list them, across all lanes. So list events in the order they happen, even when they sit in different lanes.
- **Anchored elements stack below their anchor** in the same column and do not advance the timeline.
- **Swimlanes** are listed once (anywhere in the array, conventionally first). Elements naming a lane are placed inside its band, and lanes stack top to bottom in the order listed.
- **Boundaries** are also listed once; each is drawn as a box around the elements that name it.
- **Pivotal events** get extra space before them, so the phases read apart.
- Anything with an explicit `position` stays exactly there.
- Adding to an existing board with bulk starts to the right of what is already there.

## Worked example

The Big Picture of online food ordering, as in [`examples/food-ordering.board.json`](examples/food-ordering.board.json):

```json
{
  "version": 1,
  "board": { "name": "Online food ordering", "level": "big-picture" },
  "elements": [
    { "key": "lane-customer", "type": "swimlane", "text": "Customer" },
    { "key": "lane-restaurant", "type": "swimlane", "text": "Restaurant" },
    { "key": "lane-courier", "type": "swimlane", "text": "Courier" },

    { "key": "ctx-ordering", "type": "boundary", "text": "Ordering" },
    { "key": "ctx-kitchen", "type": "boundary", "text": "Kitchen" },
    { "key": "ctx-delivery", "type": "boundary", "text": "Delivery" },

    { "key": "customer", "type": "actor", "text": "Customer", "swimlane": "lane-customer" },
    { "key": "basket-filled", "type": "domain-event", "text": "Basket Filled", "swimlane": "lane-customer", "boundary": "ctx-ordering" },
    { "key": "order-placed", "type": "domain-event", "text": "Order Placed", "swimlane": "lane-customer", "boundary": "ctx-ordering", "pivotal": true },
    { "key": "payment-provider", "type": "external-system", "text": "Payment provider", "anchor": "order-placed" },
    { "key": "payment-authorised", "type": "domain-event", "text": "Payment Authorised", "swimlane": "lane-customer", "boundary": "ctx-ordering" },
    { "key": "declined-late", "type": "hot-spot", "text": "What if payment is declined after the kitchen has started?", "anchor": "payment-authorised" },

    { "key": "order-accepted", "type": "domain-event", "text": "Order Accepted", "swimlane": "lane-restaurant", "boundary": "ctx-kitchen" },
    { "key": "restaurant-closed", "type": "hot-spot", "text": "What if the restaurant closes before accepting?", "anchor": "order-accepted" },
    { "key": "meal-prepared", "type": "domain-event", "text": "Meal Prepared", "swimlane": "lane-restaurant", "boundary": "ctx-kitchen" },

    { "key": "courier-assigned", "type": "domain-event", "text": "Courier Assigned", "swimlane": "lane-courier", "boundary": "ctx-delivery" },
    { "key": "order-picked-up", "type": "domain-event", "text": "Order Picked Up", "swimlane": "lane-courier", "boundary": "ctx-delivery", "pivotal": true },
    { "key": "live-location", "type": "opportunity", "text": "Show the courier's live location", "anchor": "order-picked-up" },
    { "key": "order-delivered", "type": "domain-event", "text": "Order Delivered", "swimlane": "lane-courier", "boundary": "ctx-delivery" },
    { "key": "estimates-wrong", "type": "hot-spot", "text": "Delivery estimates are often wrong", "anchor": "order-delivered" },

    { "key": "order-rated", "type": "domain-event", "text": "Order Rated", "swimlane": "lane-customer" }
  ],
  "connections": [
    { "from": "order-placed", "to": "payment-authorised" },
    { "from": "payment-authorised", "to": "order-accepted" },
    { "from": "meal-prepared", "to": "order-picked-up" },
    { "from": "order-delivered", "to": "order-rated" }
  ]
}
```

What the server makes of it:

- Three lanes, top to bottom: Customer, Restaurant, Courier.
- The story runs left to right in the order listed. "Order Accepted" lands in the Restaurant lane, one column to the right of "Payment Authorised": time keeps moving forward across lanes.
- "Payment provider" and the payment Hot Spot stack under the events they anchor to.
- Two dashed dividers mark the pivotal "Order Placed" and "Order Picked Up".
- Three boundary boxes wrap the events of Ordering, Kitchen and Delivery.

Send it:

```http
POST /api/v1/boards/import
Authorization: Bearer es_…
Content-Type: application/json
Idempotency-Key: 3f1c0b8e-5d2a-4a57-9a0e-7c1b2d3e4f50

{ …the document above… }
```

The `201` response has the new `board` (with its `id`), `elementCount`, `connectionCount`, and
`keys`, which maps each of your keys to the id the server gave that element. Keep `keys` if you
will change individual elements later.

### Adding to it later

To continue the story on the same board, send only the new elements to the bulk endpoint. New
elements start to the right of the existing content; refer to existing elements by id:

```http
POST /api/v1/boards/{boardId}/elements/bulk
Idempotency-Key: 8e2f…

{
  "elements": [
    { "key": "refund-requested", "type": "domain-event", "text": "Refund Requested" },
    { "key": "refund-paid", "type": "domain-event", "text": "Refund Paid" },
    { "key": "who-pays", "type": "hot-spot", "text": "Who pays when the courier is late?", "anchor": "refund-requested" }
  ],
  "connections": [
    { "from": "<id of Order Delivered>", "to": "refund-requested" },
    { "from": "refund-requested", "to": "refund-paid" }
  ]
}
```

### Reading a board

`GET /api/v1/boards/{boardId}/document` returns the board as a Board Document with every position
filled in and element ids as keys: the easiest thing to reason about. To change one sticky, use
`PATCH /api/v1/boards/{boardId}/elements/{id}` with only the fields that change, e.g.
`{ "text": "Order Submitted" }`. To rewrite the whole board, edit the exported document and
`PUT` it back to `/boards/{boardId}/document`. That replaces everything, and people with the board
open see it at once.

## Common mistakes

| Mistake | What you get | Do this instead |
|---|---|---|
| Inventing a type id (`"event"`, `"hotspot"`, `"Command"`) | `422 unknown-element-type` with the valid ids in `fix` | Use ids from `/element-types`, lower-case with hyphens. |
| Events in the wrong tense ("Place Order" as a `domain-event`) | Accepted, but wrong | Events are past tense; the imperative form is a `command`. |
| Technical names ("OrderCreatedEvent", "POST /orders") | Accepted, but useless to the business | Use business language. |
| Referring to a key that is not in the request | `422 unknown-reference`, often with "Did you mean …?" | Define every key you use, or use an existing element's id. |
| An element anchoring or connecting to itself | `422 self-reference` | Point at another element. |
| `"swimlane"` or `"boundary"` naming an element of another type | `422 not-a-swimlane` / `not-a-boundary` | Point at a `swimlane` / `boundary` element. |
| `"pivotal": true` on anything but a Domain Event | `422 not-pivotal-type` | Only mark `domain-event`s pivotal. |
| Guessing positions for everything | Overlaps and a messy board | Leave `position` out; order the array instead. |
| Numbers as strings (`"x": "40"`) | Tolerated, but not what the schema says | Send plain numbers. |
| Unknown or misspelt fields (`"txt"`, `"colour"`) | `400` naming the field | Use exactly the fields in the schema. |
| More than 500 elements in one bulk request | `422 too-many` | Split into batches of up to 500, or import a whole document (up to 5,000 elements). |
| Retrying a write without an `Idempotency-Key` after a timeout | Duplicates | Send an `Idempotency-Key`; reuse it only for the identical retry. |
| Reusing an `Idempotency-Key` for a different request | `422 idempotency-key-reused` | Use a fresh key for every new request. |
| Changing a board's level on replace | `422 level-immutable` | Create a new board at the other level. |
| Hitting `429` | `Retry-After` header | Wait that many seconds; use bulk instead of one call per sticky. |

## Good modelling habits

- **Start with events only.** A Big Picture board is mostly `domain-event`s in timeline order, with `hot-spot`s wherever something is unclear. Add actors, systems and lanes after the story holds together.
- **One fact per sticky.** Split "Order Placed and Paid" into two events.
- **Hot Spots are welcome.** Uncertainty you write down is the most valuable output of a session.
- **Mark few pivotal events**, two to four per board, at the points where the story changes phase.
- **Name boundaries after the language they speak** (Ordering, Kitchen, Delivery), not after teams or systems.
- **Process Modelling follows the grammar.** For each step: Actor → (Read Model) → Command → Domain Event → Policy → next Command. List them in that order so they lay out left to right.
