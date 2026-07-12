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

# ==================================================================
# HUNTS — driven by the LLM-authored HuntTick (HuntCore). The engine owns
# the RULES (grace, timer, kill, speed clamps, lunge cooldown); the script
# owns the BEHAVIOR (where to go, when to lunge). There is NO autopilot:
# the old omniscient tracking loop is gone by design (no-fallback ruling).
# ==================================================================

const HUNT_GRACE_SEC := 5.0
const HUNT_MIN_SEC := 30.0
const HUNT_MAX_SEC := 45.0
const HUNT_SPEED_SLOW := 3.0
const HUNT_SPEED_FAST := 3.8
const LUNGE_SPEED := 6.5
const LUNGE_DURATION_SEC := 2.5
const LUNGE_COOLDOWN_SEC := 4.0
const KILL_RANGE := 1.25

var hunt_in_grace := false
var hunt_deadline_msec := 0
var last_hunt_end_reason := ""
var _lunge_target: Node3D = null
var _lunge_until_msec := 0
var _lunge_cooldown_until_msec := 0
var _hunt_id := 0 # invalidates stale async timers when a hunt ends early
var _endgame_execution := false

var hunt_remaining_sec: float:
	get:
		if not chasing or hunt_deadline_msec == 0:
			return 0.0
		return max(0.0, (hunt_deadline_msec - Time.get_ticks_msec()) / 1000.0)

var hunt_lunge_ready: bool:
	get:
		return chasing and not hunt_in_grace and Time.get_ticks_msec() >= _lunge_cooldown_until_msec

# RPC method for hunt start - called by server, executed on all clients
@rpc("authority", "call_local")
func _rpc_chase(arguments):
	start_hunt(arguments)

func start_hunt(arguments):
	if chasing or gameEnded:
		return

	if (Time.get_ticks_msec() - _last_chase_time) < 5000:
		print("New hunt command was suspiciously too near a newly-ended hunt. Ignoring")
		return

	# All peers: visuals + SFX. ChaseStarted also wakes HuntCore on the host.
	chasing = true
	chasing_EntireSequence = true
	hunt_in_grace = true
	last_hunt_end_reason = ""
	skeleton.visible = true
	chaseSpeed = "fast" if arguments == "fast" else "slow"
	huntGracePeriodSFX.play(0)
	huntStartSFX.play(0)
	EventBus.emit_signal("ChaseStarted")

	# Server only from here: the hunt's rules.
	if not multiplayer.is_server():
		return

	_hunt_id += 1
	var my_hunt := _hunt_id
	speed = HUNT_SPEED_FAST if chaseSpeed == "fast" else HUNT_SPEED_SLOW

	await get_tree().create_timer(HUNT_GRACE_SEC).timeout
	if my_hunt != _hunt_id or not chasing:
		return

	hunt_in_grace = false
	var hunt_time := randf_range(HUNT_MIN_SEC, HUNT_MAX_SEC)
	hunt_deadline_msec = Time.get_ticks_msec() + int(hunt_time * 1000)
	EventBus.emit_signal("NotableEventOccurred", "Ghost hunt started for " + str(int(hunt_time)) + " seconds - instincts are in control")

	while chasing and not gameEnded and Time.get_ticks_msec() < hunt_deadline_msec:
		await get_tree().create_timer(0.1).timeout
		if my_hunt != _hunt_id:
			return

	if chasing and my_hunt == _hunt_id:
		_rpc_finish_hunt.rpc("expired")

# HuntCore steering: navigate toward a sensed position. A committed lunge overrides it.
func hunt_move_toward(position: Vector3):
	if not multiplayer.is_server() or not chasing or hunt_in_grace or gameEnded:
		return
	if _lunge_target != null:
		return
	update_target_location(position)

# HuntCore commitment: a fast burst at a player the ghost can SEE. Engine-enforced
# duration and cooldown; tracking only persists while line of sight holds.
func hunt_lunge(target: Node3D) -> bool:
	if not multiplayer.is_server() or not chasing or hunt_in_grace or gameEnded:
		return false
	if Time.get_ticks_msec() < _lunge_cooldown_until_msec:
		return false
	if target == null or not is_instance_valid(target):
		return false

	_lunge_target = target
	_lunge_until_msec = Time.get_ticks_msec() + int(LUNGE_DURATION_SEC * 1000)
	_lunge_cooldown_until_msec = _lunge_until_msec + int(LUNGE_COOLDOWN_SEC * 1000)
	current_target = target # the LOS probe follows the victim during the burst
	update_target_location(target.global_transform.origin)
	return true

# HuntCore abort: script error or script choice. Total cleanup, every peer.
func abort_hunt(reason: String):
	if not multiplayer.is_server() or not chasing:
		return
	_rpc_finish_hunt.rpc(reason)

func _lunge_tick():
	if _lunge_target == null:
		return
	if Time.get_ticks_msec() >= _lunge_until_msec or not is_instance_valid(_lunge_target):
		_lunge_target = null
		speed = HUNT_SPEED_FAST if chaseSpeed == "fast" else HUNT_SPEED_SLOW
		return
	speed = LUNGE_SPEED
	# Track only while the victim is visible; when LOS breaks, the ghost keeps
	# running to the last place it saw them.
	if inLineOfSight:
		update_target_location(_lunge_target.global_transform.origin)

func _hunt_contact_check():
	for player in get_all_players():
		if "dead" in player and player.dead:
			continue
		if not player.has_method("kill"):
			continue
		if (player.global_transform.origin - global_transform.origin).length() >= KILL_RANGE:
			continue

		_rpc_play_jumpscare.rpc()
		# Death sequence must run on the victim's own machine
		player.kill_remote.rpc_id(player.get_multiplayer_authority())
		var victim = "Player"
		if "player_number" in player:
			victim = "Player %d" % player.player_number
		EventBus.emit_signal("GameLost", victim + " was caught by the ghost")
		EventBus.emit_signal("NotableEventOccurred", "Game Lost - " + victim + " was caught by the ghost!")
		_rpc_finish_hunt.rpc("caught " + victim)
		return

@rpc("authority", "call_local")
func _rpc_play_jumpscare():
	jumpscareSFX.play(0)

@rpc("authority", "call_local")
func _rpc_finish_hunt(reason: String):
	if not chasing:
		return

	_hunt_id += 1 # cancels the server's pending grace/timer coroutines
	chasing = false
	hunt_in_grace = false
	hunt_deadline_msec = 0
	last_hunt_end_reason = reason
	_lunge_target = null
	speed = 2.5

	var caught := reason.begins_with("caught")
	if not caught:
		skeleton.visible = false
	else:
		blackTexture.visible = true
		var tween = create_tween()
		endRevealText.text = "THE GHOST WAS A " + GhostType.to_upper()
		tween.tween_property(blackTexture, "modulate", Color(0, 0, 0, 1), 0.25).set_trans(Tween.TRANS_EXPO).set_delay(1.75)
		tween.tween_property(endRevealText, "modulate", Color(1, 1, 1, 1), 1).set_delay(3)
		tween.play()

	chasing_EntireSequence = false
	_last_chase_time = Time.get_ticks_msec()

	EventBus.emit_signal("ChaseEnded")
	EventBus.emit_signal("ObjectInteraction", "unlock", "doors", "all")

# ==================================================================
# ENDGAME EXECUTION — the wrong-guess cinematic. Deterministic, omniscient,
# and deliberately NOT a hunt: no LLM, no HuntCore, no ChaseStarted.
# ==================================================================

func start_endgame_execution():
	if not multiplayer.is_server():
		return
	_rpc_endgame_execution.rpc()

@rpc("authority", "call_local")
func _rpc_endgame_execution():
	_endgame_execution = true
	chasing = true
	chasing_EntireSequence = true
	hunt_in_grace = false
	skeleton.visible = true
	huntStartSFX.play(0)

	if not multiplayer.is_server():
		return

	speed = 0
	await get_tree().create_timer(5).timeout
	speed = 35

	var revealed := false
	while true:
		var victim = get_closest_player()
		if victim == null:
			break

		update_target_location(victim.global_transform.origin)
		current_target = victim

		if (victim.global_transform.origin - global_transform.origin).length() < KILL_RANGE:
			_rpc_play_jumpscare.rpc()
			if victim.has_method("kill"):
				victim.kill_remote.rpc_id(victim.get_multiplayer_authority())
			if not revealed:
				revealed = true
				_rpc_endgame_reveal.rpc()

		await get_tree().create_timer(0.1).timeout

@rpc("authority", "call_local")
func _rpc_endgame_reveal():
	blackTexture.visible = true
	var tween = create_tween()
	endRevealText.text = "THE GHOST WAS A " + GhostType.to_upper()
	tween.tween_property(blackTexture, "modulate", Color(0, 0, 0, 1), 0.25).set_trans(Tween.TRANS_EXPO).set_delay(1.75)
	tween.tween_property(endRevealText, "modulate", Color(1, 1, 1, 1), 1).set_delay(3)
	tween.play()

func _physics_process(delta):
	if (!multiplayer.is_server()):
		return

	# The LOS probe: outside lunges and the endgame cinematic, current_target is
	# simply the nearest living player — it drives inLineOfSight (player heartbeat
	# UI + HuntCore's lunge gate), never pursuit knowledge by itself.
	if _lunge_target == null and not _endgame_execution:
		current_target = get_closest_player()

	if chasing and not hunt_in_grace and not gameEnded and not _endgame_execution:
		_hunt_contact_check()
		_lunge_tick()

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
		# Face the target only when the ghost can actually SEE it (or during the
		# manifest/cinematic) — facing through walls telegraphs knowledge the
		# hunting instincts don't have.
		if current_target and (inLineOfSight or manifesting or _endgame_execution):
			var direction = (current_target.global_transform.origin - global_transform.origin).normalized()
			rotation.y = lerp_angle(rotation.y, atan2( - direction.x, -direction.z), delta * 5)
		elif (current_location - next_location).length() > 0.1:
			var move_direction = (next_location - current_location).normalized()
			rotation.y = lerp_angle(rotation.y, atan2( - move_direction.x, -move_direction.z), delta * 5)

	move_and_slide()

	last_location = current_location

	if chasing and current_target:
		$Skeleton3D/OmniLight3D.light_energy = randf_range(0.5, 1.5)

		var distance = (current_target.global_transform.origin - global_transform.origin).length()

		huntTensionSFX.volume_db = (-(distance * 2)) - 10
		huntTensionSFX.pitch_scale = 0.75 + clamp((1 / distance), 0, 1.25)

		# Speed is engine-clamped (HUNT_SPEED_*/LUNGE_SPEED); the old omniscient
		# distance rubber-band is gone with the autopilot.
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
