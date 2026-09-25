Feature: Board documents and auto-layout
  A whole board can be created or replaced from one Board Document - the format the public API and
  LLMs use. Array order is timeline order and positions are optional: whatever has none is laid out
  left to right, in its swimlane, inside its boundary, with room around pivotal events.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And Ana owns the team "Food delivery"
    And Bo is a viewer of "Food delivery"

  Rule: Import a new board

    Scenario: A document without positions becomes a readable timeline
      When Ana imports this document into "Food delivery":
        """
        {
          "version": 1,
          "board": { "name": "Online food ordering", "level": "big-picture" },
          "elements": [
            { "key": "placed", "type": "domain-event", "text": "Order Placed" },
            { "key": "paid", "type": "domain-event", "text": "Payment Taken" },
            { "key": "late", "type": "hot-spot", "text": "What if the kitchen is busy?", "anchor": "paid" },
            { "key": "cooked", "type": "domain-event", "text": "Meal Cooked" }
          ],
          "connections": [ { "from": "placed", "to": "paid" } ]
        }
        """
      Then the request succeeds
      And the board "Online food ordering" reads left to right:
        | event         |
        | Order Placed  |
        | Payment Taken |
        | Meal Cooked   |
      And "late" is in the same column as "paid", below it
      And "placed" is connected to "paid"

    Scenario: Swimlanes stack, and each element sits inside its own
      When Ana imports this document into "Food delivery":
        """
        {
          "board": { "name": "Lanes", "level": "big-picture" },
          "elements": [
            { "key": "customer", "type": "swimlane", "text": "Customer" },
            { "key": "kitchen", "type": "swimlane", "text": "Kitchen" },
            { "key": "placed", "type": "domain-event", "text": "Order Placed", "swimlane": "customer" },
            { "key": "cooked", "type": "domain-event", "text": "Meal Cooked", "swimlane": "kitchen" }
          ]
        }
        """
      Then the request succeeds
      And "placed" is inside "customer"
      And "cooked" is inside "kitchen"
      And "customer" is above "kitchen"
      And "placed" is left of "cooked"

    Scenario: A boundary surrounds its members, and only them
      When Ana imports this document into "Food delivery":
        """
        {
          "board": { "name": "Contexts", "level": "big-picture" },
          "elements": [
            { "key": "ordering", "type": "boundary", "text": "Ordering" },
            { "key": "placed", "type": "domain-event", "text": "Order Placed", "boundary": "ordering" },
            { "key": "paid", "type": "domain-event", "text": "Payment Taken", "boundary": "ordering" },
            { "key": "cooked", "type": "domain-event", "text": "Meal Cooked" }
          ]
        }
        """
      Then "placed" is inside "ordering"
      And "paid" is inside "ordering"
      And "cooked" is not inside "ordering"

    Scenario: Pivotal events get room around them
      When Ana imports this document into "Food delivery":
        """
        {
          "board": { "name": "Pivots", "level": "big-picture" },
          "elements": [
            { "key": "a", "type": "domain-event", "text": "Basket Filled" },
            { "key": "b", "type": "domain-event", "text": "Order Placed", "pivotal": true },
            { "key": "c", "type": "domain-event", "text": "Payment Taken" },
            { "key": "d", "type": "domain-event", "text": "Receipt Sent" }
          ]
        }
        """
      Then the gap before "b" is wider than the gap before "d"

    Scenario: Explicit positions are kept
      When Ana imports this document into "Food delivery":
        """
        {
          "board": { "name": "Positioned", "level": "big-picture" },
          "elements": [
            { "key": "placed", "type": "domain-event", "text": "Order Placed", "position": { "x": 500, "y": 300 } }
          ]
        }
        """
      Then "placed" keeps its position 500,300

    Scenario: Every mistake in a document is reported at once, with a fix
      When Ana imports this document into "Food delivery":
        """
        {
          "version": 2,
          "board": { "level": "detailed" },
          "elements": [
            { "key": "placed", "type": "domain-event", "text": "Order Placed" },
            { "key": "placed", "type": "event", "text": "Order Paid" },
            { "key": "lane", "type": "domain-event", "text": "Kitchen" },
            { "key": "cooked", "type": "domain-event", "text": "Meal Cooked", "swimlane": "lane" }
          ],
          "connections": [ { "from": "placed", "to": "cookd" } ]
        }
        """
      Then the request fails with these failures:
        | field                 | code                |
        | version               | unsupported-version |
        | board.level           | unknown-level       |
        | board.name            | required            |
        | elements[1].key       | duplicate-key       |
      And every failure says how to fix it

    Scenario: References must point at the right kind of element
      When Ana imports this document into "Food delivery":
        """
        {
          "board": { "name": "Refs", "level": "big-picture" },
          "elements": [
            { "key": "lane", "type": "domain-event", "text": "Kitchen" },
            { "key": "cooked", "type": "domain-event", "text": "Meal Cooked", "swimlane": "lane" }
          ],
          "connections": [ { "from": "cooked", "to": "cookd" } ]
        }
        """
      Then the request fails with these failures:
        | field               | code              |
        | elements[1].swimlane| not-a-swimlane    |
        | connections[0].to   | unknown-reference |

    Scenario: Viewers cannot import
      When Bo imports this document into "Food delivery":
        """
        { "board": { "name": "Mine", "level": "big-picture" }, "elements": [] }
        """
      Then the request is refused as "forbidden"

  Rule: Replace a board's content

    Background:
      Given "Food delivery" has a "big-picture" board called "Ordering"
      And the board "Ordering" has these elements:
        | key | type         | text      |
        | old | domain-event | Old Event |

    Scenario: Replacing swaps all the content and tells open boards to reload
      When Ana replaces the content of "Ordering" with:
        """
        { "elements": [ { "key": "new", "type": "domain-event", "text": "New Event" } ] }
        """
      Then the request succeeds
      And the board "Ordering" has 1 element
      And the element "new" says "New Event"
      And the whole content change is announced for "Ordering"

    Scenario: The level of an existing board cannot change
      When Ana replaces the content of "Ordering" with:
        """
        { "board": { "level": "software-design" }, "elements": [] }
        """
      Then the request fails with "level-immutable" on "board.level"

  Rule: Export a board

    Background:
      Given "Food delivery" has a "big-picture" board called "Ordering"

    Scenario: An export can be imported again, exactly as it was
      Given Ana has replaced the content of "Ordering" with:
        """
        {
          "elements": [
            { "key": "customer", "type": "swimlane", "text": "Customer" },
            { "key": "placed", "type": "domain-event", "text": "Order Placed", "swimlane": "customer" },
            { "key": "paid", "type": "domain-event", "text": "Payment Taken", "swimlane": "customer" }
          ],
          "connections": [ { "from": "placed", "to": "paid" } ]
        }
        """
      When Bo exports "Ordering"
      Then the request succeeds
      And the export has 3 elements and 1 connection
      And the export says "placed" sits in the swimlane "customer"
      When Ana imports that export into "Food delivery"
      Then the request succeeds
      And the copy has every element where the original had it
