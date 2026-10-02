# Live zone reset proof fixture

This is a test-only plugin. Do not ship it or load it on production. It creates persistent test objects and changes the test world's generated-zone flags. Back up and restore the world and profile. Use the Odin `valheim-validation` skill and a device lease.

Build with `dotnet build Tests/ValdevFixture/ValdevFixture.csproj`. Temporarily load its DLL alongside Upgrade World on the maintained Valdev production-mirror profile. Use Valheim Rcon's `consoleCommand` route for the commands below. Read `WARP89_PROOF` from the server log to verify actual object IDs independently of the mod's counter.

1. Run `uw89_fixture`. It creates ten objects in zone `-200,-200`, a linked spawned object outside that zone, and three protected/control objects. The initial proof must show `targets=11 present=11 removed=0 protected=3/3`.
2. Preview `zones_reset zone=-200,-200 quadrant=northeast safeZones=2 terrainSafeZones=1`. Check that all target IDs remain present. Use `stop` to cancel the queued preview. With `zone=`, the quadrant center is that zone's center. The center belongs to northeast.
3. Repeat with `start`. Verify exactly 11 target IDs disappear and the final result reports 11 confirmed deletions. The three protected/control IDs must remain.
4. Run the same command on zone `-205,-205` to check base protection and `-207,-207` to check terrain protection. Use `zone=200,200 quadrant=southwest` to check quadrant exclusion. All must report zero removals and preserve their objects. Use `uw89_empty` and reset `-202,-202` with `quadrant=northeast` to check an eligible empty zone.
5. Run `uw89_playercheck` with the candidate mod. It temporarily adds a synthetic peer only for a synchronous call to the actual deletion helper. Verify `present=True requested=0`. It removes the synthetic peer immediately and cleans up its test ZDO. This checks the player-skip path; it does not replace multiplayer compatibility testing.
6. Run `uw89_again`, then `uw89_foreign`, then reset `-200,-200`. One target is queued for deletion outside the reset helper after the operation starts. Verify 11 independent target IDs disappear but the attributed reset count is 10.
7. Run `uw89_again` and `uw89_hold`, then reset `-200,-200`. Destruction is held by a test Harmony patch. After the ten-second confirmation limit, verify zero confirmed removals, 11 pending requests, and `countComplete=false`. Run `uw89_release`, then verify the target IDs disappear.
8. Repeat the held test, but run `stop` while the reset waits. Verify a partial cancelled result. Always run `uw89_release` afterwards. Repeat a normal reset to check that counters do not carry between operations.
9. Run `uw89_again` and `uw89_fault`, then reset `-200,-200`. The fixture deliberately throws `WARP89_EXPECTED_FAILURE` after one zone. Verify a failed partial result, an empty operation queue, and correct counts on the next normal run.
10. Request and independently confirm a world save. Preserve result JSON lines, command output, the proof lines, loaded mod versions, and the inspected log window. Restore the original profile and world, remove this plugin, verify restoration, and release the lease.

The fixture's deliberately invalid terrain-data marker is used only in an excluded test zone outside the normal world radius. Do not visit that zone. Restore the backed-up world after the test.
