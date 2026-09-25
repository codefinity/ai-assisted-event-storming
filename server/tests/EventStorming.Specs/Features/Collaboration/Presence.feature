Feature: Presence
  People on the same board see each other: an avatar for everyone there, their cursors, who is
  editing which sticky, and stickies moving while someone drags them. Nothing here is stored.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And Ana owns the team "Food delivery"
    And Bo is a viewer of "Food delivery"
    And "Food delivery" has a "big-picture" board called "Ordering"
    And the board "Ordering" has these elements:
      | key    | type         | text         |
      | placed | domain-event | Order Placed |

  Scenario: Joining a board shows who is already there
    Given Ana has joined "Ordering" on connection "ana-1"
    When Bo joins "Ordering" on connection "bo-1"
    Then the request succeeds
    And Bo sees 2 participants, including themselves
    And the others are told that Bo joined
    And Bo always gets the same color

  Scenario: Only members can join
    Given Cy has an account
    When Cy joins "Ordering" on connection "cy-1"
    Then the request is refused as "not-found"

  Scenario: Disconnecting leaves the board
    Given Ana has joined "Ordering" on connection "ana-1"
    When connection "ana-1" disconnects
    Then 0 people are on "Ordering"
    And the others are told that Ana left

  Scenario: A second tab is a second participant
    Given Ana has joined "Ordering" on connection "ana-1"
    When Ana joins "Ordering" on connection "ana-2"
    Then 2 people are on "Ordering"

  Scenario: Cursors are shared with everyone else on the board
    Given Ana has joined "Ordering" on connection "ana-1"
    When connection "ana-1" moves its cursor to 120,80 on "Ordering"
    Then the request succeeds
    And the others are told that Ana cursor

  Scenario: A cursor cannot be moved on a board that was not joined
    When connection "nobody" moves its cursor to 120,80 on "Ordering"
    Then the request fails with "not-joined"

  Scenario: Others see who is editing which sticky
    Given Ana has joined "Ordering" on connection "ana-1"
    When connection "ana-1" starts editing "placed" on "Ordering"
    Then connection "ana-1" is shown editing "placed"
    And the others are told that Ana focus

  Scenario: Others see a drag while it happens
    Given Ana has joined "Ordering" on connection "ana-1"
    When connection "ana-1" drags "placed" to 400,40 on "Ordering"
    Then the request succeeds
    And the others are told that Ana drag
