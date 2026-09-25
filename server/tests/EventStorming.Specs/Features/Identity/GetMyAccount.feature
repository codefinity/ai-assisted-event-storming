Feature: Show my account
  A signed-in person can see the account they are signed in with.

  Scenario: A signed-in person sees their own account
    Given Ana has an account
    When Ana asks for their account
    Then the request succeeds
    And the account shown is Ana's

  Scenario: An API key has no account of its own
    When an API key asks for its account
    Then the request is refused as "forbidden"
