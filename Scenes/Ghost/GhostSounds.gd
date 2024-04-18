extends Node3D

@export var sound_streams: Array[AudioStreamWAV]
@onready var player = $Player

static var _instance = null

func connect_to_event_bus():
	await get_tree().create_timer(3).timeout
	EventBus.GhostAction.connect(_on_ghost_action)

static func get_available_sound_names() -> String:
	return _instance._get_available_sound_names()

func _get_available_sound_names() -> String:
	var result := ""
	
	for stream in sound_streams:
		result += stream.resource_path.get_file().get_basename() + ", "
	
	return result

func _on_ghost_action(verb: String, arguments: String):
	if verb.contains("emitsound"):
		var __ := arguments.to_lower().split(",")
		var sound_name = __[0]
		var target = ""
		
		if (__.size() > 1):
			target = __[1]
		
		var stream: AudioStreamWAV
		
		for _stream in sound_streams:
			print("Comparing ", _stream.resource_path.get_file().get_basename(), " with ", sound_name)
			var factor = FuzzySharpGodotBridge.PartialRatio(_stream.resource_path.get_file().get_basename(), sound_name)
			print("Factor was ", factor)
			if factor > 75:
				stream = _stream

		if stream == null:
			print("No sound found for ", sound_name)
			return
		
		if verb == "emitsoundasghost":
			var _player = AudioStreamPlayer3D.new()
			_player.bus = "GhostSFX"
			_player.panning_strength = 1.5
			_player.stream = stream
			_player.pitch_scale = randf_range(0.8, 1.2)
			add_child(_player)
			_player.play(0)

			await _player.finished

			_player.queue_free()

		elif verb == "emitsoundinroom":
			var node = AudioStreamPlayer3D.new()
			get_tree().current_scene.add_child(node)
			node.global_position = TargetResolution.GetTargetPosition(target)
			
			node.stream = stream
			node.play(0)
			
			await node.finished
			node.queue_free()

# Called when the node enters the scene tree for the first time.
func _ready():
	_instance = self
	connect_to_event_bus()

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	pass
