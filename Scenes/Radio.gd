extends Node

@export var statusLabel: Label3D

@export var switch: AudioStreamPlayer3D
@export var staticSound: AudioStreamPlayer3D
@export var song: AudioStreamPlayer3D
@export var locator: Node

@export var indicator: CSGBox3D

var lastSongPosition = 0

var isOn = true;
var isPlaying = false;

var registry = preload ("res://Scripts/InteractableRegistry.cs")

func generate_random_red():
	var r = randf_range(0.0, 1) # Full red
	var g = randf_range(0.0, 0.2) # Random green, up to 0.2 intensity
	var b = randf_range(0.0, 0.2) # Random blue, up to 0.2 intensity
	return Color(r, g, b)

func _on_object_interact(verb: String, type: String, target: String):
	if not type.to_lower().contains("radio"):
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

var material: StandardMaterial3D

var random: RandomNumberGenerator = RandomNumberGenerator.new()

var objectType := "radio"

# Called when the node enters the scene tree for the first time.
func _ready():
	registry.Register(objectType)
	_updateStatusLabel()

	EventBus.ObjectInteraction.connect(_on_object_interact)

var elapsed = 0

func _process(delta):
	elapsed += delta

	if (elapsed > 0.075):
		elapsed = 0
		_updateStatusLabel()
	
func _updateStatusLabel():
	var out = "";

	if isPlaying:
		var color = generate_random_red()
		statusLabel.modulate = color
		out += str(round(randf_range(87.5, 108.0) * 100) / 100)
		indicator.material_override.emission_energy_multiplier = 10.0 * randf()
		indicator.material_override.emission = color
		song.pitch_scale = randf_range(0.8, 0.85)

		if randf() > 0.99:
			song.seek(song.get_playback_position() - randf_range(0.05, 0.25))
			await get_tree().create_timer(0.2).timeout
			song.seek(song.get_playback_position() - randf_range(0.05, 0.25))
			await get_tree().create_timer(0.2).timeout
			song.seek(song.get_playback_position() - randf_range(0.05, 0.25))
			await get_tree().create_timer(0.2).timeout
		
	else:
		indicator.material_override.emission_energy_multiplier = 5.0
		indicator.material_override.emission = Color.WHITE
		statusLabel.modulate = Color.WHITE
		out += "87.5"

	if not isOn:
		indicator.material_override.emission_energy_multiplier = 0.0
		out = ""
	
	statusLabel.text = out
	
func turnon():
	switch.seek(0)
	switch.play()

	isOn = true
	
	stop()

	_updateStatusLabel()

func turnoff():
	switch.seek(0)
	switch.play()

	staticSound.stop()
	song.stop()
	isOn = false

	_updateStatusLabel()
	
func playfreakymusicon():
	switch.seek(0)
	switch.play()

	if not isOn:
		turnon()

	staticSound.stop()
	song.play(lastSongPosition)
	isPlaying = true

	_updateStatusLabel()
	
func stop():
	switch.seek(0)
	switch.play()

	lastSongPosition = song.get_playback_position()

	if not isOn:
		turnon()
	
	song.stop()

	staticSound.play()
	isPlaying = false

	_updateStatusLabel()

func togglePower():
	if isOn:
		turnoff()
	else:
		turnon()

func togglePlay():
	if isPlaying:
		stop()
	else:
		playfreakymusicon()

func interact():
	togglePower()
	EventBus.emit_signal("NotableEventOccurred", "Player turned " + ("on" if isOn else "off") + " the radio in " + locator.Room)

func secondaryInteract():
	togglePlay()
	# EventBus.emit_signal("NotableEventOccurred", "Player " + ("started" if isPlaying else "stopped") + " the radio in " + locator.Room)

func getStatus():
	if isOn:
		return "Radio Status: On, " + ("Playing freaky music" if isPlaying else "Static")
	else:
		return "Radio Status: Off"
 
func getStatusForPlayer():
	if isOn:
		return "Radio Status: On, " + ("???" if isPlaying else "Static")
	else:
		return "Radio Status: Off"
