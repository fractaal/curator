extends CharacterBody3D

var speed = 2.5

@onready var nav_agent = $NavigationAgent3D
@onready var skeleton: Node3D = $Skeleton3D
@onready var CollisionShape = $CollisionShape3D
@onready var LineOfSightCheck = $RayCast3D
@export var Locator: Node

@export var huntStartSFX: AudioStreamPlayer
@export var heartbeatSFX: AudioStreamPlayer
@export var huntTensionSFX: AudioStreamPlayer
@export var jumpscareSFX: AudioStreamPlayer
@export var appearSFX: AudioStreamPlayer3D
@export var disappearSFX: AudioStreamPlayer3D
@export var huntGracePeriodSFX: AudioStreamPlayer

@onready var ghost_sounds = $GhostSounds

@export var blackTexture: ColorRect
@export var evidenceDepositor: Node
@export var endRevealText: Label

var GhostData := preload ("res://Scripts/GhostData.gd")

var current_target: Node3D = null

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

	# Only server should generate ghost properties to ensure consistency
	if multiplayer.is_server():
		# Set up name and type
		FirstName = FirstNames[randi() % FirstNames.size()]
		LastName = LastNames[randi() % LastNames.size()]
		GhostType = GhostTypes[randi() % GhostTypes.size()]
		GhostAge = randi_range(10, 1000)
		FavoriteRoom = "None Yet..."

		var rooms = get_tree().get_nodes_in_group("rooms")
		FavoriteRoom = rooms[randi() % rooms.size()].name

		# RPC the ghost properties to all clients
		_rpc_set_ghost_properties.rpc(FirstName, LastName, GhostType, GhostAge, FavoriteRoom)

	print("Ghost Type is ", GhostType)

	# Identity was broadcast in _ready, before any client existed — late joiners
	# get it via the late-join sync walk in MultiplayerManager.
	add_to_group("late_join_synced")

	EventBus.GhostAction.connect(_on_ghost_action)
	lastLocationForRoomCheck = global_transform.origin

	EventBus.GameWon.connect(_on_game_won)
	EventBus.GameLost.connect(_on_game_lost)

	# Wait for ghost properties to be set before starting movement
	if multiplayer.is_server():
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
		var _position = TargetResolution.GetTargetPosition(arguments)

		if _position == Vector3.ZERO:
			return

		update_target_location(_position)

		await nav_agent.navigation_finished

		moveFlag = false

	elif verb == "chaseplayerasghost" or verb == "chasetargetasghost":
		# For backwards compatibility, "chaseplayerasghost" still works
		# but now we also support "chasetargetasghost" for any target
		if multiplayer.is_server():
			_rpc_chase.rpc(arguments)
		else:
			print("Non-server tried to make ghost chase - ignoring")

	elif verb == "settargetasghost":
		# New action to set a specific target
		var target_node = TargetResolution.GetTarget(arguments)
		if target_node and target_node is Node3D:
			set_target(target_node)
		else:
			print("Ghost cannot set target - invalid target: ", arguments)

	elif verb == "appearasghost":
		if multiplayer.is_server():
			_rpc_appear.rpc()
		else:
			print("Non-server tried to make ghost appear - ignoring")

	elif verb == "depositevidenceasghost":
		if evidenceDepositor:
			evidenceDepositor.DepositEvidence(GhostType)

# RPC method for setting ghost properties - called by server, executed on all clients
@rpc("authority", "call_local")
func _rpc_set_ghost_properties(first_name: String, last_name: String, ghost_type: String, ghost_age: int, favorite_room: String):
	FirstName = first_name
	LastName = last_name
	GhostType = ghost_type
	GhostAge = ghost_age
	FavoriteRoom = favorite_room

	# Emit the ghost information signals on all clients
	EventBus.emit_signal("GhostInformation", "Name - " + FirstName + " " + LastName)
	EventBus.emit_signal("GhostInformation", "Type - " + GhostType)
	EventBus.emit_signal("GhostInformation", "Age - " + str(GhostAge))
	EventBus.emit_signal("GhostInformation", "Favorite Room - " + FavoriteRoom)

# RPC method for ghost appearance - called by server, executed on all clients
@rpc("authority", "call_local")
func _rpc_appear():
	appear()

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

# RPC method for ghost chase - called by server, executed on all clients
@rpc("authority", "call_local")
func _rpc_chase(arguments):
	chase(arguments)

func chase(arguments):
	if chasing or gameEnded:
		return

	if (Time.get_ticks_msec() - _last_chase_time) < 5000:
		print("New chase command was suspiciously too near a newly-ended chase. Ignoring")
		return

	# Acquire the victim: the nearest living player (what the chase tool promises).
	# Also re-acquires when the previous target died or despawned.
	if not current_target or not is_instance_valid(current_target) or ("dead" in current_target and current_target.dead):
		set_target(get_closest_player())

	# If still no target, can't chase
	if not current_target:
		print("Ghost cannot chase - no target available")
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

	var huntTime = randf_range(30, 45) if arguments != "end" else 9999.0
	EventBus.emit_signal("NotableEventOccurred", "Ghost chase started for " + str(huntTime) + " seconds. REMEMBER - TERRIFY THE PLAYER!")

	for i in range(0, int(huntTime * 10)):
		# Check if target still exists
		if not is_instance_valid(current_target):
			print("Chase target became invalid, ending chase")
			break

		skeleton.visible = true
		update_target_location(current_target.global_transform.origin)
		var length = (current_target.global_transform.origin - global_transform.origin).length()

		if (length < 1.25):
			jumpscareSFX.play(0)

			# Only kill if target is a player with a kill method
			if current_target.has_method("kill"):
				# Death sequence must run on the victim's own machine
				current_target.kill_remote.rpc_id(current_target.get_multiplayer_authority())
				var victim = "Player"
				if "player_number" in current_target:
					victim = "Player %d" % current_target.player_number
				EventBus.emit_signal("GameLost", victim + " was caught by the ghost")
				EventBus.emit_signal("NotableEventOccurred", "Game Lost - " + victim + " was caught by the ghost!")
			else:
				print("Ghost reached target: ", current_target.name)
			break

		await get_tree().create_timer(0.1).timeout

	chasing = false
	speed = 2.5

	# Check if target is a player and handle death state
	var target_is_dead = false
	if current_target and current_target.has_method("kill") and "dead" in current_target:
		target_is_dead = current_target.dead

	if not target_is_dead:
		skeleton.visible = false

	if target_is_dead:
		blackTexture.visible = true
		var tween = create_tween()

		endRevealText.text = "THE GHOST WAS A " + GhostType.to_upper()

		tween.tween_property(blackTexture, "modulate", Color(0, 0, 0, 1), 0.25).set_trans(Tween.TRANS_EXPO).set_delay(1.75)
		tween.tween_property(endRevealText, "modulate", Color(1, 1, 1, 1), 1).set_delay(3)

		tween.play()

		await tween.finished

	chasing_EntireSequence = false
	_last_chase_time = Time.get_ticks_msec()

	EventBus.emit_signal("ChaseEnded")
	EventBus.emit_signal("ObjectInteraction", "unlock", "doors", "all")

func _physics_process(delta):
	if (!multiplayer.is_server()):
		return

	# If still no target, skip line of sight checks but continue with movement
	if current_target:
		LineOfSightCheck.look_at(current_target.global_position + Vector3(0, .75, 0))
		LineOfSightCheck.rotate_object_local(Vector3(0, 1, 0), PI)

		if LineOfSightCheck.is_colliding():
			var object: Node3D = LineOfSightCheck.get_collider()

			var parent = object

			inLineOfSight = false

			while (parent != null):
				if parent != current_target:
					parent = parent.get_parent()
				else:
					inLineOfSight = true
					break
	else:
		inLineOfSight = false

	var current_location = global_transform.origin
	var next_location = nav_agent.get_next_path_position()
	var new_velocity = (next_location - current_location).normalized() * speed

	velocity = new_velocity

	if (current_location - next_location).length() > 0.1 and not chasing and not manifesting:
		var direction = (next_location - current_location).normalized()
		rotation.y = lerp_angle(rotation.y, atan2( - direction.x, -direction.z), delta * 5)

	if manifesting or chasing:
		$Skeleton3D/OmniLight3D.light_energy = randf_range(0.01, 0.05)
		if current_target:
			var direction = (current_target.global_transform.origin - global_transform.origin).normalized()
			rotation.y = lerp_angle(rotation.y, atan2( - direction.x, -direction.z), delta * 5)

	move_and_slide()

	last_location = current_location

	if chasing and current_target:
		$Skeleton3D/OmniLight3D.light_energy = randf_range(0.5, 1.5)

		var distance = (current_target.global_transform.origin - global_transform.origin).length()

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
	if (!multiplayer.is_server()):
		return

	# Check if current target is a player and is dead
	var target_is_dead = false
	if current_target and current_target.has_method("kill") and "dead" in current_target:
		target_is_dead = current_target.dead

	if not target_is_dead: # Otherwise it looks like we're humping the target
		skeleton.position.y = remap((sin(float(Time.get_ticks_msec()) / 600)), -1, 1, 0.05, 0.25)
	else:
		skeleton.position.y = 0.1

	if (global_transform.origin - lastLocationForRoomCheck).length() > 1:
		lastLocationForRoomCheck = global_transform.origin
		Locator.FindRoom()
	
func late_join_sync(peer_id: int):
	_rpc_set_ghost_properties.rpc_id(peer_id, FirstName, LastName, GhostType, GhostAge, FavoriteRoom)

func update_target_location(target_location):
	nav_agent.target_position = target_location

# Target management functions for multiplayer support
func set_target(new_target: Node3D):
	"""Set the ghost's current target to chase/stalk"""
	current_target = new_target
	if current_target:
		print("Ghost target set to: ", current_target.name)
	else:
		print("Ghost target cleared")

func get_all_players() -> Array[Node3D]:
	"""Get all players in the game (both singleplayer and multiplayer)"""
	var players: Array[Node3D] = []

	# Try to find multiplayer players first
	var player_spawn_location = get_tree().current_scene.get_node_or_null("PlayerSpawnLocation")
	if player_spawn_location:
		for child in player_spawn_location.get_children():
			if child.name.begins_with("Player_"):
				players.append(child)

	# Fall back to singleplayer player if no multiplayer players found
	if players.is_empty():
		var single_player = get_tree().current_scene.get_node_or_null("Player")
		if single_player:
			players.append(single_player)

	return players

func get_closest_player() -> Node3D:
	"""Get the closest living player to the ghost"""
	var players = get_all_players()

	var closest_player: Node3D = null
	var closest_distance = INF

	for player in players:
		if "dead" in player and player.dead:
			continue
		var distance = global_position.distance_to(player.global_position)
		if distance < closest_distance:
			closest_distance = distance
			closest_player = player

	return closest_player

func get_random_player() -> Node3D:
	"""Get a random player from all available players"""
	var players = get_all_players()
	if players.is_empty():
		return null

	return players[randi() % players.size()]

func getStatus():
	var out = "Name: " + FirstName + " " + LastName + "\n"
	out += "Type: " + GhostType + "\n"
	out += "Age: " + str(GhostAge) + "\n"
	out += "Favorite Room: " + FavoriteRoom + "\n"
	out += "Current Room: " + Locator.Room + "\n"
	out += "---\n"

	# Target information
	if current_target:
		out += "CURRENT TARGET: " + current_target.name + "\n"
		var distance = global_position.distance_to(current_target.global_position)
		out += "DISTANCE TO TARGET: " + str(distance).pad_decimals(1) + " units\n"
	else:
		out += "CURRENT TARGET: None\n"

	# Available players
	var players = get_all_players()
	out += "AVAILABLE PLAYERS: " + str(players.size()) + "\n"
	for player in players:
		var dist = global_position.distance_to(player.global_position)
		out += "  - " + player.name + " (distance: " + str(dist).pad_decimals(1) + ")\n"

	out += "---\n"
	out += "IN LINE OF SIGHT? (WOULD THE TARGET SEE THE GHOST IF IT MANIFESTS?): " + ("YES" if inLineOfSight else "NO") + "\n"
	out += "CHASING TARGET?: " + ("YES - GO CRAZY!" if chasing else "No") + "\n"
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
