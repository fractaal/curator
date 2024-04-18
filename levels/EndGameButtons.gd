extends HBoxContainer

@export var retry_button: Button

func connect_to_event_bus():
	await get_tree().create_timer(3).timeout
	
	EventBus.GameLost.connect(_on_game_lost)

func _on_game_lost(_reason):
	var tween = create_tween()
	
	visible = true
	modulate = Color(1,1,1,0)
	
	tween.tween_property(self, "modulate", Color(1,1,1,1), 2).set_delay(5)
	tween.play()
	
func _on_retry_clicked():
	get_tree().reload_current_scene()

func _ready():
	connect_to_event_bus()
	
	retry_button.pressed.connect(_on_retry_clicked)
	
