extends Node3D

# var Config := preload ("res://Scripts/Config.cs")

func _ready():
	var platform := OS.get_name()
	
	print("Platform is ", platform)
	
	var environment: WorldEnvironment = $WorldEnvironment

	var scaling_mode = Config.Get("SCALING_ALGORITHM").to_upper()
	if scaling_mode == "":
		scaling_mode = "FSR2"

	if Viewport["SCALING_3D_MODE_" + scaling_mode]:
		get_viewport().scaling_3d_mode = Viewport["SCALING_3D_MODE_" + scaling_mode]
	else:
		print("Invalid scaling mode: ", scaling_mode)
		get_viewport().scaling_3d_mode = Viewport.SCALING_3D_MODE_FSR

	var scaling_factor = Config.Get("SCALING_FACTOR")

	if scaling_factor.is_valid_float():
		get_viewport().scaling_3d_scale = scaling_factor.to_float()
	else:
		print("Invalid scaling factor: ", scaling_factor)
		get_viewport().scaling_3d_scale = 1.0

	var max_fps = Config.Get("MAX_FPS")

	if max_fps.is_valid_int():
		Engine.max_fps = max_fps.to_int()
	else:
		print("Invalid max FPS: ", max_fps)
		Engine.max_fps = 0

	var fullscreen: String = Config.Get("FULLSCREEN").to_lower()

	if fullscreen == "true":
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	else:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)

	var sdfgi_enabled: String = Config.Get("SDFGI").to_lower()
	var volumetric_fog_enabled: String = Config.Get("VOLUMETRICS").to_lower()
	var ssao_enabled: String = Config.Get("SSAO").to_lower()
	var ssr_enabled: String = Config.Get("SSR").to_lower()
	var ssil_enabled: String = Config.Get("SSIL").to_lower()
	
	if sdfgi_enabled == "true":
		environment.environment.sdfgi_enabled = true
	else:
		environment.environment.sdfgi_enabled = false
	
	if volumetric_fog_enabled == "true":
		environment.environment.volumetric_fog_enabled = true
	else:
		environment.environment.volumetric_fog_enabled = false
		
	if ssao_enabled == "true":
		environment.environment.ssao_enabled = true
	else:
		environment.environment.ssao_enabled = false
	
	if ssr_enabled == "true":
		environment.environment.ssr_enabled = true
	else:
		environment.environment.ssr_enabled = false
		
	if ssil_enabled == "true":
		environment.environment.ssil_enabled = true
	else:
		environment.environment.ssil_enabled = false
	
	environment.environment.adjustment_brightness = 1.0
