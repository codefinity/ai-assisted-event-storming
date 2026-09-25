Feature: API keys
  A team's Owners create API keys so other systems and LLMs can use the public API. The full key is
  shown once; only a hash of its secret is kept. A key has the "read" scope, or "read" and "write".

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And Ana owns the team "Food delivery"
    And Bo is an editor of "Food delivery"

  Rule: Create, list and revoke keys

    Scenario: An Owner creates a key; "write" brings "read" with it
      When Ana creates an API key "Claude" for "Food delivery" with scopes "write"
      Then the request succeeds
      And the key has the scopes "read, write"
      And the full key is shown once, and only the hash of its secret is kept

    Scenario: Only Owners manage keys
      When Bo creates an API key "Mine" for "Food delivery" with scopes "read"
      Then the request is refused as "forbidden"
      When Bo lists the API keys of "Food delivery"
      Then the request is refused as "forbidden"

    Scenario: Scopes must be read or write
      When Ana creates an API key "Odd" for "Food delivery" with scopes "admin"
      Then the request fails with "unknown-scope" on "scopes"
      And every failure says how to fix it

    Scenario: An expiry must lie in the future
      When Ana creates an API key "Old" for "Food delivery" that expired yesterday
      Then the request fails with "in-the-past" on "expiresAt"

    Scenario: Revoked keys stay listed, for the record
      Given Ana has created an API key "Claude" for "Food delivery" with scopes "read"
      And Ana has created an API key "CI" for "Food delivery" with scopes "read, write"
      And Ana has revoked the API key "CI"
      When Ana lists the API keys of "Food delivery"
      Then 2 API keys are listed

  Rule: Authenticate with a key

    Scenario: A valid key stands for its team and scopes
      Given Ana has created an API key "Claude" for "Food delivery" with scopes "read, write"
      When the API key "Claude" is presented
      Then the request succeeds
      And it stands for "Food delivery" with the scopes "read, write"
      And the API key "Claude" was last used just now

    Scenario: A revoked key stops working at once
      Given Ana has created an API key "Claude" for "Food delivery" with scopes "read"
      And Ana has revoked the API key "Claude"
      When the API key "Claude" is presented
      Then the request fails with "api-key-revoked"
      And the request is refused as "unauthenticated"
      And every failure says how to fix it

    Scenario: A key with a wrong secret is refused
      Given Ana has created an API key "Claude" for "Food delivery" with scopes "read"
      When the API key "Claude" is presented with a wrong secret
      Then the request fails with "api-key-invalid"

    Scenario: Something that is not a key at all says so
      When the text "sk-123" is presented as an API key
      Then the request fails with "api-key-malformed"

  Rule: What a key may do

    Scenario: A read-only key can read boards but not change them
      Given "Food delivery" has an API key Reader with scopes "read"
      And "Food delivery" has a "big-picture" board called "Ordering"
      When Reader looks up the board "Ordering"
      Then the request succeeds
      When Reader adds a "domain-event" saying "Order Placed" to "Ordering"
      Then the request is refused as "forbidden"

    Scenario: A key only sees its own team's boards
      Given Cy owns the team "Other team"
      And "Other team" has an API key Stranger with scopes "read, write"
      And "Food delivery" has a "big-picture" board called "Ordering"
      When Stranger looks up the board "Ordering"
      Then the request is refused as "not-found"

    Scenario: Changes made with a key are announced like any other
      Given "Food delivery" has an API key Writer with scopes "read, write"
      And "Food delivery" has a "big-picture" board called "Ordering"
      When Writer adds a "domain-event" saying "Order Placed" to "Ordering"
      Then the request succeeds
      And the change is announced to everyone on the board
