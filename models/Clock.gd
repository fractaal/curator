extends Node

@export var pendulum: Node3D
@export var tick_sounds: Array[AudioStreamWAV]
@export var sound_player: AudioStreamPlayer3D
@export var chime_player: AudioStreamPlayer3D
@export var bell_player: AudioStreamPlayer3D

@export var locator: Node

var sound_index: int = 0
var time: float = 0
var tick_elapsed: float = 0
var chiming: bool = false

var registry = preload ("res://Scripts/InteractableRegistry.cs")
var objectType := "clock"

func connect_to_event_bus():
	await get_tree().create_timer(3).timeout
	EventBus.GhostAction.connect(_on_ghost_action)

func _on_ghost_action(verb: String, arguments: String):
	var __ = arguments.split(",")
	
	if verb == "chimeclock":
		var num_chimes = __[0].to_int()
		
		for i in range(num_chimes):
			bell_player.play(0)
			await get_tree().create_timer(1.5).timeout
	elif verb == "chimeclockwestminster":
		chime()

func getStatus():
	return "Grandfather Clock (chimeClock(number) and chimeClocksWestminster() to use!)" + ("(Chiming Westminster Quarters)" if chiming else "")

func _ready():
	registry.Register(objectType)
	connect_to_event_bus()

func _process(delta):
	time += delta
	tick_elapsed += delta
	
	var angle = sin(PI * (time + 0.5)) * 4

	pendulum.global_rotation_degrees = Vector3(angle, 0, 0)
	
	if tick_elapsed > 1:
		tick_elapsed = 0
		
		sound_player.stream = tick_sounds[sound_index]
		sound_player.pitch_scale = randf_range(0.8, 1.1)
		sound_player.play(0)
		
		sound_index = (sound_index + 1) % tick_sounds.size()

func chime():
	print("Playing")
	chiming = true
	chime_player.play(0)
	await chime_player.finished
	chiming = false
