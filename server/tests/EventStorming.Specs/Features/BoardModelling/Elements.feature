Feature: Elements on a board
  Everyone on a board adds, edits, moves and deletes stickies and structures. Every change bumps the
  element's version and the board's revision, and is announced to everyone who has the board open.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And Ana owns the team "Food delivery"
    And Bo is a viewer of "Food delivery"
    And "Food delivery" has a "big-picture" board called "Ordering"

  Rule: Add elements

    Scenario: A Domain Event added at a position lands there with its type's size
      When Ana adds a "domain-event" saying "Order Placed" at 100,40 to "Ordering"
      Then the request succeeds
      And the board "Ordering" has 1 element
      And the element "added" is at 100,40
      And the element "added" is 160 wide and 100 high
      And the change is announced to everyone on the board with operation "op-1"
      And the board "Ordering" is at revision 1

    Scenario: Without a position, a new element continues the timeline to the right
      Given the board "Ordering" has these elements:
        | key    | type         | text         | x   | y  |
        | placed | domain-event | Order Placed | 100 | 40 |
      When Ana adds a "domain-event" saying "Payment Taken" to "Ordering"
      Then the element "added" is placed to the right of "placed"

    Scenario: An element can be stacked below one that is already there
      Given the board "Ordering" has these elements:
        | key    | type         | text         | x   | y  |
        | placed | domain-event | Order Placed | 100 | 40 |
      When Ana adds these elements to "Ordering":
        | key | type     | text                | anchor |
        | q   | hot-spot | What if it is late? | placed |
      Then the element "q" is placed below "placed"

    Scenario: Elements and the arrows between them arrive together, joined by key
      When Ana adds these elements with connections to "Ordering":
        | kind       | key    | type         | text          | from   | to     |
        | element    | placed | domain-event | Order Placed  |        |        |
        | element    | paid   | domain-event | Payment Taken |        |        |
        | connection |        |              |               | placed | paid   |
      Then the request succeeds
      And "placed" is connected to "paid"

    Scenario: Elements given ids by the client can be joined by those ids
      When Ana adds two elements with client ids and an arrow between those ids to "Ordering"
      Then the request succeeds
      And the arrow joins the two client ids

    Scenario: Adding an element with a client id twice stores it once
      When Ana adds the same element with id "5f2d6c9e-1111-4222-8333-944455556666" twice to "Ordering"
      Then the request succeeds
      And the board "Ordering" has 1 element

    Scenario: Unknown types and misplaced pivots are all reported, with the fix
      When Ana adds these elements to "Ordering":
        | key | type    | text         | pivotal |
        | a   | event   | Order Placed |         |
        | b   | command | Place Order  | yes     |
      Then the request fails with these failures:
        | field               | code                 |
        | elements[0].type    | unknown-element-type |
        | elements[1].pivotal | not-pivotal-type     |
      And every failure says how to fix it
      And nothing is announced

    Scenario: A typo in a reference suggests the key that was meant
      When Ana adds these elements to "Ordering":
        | key    | type         | text              | anchor |
        | placed | domain-event | Order Placed      |        |
        | q      | hot-spot     | What if it fails? | plced  |
      Then the request fails with "unknown-reference" on "elements[1].anchor"

    Scenario: Viewers cannot add
      When Bo adds a "domain-event" saying "Order Placed" to "Ordering"
      Then the request is refused as "forbidden"

    Scenario: Up to 500 elements can be added in one request
      When Ana adds 500 elements to "Ordering"
      Then the request succeeds
      And the board "Ordering" has 500 elements

  Rule: Edit an element

    Background:
      Given the board "Ordering" has these elements:
        | key    | type         | text          | x   | y  |
        | placed | domain-event | Order Placed  | 100 | 40 |
        | pay    | command      | Pay for order | 300 | 40 |

    Scenario: Changing the text bumps the version and announces the full element
      When Ana changes the text of "placed" to "Order Submitted"
      Then the request succeeds
      And the element "placed" says "Order Submitted"
      And the element "placed" is at version 2
      And the announcement carries the element "placed" at version 2

    Scenario: A Domain Event can be marked pivotal
      When Ana marks "placed" as pivotal
      Then the element "placed" is pivotal

    Scenario: A Command cannot be pivotal
      When Ana marks "pay" as pivotal
      Then the request fails with "not-pivotal-type" on "pivotal"

    Scenario: Changing a pivotal event into a command quietly drops the emphasis
      Given Ana marks "placed" as pivotal
      When Ana changes the type of "placed" to "command"
      Then the request succeeds
      And the element "placed" is a "command"
      And the element "placed" is not pivotal

    Scenario: A stale expected version is refused and names the current one
      Given Ana changes the text of "placed" to "Order Submitted"
      When Ana changes the text of "placed" to "Order Sent" expecting version 1
      Then the request fails with "version-conflict" on "expectedVersion"
      And the request is refused as "conflict"
      And the element "placed" says "Order Submitted"

    Scenario: Resizing keeps the position
      When Ana resizes "placed" to 240 by 120
      Then the element "placed" is 240 wide and 120 high
      And the element "placed" is at 100,40

    Scenario: An update must change something
      When Ana sends an update for "placed" that changes nothing
      Then the request fails with "nothing-to-change"

    Scenario: Viewers can look but not touch
      When Bo looks up the element "placed"
      Then the request succeeds
      When Bo changes the text of "placed" to "Mine"
      Then the request is refused as "forbidden"

  Rule: Move elements

    Background:
      Given the board "Ordering" has these elements:
        | key    | type         | text          | x   | y  |
        | placed | domain-event | Order Placed  | 100 | 40 |
        | paid   | domain-event | Payment Taken | 300 | 40 |

    Scenario: A selection moves together, as one change
      When Ana moves these elements on "Ordering":
        | key    | x   | y   |
        | placed | 120 | 200 |
        | paid   | 320 | 200 |
      Then the request succeeds
      And the element "placed" is at 120,200
      And the element "paid" is at 320,200
      And the board "Ordering" is at revision 1

    Scenario: Moving an element someone just deleted is skipped, not an error
      When Ana moves these elements on "Ordering":
        | key     | x   | y   |
        | placed  | 120 | 200 |
        | deleted | 0   | 0   |
      Then the request succeeds
      And the element "placed" is at 120,200

  Rule: Delete elements

    Background:
      Given the board "Ordering" has these elements:
        | key    | type         | text          | x   | y  |
        | placed | domain-event | Order Placed  | 100 | 40 |
        | paid   | domain-event | Payment Taken | 300 | 40 |
      And "placed" is connected to "paid"

    Scenario: Deleting an element deletes its arrows too
      When Ana deletes "placed"
      Then the request succeeds
      And "placed" no longer exists
      And the announcement removes "placed" and 1 connection
      And the board "Ordering" has 1 element

    Scenario: Deleting twice is harmless
      Given Ana deletes "placed"
      When Ana deletes "placed" again
      Then the request succeeds

  Rule: Connections

    Background:
      Given the board "Ordering" has these elements:
        | key    | type         | text          | x   | y  |
        | placed | domain-event | Order Placed  | 100 | 40 |
        | paid   | domain-event | Payment Taken | 300 | 40 |

    Scenario: Two elements can be joined by an arrow
      When Ana connects "placed" to "paid"
      Then the request succeeds
      And "placed" is connected to "paid"

    Scenario: An element cannot point at itself
      When Ana connects "placed" to "placed"
      Then the request fails with "self-connection" on "to"

    Scenario: An arrow can be deleted
      Given "placed" is connected to "paid"
      When Ana deletes the connection from "placed" to "paid"
      Then the request succeeds
      And "placed" has no connections

    Scenario: Connections can be listed
      Given "placed" is connected to "paid"
      When Bo lists the connections of "Ordering"
      Then 1 connection is listed

  Rule: List elements

    Scenario: Elements come in pages
      When Ana adds 5 elements to "Ordering"
      And Bo lists the elements of "Ordering" 2 at a time
      Then 3 pages hold 5 elements in total

    Scenario: Elements can be listed by type
      Given the board "Ordering" has these elements:
        | key | type         | text          |
        | e1  | domain-event | Order Placed  |
        | h1  | hot-spot     | Too slow?     |
        | e2  | domain-event | Payment Taken |
      When Bo lists the "domain-event" elements of "Ordering"
      Then 2 elements are listed

    Scenario: Listing by an unknown type explains the valid ones
      When Bo lists the "event" elements of "Ordering"
      Then the request fails with "unknown-element-type" on "type"

  Rule: Duplicate a board

    Scenario: A duplicate copies every element and arrow into a new board
      Given the board "Ordering" has these elements:
        | key    | type         | text          |
        | placed | domain-event | Order Placed  |
        | paid   | domain-event | Payment Taken |
      And "placed" is connected to "paid"
      When Ana duplicates the board "Ordering"
      Then the request succeeds
      And "Food delivery" has a board called "Copy of Ordering" with 2 elements

    Scenario: Viewers cannot duplicate
      When Bo duplicates the board "Ordering"
      Then the request is refused as "forbidden"
