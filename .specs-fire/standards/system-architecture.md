# System Architecture

Single deployable ASP.NET Core service, layered per Clean Architecture:

```
Domain  <-  Application  <-  Infrastructure  <-  Api
```

- **Domain**: directory language (profiles, catalogs, entries, identifiers), value objects, domain events.
- **Application**: use cases dispatched via Wolverine, FluentValidation, result-logging behavior, ports.
- **Infrastructure**: Bitai.LDAPHelper provider, directory connector, server profile read service, domain event publisher.
- **Api**: controllers, middleware, options, OpenAPI (NSwag), problem-details mapping.

Capabilities: authentication, search and entry retrieval are directory-neutral (LDAPv3). MS AD provisioning and the LC/GC catalog split apply only to AD-compatible servers. Profiles are static configuration, not runtime-managed.
