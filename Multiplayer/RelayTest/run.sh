#!/usr/bin/env bash
# Two-instance ENet loopback test of EventBusRelay + LanDiscovery.
# Exits 0 only if BOTH the host and client instances pass their assertions.
set -u
cd "$(dirname "$0")/../.."

SCENE=res://Multiplayer/RelayTest/MultiplayerRelayTest.tscn
GODOT="${GODOT:-godot}"

"$GODOT" --headless "$SCENE" -- --host &
HOST_PID=$!

# Give the host instance time to boot .NET and open the port.
sleep 8

"$GODOT" --headless "$SCENE" -- --client &
CLIENT_PID=$!

wait "$HOST_PID"
HOST_RC=$?
wait "$CLIENT_PID"
CLIENT_RC=$?

echo "host exit=$HOST_RC client exit=$CLIENT_RC"

if [ "$HOST_RC" = 0 ] && [ "$CLIENT_RC" = 0 ]; then
	echo "RELAY TEST: ALL PASS"
else
	echo "RELAY TEST: FAILED"
	exit 1
fi
