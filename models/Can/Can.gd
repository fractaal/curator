extends RigidBody3D

@export var textures: Array[Texture2D] 
@export var can: MeshInstance3D

func _ready():
	var material = StandardMaterial3D.new()
	
	material.albedo_texture = textures[randi() % textures.size()]
	material.uv1_triplanar = true
	
	can.set_surface_override_material(1, material)
