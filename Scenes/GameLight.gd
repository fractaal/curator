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
	rng.seed = position.x+position.y+position.z
	noise.seed = position.x+position.y+position.z
	lights = find_children("*", "Light3D")

	for light in lights:
		defaultIntensities[light.name] = light.light_energy

	for node in nodesWithEmission:
		defaultIntensities[node.name] = node.material.emission_energy_multiplier


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	if isFlickering:
		hasSetBackToDefault = false
		setEnergy(clamp(noise.get_noise_1d(Time.get_ticks_msec())+0.5, 0, 1))
	elif isDead:
		hasSetBackToDefault = false
		if (deathFrame < 100):
			var noiseMult = clamp(noise.get_noise_1d(Time.get_ticks_msec()) + 0.5, 0, 1)
			deathFrame += 0.25
			setEnergy((5/pow(deathFrame,2))*noiseMult)
	elif isReviving:
		hasSetBackToDefault = false
		if (reviveFrame < 25):
			reviveFrame += 0.5
			var noiseMult = clamp(noise.get_noise_1d(Time.get_ticks_msec()) + 0.5, 0, 1)
			setEnergy((reviveFrame**2.0/500) * noiseMult)
		else:
			isReviving = false


	else:
		if not hasSetBackToDefault:
			hasSetBackToDefault = true
			setEnergiesToDefault()

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
	isFlickering = false
	isDead = false
	reviveFrame = 0
	isReviving = true

func flicker():
	await get_tree().create_timer(rng.randf_range(1,2)).timeout
	isFlickering = true
	await get_tree().create_timer(1.0).timeout
	isFlickering = false
	
func explode():
	await get_tree().create_timer(rng.randf_range(1,2)).timeout
	$Sparks.emitting = true
	deathFrame = 0;
	isDead = true

func turnOff():
	await get_tree().create_timer(rng.randf_range(1,2)).timeout
	deathFrame = 0;
	isDead = true
	
