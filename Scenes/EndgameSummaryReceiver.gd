extends RichTextLabel

func _on_endgame_summary(message: String):
	text = message

# Called when the node enters the scene tree for the first time.
func _ready():
	EventBus.EndgameSummary.connect(_on_endgame_summary)
