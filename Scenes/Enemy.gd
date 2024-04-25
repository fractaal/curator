extends CharacterBody3D

var speed = 2.5

@onready var nav_agent = $NavigationAgent3D
#@onready var _animator = $AnimationPlayer
@onready var skeleton: Node3D = $Skeleton3D
@onready var CollisionShape = $CollisionShape3D
@onready var LineOfSightCheck = $RayCast3D
@export var Locator: Node

@export var huntStartSFX: AudioStreamPlayer
@export var heartbeatSFX: AudioStreamPlayer
@export var huntTensionSFX: AudioStreamPlayer
@export var jumpscareSFX: AudioStreamPlayer
@export var appearSFX: AudioStreamPlayer3D;
@export var disappearSFX: AudioStreamPlayer3D;
@export var huntGracePeriodSFX: AudioStreamPlayer

@onready var ghost_sounds = $GhostSounds
	
@export var blackTexture: ColorRect

@export var evidenceDepositor: Node

@export var endRevealText: Label

var GhostData := preload ("res://Scripts/GhostData.gd")

var player: Node3D

var last_location = Vector3()
var lastLocationForRoomCheck: Vector3

var chasing = false
var chaseSpeed = "slow"

var chasing_EntireSequence = false

var _last_chase_time = 0

var FirstNames := [
	"John",
	"Jennifer",
	"Madison",
	"Mark",
	"Abrahm",
	"Dominic",
	"Kimi",
	"Shan",
	"Mariane",
	"Sofia",
	"Elijah",
	"Venj",
	"Raj",
	"Ben"
]
var LastNames := [
	"Black",
	"Brown",
	"Jackson",
	"Peralta",
	"Walker",
	"Carpenter",
	"Baylin",
	"John",
	"Requinton",
	"Samonte",
	"Torrejos",
	"Abadilla",
	"Rocat",
	"Lumbay"
]

var GhostTypes: Array[String]

var FirstName
var LastName
var GhostType
var GhostAge
var FavoriteRoom

var manifesting = false

var gameEnded = false

var inLineOfSight = false

func _on_game_won(_reason):
	gameEnded = true
	set_process(false)

func _on_game_lost(_reason):
	gameEnded = true
	set_process(false)

func _ready():
	GhostTypes = GhostData.GetGhostTypes()

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

	EventBus.GameWon.connect(_on_game_won)
	EventBus.GameLost.connect(_on_game_lost)

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

		if manifesting:
			return

		print("Ghost now pathing to " + arguments)
			
		moveFlag = true
		var _position = TargetResolution.GetTargetPosition(arguments);

		if _position == Vector3.ZERO:
			return

		update_target_location(_position)

		await nav_agent.navigation_finished

		moveFlag = false

	if verb == "chaseplayerasghost":
		chase(arguments)
		pass

	if verb == "appearasghost":
		appear()
	
	if verb == "depositevidenceasghost":
		if evidenceDepositor:
			evidenceDepositor.DepositEvidence(GhostType)

func appear():
	if chasing or gameEnded:
		return
	manifesting = true
	appearSFX.pitch_scale = randf_range(0.75, 0.85)
	if inLineOfSight:
		appearSFX.play(0)
	skeleton.visible = true
	await get_tree().create_timer(randf_range(3, 7)).timeout
	if inLineOfSight:
		disappearSFX.play(0)
	await get_tree().create_timer(0.6).timeout
	manifesting = false
	if chasing:
		return
	skeleton.visible = false

func chase(arguments):
	if chasing or gameEnded:
		return
	
	if (Time.get_ticks_msec() - _last_chase_time) < 5000:
		print("New chase command was suspiciously too near a newly-ended chase. Ignoring")
		return

	EventBus.emit_signal("ChaseStarted")
	chasing_EntireSequence = true
	chasing = true
	skeleton.visible = true
	huntGracePeriodSFX.play(0)
	huntStartSFX.play(0)

	if arguments == "end":
		speed = 0
		chaseSpeed = "end"
	elif arguments == "fast":
		chaseSpeed = "fast"
	else:
		chaseSpeed = "slow"

	skeleton.visible = true

	await get_tree().create_timer(5).timeout

	if arguments == "end":
		speed = 35

	var huntTime = randf_range(30, 45) if arguments != "end" else 9999
	EventBus.emit_signal("NotableEventOccurred", "Ghost chase started for " + str(huntTime) + " seconds. REMEMBER - TERRIFY THE PLAYER!")

	for i in range(0, huntTime * 10):
		skeleton.visible = true
		update_target_location(player.global_transform.origin)
		var length = (player.global_transform.origin - global_transform.origin).length()

		if (length < 1.25):
			jumpscareSFX.play(0)
			player.kill()
			# if arguments != "end":
			EventBus.emit_signal("GameLost", "Player was caught by the ghost")
			EventBus.emit_signal("NotableEventOccurred", "Game Lost - Player was caught by the ghost!")
			break

		await get_tree().create_timer(0.1).timeout

	chasing = false
	speed = 2.5

	if not player.dead:
		skeleton.visible = false

	if player.dead:
		blackTexture.visible = true
		var tween = create_tween()

		endRevealText.text = "THE GHOST WAS A " + GhostType.to_upper();

		tween.tween_property(blackTexture, "modulate", Color(0, 0, 0, 1), 0.25).set_trans(Tween.TRANS_EXPO).set_delay(1.75)
		tween.tween_property(endRevealText, "modulate", Color(1, 1, 1, 1), 1).set_delay(3)

		tween.play()

		await tween.finished

	chasing_EntireSequence = false
	_last_chase_time = Time.get_ticks_msec()

	EventBus.emit_signal("ChaseEnded")
	EventBus.emit_signal("ObjectInteraction", "unlock", "doors", "all")

func _physics_process(delta):
	LineOfSightCheck.look_at(player.global_position + Vector3(0, .75, 0))
	LineOfSightCheck.rotate_object_local(Vector3(0, 1, 0), PI)

	if LineOfSightCheck.is_colliding():
		var object: Node3D = LineOfSightCheck.get_collider()
		
		var parent = object
		
		inLineOfSight = false
		
		while (parent != null):
			if parent != player:
				parent = parent.get_parent()
			else:
				inLineOfSight = true
				break
	
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
	
	# for i in get_slide_collision_count():
	# 	var c = get_slide_collision(i)
	# 	if c.get_collider() is RigidBody3D:
	# 		var obj := c.get_collider() as RigidBody3D
	# 		obj.apply_central_impulse( - c.get_normal() * 10)
	
	last_location = current_location

	if chasing:
		$Skeleton3D/OmniLight3D.light_energy = randf_range(0.5, 1.5)

		var distance = (player.global_transform.origin - global_transform.origin).length()

		huntTensionSFX.volume_db = (-(distance * 2)) - 10

		huntTensionSFX.pitch_scale = 0.75 + clamp((1 / distance), 0, 1.25)

		if chaseSpeed == "fast":
			speed = 2.5 + (log(distance) * 1.25)
		elif chaseSpeed == "slow":
			speed = 2.25 + log(distance)
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
	out += "IN LINE OF SIGHT? (WOULD THE PLAYER SEE THE GHOST IF IT MANIFESTS?): " + ("YES" if inLineOfSight else "NO") + "\n"
	out += "CHASING PLAYER?: " + ("YES - GO CRAZY!" if chasing else "No") + "\n"
	out += "VISIBLE?: " + ("Yes" if skeleton.visible else "No") + "\n"
	out += "---\n"

	return out

func getStatusStateless():
	var out = "Name: " + FirstName + " " + LastName + "\n"
	out += "Type: " + GhostType + "\n"
	out += "Age: " + str(GhostAge) + "\n"
	out += "Favorite Room: " + FavoriteRoom + "\n"
	out += "---\n"

	return out
