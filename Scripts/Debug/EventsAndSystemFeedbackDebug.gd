extends RichTextLabel



func _on_notable_event_occured(message: String):
	text += message + "\n"
	scroll_to_line(get_line_count())
	
func _on_notable_event_occured_specific_time(message: String, time: int):
	text += message + "\n"
	scroll_to_line(get_line_count())

func _on_system_feedback(message: String):
	text += "[b](SYSTEM FEEDBACK):[/b] " + message + "\n"
	scroll_to_line(get_line_count())

func _ready():
	connect_to_event_bus()
	
func connect_to_event_bus():
	await get_tree().create_timer(1).timeout
	
	EventBus.NotableEventOccurred.connect(_on_notable_event_occured)
	EventBus.NotableEventOccurredSpecificTime.connect(_on_notable_event_occured_specific_time)
	EventBus.SystemFeedback.connect(_on_system_feedback)
