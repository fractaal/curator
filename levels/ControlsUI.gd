extends RichTextLabel

var tween: Tween

func _physics_process(_delta):
	if Input.is_action_just_pressed("ShowHelp"):
		animate()
			
func _ready():
	text = text.c_escape()
	text = text.replace("\\r\\n", "\n")
	text = text.c_unescape()
	animate()

func animate():
	if tween:
		tween.stop()
	
	tween = create_tween()
	
	modulate = Color(1, 1, 1, 1)
	tween.tween_property(self, "modulate", Color(1, 1, 1, 0), 1).set_delay(5)
	
	tween.play()
