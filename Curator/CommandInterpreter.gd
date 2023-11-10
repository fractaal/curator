extends Node

@export var Player: Node3D
@export var Ghost: Node3D

# This is more for recognizing commands
const VERBS = ['toggle', 'flicker', 'explode', 'ring', 'throw', 'knock', 'open', 'close', 'lock', 'restore']
const OBJECTS = ['Lights', 'TVs']

enum ObjectType {LIGHT, TV} # PHONE, CHAIR, PHYSICS_OBJECT, WINDOW, DOOR}

func _ready():
	Player = get_tree().get_first_node_in_group("player")
	Ghost = get_tree().get_first_node_in_group("ghost")
	print("Set player & ghost to ", Player, Ghost)

func interpretCommand(rawCommand: String):

	var text = Array(rawCommand.split("(")) 
	text = text.map(func (t: String): return t.replace(")", "").replace("\"", "").strip_edges())

	var args = text[1].split(",")

	var command = text[0]

	print("command was ", command, " with args ", args)

	var isObjectInteractionCommand = false
	
	var verb: String = ""
	
	for _verb in VERBS:
		if command.contains(_verb):
			isObjectInteractionCommand = true
			verb = _verb 
			break
	
	if isObjectInteractionCommand:

		var type: ObjectType

		if command.to_lower().contains("light"):
			type = ObjectType.LIGHT
		elif command.to_lower().contains("tv"):
			type = ObjectType.TV
		else:
			push_warning("Invalid object type ", command, ". Defaulting to light")
			type = ObjectType.LIGHT

		var object = _resolveObject(type, args[0]);

		if (object is Array):
			for o in object:
				print("calling ", verb, " on ", o)
				o.call(verb)
		else:
			print("calling ", verb, " on ", object)
			object.call(verb)
		
	if command == "moveToPlayer":
		get_tree().call_group("enemies", "update_target_location", Player.global_transform.origin)
		pass
	

# target only really matters for nearby, and is the name of the entity

func _resolveObject(type: ObjectType, selector: String):

	var splitted = selector.split(" ")

	var mode = splitted[0].to_lower().strip_edges()
	var target

	if (splitted.size() > 1):
		target = splitted[1].to_lower().strip_edges()

	var lights = get_tree().get_nodes_in_group("lights")
	var tvs = get_tree().get_nodes_in_group("tvs")

	var object

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
	
	else:
		if mode != "all":
			push_warning("Invalid mode ", mode, " defaulting to all")
		
		if type == ObjectType.LIGHT:
			object = lights
		elif type == ObjectType.TV:
			object = tvs
		else:
			push_warning("Invalid object type ", type, ". Defaulting to light")
			object = lights



	return object

		

	





