extends TextEdit

var LogManager = load("res://Scripts/LogManager.cs");

# Called when the node enters the scene tree for the first time.
func _ready():
	EventBus.VoiceRecognition.connect(_on_capture_stream_to_text_transcribed_msg)
	
	while true:
		await get_tree().create_timer(10).timeout
		placeholder_text = "Press ENTER to say something..."

func _input(event):
	if (event.is_action_pressed("FocusSaySomething") and get_viewport().gui_get_focus_owner() == null):
		grab_focus()
	elif (event.is_action_pressed("ui_text_completion_accept")&&has_focus()):

		if (text.strip_edges() == ""):
			clear()
			release_focus()
			return

		# EventBus.emit_signal("NotableEventOccurred", "Player said: \"" + text.strip_edges() + "\"");
		EventBus.emit_signal("PlayerTalked", text.strip_edges());
		
		clear()
		release_focus()

var completed_text := ""
var partial_text := ""

func update_text(_text):
	# This method might no longer be necessary depending on your use case
	placeholder_text = _text

func _on_capture_stream_to_text_transcribed_msg(is_partial, new_text):
	if is_partial:
		# Accumulate partial texts
		partial_text += new_text
	else:
		# Here, check if new_text is a complete sentence and not just ellipses
		if _is_complete_sentence(new_text):
			# EventBus.emit_signal("NotableEventOccurred", "Player said: \"" + new_text.strip_edges() + "\"")
			EventBus.emit_signal("PlayerTalked", new_text.strip_edges());
			completed_text = "" # Reset the completed_text since it's already handled
			partial_text = "" # Clear partial text as well
			update_text("You said - " + new_text)
			# EventBus.emit_signal("ToastNotification", "You said \"" + new_text + "\"")
		else:
			# If it's not a complete sentence, just update the partial_text
			partial_text = new_text
			update_text("You're saying - " + completed_text + partial_text)

func _is_complete_sentence(t):
	# Check if the text ends with '.', '!', or '?' and is longer than 6 characters, and not just ellipses
	return t.length() > 6 and (t.ends_with(".") or t.ends_with("!") or t.ends_with("?")) and not t.strip_edges().begins_with(".")
