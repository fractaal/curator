extends StaticBody3D

var CollisionShapes: Array[Node] 
@export var Objects: Array[PackedScene]

func _ready():
	CollisionShapes = find_children("*", "CollisionShape3D")
	
	for shape in CollisionShapes:
		if not shape is CollisionShape3D:
			print(shape.name + " is not a CollisionShape3D")
			return
			
		
		for i in range(10):
			var scene := Objects[randi() % Objects.size()]
			var object = scene.instantiate()
			
			var position = Vector3(
				shape.global_position.x + randf_range(-(shape.shape.size.x/2), shape.shape.size.x/2),
				shape.global_position.y + 0.1,
				shape.global_position.z + randf_range(-(shape.shape.size.z/2), shape.shape.size.z/2)
			)
						
			add_child(object)
			
			object.global_position = position

	
