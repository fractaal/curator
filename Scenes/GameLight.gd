extends Node3D

var lights = []
@export var nodesWithEmission : Array[Node3D] = []

var defaultIntensities = {}
var noise : FastNoiseLite

# States of the light
var hasSetBackToDefault = false
var isFlickering = false

var isDead = false
var deathFrame = 0

var isReviving = false
var reviveFrame = 0

var rng = RandomNumberGenerator.new()

# Called when the node enters the scene tree for the first time.
func _ready():
	noise = FastNoiseLite.new()
	rng.seed = position.x + position.y + position.z
	noise.seed = position.x + position.y + position.z
	lights = find_children("*", "Light3D")

	for light in lights:
		defaultIntensities[light.name] = light.light_energy

	for node in nodesWithEmission:
		defaultIntensities[node.name] = node.material.emission_energy_multiplier

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

		
func restore():
	await get_tree().create_timer(rng.randf_range(1,2)).timeout

	var tween = create_tween()

	isFlickering = false;
	isDead = false;
	isReviving = true;

	await tween.tween_method(_restoreStep, 0.0, 1.0, 1.0).finished;

	setEnergiesToDefault();

	isReviving = false

func _restoreStep(progress: float):
	var noiseMult = clamp(noise.get_noise_1d(Time.get_ticks_msec()) + 0.5, 0, 1)
	setEnergy(pow(progress, 2) * noiseMult)

func flicker():
	var tween = create_tween()

	await get_tree().create_timer(rng.randf_range(1,2)).timeout

	isFlickering = true

	await tween.tween_method(_flickerStep, 0.0, 1.0, 1.0).finished;

	isFlickering = false

	setEnergiesToDefault();

func _flickerStep():
	setEnergy(clamp(noise.get_noise_1d(Time.get_ticks_msec())+0.5, 0, 1))


func explode():
	await get_tree().create_timer(rng.randf_range(1,2)).timeout
	$Sparks.emitting = true

	var tween = create_tween()
	await tween.tween_method(_explodeStep, 0, 1, 1).finished

	isDead = true

func _explodeStep(progress: float):
	var noiseMult = clamp(noise.get_noise_1d(Time.get_ticks_msec() + 0.5), 0, 1)
	setEnergy((1 - pow(progress, 2)) * noiseMult)

func turnOff():
	await get_tree().create_timer(rng.randf_range(1,2)).timeout;

	var tween = create_tween()

	await tween.tween_method(_explodeStep, 0, 1, 1).finished
	 
	isDead = true
	
