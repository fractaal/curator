extends Node


static func interpretCommand(rawCommand: String):
	var text = Array(rawCommand.split("(")) 
	text = text.map(func (t: String): return t.replace(")", "").replace("\"", "").strip_edges())

	var args = text[1].split(",")

	var command = text[0]

	print("command was ", command, " with args ", args)


enum ObjectType {LIGHT, TV} # PHONE, CHAIR, PHYSICS_OBJECT, WINDOW, DOOR}

# Currently valid selector states are: nearby {entity} | random | all.
# target only really matters for nearby, and is the name of the entity

static func _resolveTarget(type: ObjectType, selector: String):
	var mode = selector.split(" ")[0]
	var target = selector.split(" ")[1]
