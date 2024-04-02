extends TextEdit

var LogManager = load("res://Scripts/LogManager.cs");

# Called when the node enters the scene tree for the first time.
func _ready():
	pass # Replace with function body.

func _input(event):
	if (event.is_action_pressed("FocusSaySomething") and get_viewport().gui_get_focus_owner() == null):
		grab_focus()
	elif (event.is_action_pressed("ui_text_completion_accept")&&has_focus()):

		EventBus.emit_signal("NotableEventOccurred", "Player said: \"" + text + "\"");
		
		clear()
		release_focus()

func fakeTokenize(_text):

	var currentLength = 0
	var maxLength = _text.length()

	var tokenized = []

	while (currentLength < maxLength):
		var interval = randi_range(2, 4)
		var start = currentLength
		tokenized.append(_text.substr(start, interval))
		currentLength += interval
	
	return tokenized