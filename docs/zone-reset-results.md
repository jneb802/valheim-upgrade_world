# Zone reset ZDO results

A ZDO is a stored world object. `zones_reset` reports confirmed deletions caused by that operation, including linked spawned objects. This count is different from zones reset, the world's net object change, and AsyncSave's removed/skipped save entries.

The reset passes an operation-local counter through `Helper.RemoveZDO`. It skips players and invalid objects before recording a request. Requests already queued before the reset are excluded. A `DestroyZDO` observer also excludes requests made by other mods while the reset yields. Each ID is counted once. Recursive linked-object calls share the counter. The observer's active reference is released on completion, failure, or cancellation.

Valheim queues `DestroyZDO` requests. The counter confirms a deletion only when the requested ID no longer exists in `ZDOMan.m_objectsByID`. After resetting the zones, the operation waits up to ten seconds for confirmation. It checks pending IDs at most once per 100 ms. It does not scan the entire world for each object.

## Output contract

Each terminal result uses schema version 1 and event `zone_reset_result`. It appears in the BepInEx log with prefix `[UpgradeWorldZoneReset]` and is appended as one JSON object per line to `BepInEx/config/upgrade_world_zone_reset_results.jsonl`. The file contains operation results, not secrets or player IDs. A file-write failure leaves the result in the server log and does not fail maintenance.

| Field | Meaning |
| --- | --- |
| `operationId` | Unique ID for this reset |
| `world`, `command`, `quadrants` | World and requested operation |
| `startedUtc`, `endedUtc` | UTC execution times; start is null for an unstarted cancellation |
| `status` | `completed`, `failed`, or `cancelled` |
| `zonesReset` | Zones reset before the operation ended |
| `zdosRemoved` | Confirmed unique deletions requested by this reset; null if execution never started |
| `zdosRequested` | Unique eligible deletion requests tracked |
| `zdosPending` | Tracked requests still present in the world at reporting time |
| `worldZdosBefore`, `worldZdosAfter` | Counts in the world's object table at execution start and result emission |
| `worldZdoNetDecrease` | Before minus after; can be negative and can differ from `zdosRemoved` because of unrelated changes |
| `countComplete` | True only if execution started and all tracked deletions were confirmed |
| `error` | Execution, cancellation, or confirmation error; null when none |

Failures and cancellations emit partial confirmed counts. Count completeness describes deletion confirmation, not whether every requested zone was processed. Use both `status` and `countComplete`. A queued preview does not execute or emit a completed result; cancelling it emits null counts. Counters are released after their terminal result.

The existing `Reset completed.` console text remains available for the maintenance coordinator. A final ZDO result appears after deletion confirmation and the normal border/terrain work. A result does not confirm that the world was saved. A future coordinator integration must match this result to its current world/command/run, require `status=completed` and `countComplete=true`, then independently confirm a world save before posting the count to Discord. A missing result must be reported as unavailable, never as zero.

## Counter tests

```sh
dotnet build Tests/ResetZdoCounter.Tests.csproj
mono Tests/bin/Debug/net48/ResetZdoCounter.Tests.exe
```

Use the matching Valheim references in the sibling `Libs` directory. If those publicized assemblies include Jotunn build attributes, set `MONO_PATH` to the directory that contains `JotunnBuildTask.dll`.
