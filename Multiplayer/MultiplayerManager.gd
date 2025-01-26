extends Node

const SERVER_PORT = 4444
const SERVER_IP = "127.0.0.1"

var multiplayer_scene = preload("res://Scenes/Player/player.tscn");
var _player_spawn_node: Node3D

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass

func start_host():
	print("Starting host")
	var server_peer = ENetMultiplayerPeer.new()
	server_peer.create_server(SERVER_PORT, 1)
	server_peer.set_bind_ip(SERVER_IP)

	_player_spawn_node = get_tree().get_current_scene().get_node("PlayerSpawnLocation")
	
	if _player_spawn_node == null:
		push_error("Player spawn node not found")
	else:
		print("Player spawn node found")
	multiplayer.multiplayer_peer = server_peer 

	multiplayer.peer_connected.connect(on_peer_connected)
	multiplayer.peer_disconnected.connect(on_peer_disconnected)

	_remove_singleplayer_player()
	_add_player_to_game(1)

	print("Server started on ", SERVER_IP, ":", SERVER_PORT)

func on_peer_connected(peer_id: int):
	print("Peer connected: ", peer_id)

	_add_player_to_game(peer_id)


func on_peer_disconnected(peer_id: int):
	print("Peer disconnected: ", peer_id)

func _add_player_to_game(id: int):
	var player = multiplayer_scene.instantiate()
	player.player_id = id
	player.name = "Player_" + str(id)

	_player_spawn_node.add_child(player, true)

func join_server(_address: String):
	var client_peer = ENetMultiplayerPeer.new()

	client_peer.create_client("127.0.0.1", SERVER_PORT)

	multiplayer.multiplayer_peer = client_peer

	_remove_singleplayer_player()


func _remove_singleplayer_player():
	var player = get_tree().get_current_scene().get_node("Player")
	if player != null:
		player.queue_free()
	else:
		push_warning("Player not found while removing singleplayer player")
