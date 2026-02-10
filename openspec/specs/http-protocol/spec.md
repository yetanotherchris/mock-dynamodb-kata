# HTTP Protocol Specification

## Purpose
The mock DynamoDB server SHALL accept HTTP requests using the DynamoDB JSON wire protocol.

## Requirements

### Requirement: Request Routing
The server SHALL route all DynamoDB operations through a single POST endpoint at `/`.

#### Scenario: Valid operation dispatch
- **WHEN** a POST request arrives with header `X-Amz-Target: DynamoDB_20120810.CreateTable`
- **THEN** the server SHALL dispatch to the CreateTable handler
- **AND** respond with Content-Type `application/x-amz-json-1.0`

#### Scenario: Unknown operation
- **WHEN** the X-Amz-Target header contains an unrecognised operation
- **THEN** the server SHALL return HTTP 400
- **AND** return error type `com.amazonaws.dynamodb.v20120810#UnknownOperationException`

#### Scenario: Missing X-Amz-Target header
- **WHEN** a POST request arrives without the X-Amz-Target header
- **THEN** the server SHALL return HTTP 400
- **AND** return error type `com.amazonaws.dynamodb.v20120810#MissingAuthenticationTokenException`

### Requirement: Authentication Bypass
The server SHALL accept any AWS credentials without validation.

#### Scenario: Any credentials accepted
- **GIVEN** any value in the Authorization header (or no header at all)
- **WHEN** a request is made
- **THEN** the server SHALL process the request normally

### Requirement: Content Type
The server SHALL use `application/x-amz-json-1.0` for all request and response bodies.

### Requirement: Health Check
The server SHALL respond to GET `/` with HTTP 200 and a JSON body indicating the server is running.

#### Scenario: Health check
- **WHEN** a GET request is made to `/`
- **THEN** the server SHALL return HTTP 200
- **AND** return a JSON body with a status field

### Requirement: Port Configuration
The server SHALL listen on port 8000 by default, configurable via `MOCK_DYNAMODB_PORT` environment variable or `--port` argument.
