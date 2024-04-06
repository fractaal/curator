extends Label3D

func _ready():
	EventBus.GhostBackstory.connect(_on_ghost_action)
		
func _on_ghost_action(message):
	text = message
