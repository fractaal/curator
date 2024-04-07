extends Node

@export var switch: Node3D
@export var locator: Node
@export var sfx: AudioStreamPlayer3D

var isOn = true

func turnOn():
	sfx.seek(0)
	sfx.play()
	
	switch.rotation_degrees.x = -15
	
	isOn = true
	EventBus.emit_signal("ObjectInteraction", "turnOnInstant", "lights", "in " + locator.Room)
	EventBus.emit_signal("NotableEventOccurred", "Player turned on the lights in " + locator.Room)
	
func turnOff():
	sfx.seek(0)
	sfx.play()
	
	switch.rotation_degrees.x = 15
	
	isOn = false
	EventBus.emit_signal("ObjectInteraction", "turnOffInstant", "lights", "in " + locator.Room)
	EventBus.emit_signal("NotableEventOccurred", "Player turned off the lights in " + locator.Room)
	
func toggle():
	if isOn:
		turnOff()
	else:
		turnOn()
		
func interact():
	toggle()

func secondaryInteract():
	pass

func getStatus():
	return ""

func getStatusForPlayer():
	return "Switch, " + ("On" if isOn else "Off")
