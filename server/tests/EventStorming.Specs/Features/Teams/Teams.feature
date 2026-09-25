Feature: Teams
  A team is a group of people who model together. Whoever creates a team owns it; Owners manage the
  members, Editors change boards, Viewers only look.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And Ana has an account

  Rule: Create a team

    Scenario: The person who creates a team is its Owner
      When Ana creates a team called "Checkout squad"
      Then the request succeeds
      And Ana is the owner of "Checkout squad"

    Scenario: A team needs a name
      When Ana creates a team called "   "
      Then the request fails with "required" on "name"
      And every failure says how to fix it

    Scenario: An API key cannot create a team
      Given Ana owns the team "Checkout squad"
      And "Checkout squad" has an API key CiKey with scopes "read, write"
      When CiKey creates a team called "Another"
      Then the request is refused as "forbidden"

  Rule: List my teams

    Scenario: A person sees the teams they belong to, with their role in each
      Given Ana owns the team "Checkout squad"
      And Bo owns the team "Payments"
      And Ana is a viewer of "Payments"
      And Bo owns the team "Search"
      When Ana lists their teams
      Then the teams listed are:
        | team           | role   |
        | Checkout squad | owner  |
        | Payments       | viewer |

  Rule: Open a team

    Scenario: Members see everyone in the team
      Given Ana owns the team "Checkout squad"
      And Bo is an editor of "Checkout squad"
      When Bo opens the team "Checkout squad"
      Then the request succeeds
      And the team's members are:
        | name | role   |
        | Ana  | owner  |
        | Bo   | editor |

    Scenario: A team is invisible to people outside it
      Given Ana owns the team "Checkout squad"
      And Cy has an account
      When Cy opens the team "Checkout squad"
      Then the request is refused as "not-found"
