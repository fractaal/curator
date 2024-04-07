extends CharacterBody3D

var speed = 2.5

@onready var nav_agent = $NavigationAgent3D
#@onready var _animator = $AnimationPlayer
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

@export var endRevealText: Label

var player: Node3D

var last_location = Vector3()
var lastLocationForRoomCheck: Vector3

var chasing = false
var chaseSpeed = "slow"

var chasing_EntireSequence = false

var FirstNames = ["John", "Jennifer", "Madison", "Mark", "Abrahm", "Dominic", "Kimi", "Shan", "Mariane", "Sofia", "Elijah", "Venj", "Chun-chun", "Raj", "Elden", "Niggy", "Ben"]
var LastNames = ["Black", "Brown", "Jackson", "Peralta", "Walker", "Carpenter", "Mabait", "Baylin", "John", "Requinton", "Samonte", "Torrejos", "Abadilla", "Poggers", "Rocat", "Lumbay"]
var GhostTypes = ["Demon", "Wraith", "Phantom", "Shade", "Banshee", "Poltergeist"]

var FirstName
var LastName
var GhostType
var GhostAge
var FavoriteRoom

var manifesting = false

func _ready():
	# Set up name and type
	FirstName = FirstNames[randi() % FirstNames.size()]
	LastName = LastNames[randi() % LastNames.size()]
	GhostType = GhostTypes[randi() % GhostTypes.size()]
	GhostAge = randi_range(10, 1000)
	FavoriteRoom = "None Yet..."

	EventBus.GhostAction.connect(_on_ghost_action)
	lastLocationForRoomCheck = global_transform.origin

	player = get_tree().current_scene.get_node("Player");

	var rooms = get_tree().get_nodes_in_group("rooms")
	FavoriteRoom = rooms[randi() % rooms.size()].name
	# FavoriteRoom = "Pantry"

	EventBus.emit_signal("GhostInformation", "Name - " + FirstName + " " + LastName)
	EventBus.emit_signal("GhostInformation", "Type - " + GhostType)
	EventBus.emit_signal("GhostInformation", "Age - " + str(GhostAge))
	EventBus.emit_signal("GhostInformation", "Favorite Room - " + FavoriteRoom)

	_on_ghost_action("movetoasghost", FavoriteRoom)

	print("Ghost favorite room is " + FavoriteRoom + " starting to path there")

	while moveFlag:
		await get_tree().create_timer(0.1).timeout

	print("Ghost is now in " + FavoriteRoom)

	while true:
		await get_tree().create_timer(randf_range(5, 10)).timeout

		if not chasing_EntireSequence and not manifesting and not chasing and not moveFlag:
			if Locator.RoomObject:
				update_target_location(Locator.RoomObject.GetRandomPosition())
	
var moveFlag = false

func _on_ghost_action(verb, arguments):
	verb = verb.to_lower()
	
	if verb == "moveasghost" or verb == "movetoasghost":
		# if moveFlag:
		# 	print("Ghost was going to path to " + arguments + " but is already pathing to something else");
		# 	return

		print("Ghost now pathing to " + arguments)
			
		moveFlag = true
		if manifesting:
			return
		var _position = TargetResolution.GetTargetPosition(arguments);

		if _position == Vector3.ZERO:
			return

		update_target_location(_position)

	if verb == "chaseplayerasghost":
		chase(arguments)
		pass

	if verb == "appearasghost":
		appear()
	
	if verb == "depositevidenceasghost":
		if evidenceDepositor:
			evidenceDepositor.DepositEvidence(GhostType)

func appear():
	if chasing:
		return
	manifesting = true
	appearSFX.pitch_scale = randf_range(0.75, 0.85)
	appearSFX.play(0)
	skeleton.visible = true
	await get_tree().create_timer(randf_range(3, 7)).timeout
	disappearSFX.play(0)
	await get_tree().create_timer(0.5).timeout
	manifesting = false
	if chasing:
		return
	skeleton.visible = false

func chase(arguments):
	if chasing:
		return

	chasing_EntireSequence = true
	chasing = true
	skeleton.visible = true
	huntGracePeriodSFX.play(0)
	huntStartSFX.play(0)

	if arguments == "end":
		speed = 0
	elif arguments == "fast":
		speed = 5.25
	else:
		speed = 4

	await get_tree().create_timer(5).timeout

	if arguments == "end":
		speed = 35

	var huntTime = 25 if arguments != "end" else 9999

	for i in range(0, huntTime * 10):
		update_target_location(player.global_transform.origin)
		var length = (player.global_transform.origin - global_transform.origin).length()

		if (length < 1.25):
			jumpscareSFX.play(0)
			player.kill()
			EventBus.emit_signal("GameLost", "Player was caught by the ghost")
			break

		await get_tree().create_timer(0.1).timeout

	chasing = false
	speed = 2.5

	if not player.dead:
		skeleton.visible = false

	if player.dead:
		var tween = create_tween()

		endRevealText.text = "THE GHOST WAS A " + GhostType.to_upper();

		tween.tween_property(blackTexture, "modulate", Color(0, 0, 0, 1), 0.25).set_trans(Tween.TRANS_EXPO).set_delay(1.75)
		tween.tween_property(endRevealText, "modulate", Color(1, 1, 1, 1), 1).set_delay(3)

		tween.play()

		await tween.finished

	chasing_EntireSequence = false

var sameLocationCheckInterval = 0.5
var sameLocationCheckElapsed = 0
var sameLocationCheckLast = Vector3.ZERO

func _physics_process(delta):
	var current_location = global_transform.origin
	var next_location = nav_agent.get_next_path_position()
	var new_velocity = (next_location - current_location).normalized() * speed

	# if (current_location - next_location).length() > 0.1:
	# 	return
	
	velocity = new_velocity
	
	if (current_location - next_location).length() > 0.1 and not chasing and not manifesting:
		var direction = (next_location - current_location).normalized()
		rotation.y = lerp_angle(rotation.y, atan2( - direction.x, -direction.z), delta * 5)
	
	if manifesting or chasing:
		$Skeleton3D/OmniLight3D.light_energy = randf_range(0.01, 0.05)
		var direction = (player.global_transform.origin - global_transform.origin).normalized()
		rotation.y = lerp_angle(rotation.y, atan2( - direction.x, -direction.z), delta * 5)
	
	# _animator.play("mixamocom", -1, (last_location - current_location).normalized().length())
	move_and_slide()
	
	last_location = current_location

	sameLocationCheckElapsed += delta

	if sameLocationCheckElapsed > sameLocationCheckInterval:
		sameLocationCheckElapsed = 0

		var length = (current_location - sameLocationCheckLast).length()
		if (length) < 0.25 and moveFlag:
			moveFlag = false
			print("Ghost done pathing")

		sameLocationCheckLast = current_location

	if chasing:
		$Skeleton3D/OmniLight3D.light_energy = randf_range(0.5, 1.5)

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
	out += "Favorite Room: " + FavoriteRoom + "\n"
	out += "Current Room: " + Locator.Room + "\n"
	out += "---\n"
	out += "CHASING PLAYER?: " + ("YES - GO CRAZY!" if chasing else "No") + "\n"
	out += "VISIBLE?: " + ("Yes" if skeleton.visible else "No") + "\n"

	return out

func getStatusStateless():
	var out = "Name: " + FirstName + " " + LastName + "\n"
	out += "Type: " + GhostType + "\n"
	out += "Age: " + str(GhostAge) + "\n"
	out += "Favorite Room: " + FavoriteRoom + "\n"

	return out
