extends Node

const SERVER_PORT = 4444
const MAX_CLIENTS = 7 # 8 players total including the host

var multiplayer_scene = preload("res://Scenes/Player/player.tscn")
var _player_spawn_node: Node3D

# Human-facing crew numbering (1 = host). Monotonic — numbers are never reused
# within a session, so "Player 2" stays unambiguous in the ghost's event log
# even after someone disconnects.
var _next_player_number := 0

func start_host():
	var server_peer = ENetMultiplayerPeer.new()
	var err = server_peer.create_server(SERVER_PORT, MAX_CLIENTS)
	if err != OK:
		push_error("Failed to start server on port %d (error %d)" % [SERVER_PORT, err])
		return

	_player_spawn_node = get_tree().get_current_scene().get_node("PlayerSpawnLocation")
	if _player_spawn_node == null:
		push_error("Player spawn node not found")
		return

	multiplayer.multiplayer_peer = server_peer

	multiplayer.peer_connected.connect(on_peer_connected)
	multiplayer.peer_disconnected.connect(on_peer_disconnected)

	_add_player_to_game(1)

	LanDiscovery.StartBeacon(MAX_CLIENTS + 1)

	print("Server started on port ", SERVER_PORT)

func on_peer_connected(peer_id: int):
	print("Peer connected: ", peer_id)

	_add_player_to_game(peer_id)
	_late_join_sync(peer_id)

func on_peer_disconnected(peer_id: int):
	print("Peer disconnected: ", peer_id)

	# Freeing the node despawns it on every peer; player.gd unregisters itself
	# from PlayerManager in _exit_tree.
	var player = _player_spawn_node.get_node_or_null("Player_" + str(peer_id))
	if player:
		player.queue_free()

func _add_player_to_game(id: int):
	var player = multiplayer_scene.instantiate()

	# player_id (and multiplayer authority) derive from this name in player.gd
	# _enter_tree, and every peer registers the node with PlayerManager in _ready.
	player.name = "Player_" + str(id)

	_next_player_number += 1
	player.player_number = _next_player_number # replicated at spawn

	_player_spawn_node.add_child(player, true)

# Push authoritative one-shot state (door poses, dead lights, radio playback...)
# to a late joiner. Continuous state rides the MultiplayerSynchronizers; this
# covers the visual/audio state a late joiner couldn't replay. Scripts that own
# such state add themselves to the group in _ready.
func _late_join_sync(peer_id: int):
	for node in get_tree().get_nodes_in_group("late_join_synced"):
		node.late_join_sync(peer_id)

func join_server(address: String):
	var client_peer = ENetMultiplayerPeer.new()

	var err = client_peer.create_client(address, SERVER_PORT)
	if err != OK:
		push_error("Failed to connect to %s:%d (error %d)" % [address, SERVER_PORT, err])
		return

	multiplayer.multiplayer_peer = client_peer

	print("Joining ", address, ":", SERVER_PORT)
