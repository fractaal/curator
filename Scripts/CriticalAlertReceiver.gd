extends RichTextLabel

@export var sound_player: AudioStreamPlayer

func connect_to_event_bus():
	await get_tree().create_timer(3).timeout
	
	EventBus.CriticalMessage.connect(_on_critical_message)

var tween

func _on_critical_message(message: String):
	if tween:
		tween.stop()
	print("Received critical message ", message)
	sound_player.volume_db = -20
	sound_player.play(0)
	tween = create_tween()
	modulate = Color(1, 1, 1, 1) * 2
	tween.tween_property(self, "modulate", Color(1, 1, 1, 0.75), 1).set_delay(3)
	tween.tween_property(self, "modulate", Color(1, 1, 1, 0), 5).set_delay(6)

	tween.play()

	text = "[center]" + message + "[/center]"

func _ready():
	connect_to_event_bus()
