# Research: Directory Review Remediation

## Decision: Validate the primary filter attribute against defined enum members

**Rationale**: A presence check treats the defined zero-valued `c` attribute as
missing and treats undefined nonzero values as present. Defined-member validation
accepts every supported value and rejects values that cannot represent a supported
directory attribute.

**Alternatives considered**:

- Keep a presence check: rejected because it has the inverse behavior for zero and
  undefined nonzero values.
- Add a special case for the zero-valued attribute: rejected because it duplicates
  enum knowledge and still does not provide a general invalid-value boundary.

## Decision: Mirror the established secondary-filter boundary rules

**Rationale**: Existing user and group searches require a secondary attribute, its
nonblank value, and a combination choice as a complete set. The generic directory
search has the same request shape and should enforce the same invariant.

**Alternatives considered**:

- Allow a combination choice without a secondary filter: rejected because it has no
  meaningful effect and admits inconsistent input.
- Infer a combination choice when omitted: rejected because it changes the request
  contract and hides malformed client input.

## Decision: Verify handlers through existing ports and completion events

**Rationale**: The existing handler base invokes the directory connector, preserves
its result, and publishes an `OperationCompletedDomainEvent`. Unit tests can prove
the required boundaries without a live directory by mocking the connector and domain
event publisher.

**Alternatives considered**:

- Integration tests against a directory server: rejected because the constitution
  requires mock-adapter verification and the scope is deterministic handler behavior.
- Change handler production logic: rejected because the existing handlers already
  delegate through the common execution path; the gap is focused coverage and event
verification.

## Decision: Verify the publisher's total invocation count before event details

**Rationale**: Matching only a desired event permits an extra, unrelated publication
to go unnoticed. Requiring one overall publication and then checking its operation,
profile, catalog, and status proves both uniqueness and correctness.

**Alternatives considered**:

- Verify only a matching event: rejected because it does not detect extra events.
- Assert event objects through a broad callback: rejected because the existing mock
verification pattern is simpler and directly expresses the count boundary.

## Decision: Preserve public HTTP contract and generated source

**Rationale**: The controller already exposes the required query parameters and
dispatches the existing query. This feature changes acceptance rules only; generated
Wolverine source is derived output and must not be edited manually.

**Alternatives considered**:

- Add new request fields or endpoints: rejected because no new client capability is
  needed.
- Edit generated message handlers: rejected because generated code is not the source
  of the validation rule and would be overwritten.
