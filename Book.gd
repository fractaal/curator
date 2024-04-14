extends RigidBody3D

@onready var mesh: MeshInstance3D = $Cube

func _ready():
	var material := mesh.get_surface_override_material(0) as StandardMaterial3D
	material = material.duplicate()
	
	material.albedo_color = Color(randf(), randf(), randf())
	
	mesh.set_surface_override_material(0, material)
