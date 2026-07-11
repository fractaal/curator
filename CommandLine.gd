extends TextEdit

# Debug affordance: typed text is injected into the ghost's context as an out-of-game
# operator note (the old version fake-streamed it as LLM tokens for the Interpreter,
# which no longer exists — the AgenticCore loop consumes real messages instead).
func _input(event):
	if (event.is_action_pressed("ui_text_completion_accept")&&has_focus()):
		var message := text.strip_edges()

		clear()
		release_focus()

		if message == "":
			return

		EventBus.emit_signal("OperatorNote", message)
	elif event.is_action_pressed("ui_cancel") and has_focus():
		release_focus()

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(_delta):
	if Input.is_action_just_pressed("FocusCommandLine") and get_viewport().gui_get_focus_owner() == null:
		grab_focus()
