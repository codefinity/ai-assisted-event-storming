Feature: Sign in
  A registered person signs in with their email and password and receives a session: a short-lived
  access token and a long-lived refresh token.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And "ana@example.com" has registered as "Ana" with password "correct horse"

  Scenario: The right password starts a session
    When "ana@example.com" signs in with password "correct horse"
    Then the request succeeds
    And a session is started for "ana@example.com"
    And only the hash of the refresh token is stored

  Scenario: The email is matched regardless of case
    When "ANA@EXAMPLE.COM" signs in with password "correct horse"
    Then the request succeeds

  Scenario: A wrong password is refused without saying which part was wrong
    When "ana@example.com" signs in with password "wrong horse"
    Then the request fails with "incorrect-credentials"
    And the request is refused as "unauthenticated"

  Scenario: An unknown email is refused exactly like a wrong password
    When "bo@example.com" signs in with password "correct horse"
    Then the request fails with "incorrect-credentials"

  Scenario: Missing details are reported at once
    When "" signs in with password ""
    Then the request fails with these failures:
      | field    | code     |
      | email    | required |
      | password | required |
