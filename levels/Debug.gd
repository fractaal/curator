extends Node

@export var DebugUI: Control

var isVisible = false

func _physics_process(delta):
	if Input.is_action_just_pressed("ToggleDebugUI"):
		isVisible = !isVisible;
		
	DebugUI.visible = isVisible
	get_tree().current_scene.get_node("Ghost").get_node("Label3D").visible = isVisible
