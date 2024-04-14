extends RigidBody3D

@onready var decal = $Decal

func _ready():
	var tween = create_tween()
	tween.tween_property(decal, "modulate", Color(1,1,1,0), 20).set_delay(10)
	
	await tween.finished
	
	queue_free()
