Feature: Sign out
  Signing out ends the session the refresh token belongs to, on this device and any other device
  that shares it.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And "ana@example.com" has registered as "Ana" with password "correct horse"
    And "ana@example.com" is signed in with password "correct horse"

  Scenario: Signing out revokes the session
    When they sign out
    Then the request succeeds
    And every refresh token of the session is revoked
    And the current session can no longer be refreshed

  Scenario: Signing out without a session is harmless
    When someone signs out without a session
    Then the request succeeds
