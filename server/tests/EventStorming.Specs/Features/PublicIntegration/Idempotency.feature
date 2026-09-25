Feature: Idempotent writes
  A public-API write sent with an Idempotency-Key runs once. A retry of the same request gets the
  same answer back without running again; reusing the key for a different request is refused.

  Background:
    Given the time is "2026-09-25T10:00:00Z"

  Scenario: The first request with a key runs
    When a write is reserved with the idempotency key "import-1" for the request "POST /boards/import {…}"
    Then the request succeeds
    And the write may run

  Scenario: A retry of the same request gets the stored response
    Given a write has been reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    And its response 201 "{created}" has been recorded for the idempotency key "import-1"
    When a write is reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    Then the stored response 201 "{created}" is replayed

  Scenario: The same key for a different request is refused
    Given a write has been reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    And its response 201 "{created}" has been recorded for the idempotency key "import-1"
    When a write is reserved with the idempotency key "import-1" for the request "POST /boards/import {b}"
    Then the request fails with "idempotency-key-reused"
    And every failure says how to fix it

  Scenario: A retry while the first is still running is asked to wait
    Given a write has been reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    When a write is reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    Then the request fails with "idempotency-in-progress"
    And the request is refused as "conflict"

  Scenario: A request abandoned mid-way can be retried later
    Given a write has been reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    And 3 minutes pass
    When a write is reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    Then the write may run

  Scenario: After a server error, a retry runs again
    Given a write has been reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    And the write with idempotency key "import-1" failed on the server
    When a write is reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    Then the write may run

  Scenario: Keys are remembered for a day
    Given a write has been reserved with the idempotency key "import-1" for the request "POST /boards/import {a}"
    And its response 201 "{created}" has been recorded for the idempotency key "import-1"
    And 2 days pass
    When a write is reserved with the idempotency key "import-1" for the request "POST /boards/import {b}"
    Then the write may run

  Scenario: A key must be printable and not too long
    When a write is reserved with the idempotency key "has space" for the request "POST /boards {a}"
    Then the request fails with "invalid-idempotency-key"
