extends CharacterBody3D

var speed = 2.5

@onready var nav_agent = $NavigationAgent3D
@onready var _animator = $AnimationPlayer
@onready var skeleton: Node3D = $Skeleton3D
@onready var CollisionShape = $CollisionShape3D
@export var Locator: Node

@export var huntStartSFX: AudioStreamPlayer
@export var heartbeatSFX: AudioStreamPlayer
@export var huntTensionSFX: AudioStreamPlayer
@export var jumpscareSFX: AudioStreamPlayer
@export var appearSFX: AudioStreamPlayer3D;
@export var disappearSFX: AudioStreamPlayer3D;
@export var huntGracePeriodSFX: AudioStreamPlayer
	
@export var blackTexture: TextureRect

@export var evidenceDepositor: Node

var player: Node3D

var last_location = Vector3()
var lastLocationForRoomCheck: Vector3

var chasing = false
var chaseSpeed = "slow"

var FirstNames = ["John", "Jennifer", "Madison", "Mark", "Abrahm", "Dominic", "Kimi"]
var LastNames = ["Black", "Brown", "Jackson", "Peralta", "Walker", "Carpenter"]
var GhostTypes = ["Demon", "Wraith", "Phantom", "Shade", "Banshee", "Poltergeist"]

var FirstName
var LastName
var GhostType
var GhostAge

func _ready():
	# Set up name and type
	FirstName = FirstNames[randi() % FirstNames.size()]
	LastName = LastNames[randi() % LastNames.size()]
	GhostType = GhostTypes[randi() % GhostTypes.size()]
	GhostAge = randi_range(10, 1000)

	EventBus.GhostAction.connect(_on_ghost_action)
	lastLocationForRoomCheck = global_transform.origin

	player = get_tree().current_scene.get_node("Player");

	while true:
		await get_tree().create_timer(randf_range(5, 10)).timeout

		if not chasing:
			if Locator.RoomObject:
				update_target_location(Locator.RoomObject.GetRandomPosition())
	
func _on_ghost_action(verb, arguments):
	verb = verb.to_lower()
	
	if verb == "moveasghost" or verb == "movetoasghost":
		var _position = TargetResolution.GetTargetPosition(arguments);

		if _position == Vector3.ZERO:
			return

		update_target_location(_position)

	if verb == "chaseplayerasghost":
		chase(arguments)

	if verb == "ghostappear":
		appear()
	
	if verb == "ghostdepositevidence":
		if evidenceDepositor:
			evidenceDepositor.DepositEvidence(GhostType)

func appear():
	if chasing:
		return
	appearSFX.pitch_scale = randf_range(0.75, 0.85)
	appearSFX.play(0)
	skeleton.visible = true
	await get_tree().create_timer(randf_range(3, 7)).timeout
	disappearSFX.play(0)
	await get_tree().create_timer(0.5).timeout
	if chasing:
		return
	skeleton.visible = false

func chase(arguments):
	if chasing:
		return

	chasing = true
	skeleton.visible = true
	huntGracePeriodSFX.play(0)
	huntStartSFX.play(0)

	if arguments == "fast":
		speed = 4
	else:
		speed = 2.5

	await get_tree().create_timer(5).timeout

	for i in range(0, 200):
		update_target_location(player.global_transform.origin)
		var length = (player.global_transform.origin - global_transform.origin).length()

		if (length < 1.25):
			jumpscareSFX.play(0)
			player.kill()
			break

		await get_tree().create_timer(0.1).timeout

	chasing = false
	speed = 2.5

	if not player.dead:
		skeleton.visible = false

	if player.dead:
		var tween = create_tween()

		tween.tween_property(blackTexture, "modulate", Color(0, 0, 0, 1), 1).set_trans(Tween.TRANS_EXPO).set_delay(1)
		tween.play()

		await tween.finished

func _physics_process(delta):
	var current_location = global_transform.origin
	var next_location = nav_agent.get_next_path_position()
	var new_velocity = (next_location - current_location).normalized() * speed

	if (current_location - next_location).length() < 0.1:
		return
	
	velocity = new_velocity
	
	var direction = (next_location - current_location).normalized()
	rotation.y = lerp_angle(rotation.y, atan2( - direction.x, -direction.z), delta * 5)
	
	# _animator.play("mixamocom", -1, (last_location - current_location).normalized().length())
	move_and_slide()
	
	last_location = current_location

	if chasing:
		var distance = (player.global_transform.origin - global_transform.origin).length()

		heartbeatSFX.volume_db = (-(distance * 2)) + 5
		huntTensionSFX.volume_db = (-(distance * 2)) - 10

		heartbeatSFX.pitch_scale = 0.75 + clamp((3 / distance), 0, 1.25)
		huntTensionSFX.pitch_scale = 0.75 + clamp((1 / distance), 0, 1.25)

	else:
		heartbeatSFX.volume_db = -80
		huntTensionSFX.volume_db = -80

func _process(_delta):

	if not player.dead: # Otherwise it looks like we're humping the player
		skeleton.position.y = remap((sin(float(Time.get_ticks_msec()) / 600)), -1, 1, 0.05, 0.25)
	else:
		skeleton.position.y = 0.1

	if (global_transform.origin - lastLocationForRoomCheck).length() > 1:
		lastLocationForRoomCheck = global_transform.origin
		Locator.FindRoom()
	
func update_target_location(target_location):
	nav_agent.target_position = target_location

func getStatus():
	var out = "Name: " + FirstName + " " + LastName + "\n"
	out += "Type: " + GhostType + "\n"
	out += "Age: " + str(GhostAge) + "\n"
	out += "\n"
	out += "Current Room:" + Locator.Room + "\n"
	out += "CHASING PLAYER?: " + ("YES" if chasing else "No") + "\n"
	out += "VISIBLE?: " + ("Yes" if skeleton.visible else "No") + "\n"

	return out
