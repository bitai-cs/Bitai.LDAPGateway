namespace Bitai.LDAPGateway.Domain.Enums;

/// <summary>
/// LDAP entry attributes that can be loaded or filtered.
/// </summary>
public enum LdapEntryAttribute
{
	/// <summary>
	/// country abbreviation (2 letter code)
	/// </summary>
	c,
	cn,
	company,
	co,
	department,
	description,
	displayName,
	distinguishedName,
	givenName,
	l,
	lastLogonTimestamp,
	mail,
	manager,
	member,
	memberOf,
	name,
	objectCategory,
	objectClass,
	sAMAccountName,
	sAMAccountType,
	sn,
	telephoneNumber,
	title,
	userPassword,
	unicodePwd,
	userPrincipalName,
	whenCreated,
	objectGuid,
	objectSid,
	userAccountControl
}
