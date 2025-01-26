extends Node

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	%HostGameButton.pressed.connect(on_host_game_button_pressed)
	%JoinGameButton.pressed.connect(on_join_game_button_pressed)
	pass # Replace with function body.

func on_host_game_button_pressed():
	print("Host Game Button Pressed")
	MultiplayerManager.start_host()
	_hide_multiplayer_hud()

func on_join_game_button_pressed():
	print("Join Game Button Pressed")
	MultiplayerManager.join_server("127.0.0.1")
	_hide_multiplayer_hud()

func _hide_multiplayer_hud():
	%MultiplayerHUD.visible = false