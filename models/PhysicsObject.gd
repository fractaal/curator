extends Node

var rigidBody: RigidBody3D
var locator: Node
var player: Node3D

var shiftSfx: AudioStreamPlayer3D
var joltSfx: AudioStreamPlayer3D
var throwSfx: AudioStreamPlayer3D

var registry = preload ("res://Scripts/InteractableRegistry.cs")

var originalRotation: Vector3

var objectType := "physics object"

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
	player = get_tree().current_scene.get_node("Player")

	rigidBody.add_child.call_deferred(shiftSfx)
	rigidBody.add_child.call_deferred(joltSfx)
	rigidBody.add_child.call_deferred(throwSfx)
	
	originalRotation = rigidBody.global_rotation_degrees

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

func jolt():
	await get_tree().create_timer(randf_range(0.0, 1.5)).timeout
	joltSfx.pitch_scale = randf_range(0.8, 1.2)
	joltSfx.play(0)
	rigidBody.apply_impulse(Vector3(
		randf(),
		randf(),
		randf()
	) * 2 * rigidBody.mass)

func throw():
	await get_tree().create_timer(randf_range(0.0, 1.5)).timeout
	throwSfx.pitch_scale = randf_range(0.8, 1.2)
	throwSfx.play(0)
	var direction = player.global_transform.origin - rigidBody.global_transform.origin
	direction = direction.normalized()
	# Adjust the magnitude of the impulse as necessary. 
	# You might want to experiment with different values for different effects.
	var impulse_strength = 8
	var impulse = direction * impulse_strength * rigidBody.mass
	
	rigidBody.apply_impulse(impulse)

func shift():
	await get_tree().create_timer(randf_range(0.0, 1.5)).timeout
	shiftSfx.pitch_scale = randf_range(0.8, 1.2)
	shiftSfx.play(0)
	rigidBody.apply_impulse(Vector3(
		randf() * 0.25,
		randf(),
		randf() * 0.25
	) * rigidBody.mass)

func getStatus():
	return "Physics Object"
