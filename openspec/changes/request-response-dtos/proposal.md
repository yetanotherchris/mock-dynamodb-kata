# Request/Response DTOs

## Spec
`specs/server`

## Problem

Every operation handler (`TableOperations`, `ItemOperations`, `QueryScanOperations`, `BatchOperations`, `TransactionOperations`) accepts a raw `JsonDocument`, then manually navigates the JSON tree with `GetProperty()` / `TryGetProperty()` calls to extract request fields. Responses are built by hand with `Utf8JsonWriter` → `MemoryStream` → `JsonDocument.Parse()`. This has several downsides:

- **No compile-time contract.** Each handler implicitly defines its request/response shape through scattered property access. A typo in a property name string compiles fine but fails at runtime.
- **Duplicated parsing logic.** Common field groups (expression attributes, key extraction, projection) are re-extracted by multiple operations with identical boilerplate.
- **Unclear API surface.** Discovering which fields an operation reads or writes requires reading the entire method body.
- **Harder to test.** Unit-testing operation logic requires constructing `JsonDocument` instances instead of plain C# objects.
- **Verbose response building.** The `Utf8JsonWriter` → `MemoryStream` → `JsonDocument.Parse()` pattern is repeated in every response builder.

## Solution

Introduce strongly-typed C# record types for each DynamoDB operation's request and response. Deserialize the incoming `JsonDocument` into a request DTO at the router level and serialize the response DTO back to JSON when writing the HTTP response. Operation handlers accept and return typed DTOs instead of `JsonDocument`.

## Scope

- Define request and response record types for all 14 operations in `src/MockDynamoDB.Core/Models/`
- Define shared record types for recurring structures (KeySchemaElement, Projection, ConsumedCapacity, etc.)
- Update `DynamoDbRequestRouter` to deserialize requests and serialize responses using `JsonSerializer`
- Update all five operation classes to accept request DTOs and return response DTOs
- Remove the `Utf8JsonWriter` / `MemoryStream` / `JsonDocument.Parse()` response-building pattern
- Remove `DeserializeItem()`, `DeserializeStringMap()`, `WriteItem()`, and related helper methods that become unnecessary
- Retain `AttributeValue` and its custom `AttributeValueConverter` (already a proper DTO)
- All existing tests must continue to pass with no changes

## Out of Scope

- Changes to the expression parsing engine (ANTLR grammars, visitors, evaluators)
- Changes to storage interfaces (`ITableStore`, `IItemStore`) or their implementations
- Changes to the `AttributeValue` type or its JSON converter
- Changes to error types or exception handling
- Adding new DynamoDB operations
- Request validation beyond what already exists
