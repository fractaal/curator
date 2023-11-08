extends CharacterBody3D

var SPEED = 3.252

@onready var nav_agent = $NavigationAgent3D
@onready var _animator = $AnimationPlayer

var last_location = Vector3()

func _physics_process(delta):
	
	var current_location = global_transform.origin
	var next_location = nav_agent.get_next_path_position()
	var new_velocity = (next_location - current_location).normalized() * SPEED
	
	velocity = new_velocity
	
	look_at(next_location)
	
	_animator.play("mixamocom", -1, (last_location - current_location).normalized().length())
	move_and_slide()
	
	last_location = current_location
	
func update_target_location(target_location):
	nav_agent.target_position = target_location
