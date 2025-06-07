extends Node

var rigidBody: RigidBody3D
var locator: Node
var playerManager: Node  # Reference to PlayerManager for multiplayer support

var shiftSfx: AudioStreamPlayer3D
var joltSfx: AudioStreamPlayer3D
var throwSfx: AudioStreamPlayer3D

var originalRotation: Vector3

var registry = preload ("res://Scripts/InteractableRegistry.cs")
var objectType := "physics object"

func connect_to_event_bus():
	await get_tree().create_timer(3).timeout
	EventBus.ObjectInteraction.connect(_on_object_interact)

func _on_object_interact(verb: String, type: String, target: String):
	if not type.to_lower().contains("object"):
		return

	target = target.strip_edges()

	if target == "all":
		self[verb].call()
		EventBus.emit_signal("ObjectInteractionAcknowledged", verb, type, target)
	else:
		var targetRoom = target
		if targetRoom.begins_with("in"):
			targetRoom = targetRoom.substr(2)
		if locator.IsInRoom(targetRoom):
			self[verb].call()
			EventBus.emit_signal("ObjectInteractionAcknowledged", verb, type, target)
		else:
			return

func getStatus():
	return "Physics Object"

func _ready():
	registry.Register(objectType)
	shiftSfx = AudioStreamPlayer3D.new()
	joltSfx = AudioStreamPlayer3D.new()
	throwSfx = AudioStreamPlayer3D.new()

	shiftSfx.stream = load("res://Audio/Rattle.wav")
	joltSfx.stream = load("res://Audio/Rattle.wav")
	throwSfx.stream = load("res://Audio/Throw.wav")

	joltSfx.volume_db = -10
	shiftSfx.volume_db = -20

	shiftSfx.panning_strength = 2.5;
	joltSfx.panning_strength = 2.5;
	throwSfx.panning_strength = 2.5;

	rigidBody = get_parent() as RigidBody3D
	locator = get_parent().get_node("RoomLocator")

	# Get PlayerManager reference for multiplayer support
	playerManager = get_node("/root/PlayerManager")
	if playerManager == null:
		push_error("PlayerManager not found - required for multiplayer architecture")

	rigidBody.add_child.call_deferred(shiftSfx)
	rigidBody.add_child.call_deferred(joltSfx)
	rigidBody.add_child.call_deferred(throwSfx)

	originalRotation = rigidBody.global_rotation_degrees

	connect_to_event_bus.call_deferred()

func jolt():
	# Only server should initiate physics actions
	if multiplayer.is_server():
		_handle_jolt.rpc()
	else:
		# Clients request the server to perform jolt
		_request_jolt.rpc_id(1)

@rpc("any_peer", "call_local")
func _request_jolt():
	if not multiplayer.is_server():
		return
	_handle_jolt.rpc()

@rpc("authority", "call_local")
func _handle_jolt():
	# Server generates random values and sends to all clients
	var delay = randf_range(0.0, 1.5)
	var pitch = randf_range(0.8, 1.2)
	var impulse_vector = Vector3(randf(), randf(), randf()) * 2 * rigidBody.mass

	_execute_jolt.rpc(delay, pitch, impulse_vector)

@rpc("authority", "call_local")
func _execute_jolt(delay: float, pitch: float, impulse_vector: Vector3):
	await get_tree().create_timer(delay).timeout
	joltSfx.pitch_scale = pitch
	joltSfx.play(0)
	rigidBody.apply_impulse(impulse_vector)

func throw():
	# Only server should initiate physics actions
	if multiplayer.is_server():
		_handle_throw.rpc()
	else:
		# Clients request the server to perform throw
		_request_throw.rpc_id(1)

@rpc("any_peer", "call_local")
func _request_throw():
	if not multiplayer.is_server():
		return
	_handle_throw.rpc()

@rpc("authority", "call_local")
func _handle_throw():
	# Server determines target player and generates random values
	var target_player = _get_closest_player()
	if target_player == null:
		push_warning("No players found for throw action")
		return

	var delay = randf_range(0.0, 1.5)
	var pitch = randf_range(0.8, 1.2)
	var direction = target_player.global_transform.origin - rigidBody.global_transform.origin
	direction = direction.normalized()
	var impulse_strength = 8
	var impulse = direction * impulse_strength * rigidBody.mass

	_execute_throw.rpc(delay, pitch, impulse)

@rpc("authority", "call_local")
func _execute_throw(delay: float, pitch: float, impulse: Vector3):
	await get_tree().create_timer(delay).timeout
	throwSfx.pitch_scale = pitch
	throwSfx.volume_db = -15
	throwSfx.play(0)
	rigidBody.apply_impulse(impulse)

func shift():
	# Only server should initiate physics actions
	if multiplayer.is_server():
		_handle_shift.rpc()
	else:
		# Clients request the server to perform shift
		_request_shift.rpc_id(1)

@rpc("any_peer", "call_local")
func _request_shift():
	if not multiplayer.is_server():
		return
	_handle_shift.rpc()

@rpc("authority", "call_local")
func _handle_shift():
	# Server generates random values and sends to all clients
	var delay = randf_range(0.0, 1.5)
	var pitch = randf_range(0.8, 1.2)
	var impulse_vector = Vector3(
		randf() * 0.25,
		randf(),
		randf() * 0.25
	) * rigidBody.mass

	_execute_shift.rpc(delay, pitch, impulse_vector)

@rpc("authority", "call_local")
func _execute_shift(delay: float, pitch: float, impulse_vector: Vector3):
	await get_tree().create_timer(delay).timeout
	shiftSfx.pitch_scale = pitch
	shiftSfx.play(0)
	rigidBody.apply_impulse(impulse_vector)

# Helper function to get the closest player for throw actions
func _get_closest_player() -> Node3D:
	if playerManager == null:
		return null

	var all_players = playerManager.GetAllPlayers()
	if all_players.size() == 0:
		return null

	var closest_player: Node3D = null
	var closest_distance: float = INF

	for player_id in all_players:
		var player = all_players[player_id]
		if player != null:
			var distance = rigidBody.global_position.distance_to(player.global_position)
			if distance < closest_distance:
				closest_distance = distance
				closest_player = player

	return closest_player
