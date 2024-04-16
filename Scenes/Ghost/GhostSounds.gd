extends Node3D

@export var sound_streams: Array[AudioStreamWAV]
@onready var player = $Player

func connect_to_event_bus():
	await get_tree().create_timer(3).timeout
	EventBus.GhostAction.connect(_on_ghost_action)

func _on_ghost_action(verb: String, arguments: String):
	if verb == "emitsoundasghost":
		#
	elif verb == "emitsoundinroom":
		#

# Called when the node enters the scene tree for the first time.
func _ready():
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	pass
