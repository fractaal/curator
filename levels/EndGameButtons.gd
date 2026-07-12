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
	# In multiplayer a bare scene reload desyncs the session: tear this machine's
	# session down first. Each machine that clicks Retry returns to a fresh scene
	# with the lobby UI, and a new game gets hosted/joined from there.
	if multiplayer.has_multiplayer_peer() and not (multiplayer.multiplayer_peer is OfflineMultiplayerPeer):
		multiplayer.multiplayer_peer.close()
		multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
		LanDiscovery.StopBeacon()

	get_tree().reload_current_scene()

func _ready():
	connect_to_event_bus()
	
	retry_button.pressed.connect(_on_retry_clicked)
	
