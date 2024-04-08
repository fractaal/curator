extends Node3D

func _ready():
	var platform := OS.get_name()
	
	print("Platform is ", platform)
	
	var environment: WorldEnvironment = $WorldEnvironment
	
	if platform == "macOS":
		print("Low-power machine detected, disabling graphics features")
		DisplayServer.window_set_size(Vector2i(960,540))
		
		environment.environment.sdfgi_enabled = false
		environment.environment.volumetric_fog_enabled = false
		environment.environment.ssao_enabled = false
		
