extends LineEdit

var LogManager = load("res://Scripts/LogManager.cs");

# Called when the node enters the scene tree for the first time.
func _ready():
	pass # Replace with function body.

func _input(event):
	if (event.is_action_pressed("ui_text_completion_accept")&&has_focus()):
		
		var tokenized = fakeTokenize(text)
		
		clear()
		release_focus()

		for chunk in tokenized:
			if (randf_range(0, 1) < 0.9):
				await get_tree().create_timer(randf_range(0.025, 0.25)).timeout
			else:
				await get_tree().create_timer(randf_range(0.5, 1)).timeout

			Curator.Interpret(chunk)

func fakeTokenize(text):

	var currentLength = 0
	var maxLength = text.length()

	var tokenized = []

	while (currentLength < maxLength):
		var interval = randi_range(2, 4)
		var start = currentLength
		tokenized.append(text.substr(start, interval))
		currentLength += interval
	
	return tokenized

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	if Input.is_action_just_pressed("FocusCommandLine"):
		grab_focus()
