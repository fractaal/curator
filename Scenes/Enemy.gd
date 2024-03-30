extends CharacterBody3D

var SPEED = 3.252

@onready var nav_agent = $NavigationAgent3D
@onready var _animator = $AnimationPlayer

var last_location = Vector3()

func _ready():
	EventBus.GhostAction.connect(_on_ghost_action)
	
func _on_ghost_action(verb, arguments):
	verb = verb.to_lower()
	
	if verb == "moveasghost" or verb == "movetoasghost":
		
		var _position = TargetResolution.GetTargetPosition(arguments);

		if _position == Vector3.ZERO:
			return

		update_target_location(_position)

func _physics_process(_delta):
	
	var current_location = global_transform.origin
	var next_location = nav_agent.get_next_path_position()
	var new_velocity = (next_location - current_location).normalized() * SPEED

	if (current_location - next_location).length() < 0.1:
		return
	
	velocity = new_velocity
	
	look_at(next_location)
	
	_animator.play("mixamocom", -1, (last_location - current_location).normalized().length())
	move_and_slide()
	
	last_location = current_location
	
func update_target_location(target_location):
	nav_agent.target_position = target_location
