# CLAUDE.md — Bruno request collection

Manual/exploratory API testing for CES lives here. **This is Bruno, not Postman** — no
`.postman_collection.json`, no Insomnia export, no `.http` files (the stray
`api/CES.API/CES.API.http` is a leftover, not the collection).

Bruno is a git-friendly API client: every request is a plain file on disk, so requests are
reviewed and versioned like code. Adding a request means writing a file, not clicking
through a UI.

## Layout

```
Bruno/
└── bcgov/                    # the collection root
    ├── opencollection.yml    # collection manifest — name, ignore globs
    ├── <Request>.yml         # top-level requests
    ├── environments/
    │   ├── *.sample.yml      # committed templates
    │   └── *.yml             # real values — GITIGNORED, never commit
    ├── keycloak/             # a folder
    │   ├── folder.yml        # folder metadata + shared request defaults
    │   └── <Request>.yml
    └── smb/
        ├── folder.yml
        ├── Login.yml
        └── SmbHealth.yml
```

Folders are ordinary directories containing a `folder.yml`. Ordering in the UI is by the
`seq` number in each file's `info` block — top-level items and each folder's children are
numbered independently, starting at 1.

## Format: OpenCollection 1.0.0 YAML — *not* `.bru`

This collection uses Bruno's newer YAML format (`opencollection: 1.0.0` in
`opencollection.yml`). Most Bruno documentation and blog posts online describe the older
`.bru` DSL (`meta { … }`, `post { … }`, `script:post-response { … }`). **Do not write
`.bru` syntax here.** The concepts map 1:1, but the keys are different.

The authoritative schema is published and worth fetching rather than guessing:

```
https://raw.githubusercontent.com/usebruno/opencollection/main/packages/oc-schema/src/opencollection.schema.json
```

Look up `$defs.HttpRequest`, `$defs.Folder`, `$defs.Environment` and follow the `$ref`s.

### An HTTP request file

Top-level keys are exactly `info`, `http`, `runtime`, `settings`, `app`, `examples`,
`docs` — nothing else. `additionalProperties: false` is set throughout, so a misplaced
key (e.g. `headers` at the top level instead of under `http:`) is silently ignored by the
runtime rather than flagged.

```yaml
info:
  name: Human readable name
  type: http
  seq: 1

http:
  method: POST
  url: "{{apiBaseUrl}}/api/auth/login"
  headers:
    - name: Content-Type
      value: application/json
  params:                       # `type` is required: query | path
    - name: agencyId
      value: "4801"
      type: query
  body:
    type: json                  # json | text | xml | sparql
    data: |-
      { "username": "{{username}}" }
  auth: inherit                 # or a typed block; see below

runtime:
  assertions:                   # the pass/fail gate
    - expression: res.status
      operator: eq
      value: "200"              # values are strings, even for numbers
    - expression: res.body.token
      operator: isString
  actions:                      # capture a value into a variable
    - type: set-variable
      phase: after-response     # before-request | after-response
      selector:
        method: jsonq
        expression: res.body.token
      variable:
        name: cesAccessToken
        scope: runtime          # runtime | request | folder | collection | environment
  scripts:
    - type: after-response      # before-request | after-response | tests | hooks
      code: |-
        console.log(res.body);

settings:
  encodeUrl: true
  timeout: 0
  followRedirects: true
  maxRedirects: 5

docs: |-
  Prose explaining how to read the result.
```

### Auth

`http.auth` is either the string `inherit` or a typed object (`bearer`, `basic`, `oauth2`,
`ntlm`, `apikey`, `digest`, `awsv4`, `wsse`, `oauth1`):

```yaml
  auth:
    type: bearer
    token: "{{cesAccessToken}}"
```

A folder supplies defaults to its children under `request:` (not `http:`) — see
[bcgov/smb/folder.yml](bcgov/smb/folder.yml) and [bcgov/keycloak/folder.yml](bcgov/keycloak/folder.yml):

```yaml
request:
  auth:
    type: bearer
    token: "{{cesAccessToken}}"
```

There is **no `none` in the schema** — omit `auth` entirely for an unauthenticated
request. One existing file (`keycloak/RefreshSession.yml`) uses `auth: none`; Bruno
tolerates it, but prefer omission in new files.

### Environments

```yaml
name: dev
variables:
  - name: apiBaseUrl
    value: "http://localhost:9080"
  - secret: true            # secret vars carry NO value in the committed sample
    name: password
```

`environments/*.yml` is gitignored, `*.sample.yml` is not. **Never put a real credential,
hostname, client ID or token in a sample file or in a request's `docs`.** When a request
needs a new secret, add the *name* to both sample files with a comment explaining what it
is and where to get it.

## Repo conventions

- **`docs` is the deliverable, not decoration.** Every request in this collection carries
  a `docs` block that says what a pass proves, what each plausible failure status means,
  and what to change next. These requests exist for validating specs against real
  infrastructure, often by someone who has not read the implementation. Write them that way.
- **Assertions encode the acceptance criteria.** Where a request is validating a spec
  stage, its assertions should be that stage's exit criteria, so a green run *is* the
  sign-off. Leave genuinely optional/informational steps unasserted, and say in `docs`
  why.
- **Scripts render, assertions judge.** Use an `after-response` script for a legible
  transcript and next-step hints in the Console tab; keep pass/fail in `assertions`.
- **Chain auth through a runtime variable.** A login request captures the token with a
  `set-variable` action at `scope: runtime`, and the folder's bearer auth reads it. Runtime
  scope never touches disk, so no token is ever committed.
- No trailing `/api` on `apiBaseUrl` — each request writes its own `/api/...` path.
  Use `http://localhost:9080` (nginx, the `./manage debug` workflow), not `:5285`, unless
  hitting a bare `dotnet run`.

## Before committing a new request

The Bruno UI is the real consumer, but these checks catch the mistakes that are otherwise
invisible (a mistyped key is ignored, not rejected):

1. **Validate against the schema.** Download `opencollection.schema.json` (URL above) and
   run each file through a JSON Schema validator, picking the `$defs` entry by filename:
   `folder.yml` → `Folder`, `environments/*.yml` → `Environment`, everything else →
   `HttpRequest`. `pip install pyyaml jsonschema` is enough.
2. **Syntax-check embedded scripts.** Extract `runtime.scripts[].code` to a `.js` file and
   `node --check` it. A script that throws produces no output at all in Bruno.
3. **Exercise the script against fixtures.** For anything non-trivial, wrap the extracted
   code in `new Function('res', 'console', code)` and feed it hand-built response bodies
   for each interesting branch (success, each failure mode). Much faster than
   round-tripping through the real service, and it forces you to state the response shapes
   you're assuming.

Known pre-existing schema failures, so they aren't mistaken for regressions:
`keycloak/RefreshSession.yml` (top-level `headers` should be under `http:`, so the `Cookie`
header is probably not sent) and `CourtList.yml` (unquoted date parses as a YAML date, not
a string).

## Gotcha: writing these files

Write and edit these YAML files with the **Write/Edit tools**, not bash heredocs. The
embedded scripts and Windows-style SMB paths are full of backslashes and quotes, and the
shell mangles them even inside a quoted heredoc — this has already produced one truncated
file and one silently corrupted string.
