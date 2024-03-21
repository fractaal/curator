extends Node3D

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

var rng = RandomNumberGenerator.new()

var room = "";

var debugLabel: Label3D

func _on_object_interact(verb: String, objectType: String, target: String):
	
	print("I, ", self.name, " have received event ", verb, " ", objectType, " ", target)

	target = target.strip_edges()
	var _room = room.to_lower()

	if target.begins_with("in"):
		var targetRoom = target.substr(3).to_lower().strip_edges()
		if _room.contains(targetRoom):
			self[verb].call()
		else:
			return
	elif target == "all":
		self[verb].call()
	else:
		push_warning("Invalid target declaration ", target)

# Called when the node enters the scene tree for the first time.
func _ready():
	noise = FastNoiseLite.new()
	rng.seed = position.x + position.y + position.z
	noise.seed = position.x + position.y + position.z
	lights = find_children("*", "Light3D")

	Curator.ObjectInteraction.connect(_on_object_interact);

	debugLabel = Label3D.new()
	debugLabel.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	debugLabel.no_depth_test = true

	get_tree().get_root().add_child.call_deferred(debugLabel)
	debugLabel.position = global_position

	for light in lights:
		defaultIntensities[light.name] = light.light_energy

	for node in nodesWithEmission:
		defaultIntensities[node.name] = node.material.emission_energy_multiplier

	var bodies = find_children("*", "CollisionObject3D", true)
	var body: CollisionObject3D

	if bodies.size() > 0:
		body = bodies[0]
	else:
		debugLabel.text = "Can't determine room"
		return

	debugLabel.text = "Not in room"
	
	# Figure out what room I'm in
	await get_tree().create_timer(1).timeout
	var rooms = get_tree().get_nodes_in_group("rooms")
	for _room in rooms:
		var nodes = _room.get_overlapping_bodies()
		for node in nodes:
			if node == body:
				room = _room.name.strip_edges()
				debugLabel.text = _room.name.strip_edges()

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
	var tween = create_tween()
	isFlickering = false;
	isDead = false;
	isReviving = true;
	await tween.tween_method(_restoreStep, 0.0, 1.0, 2.0).finished;
	setEnergiesToDefault();
	isReviving = false

func _restoreStep(progress: float):
	var noiseMult = clamp(noise.get_noise_1d(Time.get_ticks_msec()) + 0.5, 0, 1)
	var cleanProgress = pow(progress, 2)
	setEnergy((cleanProgress * noiseMult * 0.75) + (cleanProgress * 0.25))

func flicker():
	var tween = create_tween()
	isFlickering = true
	await tween.tween_method(_flickerStep, 0.0, 1.0, randf_range(0.75, 1.5)).finished
	isFlickering = false
	setEnergiesToDefault();

func _flickerStep(_step: float):
	setEnergy(clamp(noise.get_noise_1d(Time.get_ticks_msec()), 0, 1))

func explode():
	$Sparks.emitting = true
	var tween = create_tween()
	await tween.tween_method(_explodeStep, 0.0, 1.0, 2.0).finished
	setEnergy(0.0)
	isDead = true

func _explodeStep(progress: float):
	var noiseMult = clamp(noise.get_noise_1d(Time.get_ticks_msec() + 0.5), 0, 1)
	var cleanEnergy = 1 / pow(10 * progress, 2.5)
	setEnergy((cleanEnergy * noiseMult * 0.75) + (cleanEnergy * 0.25))
 
func turnOff():
	var tween = create_tween()
	await tween.tween_method(_explodeStep, 0.0, 1.0, 2.0).finished
	setEnergy(0.0)
	isDead = true
