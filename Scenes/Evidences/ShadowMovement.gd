extends CharacterBody3D

var speed = 5

@onready var nav_agent = $NavigationAgent3D
@onready var CollisionShape = $CollisionShape3D
@export var Locator: Node

@export var textures: Array[CompressedTexture2D]

var last_location: Vector3
var current_location: Vector3
var next_location: Vector3

var rooms;

var maxIters = 3
var iters = 0

func _ready():
	($CollisionShape3D/Sprite3D as Sprite3D).texture = textures[randi() % textures.size()]

	while true:
		iters += 1
		await get_tree().create_timer(randf_range(5, 10)).timeout

		var tween = create_tween()

		tween.tween_property($CollisionShape3D/Sprite3D, "modulate", Color(1, 1, 1, 1), 0.5)

		tween.play()

		if Locator.RoomObject:
			update_target_location(Locator.RoomObject.GetRandomPosition())

		await get_tree().create_timer(1).timeout

		tween = create_tween()

		tween.tween_property($CollisionShape3D/Sprite3D, "modulate", Color(1, 1, 1, 0), 0.5)
		tween.play()

		if iters >= maxIters:
			break

func _physics_process(delta):
	current_location = global_transform.origin
	next_location = nav_agent.get_next_path_position()
	var new_velocity = (next_location - current_location).normalized() * speed

	if (current_location - next_location).length() < 0.1:
		return
	
	velocity = new_velocity
	
	var direction = (next_location - current_location).normalized()
	rotation.y = lerp_angle(rotation.y, atan2( - direction.x, -direction.z), delta * 5)
	
	move_and_slide()
	
	last_location = current_location

func update_target_location(target_location):
	nav_agent.target_position = target_location
