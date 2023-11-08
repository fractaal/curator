extends Node

var elapse = Time.get_ticks_msec()

# Called when the node enters the scene tree for the first time.
func _ready():
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	if (Time.get_ticks_msec() - elapse > 2000):
		var newScale = randf_range(0, 1)
		print(ProjectSettings.get_setting("rendering/scaling_3d/scale"))
		ProjectSettings.set_setting("rendering/scaling_3d/scale", newScale)
		print("resolution changed to ", newScale)
		elapse = Time.get_ticks_msec()
