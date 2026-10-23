# Constitution — Bitai.LDAPGateway

## Core Principles

### I. Stateless, Directory-Neutral Gateway
Bitai.LDAPGateway MUST expose configured LDAP server profiles through one REST entry point without
owning identity data, synchronizing directory data, or managing profiles at runtime. LDAPv3-neutral
authentication, search, and entry-retrieval paths MUST remain independent of directory products.
Active Directory-specific provisioning and LC/GC catalog behavior MUST apply only to compatible
servers and MUST NOT leak into neutral paths. This preserves the directory as the source of truth
and prevents product-specific behavior from reducing protocol compatibility.

### II. Clean Architecture and Explicit Use Cases
Dependencies MUST point inward: Domain <- Application <- Infrastructure <- Api. Domain and
Application MUST NOT depend on LDAP, HTTP, configuration, or logging; Application defines its
required ports and Infrastructure implements them. Api MUST translate HTTP requests to use cases
and use-case results to responses without business logic. Every capability MUST be one explicit
command or query with one handler, one validator, and one result; shared behavior MUST be applied
uniformly rather than copied. This keeps business rules independently testable and infrastructure
replaceable.

### III. Intentional Domain Modelling
Directory concepts, including profiles, catalogs, entries, and identifiers, MUST be represented in
the domain language when they support a business rule or use case. The codebase MUST NOT add
aggregates, repositories, persistence, or other abstractions that the stateless gateway does not
need. This applies Domain-Driven Design where it improves clarity while preserving a small,
maintainable service.

### IV. Stable, Safe API Contracts
Successful and failed use-case outcomes MUST be explicit values, not exception-driven control flow.
Failures MUST be returned as RFC 7807 problem details with a stable code and a safe message; stack
traces, infrastructure details, and other internal information MUST NOT be exposed. The REST API
MUST be described by OpenAPI. This gives clients predictable contracts without disclosing service
internals.

### V. Security by Design
Credentials and other secrets MUST NOT be committed, logged, or returned in API responses. Bind
accounts MUST use least privilege, every endpoint MUST have a documented trust decision describing
who may call it and why, and all caller input MUST be treated as hostile. These requirements protect
directory access and limit the impact of malformed or malicious requests.

## Configuration and Operations

Directory server profiles MUST be static `appsettings.json` entries managed by operators. Adding or
changing a directory server MUST be a configuration change rather than a code change. Configuration
MUST be validated during startup, and invalid configuration MUST prevent the service from running.
This makes deployed behavior explicit and prevents a partially configured gateway from serving
directory requests.

## Quality and Evolution

The gateway MUST be verifiable without a live directory: CI tests MUST use a mock adapter, and each
new use case MUST have at least one boundary test. Builds MUST be clean, warnings MUST be treated as
failures, and generated artifacts MUST be committed with the change that produced them. Architectural
exceptions MUST be documented in an ADR, explicitly time-bounded, and removed or ratified before
their expiry. This constitution takes precedence over conflicting project guidance.

## Governance

This constitution governs implementation, review, and operational changes in Bitai.LDAPGateway.
Proposed amendments MUST document the affected principles, rationale, migration impact, and semantic
version change; maintainers approve and record them by updating this file. Versioning follows
semantic intent: MAJOR for incompatible principle removal or redefinition, MINOR for a new principle
or materially expanded requirement, and PATCH for clarifications that preserve obligations. Every
pull request and review MUST verify applicable principles, API contracts, security decisions,
configuration validation, and required tests. An approved ADR is required for every temporary
architectural exception. Always use English.
