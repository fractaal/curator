extends TextEdit

var LogManager = load("res://Scripts/LogManager.cs");

# Called when the node enters the scene tree for the first time.
func _ready():
	EventBus.VoiceRecognition.connect(_on_capture_stream_to_text_transcribed_msg)

func _input(event):
	if (event.is_action_pressed("FocusSaySomething") and get_viewport().gui_get_focus_owner() == null):
		grab_focus()
	elif (event.is_action_pressed("ui_text_completion_accept")&&has_focus()):

		if (text.strip_edges() == ""):
			clear()
			release_focus()
			return

		EventBus.emit_signal("NotableEventOccurred", "Player said: \"" + text.strip_edges() + "\"");
		
		clear()
		release_focus()

var completed_text := ""
var partial_text := ""

func update_text():
	# This method might no longer be necessary depending on your use case
	placeholder_text = completed_text + partial_text

func _process(_delta):
	# Depending on how your game's logic works, you might not need to call update_text here anymore
	update_text()

func _on_capture_stream_to_text_transcribed_msg(is_partial, new_text):
	if is_partial:
		# Accumulate partial texts
		partial_text += new_text
	else:
		# Here, check if new_text is a complete sentence and not just ellipses
		if _is_complete_sentence(new_text):
			EventBus.emit_signal("NotableEventOccurred", "Player said: \"" + new_text.strip_edges() + "\"")
			completed_text = "" # Reset the completed_text since it's already handled
			partial_text = "" # Clear partial text as well
		else:
			# If it's not a complete sentence, just update the partial_text
			partial_text = new_text

func _is_complete_sentence(text):
	# Check if the text ends with '.', '!', or '?' and is longer than 6 characters, and not just ellipses
	return text.length() > 6 and (text.ends_with(".") or text.ends_with("!") or text.ends_with("?")) and not text.strip_edges().begins_with(".")
