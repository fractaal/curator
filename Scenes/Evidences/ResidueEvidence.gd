extends RigidBody3D

@onready var decal = $Decal

func _ready():
	var tween = create_tween()
	tween.tween_property(decal, "modulate", Color(1, 1, 1, 0), 30).set_delay(30)
	
	await tween.finished
	
	queue_free()
