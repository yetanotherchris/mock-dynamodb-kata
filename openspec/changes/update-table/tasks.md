# Tasks

## Phase 1: Spec Update (Table Operations Specification)
- [ ] Add `UpdateTable` requirement and scenarios to `openspec/specs/tables/spec.md`

## Phase 2: Core Implementation (UpdateTable Operation)
- [ ] Add `UpdateTable` method to `TableOperations` — deserialise request, validate, delegate to store
- [ ] Add `UpdateTable` to `ITableStore` and `InMemoryTableStore` — mutate `BillingMode` and `ProvisionedThroughput` under write lock
- [ ] Implement GSI Create action — append new GSI and populate from existing items
- [ ] Implement GSI Update action — replace throughput on existing GSI
- [ ] Implement GSI Delete action — remove GSI and drop index structure
- [ ] Return `ResourceNotFoundException` when table is not found
- [ ] Return `ValidationException` for invalid input (duplicate GSI name, unknown GSI, throughput on PAY_PER_REQUEST)

## Phase 3: Routing (HTTP Dispatch)
- [ ] Add `"UpdateTable"` case to `DynamoDbRequestRouter` dispatch

## Phase 4: Integration Tests (UpdateTable via SDK)
- [ ] Test `UpdateTable` switches billing mode to `PAY_PER_REQUEST` — *Scenario: Update billing mode*
- [ ] Test `UpdateTable` changes provisioned throughput — *Scenario: Update provisioned throughput*
- [ ] Test `UpdateTable` on non-existent table throws `ResourceNotFoundException` — *Scenario: Update non-existent table*
- [ ] Test `UpdateTable` creates a new GSI and query returns correct results — *Scenario: Create GSI*
- [ ] Test `UpdateTable` updates GSI provisioned throughput — *Scenario: Update GSI throughput*
- [ ] Test `UpdateTable` deletes a GSI and subsequent query throws — *Scenario: Delete GSI*
- [ ] Test `UpdateTable` create with duplicate GSI name throws `ValidationException` — *Scenario: Create duplicate GSI*
- [ ] Test `UpdateTable` delete with unknown GSI name throws `ValidationException` — *Scenario: Delete unknown GSI*
