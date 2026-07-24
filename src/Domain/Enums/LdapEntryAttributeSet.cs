namespace Bitai.LDAPGateway.Domain.Enums;

public enum LdapEntryAttributeSet
{
	Minimum,
	MinimumWithMember,
	MinimumWithMemberOf,
	MinimumWithMemberAndMemberOf,
	Few,
	FewWithMember,
	FewWithMemberOf,
	FewWithMemberAndMemberOf,
	All,
	AllWithMember,
	AllWithMemberOf,
	AllWithMemberAndMemberOf,
	MemberAndMemberOf,
	ObjectSidAndSAMAccountName,
	OnlyMember,
	OnlyMemberOf,
	OnlyCN,
	OnlyObjectSid
}
