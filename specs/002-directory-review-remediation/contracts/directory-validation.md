# Directory Validation Contract

## Status

Existing public contract; behavioral validation correction only. No route, HTTP
method, authorization policy, parameter name, response schema, or success payload
change is introduced.

## Affected Operation

`GET /api/{serverProfile}/{catalogType}/Directory/filterBy`

The operation retains these query parameters:

| Parameter | Required | Contract |
|-----------|----------|----------|
| `filterAttribute` | Yes | Must name a defined directory-entry attribute, including a defined zero-valued attribute. Undefined values are rejected. |
| `filterValue` | Yes | Must be nonblank. |
| `secondFilterAttribute` | Conditional | Requires `secondFilterValue` and `combineFilters`. |
| `secondFilterValue` | Conditional | Must be nonblank and requires `secondFilterAttribute` and `combineFilters`. |
| `combineFilters` | Conditional | Requires `secondFilterAttribute` and `secondFilterValue`. |
| `requiredAttributeSet` | Optional | Retains its existing default and must name a defined attribute set when supplied. |

## Outcomes

| Condition | Outcome |
|-----------|---------|
| Defined primary attribute and complete filter set | Existing successful search behavior is retained. |
| Undefined primary attribute | Existing validation-failure response is returned before directory access. |
| Partial secondary-filter triple | Existing validation-failure response is returned before directory access. |
| Directory connector failure for covered lookup or provisioning operations | Existing failure result is preserved and exactly one failed completion event is published. |

## Compatibility

Previously accepted malformed primary attributes and partial secondary-filter
combinations become invalid. This is an intentional correction to enforce the
documented request invariant. Valid requests, route shapes, authorization policies,
and response payloads remain compatible.
