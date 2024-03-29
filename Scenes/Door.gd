extends Node

var locked = false
var isOpen = false
var isAnimating = false

@export var door: Node3D

@export var openSFX: AudioStreamPlayer3D
@export var closeSFX: AudioStreamPlayer3D
@export var closeByGhostSFX: AudioStreamPlayer3D
@export var lockSFX: AudioStreamPlayer3D
@export var unlockSFX: AudioStreamPlayer3D
@export var rattleSFX: AudioStreamPlayer3D

@export var locator: Node

func _on_object_interact(verb: String, type: String, target: String):

	if not type.to_lower().contains("door"):
		return

	target = target.strip_edges()

	if target.begins_with("in"):
		var targetRoom = target.substr(3).to_lower().strip_edges()
		if locator.IsInRoom(targetRoom):
			EventBus.emit_signal("ObjectInteractionAcknowledged", verb, type, target)
			self[verb].call()
		else:
			return
	elif target == "all":
		EventBus.emit_signal("ObjectInteractionAcknowledged", verb, type, target)
		self[verb].call()
	else:
		push_warning("Invalid target declaration ", target)

# Called when the node enters the scene tree for the first time.
func _ready():
	EventBus.ObjectInteraction.connect(_on_object_interact)

func open():

	if locked:
		rattleSFX.seek(0)
		rattleSFX.play()
		return

	if isOpen: return
	if isAnimating: return

	openSFX.seek(0)
	openSFX.play()

	isAnimating = true
	isOpen = true
	
	var tween = create_tween()
	await tween.tween_method(_openStep, 0.0, 1.0, 1.0).set_ease(Tween.EASE_OUT_IN).finished

	isAnimating = false

func playerClose():
	closeSFX.seek(0)
	closeSFX.play()
	_close()

func close():
	closeByGhostSFX.seek(0)
	closeByGhostSFX.play()
	_close()

func _close():
	if locked: return
	if not isOpen: return
	if isAnimating: return
	
	isOpen = false
	isAnimating = true

	var tween = create_tween()
	await tween.tween_method(_closeStep, 0.0, 1.0, 1.0).set_ease(Tween.EASE_OUT_IN).finished

	isAnimating = false

func toggle():
	if isOpen:
		playerClose()
	else:
		open()

func lock():
	lockSFX.seek(0)
	lockSFX.play()

	if isOpen:
		close()

	locked = true

func unlock():
	unlockSFX.seek(0)
	unlockSFX.play()
	locked = false

func _openStep(progress: float):
	var y = lerp(0, 135, progress)
	door.rotation.y = deg_to_rad(y)
	# print(y)
	# rotation.y = y

func _closeStep(progress: float):
	var y = lerp(135, 0, progress)
	door.rotation.y = deg_to_rad(y)
	
func interact():
	if locked:
		EventBus.emit_signal("NotableEventOccurred", "Player tried to open door in " + locator.Room + " - but it was locked")
	else:
		if isOpen:
			EventBus.emit_signal("NotableEventOccurred", "Player closed door in " + locator.Room)
		else:
			EventBus.emit_signal("NotableEventOccurred", "Player opened door in " + locator.Room)

	toggle()
	
func secondaryInteract():
	pass

func getStatus():
	if isOpen:
		return "Door - Open"
	else:
		return "Door - Closed, " + ("Locked" if locked else "Unlocked")
