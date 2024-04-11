extends Node

var label: RichTextLabel

var previous_text = ""

func _ready():
	label = get_parent() as RichTextLabel
	
func _physics_process(_delta):
	if previous_text != label.text:
		label.text = label.text.c_escape()
		label.text = label.text.replace("\\r\\n", "\n")
		label.text = label.text.c_unescape()
		
		previous_text = label.text
