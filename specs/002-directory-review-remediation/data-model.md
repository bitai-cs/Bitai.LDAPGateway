# Data Model: Directory Review Remediation

## Directory Search Request

| Field | Required | Rules |
|-------|----------|-------|
| Server profile | Yes | Must be nonblank. |
| Catalog type | Yes | Must be a defined catalog value. |
| Primary filter attribute | Yes | Must be a defined directory-entry attribute; a defined zero-valued attribute is valid. |
| Primary filter value | Yes | Must be nonblank. |
| Secondary filter attribute | Conditional | When present, the secondary value and combination choice are both required. |
| Secondary filter value | Conditional | Must be nonblank when a secondary attribute is present; otherwise must be absent. |
| Combine filters | Conditional | Must be present when a secondary attribute is present; otherwise must be absent. |
| Required attribute set | Yes | Must be a defined attribute-set value. |

**Validation state**: A request is valid only when all required fields are valid and
the secondary-filter triple is either entirely absent or entirely present.

## Directory Operation Outcome

| Field | Meaning | Relationship |
|-------|---------|--------------|
| Success state | Whether the directory operation completed successfully | Determines the completion record status. |
| Value | Returned directory entry or entry list on success | Returned to the caller unchanged. |
| Error | Safe failure result on unsuccessful operations | Returned to the caller unchanged. |

**State transitions**: `requested -> connector succeeded -> successful outcome`, or
`requested -> connector failed -> failed outcome`. A lookup that completes
successfully without an entry retains its existing must-exist behavior.

## Operation Completion Record

| Field | Meaning | Relationship |
|-------|---------|--------------|
| Operation name | Directory action that completed | Must identify the handled operation. |
| Server profile | Requested profile | Must match the originating request. |
| Catalog type | Requested catalog | Must match the originating request. |
| Success state | Final operation outcome | Must match the returned result. |
| Safe message | Completion description | Must not disclose credentials. |
| Occurrence time | Completion timestamp | Records when the outcome was published. |

**Cardinality rule**: Each covered operation produces exactly one completion record.
