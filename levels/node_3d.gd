extends Node3D

# var Config := preload ("res://Scripts/Config.cs")

func _set_shadows(directional_shadow_size, positional_shadow_size):
	await get_tree().create_timer(1).timeout
	if directional_shadow_size == "":
		directional_shadow_size = 256
	else:
		directional_shadow_size = int(directional_shadow_size)
		if directional_shadow_size < 1:
			directional_shadow_size = 1
	
	if positional_shadow_size == "":
		positional_shadow_size = 1024
	else:
		positional_shadow_size = int(positional_shadow_size)
		if positional_shadow_size < 0:
			positional_shadow_size = 0
	
	RenderingServer.viewport_set_positional_shadow_atlas_size(get_viewport().get_viewport_rid(), positional_shadow_size, true)
	RenderingServer.directional_shadow_atlas_set_size(directional_shadow_size, true)

func _set_shadow_filters(quality):
	var constants = ClassDB.class_get_integer_constant_list("RenderingServer")
	
	var available_quality_settings = []
	
	for constant in constants:
		if constant.begins_with("SHADOW_QUALITY_") and not constant.ends_with("MAX"):
			available_quality_settings.append(constant.substr("SHADOW_QUALITY_".length()))
	
	if quality in available_quality_settings:
		RenderingServer.positional_soft_shadow_filter_set_quality(RenderingServer["SHADOW_QUALITY_" + quality])
		RenderingServer.directional_soft_shadow_filter_set_quality(RenderingServer["SHADOW_QUALITY_" + quality])
	else:
		push_error(
			"Invalid shadow quality setting: " +
			quality + " is not a valid quality setting!\n" +
			"Valid quality settings are " + available_quality_settings.reduce(func(acc, qual): return acc + qual + ", ", ""),
		)
		
func _ready():
	var platform := OS.get_name()
	
	print("Platform is ", platform)
	
	var environment: WorldEnvironment = $WorldEnvironment

	var scaling_mode = Config.Get("SCALING_ALGORITHM").to_upper()
	if scaling_mode == "":
		scaling_mode = "FSR"
	
	var directional_shadow_size = Config.Get("DIRECTIONAL_SHADOW_SIZE")
	var positional_shadow_size = Config.Get("POSITIONAL_SHADOW_SIZE")
	
	var soft_shadow_quality = Config.Get("SHADOW_FILTER_QUALITY")

	if soft_shadow_quality == "":
		soft_shadow_quality = "SOFT_VERY_LOW"
	
	_set_shadows(directional_shadow_size, positional_shadow_size)
	_set_shadow_filters(soft_shadow_quality)

	var vsync = Config.Get("VSYNC").to_lower()
	if vsync == "true":
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED)
	else:
		DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)

	get_viewport().scaling_3d_mode = Viewport["SCALING_3D_MODE_" + scaling_mode]
	
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
