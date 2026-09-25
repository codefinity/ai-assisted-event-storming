Feature: Membership
  Owners change members' roles and remove members; anyone can leave. A team always keeps at least
  one Owner.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And Ana owns the team "Checkout squad"
    And Bo is an editor of "Checkout squad"

  Scenario: An Owner changes a member's role
    When Ana makes Bo a viewer of "Checkout squad"
    Then the request succeeds
    And Bo is a viewer of "Checkout squad"

  Scenario: An Owner can make another Owner
    When Ana makes Bo an owner of "Checkout squad"
    Then Bo is the owner of "Checkout squad"

  Scenario: Only Owners change roles
    When Bo makes Bo an owner of "Checkout squad"
    Then the request is refused as "forbidden"

  Scenario: The last Owner cannot step down
    When Ana makes Ana an editor of "Checkout squad"
    Then the request fails with "last-owner"
    And every failure says how to fix it

  Scenario: With a second Owner, the first can step down
    Given Bo is an owner of "Checkout squad"
    When Ana makes Ana an editor of "Checkout squad"
    Then the request succeeds
    And Ana is an editor of "Checkout squad"

  Scenario: An Owner removes a member
    When Ana removes Bo from "Checkout squad"
    Then the request succeeds
    And Bo is not a member of "Checkout squad"

  Scenario: A member leaves
    When Bo leaves "Checkout squad"
    Then the request succeeds
    And Bo is not a member of "Checkout squad"

  Scenario: Editors cannot remove others
    Given Cy is a viewer of "Checkout squad"
    When Bo removes Cy from "Checkout squad"
    Then the request is refused as "forbidden"

  Scenario: The last Owner cannot leave
    When Ana leaves "Checkout squad"
    Then the request fails with "last-owner"
