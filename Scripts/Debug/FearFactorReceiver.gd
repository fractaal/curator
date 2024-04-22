extends Control

@export var bar: Control
@export var label: RichTextLabel

func _get_color(fear_factor):
	if fear_factor > 90:
		return Color.RED
	elif fear_factor > 70:
		return Color.ORANGE_RED
	elif fear_factor > 50:
		return Color.YELLOW
	elif fear_factor > 30:
		return Color.GREEN_YELLOW
	else:
		return Color.WEB_GREEN
		
func _get_color_string(fear_factor):
	return "#" + _get_color(fear_factor).to_html()
	

func _on_fear_factor_changed(fear_factor):
	bar.scale = Vector2((float(fear_factor)/100.0), bar.scale.y)
	bar.modulate = _get_color(fear_factor)
	label.text = "[right][color="+ _get_color_string(fear_factor)+"] FEAR FACTOR (" + str(fear_factor) + ")[/color][/right]"
	

func _ready():
	await get_tree().create_timer(3).timeout
	EventBus.FearFactorChanged.connect(_on_fear_factor_changed)
	_on_fear_factor_changed(0)
