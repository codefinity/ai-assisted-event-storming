Feature: Invitations
  Owners invite people to their team, either by email or with a link they share. An email invitation is
  for one address and works once; a link works for anyone who has it until it expires after 7 days or
  is revoked.

  Background:
    Given the time is "2026-09-25T10:00:00Z"
    And Ana owns the team "Checkout squad"

  Rule: Create an invitation

    Scenario: Inviting by email sends the link to that address
      When Ana invites "bo@example.com" to "Checkout squad" as editor
      Then the request succeeds
      And an invitation email is sent to "bo@example.com" from Ana
      And only the hash of the invitation token is stored

    Scenario: A link invitation sends no email
      When Ana creates an invitation link to "Checkout squad" as viewer
      Then the request succeeds
      And no invitation email is sent

    Scenario: Only Owners can invite
      Given Bo is an editor of "Checkout squad"
      When Bo creates an invitation link to "Checkout squad" as editor
      Then the request is refused as "forbidden"

    Scenario: The role must be one the team knows
      When Ana creates an invitation link to "Checkout squad" as admin
      Then the request fails with "unknown-role" on "role"
      And every failure says how to fix it

  Rule: See and revoke open invitations

    Scenario: Revoked invitations are no longer listed
      Given Ana has created an invitation link to "Checkout squad" as editor
      And Ana has invited "cy@example.com" to "Checkout squad" as viewer
      When Ana revokes the invitation
      And Ana lists the invitations of "Checkout squad"
      Then 1 invitation is open

    Scenario: Expired invitations are no longer listed
      Given Ana has created an invitation link to "Checkout squad" as editor
      And 8 days pass
      When Ana lists the invitations of "Checkout squad"
      Then 0 invitations are open

  Rule: Preview an invitation

    Scenario: Anyone holding the link can see what it is for
      Given Ana has created an invitation link to "Checkout squad" as editor
      When someone previews the invitation
      Then the request succeeds
      And the preview says Ana invited them to "Checkout squad" as editor
      And the invitation is valid

    Scenario: A revoked invitation says so
      Given Ana has created an invitation link to "Checkout squad" as editor
      And Ana has revoked the invitation
      When someone previews the invitation
      Then the invitation is revoked

    Scenario: A made-up token finds nothing
      When someone previews the invitation "not-a-token"
      Then the request is refused as "not-found"

  Rule: Accept an invitation

    Scenario: Accepting a link joins the team with the invited role
      Given Ana has created an invitation link to "Checkout squad" as editor
      When Bo accepts the invitation
      Then the request succeeds
      And Bo is an editor of "Checkout squad"

    Scenario: A link can be used by several people
      Given Ana has created an invitation link to "Checkout squad" as viewer
      And Bo has accepted the invitation
      When Cy accepts the invitation
      Then Cy is a viewer of "Checkout squad"

    Scenario: An email invitation works once, and only for its address
      Given Ana has invited "bo@example.com" to "Checkout squad" as editor
      When Cy accepts the invitation
      Then the request is refused as "forbidden"
      And Cy is not a member of "Checkout squad"

    Scenario: An email invitation cannot be used twice
      Given Ana has invited "bo@example.com" to "Checkout squad" as editor
      And Bo has accepted the invitation
      And Bo leaves "Checkout squad"
      When Bo accepts the invitation
      Then the request fails with "invitation-used"

    Scenario: Accepting again changes nothing
      Given Ana has created an invitation link to "Checkout squad" as viewer
      And Bo has accepted the invitation
      When Bo accepts the invitation
      Then the request succeeds
      And Bo is a viewer of "Checkout squad"

    Scenario: An expired invitation cannot be accepted
      Given Ana has created an invitation link to "Checkout squad" as editor
      And 8 days pass
      When Bo accepts the invitation
      Then the request fails with "invitation-expired"

    Scenario: A revoked invitation cannot be accepted
      Given Ana has created an invitation link to "Checkout squad" as editor
      And Ana has revoked the invitation
      When Bo accepts the invitation
      Then the request fails with "invitation-revoked"
