extends RichTextLabel

var iterations = 0

var updateInterval = 0.25
var updateElapsed = 0

var backstoryAvailable = false

func _on_ghost_backstory(message):
	backstoryAvailable = true
	text = message

func _ready():
	EventBus.GhostBackstory.connect(_on_ghost_backstory)

func _process(delta):
	updateElapsed += delta
	
	if (updateElapsed > updateInterval) and not backstoryAvailable:
		updateElapsed = 0
		iterations += 1
		
		text = "Searching ghost database " + (".".repeat(iterations))
