Feature: The board catalog
  A team's dashboard lists its boards. Owners and Editors create, rename, duplicate, archive and
  restore them; Viewers can only look. A board's level is chosen when it is created.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And Ana owns the team "Food delivery"
    And Bo is a viewer of "Food delivery"

  Rule: Create a board

    Scenario Outline: An Editor creates a board at any level
      When Ana creates a "<level>" board called "Ordering" in "Food delivery"
      Then the request succeeds
      And "Food delivery" has a board called "Ordering" at the "<level>" level

      Examples:
        | level             |
        | big-picture       |
        | process-modelling |
        | software-design   |

    Scenario: The level must be one of the three
      When Ana creates a "detailed" board called "Ordering" in "Food delivery"
      Then the request fails with "unknown-level" on "level"
      And every failure says how to fix it

    Scenario: Viewers cannot create boards
      When Bo creates a "big-picture" board called "Ordering" in "Food delivery"
      Then the request is refused as "forbidden"

    Scenario: Outsiders cannot even tell the team exists
      Given Cy has an account
      When Cy creates a "big-picture" board called "Ordering" in "Food delivery"
      Then the request is refused as "not-found"

  Rule: List the boards

    Scenario: The dashboard shows live boards, most recently changed first
      Given "Food delivery" has a "big-picture" board called "Whole business"
      And "Food delivery" has a "process-modelling" board called "Checkout"
      And "Food delivery" has a "big-picture" board called "Old ideas"
      And the board "Old ideas" is archived
      When Bo lists the boards of "Food delivery"
      Then the request succeeds
      And the boards listed are, most recent first:
        | board          |
        | Checkout       |
        | Whole business |
      And they may view the team's boards

    Scenario: Archived boards can be listed too
      Given "Food delivery" has a "big-picture" board called "Old ideas"
      And the board "Old ideas" is archived
      When Ana lists the boards of "Food delivery" including archived ones
      Then the boards listed are, most recent first:
        | board     |
        | Old ideas |
      And they may edit the team's boards

    Scenario: Long lists come in pages
      Given "Food delivery" has a "big-picture" board called "One"
      And "Food delivery" has a "big-picture" board called "Two"
      And "Food delivery" has a "big-picture" board called "Three"
      When Ana lists the boards of "Food delivery" 2 at a time
      Then the boards listed are, most recent first:
        | board |
        | Three |
        | Two   |
      And the listing has a next page

    Scenario: A cursor must be one the API issued
      When Ana lists the boards of "Food delivery" from the cursor "made-up"
      Then the request fails with "invalid-cursor" on "cursor"

  Rule: Change a board

    Scenario: Renaming a board tells everyone who has it open
      Given "Food delivery" has a "big-picture" board called "Draft"
      When Ana renames the board "Draft" to "Food ordering"
      Then the request succeeds
      And the new name is announced to everyone on the board

    Scenario: Viewers cannot rename
      Given "Food delivery" has a "big-picture" board called "Draft"
      When Bo renames the board "Draft" to "Mine now"
      Then the request is refused as "forbidden"

    Scenario: Archiving makes a board read-only, restoring brings it back
      Given "Food delivery" has a "big-picture" board called "Draft"
      When Ana archives the board "Draft"
      Then the board "Draft" is archived
      And the change of state is announced to everyone on the board
      When Ana renames the board "Draft" to "Still editing"
      Then the request fails with "board-archived"
      When Ana restores the board "Draft"
      Then the board "Draft" is not archived

  Rule: Open a board

    Scenario: Members open a board with the permission their role gives them
      Given "Food delivery" has a "big-picture" board called "Whole business"
      When Bo opens the board "Whole business"
      Then the request succeeds
      And they can view the board
      When Ana looks up the board "Whole business"
      Then they can edit the board

  Rule: The notation

    Scenario: Everyone can read the element types and levels
      When Ana asks for the element types
      Then the notation lists 11 element types and 3 levels
