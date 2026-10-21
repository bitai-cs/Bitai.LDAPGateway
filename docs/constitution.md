# Constitution — Bitai.LDAPGateway

## 1. Purpose and scope

Bitai.LDAPGateway is a single REST entry point that exposes every LDAP server profile configured in `appsettings.json`, so consuming services never implement LDAP themselves. The directory remains the source of truth. The gateway is stateless: it owns no identity data, synchronizes nothing, and does not manage profiles at runtime.

The gateway targets **LDAPv3-compatible directories**. Capabilities are layered: authentication, search, and entry retrieval are directory-neutral; MS AD provisioning and the LC/GC catalog split rely on Active Directory–compatible features and apply only to servers that provide them. Product-specific behaviour never leaks into the neutral paths, and neutral paths are never generalized at the expense of the protocol.

## 2. Architecture

The project follows Clean Architecture. Dependencies point inward: **Domain ← Application ← Infrastructure ← Api**. Domain and Application never depend on LDAP, HTTP, configuration, or logging. Application defines the ports it needs; Infrastructure implements them. Api only translates HTTP into use cases and results into responses.

## 3. Domain modelling

Domain-Driven Design is applied where it earns its keep: directory concepts are modelled explicitly as a domain language (profiles, catalogs, entries, identifiers), and every use case expresses intent. We do not add aggregates, repositories, or persistence the gateway does not need.

## 4. Use cases

Every capability is one explicit use case: a command that changes state or a query that reads it. Controllers contain no logic. One use case, one handler, one validator, one result. Shared behaviour is applied uniformly, never copy-pasted.

## 5. Contracts

Success and failure are explicit values, not exceptions. Failures are returned as RFC 7807 problem details with a stable code and a safe message. Internal details, stack traces, and infrastructure errors never leave the service. The API is described by OpenAPI.

## 6. Security

Credentials and secrets are never stored in source control, never logged, and never returned. Bind accounts follow least privilege. Every endpoint has a recorded trust decision: who may call it and why. Caller input is treated as hostile.

## 7. Configuration

Directory server profiles are static `appsettings.json` entries managed by operators. Adding or changing a directory server is a configuration change, never a code change. Configuration is validated at startup, and invalid configuration prevents the service from running.

## 8. Quality and evolution

The gateway is verifiable without a live directory: tests run against a mock adapter in CI, and every new use case has at least one test at its boundary. The build is clean, warnings are treated as failures, and generated artifacts are committed with the change that produced them. Architectural exceptions are explicit, documented as an ADR, and temporary. When this document and any other guidance disagree, this document wins.
