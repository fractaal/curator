extends RigidBody3D

func _ready():
	await get_tree().create_timer(120).timeout
	queue_free()
