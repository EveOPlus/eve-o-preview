# Character identities and portraits

Light and Dark display public character portraits beside client names and cycle
group members, including offline members. Initials occupy the same space while a
lookup is pending or unavailable. Legacy retains its original lists. Full window
titles remain the identities used by profiles, ordering, shortcuts and previews.

## Identity map

[CharacterIdentityCache](../../Eve-O-Preview/Services/Implementation/CharacterIdentityCache.cs)
is a singleton shared by discovery and the Windows workspace backend. Its
`KnownCharacters` snapshot is an in-memory map keyed by character name, containing
only the name, character ID, EVE user ID and successful lookup timestamps. The
same derived fields are saved atomically to `Cache/Characters.json` beside the
resolved global settings file, independently of gameplay profiles.

Only named `EVE - <name>` windows are accepted; `EVE` login windows are excluded.
The prefix is removed for public API lookup without changing persisted window
titles. `POST https://esi.evetech.net/universe/ids` receives a JSON name array, a
versioned application user agent and `X-Compatibility-Date: 2026-09-07`. Only an
exact, case-insensitive name match in the response's **characters** category is
accepted. Corporation, item and other IDs must never be used as character IDs.
See the [official ESI operation](https://developers.eveonline.com/api-explorer#/operations/PostUniverseIds).

Discovery passes its process snapshot to the identity cache after releasing its
lock. HTTP and command-line work runs on background tasks, deduplicated by name
and limited to two concurrent refreshes. A missing user ID is queried on first
discovery. Successful character/account lookups refresh after seven days; an
hourly maintenance timer also checks known entries. Account refresh requires the
character to be running. A changed process association invalidates an in-flight
account result. A new PID can retry a previously unavailable user ID immediately.

Failures retain previous IDs. Missing names retry after twelve hours. Other
lookup/read failures use an hour backoff; ESI failures also respect `Retry-After`
and `X-Esi-Error-Limit-Reset`. Repeated discovery polls do not perform repeated
command-line or API reads. A failed disk write leaves the in-memory map usable.

## Launch-token boundary

[EveClientUserIdReader](../../Eve-O-Preview/Services/Implementation/EveClientUserIdReader.cs)
inspects only an already-discovered `ExeFile` PID whose current window title
matches the requested character. It uses a temporary query-limited process handle
and `NtQueryInformationProcess(ProcessCommandLineInformation)`; it does not modify
the client, read arbitrary game memory or install a hook.

The reader accepts the launcher's `ssoToken` argument, decodes the JWT payload and
returns only a positive numeric root `sub` of the form `USER:EVE:<id>`. Temporary
command-line and decoding buffers are cleared. Raw command lines, JWTs, other
claims and reader exception details are never returned, logged or persisted.
Expired launch-token metadata remains readable for long-running clients.

This is a local account association, not verified authentication: decoding does
not validate a JWT signature. Never use the cached user ID to authorize requests,
log in, or infer ownership rights. ESI requests and image downloads are public and
receive no launch token. Test fixtures contain synthetic claims only.

## Portrait cache and UI

[CharacterPortraitCache](../../Eve-O-Preview/Services/Implementation/CharacterPortraitCache.cs)
reuses the existing `Cache/Portraits/<characterId>.jpg` disk cache and in-memory
bytes. Requests use `https://images.evetech.net/characters/<id>/portrait?size=128`.
See the [image-server contract](https://developers.eveonline.com/docs/services/image-server/).
Fresh images last seven days. Stale images remain visible during a deduplicated
refresh; failed downloads retain the previous image with a one-hour retry delay.
`PortraitChanged` notifies the backend when replacement bytes become available.

The portable contracts are `IWorkspaceCharacterProvider`,
`IWorkspacePortraitProvider` and `IWorkspacePortraitUpdates`. They contain no
process handles or tokens. [WorkspaceView.Characters](../../Eve-O-Preview.UI/WorkspaceView.Characters.cs)
loads images after a row attaches, retains initials on failure, ignores results
for detached/disposed rows and disposes decoded images with the workspace.
Loading does not change row dimensions or disable client/order controls.

## Validation

`CharacterIdentityTests` exercises synthetic JWT parsing, non-character ESI
matches, deduplication, persistent derived fields, offline-first resolution,
weekly account changes, error backoff and changed-process rejection.
`CharacterPortraitCacheTests` covers disk reuse, concurrent downloads, weekly
refresh and failed-image fallback. The headless smoke harness captures portraits
in Clients and normal/expanded cycle order for both modern themes and compact
sizes, plus RTL layout, while checking delayed lookups and client commands.

These automated tests do not use real launch tokens or perform ESI authentication.
A live extraction check should report only success/failure counts and must never
write a real token or command line to output or fixtures.
