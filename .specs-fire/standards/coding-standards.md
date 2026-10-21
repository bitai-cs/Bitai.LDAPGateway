# Coding Standards

- Follow `.editorconfig`: 4-space indent, LF, UTF-8, Allman braces, `System` usings first.
- Warnings are treated as errors; the build must be clean.
- Clean Architecture: Domain <- Application <- Infrastructure <- Api. Domain/Application never reference LDAP, HTTP, configuration or logging.
- Application defines ports (`Common/Interfaces`); Infrastructure implements them.
- One use case = one command/query, one handler, one validator, one result, in a feature folder (`Authentications`, `CatalogTypes`, `Directory`, `ServerProfiles`).
- Controllers hold no logic; they translate HTTP to use cases and results to responses.
- Success and failure are explicit result values, not exceptions. Failures map to RFC 7807 problem details with a stable code and a safe message.
- Never log, store or return credentials or secrets. Never leak internals or stack traces.
- Treat caller input as hostile; validate with FluentValidation.
- Match surrounding naming and comment density; document public types with XML comments.
