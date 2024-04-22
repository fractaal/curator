extends ColorRect

var frame_to_fade_in = 5
var current_frame = 0
var started_fading = false

func _ready():
	visible = true

func start_fade():
	var tween = create_tween()
	tween.tween_property(self, "modulate", Color(1,1,1,0), 1)
	tween.play()

func _process(_delta):
	if current_frame > frame_to_fade_in and not started_fading:
		started_fading = true
		start_fade()
		
	current_frame += 1
