extends Node

@export var Player: Node3D
@export var Ghost: Node3D

# This is more for recognizing commands
const VERBS = ['toggle', 'flicker', 'explode', 'ring', 'throw', 'knock', 'open', 'close', 'lock']
const OBJECTS = ['Lights', 'TVs']

enum ObjectType {LIGHT, TV} # PHONE, CHAIR, PHYSICS_OBJECT, WINDOW, DOOR}

func interpretCommand(rawCommand: String):

	var text = Array(rawCommand.split("(")) 
	text = text.map(func (t: String): return t.replace(")", "").replace("\"", "").strip_edges())

	var args = text[1].split(",")

	var command = text[0]

	print("command was ", command, " with args ", args)

	var isObjectInteractionCommand = false

	for verb in VERBS:
		if command.contains(verb):
			isObjectInteractionCommand = true
			break

	if isObjectInteractionCommand:
		var type: ObjectType

		if command.contains("Lights"):
			type = ObjectType.LIGHT
		elif command.contains("TVs"):
			type = ObjectType.
		var type = ObjectType.LIGHT if command.contains("light") else ObjectType.TV
		object = _resolveObject(
	

# target only really matters for nearby, and is the name of the entity

func _resolveObject(type: ObjectType, selector: String):
	var mode = selector.split(" ")[0].to_lower().strip_edges()
	var target = selector.split(" ")[1].to_lower().strip_edges()

	var lights = get_tree().get_nodes_in_group("lights")
	var tvs = get_tree().get_nodes_in_group("tvs")

	var object: Node3D

	if mode.contains("at"):

		if target == "player":
			target = Player
		elif target == "ghost":
			target = Ghost
		else:
			push_warning("Invalid target ", target, ". Defaulting to player")
			target = Player

		if type == ObjectType.LIGHT:
			for light in lights:
				if light.get_global_transform().origin.distance_to(target.get_global_transform().origin) < 5:
					object = light
					break
		elif type == ObjectType.TV:
			for tv in tvs:
				if tv.get_global_transform().origin.distance_to(target.get_global_transform().origin) < 5:
					object = tv
					break
		else:
			push_warning("Invalid object type ", type, ". Defaulting to light")
			for light in lights:
				if light.get_global_transform().origin.distance_to(target.get_global_transform().origin) < 5:
					object = light
					break
	
	elif mode == "random":
		if type == ObjectType.LIGHT:
			object = lights[randi() % lights.size()]
		elif type == ObjectType.TV:
			object = tvs[randi() % tvs.size()]
		else:
			push_warning("Invalid object type ", type, ". Defaulting to light")
			object = lights[randi() % lights.size()]
	
	elif mode == "all":
		if type == ObjectType.LIGHT:
			object = lights
		elif type == ObjectType.TV:
			object = tvs
		else:
			push_warning("Invalid object type ", type, ". Defaulting to light")
			object = lights

	return object

		

	





