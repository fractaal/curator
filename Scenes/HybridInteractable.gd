extends Node

@export var Interactables: Array[Node]
@export var ObjectIDs: Array[String]
@export var locator: Node

func _on_object_interact(verb: String, type: String, target: String):
	var valid := false
	
	for _type in ObjectIDs:
		if type.to_lower().contains(_type):
			valid = true
			
	if not valid:
		return

	target = target.strip_edges()

	if target == "all":
		_call(verb)
		EventBus.emit_signal("ObjectInteractionAcknowledged", verb, type, target)
	else:
		var targetRoom = target
		if targetRoom.begins_with("in"):
			targetRoom = targetRoom.substr(2)
		if locator.IsInRoom(targetRoom):
			_call(verb)
			EventBus.emit_signal("ObjectInteractionAcknowledged", verb, type, target)
		else:
			return
			
func _call(verb):
	for i in Interactables:
		if i.has_method(verb):
			i[verb].call()
			
func getStatus():
	var result := ""
	for i in Interactables:
		if i.has_method("getStatus"):
			result += i.getStatus() + "\n"
	
	return result

func connect_to_event_bus():
	await get_tree().create_timer(3).timeout
	EventBus.ObjectInteraction.connect(_on_object_interact)

func _ready():
	connect_to_event_bus.call_deferred()
