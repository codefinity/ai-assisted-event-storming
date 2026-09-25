Feature: Register an account
  A person creates an account with an email address, a display name and a password, so that they
  can sign in, create a team and take part in EventStorming sessions.

  Background:
    Given the time is "2026-09-25T10:00:00Z"

  Scenario: A person registers with valid details
    When "ana@example.com" registers as "Ana" with password "correct horse"
    Then the request succeeds
    And an account exists for "ana@example.com" named "Ana"
    And the stored password for "ana@example.com" is not "correct horse"

  Scenario: Email addresses are stored normalized
    When "  Ana@Example.COM " registers as "Ana" with password "correct horse"
    Then the request succeeds
    And an account exists for "ana@example.com" named "Ana"

  Scenario: An email address can only be registered once, whatever its case
    Given "ana@example.com" has registered as "Ana" with password "correct horse"
    When "ANA@example.com" registers as "Another Ana" with password "battery staple"
    Then the request fails with "email-taken" on "email"
    And the request is refused as "conflict"
    And every failure says how to fix it

  Scenario Outline: An invalid detail is refused and names the field
    When "<email>" registers as "<name>" with password "<password>"
    Then the request fails with "<code>" on "<field>"
    And the request is refused as "validation"

    Examples:
      | email           | name | password      | field       | code          |
      |                 | Ana  | correct horse | email       | required      |
      | not-an-email    | Ana  | correct horse | email       | invalid-email |
      | ana@example.com |      | correct horse | displayName | required      |
      | ana@example.com | Ana  | short         | password    | too-short     |

  Scenario: Every invalid detail is reported at once
    When "" registers as "" with password ""
    Then the request fails with these failures:
      | field       | code     |
      | email       | required |
      | displayName | required |
      | password    | required |
