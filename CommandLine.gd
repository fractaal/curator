extends LineEdit

# Called when the node enters the scene tree for the first time.
func _ready():
	pass # Replace with function body.

func _input(event):
	if (event.is_action_pressed("ui_text_completion_accept") && has_focus()):
		CommandInterpreter.interpretCommand(text)
		clear()
		release_focus()

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	if Input.is_action_just_pressed("FocusCommandLine"):
		grab_focus()

