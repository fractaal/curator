extends TextEdit

var LogManager = load("res://Scripts/LogManager.cs");

# Called when the node enters the scene tree for the first time.
func _ready():
	pass # Replace with function body.

func _input(event):
	if (event.is_action_pressed("ui_text_completion_accept")&&has_focus()):
		var tokenized := fakeTokenize(text)
		
		await get_tree().create_timer(0.1).timeout
		
		clear()
		release_focus()
		
		if tokenized.size() == 0:
			return
		
		await get_tree().create_timer(randf_range(0.4, 1.2)).timeout

		EventBus.emit_signal("LLMFirstResponseChunk", tokenized[0]);

		for chunk in tokenized:
			await get_tree().create_timer(randf_range(0.025, 0.1)).timeout
			EventBus.emit_signal("LLMResponseChunk", chunk);

		EventBus.emit_signal("LLMLastResponseChunk", tokenized[tokenized.size() - 1]);

func fakeTokenize(_text: String) -> Array[String]:

	var currentLength := 0
	var maxLength := _text.length()

	var tokenized: Array[String] = []

	while (currentLength < maxLength):
		var interval := randi_range(2, 4)
		var start := currentLength
		tokenized.append(_text.substr(start, interval))
		currentLength += interval
	
	return tokenized

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(_delta):
	if Input.is_action_just_pressed("FocusCommandLine") and get_viewport().gui_get_focus_owner() == null:
		grab_focus()
