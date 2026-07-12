extends Node

@export var switch: Node3D
@export var locator: Node
@export var sfx: AudioStreamPlayer3D

var isOn = true

func _ready():
	add_to_group("late_join_synced")

# The ObjectInteraction emission stays on the originating peer only — GameLight
# routes and broadcasts it itself. Only the handle visual/SFX broadcast here.
func turnOn():
	EventBus.emit_signal("ObjectInteraction", "turnOnInstant", "lights", "in " + locator.Room)
	RPCUtils.try_rpc_call(self, "turnOn")

func turnOn_impl(_args: Array = []):
	sfx.seek(0)
	sfx.play()

	switch.rotation_degrees.x = -15
	isOn = true

func turnOff():
	EventBus.emit_signal("ObjectInteraction", "turnOffInstant", "lights", "in " + locator.Room)
	RPCUtils.try_rpc_call(self, "turnOff")

func turnOff_impl(_args: Array = []):
	sfx.seek(0)
	sfx.play()

	switch.rotation_degrees.x = 15
	isOn = false

func toggle():
	if isOn:
		turnOff()
	else:
		turnOn()

func interact(_player = null):
	# isOn changes a network round-trip later; capture the intent for the message
	var turning_on = not isOn
	toggle()

	var who = "Player"
	if _player != null and "player_number" in _player:
		who = "Player %d" % _player.player_number
	EventBus.emit_signal("NotableEventOccurred", who + " turned " + ("on" if turning_on else "off") + " the lights in " + locator.Room)

# Called by MultiplayerManager on the server when a peer joins.
func late_join_sync(peer_id: int):
	_apply_join_state.rpc_id(peer_id, isOn)

@rpc("authority")
func _apply_join_state(on: bool):
	isOn = on
	switch.rotation_degrees.x = -15 if isOn else 15

func secondaryInteract(_player = null):
	pass

func getStatus():
	return ""

func getStatusForPlayer():
	return "Switch, " + ("On" if isOn else "Off")
