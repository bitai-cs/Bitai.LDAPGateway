using Bitai.LDAPGateway.Domain.Enums;

namespace Bitai.LDAPGateway.Application.Common.Models;

public sealed record LdapRequestContext(string ServerProfile, CatalogType CatalogType);
