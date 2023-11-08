extends CharacterBody3D

@onready var gunRay = $Head/Camera3d/RayCast3d as RayCast3D
@onready var Cam = $Head/Camera3d as Camera3D
@export var _bullet_scene : PackedScene
var mouseSensibility = 1200
var mouse_relative_x = 0
var mouse_relative_y = 0
var SPEED = 4.0
const JUMP_VELOCITY = 4.5

var target_velocity = Vector3()
var spring_k = 10 # Spring constant
var spring_b = 2.0 # Spring damping coefficient

var target_rotation = Vector3()

# Get the gravity from the project settings to be synced with RigidBody nodes.
var gravity = ProjectSettings.get_setting("physics/3d/default_gravity")

var noise_x_high = FastNoiseLite.new()
var noise_y_high = FastNoiseLite.new()

var flashlightIntensityNoise = FastNoiseLite.new()

var isRunning = false

var isFlashlightOn: bool = false

const zeroVector2 = Vector2()

var hasFocusOnGui = false

func _ready():
	#Captures mouse and stops rgun from hitting yourself
	gunRay.add_exception(self)
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED	

func _physics_process(delta):
	hasFocusOnGui = true if get_viewport().gui_get_focus_owner() != null else false

	var displacement = velocity - target_velocity
	var force = -spring_k * displacement - spring_b * velocity
	
	velocity.x += force.x * delta
	velocity.z += force.z * delta

	# Handle Jump.
	if Input.is_action_pressed("Jump") and is_on_floor() and not hasFocusOnGui:
		velocity.y = JUMP_VELOCITY
	# Handle Shooting
	if Input.is_action_pressed("Shoot") and not hasFocusOnGui:
		shoot()
		
	if Input.is_action_pressed("Sprint") and not hasFocusOnGui:
		SPEED = 7.5
		isRunning = true
	else:
		SPEED = 4
		isRunning = false
		
	if Input.is_action_just_pressed("ToggleFlashlight") and not hasFocusOnGui:
		isFlashlightOn = !isFlashlightOn
		
		$SpotLight3D.spot_range = 100 if isFlashlightOn else 0

	
	# Add the gravity.
	if not is_on_floor():
		velocity.y -= gravity * delta
	
	# Get the input direction and handle the movement/deceleration.
	var input_dir = Input.get_vector("moveLeft", "moveRight", "moveUp", "moveDown") if not hasFocusOnGui else zeroVector2
	var direction = (transform.basis * Vector3(input_dir.x, 0, input_dir.y)).normalized()
	
	if direction:
		target_velocity.x = direction.x * SPEED
		target_velocity.z = direction.z * SPEED
	else:
		target_velocity.x = move_toward(velocity.x, 0, 0.3)
		target_velocity.z = move_toward(velocity.z, 0, 0.3)

	
	$Head/Camera3d.position.y = 0.845 + sin(Time.get_ticks_msec() * (0.015 if isRunning else 0.01)) * velocity.length() * 0.01
	
	
	var targetHeadTiltUp = (-input_dir.y if is_on_floor() else 1) * velocity.length() * 0.015
	var targetHeadTiltSide = -input_dir.x * velocity.length() * 0.025
	var maxTilt = PI/6  # Maximum tilt angle in radians (30 degrees here)

	# Clamp the target tilt to the maximum allowed tilt
	targetHeadTiltUp = clamp(targetHeadTiltUp, -maxTilt, maxTilt)
	targetHeadTiltSide = clamp(targetHeadTiltSide, -maxTilt, maxTilt)

	var angular_velocity_up = 0.0
	var headTiltUpDisplacement = rotation.x - targetHeadTiltUp
	var headTiltUpForce = -400 * headTiltUpDisplacement - 1 * angular_velocity_up
	
	var angular_velocity_side = 0.0
	var headTiltSideDisplacement = rotation.z - targetHeadTiltSide
	var headTiltSideForce = -400 * headTiltSideDisplacement - 1 * angular_velocity_side

	angular_velocity_up += headTiltUpForce * delta
	angular_velocity_side += headTiltSideForce * delta
	
	rotation.z += angular_velocity_side * delta
	rotation.x += angular_velocity_up * delta
	
	move_and_slide()
	
	var unclampedIntensityNoise = 7 + flashlightIntensityNoise.get_noise_1d(Time.get_ticks_msec() * 0.15) * 10
	var clampedIntensityNoise = clamp(unclampedIntensityNoise, 0, 10)
	
	$SpotLight3D.light_energy = clampedIntensityNoise

func _input(event):
	if event is InputEventMouseMotion:
		rotation.y -= event.relative.x / mouseSensibility
		$Head/Camera3d.rotation.x -= event.relative.y / mouseSensibility
		$Head/Camera3d.rotation.x = clamp($Head/Camera3d.rotation.x, deg_to_rad(-90), deg_to_rad(90) )
		mouse_relative_x = clamp(event.relative.x, -50, 50)
		mouse_relative_y = clamp(event.relative.y, -50, 10)

func shoot():
	if not gunRay.is_colliding():
		return
	var bulletInst = _bullet_scene.instantiate() as Node3D
	bulletInst.set_as_top_level(true)
	get_parent().add_child(bulletInst)
	bulletInst.global_transform.origin = gunRay.get_collision_point() as Vector3
	bulletInst.look_at((gunRay.get_collision_point()+gunRay.get_collision_normal()),Vector3.BACK)
	print(gunRay.get_collision_point())
	print(gunRay.get_collision_point()+gunRay.get_collision_normal())
