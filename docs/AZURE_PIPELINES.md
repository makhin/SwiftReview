# Azure Pipelines: build and test

Two independent pipelines validate this repository without deployment:

| Pipeline | YAML | Agent | Checks |
| --- | --- | --- | --- |
| ORP Backend | `/azure-pipelines-backend.yml` | `windows-latest` | Restore, Release build, Domain and Application unit tests |
| ORP Frontend | `/azure-pipelines-frontend.yml` | `ubuntu-latest` | `npm ci`, lint, TypeScript, Vitest, production build |

Backend installs the SDK selected by `backend/global.json` and runs all .NET
commands from `backend`. Compiler warnings fail the build through the existing
`Directory.Build.props`. Frontend uses Node.js 24 and the committed npm lockfile.
Both pipelines publish test results to the Azure Pipelines **Tests** tab, including
on test failure. Neither pipeline deploys, applies migrations, or starts the API.

## Create the two pipelines

1. Commit and push both YAML files to the Azure Repos branch you want to build
   (for example, `chore/backend-init`).
2. Open **Pipelines → New pipeline → Azure Repos Git** and select the repository.
3. On **Configure your pipeline**, choose **Existing Azure Pipelines YAML file**.
4. Select the pushed branch and `/azure-pipelines-backend.yml`, then **Continue → Run**.
5. Rename the pipeline to **ORP Backend**.
6. Repeat with `/azure-pipelines-frontend.yml` and name it **ORP Frontend**.

Use the existing YAML option rather than the ASP.NET Core (.NET Framework) or React
starter templates. These YAML files already specify the correct SDK, solution,
working directories, and checks. Hosted Windows and Ubuntu agents must be enabled
for the organization; if the organization only permits a private agent pool,
replace `pool.vmImage` with its actual `pool.name` and use matching agent OSes.
Agents need access to the SDK/Node downloads, nuget.org and the npm registry.
No SQL connection, application secrets, or Azure deployment service connection is
needed for these builds.

## Push and pull request validation

Push triggers cover all branches containing these YAML files, including
`chore/backend-init` and `main`. Path filters start backend for `backend/**`
changes and frontend for `frontend/**` or `.env.example` changes. Editing a
pipeline's own YAML also starts it. Changes to both folders start both pipelines;
documentation-only changes outside those folders do not. Manual runs are available
regardless of path filters.

For Azure Repos, PR validation is configured in **Repos → Branches → target branch
→ Branch policies → Build validation**, not with a YAML `pr:` trigger:

- Add **ORP Backend** with **Automatic**, **Required** and path filter
  `/backend/*;/azure-pipelines-backend.yml`.
- Add **ORP Frontend** with **Automatic**, **Required** and path filter
  `/frontend/*;/.env.example;/azure-pipelines-frontend.yml`.
- Use **Immediately when the target branch is updated** for build expiration.

Set these policies on each protected target branch (for example, `main`). Branch
policy path filters are separate from YAML push filters; update both if paths change.
See Microsoft's [Azure Repos pipeline triggers](https://learn.microsoft.com/en-us/azure/devops/pipelines/repos/azure-repos-git?view=azure-devops)
and [build validation policies](https://learn.microsoft.com/en-us/azure/devops/repos/git/branch-policies?view=azure-devops&tabs=browser#build-validation).

## Scope of validation

CI builds the versioned `backend/ORP.sln`. The ignored Docker solution and local
API/SQL integration tests are unavailable in a Git clone and are not part of Azure
CI. Keep running `./test-backend.docker.sh -m:1` in the prepared local Docker
workspace for SQL integration coverage.

`api:check` is not included in these independent pipelines because it requires a
running API. HTTP contract changes still require the local contract check described
in `AGENTS.md`. Build outputs are verified but are not packaged or published as
deployment artifacts.
