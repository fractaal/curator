extends Node3D

@onready var player = $Player
@onready var rng = RandomNumberGenerator.new()

func _ready():

	var lastCommand;

	# while true:
	# 	await get_tree().create_timer(rng.randf_range(2,5)).timeout

	# 	var chance = randf();

	# 	if lastCommand == "explodeLights(all)":
	# 		lastCommand = "restoreLights(all)"
	# 		Curator.Interpret("restoreLights(all)")
	# 		continue

	# 	if chance > 0.95:
	# 		lastCommand = "explodeLights(all)"
	# 		Curator.Interpret("explodeLights(all)")
	# 	elif chance > 0.5:
	# 		lastCommand = "flickerLights(all)"
	# 		Curator.Interpret("flickerLights(all)")



