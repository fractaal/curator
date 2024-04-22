extends Label

@onready var precompiler = $"../ShaderPrecompiler"

func _ready():
	visible = true
	precompiler.allShadersCompiled.connect(_on_shaders_compiled)
	
func _on_shaders_compiled():
	var tween = create_tween()
	text = "Shaders compiled. Enjoy!"
	tween.tween_property(self, "modulate", Color(1,1,1,0), 1)
	tween.play()
