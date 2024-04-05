extends Node

var lights = []
@export var nodesWithEmission: Array[Node3D] = []

var defaultIntensities = {}
var noise: FastNoiseLite

# States of the light
var hasSetBackToDefault = false
var isFlickering = false

var isDead = false
var deathFrame = 0

var isReviving = false
var reviveFrame = 0

var interactable = true

var rng = RandomNumberGenerator.new()

var ExplodeSFX: AudioStreamPlayer3D
var FlickerSFX: AudioStreamPlayer3D
var RestoreSFX: AudioStreamPlayer3D
var HumSFX: AudioStreamPlayer3D
var TurnOffSFX: AudioStreamPlayer3D

@export var locator: Node
@export var Sparks: GPUParticles3D

func _on_object_interact(verb: String, type: String, target: String):
	if not type.to_lower().contains("light"):
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

# Called when the node enters the scene tree for the first time.
func _ready():
	noise = FastNoiseLite.new()

	ExplodeSFX = AudioStreamPlayer3D.new()
	ExplodeSFX.stream = load("res://Audio/Explode.wav")

	FlickerSFX = AudioStreamPlayer3D.new()
	FlickerSFX.stream = load("res://Audio/Flicker.wav")

	RestoreSFX = AudioStreamPlayer3D.new()
	RestoreSFX.stream = load("res://Audio/Restore.wav")

	TurnOffSFX = AudioStreamPlayer3D.new()
	TurnOffSFX.stream = load("res://Audio/TurnOff.wav")

	HumSFX = AudioStreamPlayer3D.new()
	HumSFX.stream = load("res://Audio/LightHum.wav")

	var parent = get_parent()
	
	var position = parent.position

	HumSFX.autoplay = true
	HumSFX.panning_strength = 3
	
	ExplodeSFX.volume_db = -7
	FlickerSFX.volume_db = -15
	RestoreSFX.volume_db = -7
	HumSFX.volume_db = -25

	HumSFX.pitch_scale = randf_range(0.95, 1.05)

	parent.add_child.call_deferred(HumSFX)
	parent.add_child.call_deferred(ExplodeSFX)
	parent.add_child.call_deferred(FlickerSFX)
	parent.add_child.call_deferred(RestoreSFX)
	parent.add_child.call_deferred(TurnOffSFX)

	rng.seed = position.x + position.y + position.z
	noise.seed = position.x + position.y + position.z
	lights = parent.find_children("*", "Light3D")

	EventBus.ObjectInteraction.connect(_on_object_interact)

	for light in lights:
		defaultIntensities[light.name] = light.light_energy

	for node in nodesWithEmission:
		defaultIntensities[node.name] = node.material.emission_energy_multiplier
		
	await get_tree().create_timer(2).timeout

func setEnergiesToDefault():
	for light in lights:
		light.light_energy = defaultIntensities[light.name]
	for node in nodesWithEmission:
		node.material.emission_energy_multiplier = defaultIntensities[node.name]

func setEnergy(num):
	for light in lights:
		light.light_energy = defaultIntensities[light.name] * num
	for node in nodesWithEmission:
		node.material.emission_energy_multiplier = defaultIntensities[node.name] * num

func turnOn():
	restore()
		
func restore():
	await get_tree().create_timer(randf_range(0.0, 1.5)).timeout
	RestoreSFX.pitch_scale = randf_range(0.85, 1.3)
	RestoreSFX.seek(0)
	RestoreSFX.play()
	HumSFX.play()

	var tween = create_tween()
	isFlickering = false;
	isDead = false;
	isReviving = true;
	await tween.tween_method(_restoreStep, 0.0, 1.0, 2.0).finished;
	setEnergiesToDefault();
	isReviving = false
	interactable = true

func _restoreStep(progress: float):
	var noiseMult = clamp(noise.get_noise_1d(Time.get_ticks_msec()) + 0.5, 0, 1)
	var cleanProgress = pow(progress, 2)
	setEnergy((cleanProgress * noiseMult * 0.75) + (cleanProgress * 0.25))

func flicker():
	await get_tree().create_timer(randf_range(0.0, 0.5)).timeout
	FlickerSFX.pitch_scale = randf_range(0.9, 1.1)
	FlickerSFX.seek(0)
	FlickerSFX.play()

	var tween = create_tween()
	isFlickering = true
	await tween.tween_method(_flickerStep, 0.0, 1.0, randf_range(0.75, 1.5)).finished
	isFlickering = false

	if not isDead:
		setEnergiesToDefault()
	else:
		setEnergy(0.0)

func _flickerStep(_step: float):
	setEnergy(clamp(noise.get_noise_1d(Time.get_ticks_msec()), 0, 1))

func explode():
	await get_tree().create_timer(randf_range(0.0, 0.5)).timeout

	HumSFX.stop()
	ExplodeSFX.pitch_scale = randf_range(0.85, 1.3)
	ExplodeSFX.seek(0)
	ExplodeSFX.play()

	await get_tree().create_timer(0.2).timeout

	Sparks.emitting = true
	var tween = create_tween()
	await tween.tween_method(_explodeStep, 0.0, 1.0, 2.0).finished
	setEnergy(0.0)
	isDead = true
	interactable = false

func _explodeStep(progress: float):
	var noiseMult = clamp(noise.get_noise_1d(Time.get_ticks_msec() + 0.5), 0, 1)
	var cleanEnergy = 1 / pow(10 * progress, 2.5)
	setEnergy((cleanEnergy * noiseMult * 0.75) + (cleanEnergy * 0.25))
 
func turnOff():
	await get_tree().create_timer(randf_range(0.0, 0.5)).timeout

	TurnOffSFX.pitch_scale = randf_range(0.85, 1.3)
	TurnOffSFX.play()

	await get_tree().create_timer(0.2).timeout

	HumSFX.stop()
	var tween = create_tween()
	await tween.tween_method(_explodeStep, 0.0, 1.0, 2.0).finished
	setEnergy(0.0)
	isDead = true

func turnOffInstant(): # Only the player should be able to do this
	if not interactable: return
	HumSFX.stop()
	setEnergy(0.0)
	isDead = true

func turnOnInstant(): # Only the player should be able to do this
	if not interactable: return
	HumSFX.play()
	setEnergiesToDefault()
	isDead = false

func getStatus():
	return "Light Status - " + ("Off" if isDead else "On") + (" " if interactable else " (**Player can't interact**)")

func interact():
	pass

func secondaryInteract():
	pass
