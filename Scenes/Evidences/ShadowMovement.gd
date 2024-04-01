extends CharacterBody3D

var speed = 2.5

@onready var nav_agent = $NavigationAgent3D
@onready var skeleton: Node3D = $Skeleton3D
@onready var CollisionShape = $CollisionShape3D
@export var Locator: Node

func _ready():
	while true:
		await get_tree().create_timer(randf_range(5, 10)).timeout

		update_target_location(Locator.RoomObject.GetRandomPosition())

var last_location: Vector3

func _physics_process(delta):
	var current_location = global_transform.origin
	var next_location = nav_agent.get_next_path_position()
	var new_velocity = (next_location - current_location).normalized() * speed

	if (current_location - next_location).length() < 0.1:
		return
	
	velocity = new_velocity
	
	var direction = (next_location - current_location).normalized()
	rotation.y = lerp_angle(rotation.y, atan2( - direction.x, -direction.z), delta * 5)
	
	move_and_slide()
	
	last_location = current_location

func _process(_delta):
	pass
	
func update_target_location(target_location):
	nav_agent.target_position = target_location
