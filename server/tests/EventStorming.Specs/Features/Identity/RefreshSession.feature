Feature: Refresh a session
  The web app trades its refresh token for a new access token. Every refresh rotates the refresh
  token; presenting one that was already rotated is treated as theft and ends the whole session.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And "ana@example.com" has registered as "Ana" with password "correct horse"
    And "ana@example.com" is signed in with password "correct horse"

  Scenario: A refresh issues new tokens and rotates the refresh token
    When the session is refreshed
    Then the request succeeds
    And a new refresh token replaces the old one

  Scenario: Two tabs refreshing at the same moment do not end the session
    Given the session has been refreshed
    When the previous refresh token is presented again
    Then the request fails with "session-rotated"
    And the current session can still be refreshed

  Scenario: Replaying a rotated token later ends every session from that sign-in
    Given the session has been refreshed
    And 60 seconds pass
    When the previous refresh token is presented again
    Then the request fails with "session-expired"
    And every refresh token of the session is revoked

  Scenario: A refresh token stops working after 30 days
    Given 31 days pass
    When the session is refreshed
    Then the request fails with "session-expired"

  Scenario: An unknown refresh token is refused
    When the refresh token "made-up" is presented
    Then the request fails with "session-expired"
    And the request is refused as "unauthenticated"
