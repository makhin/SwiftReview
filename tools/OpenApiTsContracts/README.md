# OpenApiTsContracts

`OpenApiTsContracts` is a small .NET 10 command-line tool that generates TypeScript data
contracts from `components.schemas` in a JSON OpenAPI document. The OpenAPI document is
the contract boundary: the tool does not reference backend assemblies and does not generate
clients, endpoint functions, HTTP wrappers, hooks, runtime validators, or server code.

The generator has no runtime NuGet dependencies and uses `System.Text.Json`.

## Usage

From the repository root:

```bash
dotnet run --project tools/OpenApiTsContracts -- \
  --input artifacts/openapi.json \
  --output frontend/src/api/generated/contracts.generated.ts
```

After publishing, the generated app host accepts the same arguments:

```bash
dotnet publish tools/OpenApiTsContracts -c Release -o artifacts/OpenApiTsContracts
artifacts/OpenApiTsContracts/OpenApiTsContracts \
  --input artifacts/openapi.json \
  --output frontend/src/api/generated/contracts.generated.ts
```

Both `--input` and `--output` are required. `--verbose` prints progress. `--check` compares
the in-memory result with the output file without modifying it:

```bash
dotnet run --project tools/OpenApiTsContracts -- \
  --input artifacts/openapi.json \
  --output frontend/src/api/generated/contracts.generated.ts \
  --check
```

`--namespace <schema-prefix>` selects schema names beginning with the prefix and removes it
from generated names. For example, `--namespace My.Api.Contracts.` emits
`My.Api.Contracts.UserDto` as `UserDto`. A selected schema may only reference other selected
schemas. The entire OpenAPI document is validated before this output selection, so unsupported
schemas outside the prefix still fail the run.

## Supported OpenAPI subset

- JSON OpenAPI 3.0.x and 3.1.x documents, reading only `components.schemas`.
- `string`, `integer`, `number`, and `boolean`; formats do not change wire types.
- Objects, required and optional properties, local schema `$ref` values, and simple inline
  objects.
- OpenAPI 3.0 `nullable: true` and OpenAPI 3.1 type arrays containing `null`.
- Arrays and dictionaries expressed with schema-valued `additionalProperties`. An array with
  no `items` is emitted as `unknown[]`; an empty schema is emitted as `unknown`.
- String, integer, numeric, and boolean enums as literal unions, including inline and nullable
  enums.
- Primitive OpenAPI 3.1 type unions, such as `["integer", "string"]`, as TypeScript unions.
  Unions containing objects or arrays are rejected.

Top-level declarations are sorted by generated name; property and enum value order follows
OpenAPI. Output is UTF-8 without BOM, uses LF and two-space indentation, and contains no
timestamp.

Property names are never renamed. ASCII TypeScript identifiers are emitted directly;
reserved words and other names are emitted as quoted properties. Schema names must be safe,
non-reserved ASCII TypeScript identifiers after optional namespace-prefix removal, otherwise
generation fails.

## Unsupported constructs

The tool rejects `oneOf`, `anyOf`, `allOf`, `not`, `discriminator`, external `$ref` values,
object/array type unions, tuple and conditional schemas, and objects that explicitly combine
declared properties with `additionalProperties`. It never falls back to `any`. Unsupported or
ambiguous schemas fail with the schema name and JSON path.

To add a construct, extend the internal model in `OpenApi/`, parse and validate it in
`OpenApiSchemaParser`, resolve its TypeScript representation in `Generation/`, and add both a
positive test and a rejection test for invalid variants.

## Exit codes

| Code | Meaning |
| ---: | --- |
| 0 | Success |
| 1 | Invalid command line |
| 2 | Invalid OpenAPI document or schema |
| 3 | Unsupported schema construct |
| 4 | Generation, input/output, or write error |
| 5 | Generated file is out of date in `--check` mode |

Errors are written to stderr.

## Backend and frontend integration

With the ASP.NET Core backend running, explicitly capture its HTTP contract and generate the
frontend-only data contracts:

```bash
mkdir -p artifacts frontend/src/api/generated
curl --fail http://localhost:5080/openapi/v1.json --output artifacts/openapi.json
dotnet run --project tools/OpenApiTsContracts -- \
  --input artifacts/openapi.json \
  --output frontend/src/api/generated/contracts.generated.ts
```

Handwritten frontend APIs can then use type-only imports:

```ts
import type { MessageDetailsDto } from "./generated/contracts.generated";
```

The current backend contract contains `oneOf`; by design, generation stops with exit code 3
until that unsupported construct is removed from the published contract or explicitly added
to this tool with tests. The tool does not guess at its meaning.

For CI, first produce `artifacts/openapi.json` from the backend, then run:

```bash
dotnet run --project tools/OpenApiTsContracts -- \
  --input artifacts/openapi.json \
  --output frontend/src/api/generated/contracts.generated.ts \
  --check
```

Exit code 5 means the checked-in generated file must be regenerated. Unlike a
`git diff --exit-code` workflow, `--check` does not modify the worktree.

## Tests

```bash
dotnet test tools/OpenApiTsContracts.Tests/OpenApiTsContracts.Tests.csproj
```

The test project includes a realistic OpenAPI document and its golden TypeScript output in
`Fixtures/`.
