extends Control

func _ready():
	visible = false

func _input(event):
	if event.is_action_pressed("ui_cancel"):
		if visible == false and get_viewport().gui_get_focus_owner() == null:
			Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
			visible = true
		else:
			Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
			visible = false
