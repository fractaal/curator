extends Node3D

func _ready():
	var platform := OS.get_name()
	
	print("Platform is ", platform)
	
	var environment: WorldEnvironment = $WorldEnvironment
	
	if platform == "macOS":
		print("Low-power machine detected, disabling graphics features")
		get_viewport().scaling_3d_mode = Viewport.SCALING_3D_MODE_BILINEAR
		get_viewport().scaling_3d_scale = 0.25
		get_viewport().scaling_3d_mode = Viewport.SCALING_3D_MODE_FSR2
		
		environment.environment.sdfgi_enabled = false
		environment.environment.volumetric_fog_enabled = false
		environment.environment.ssao_enabled = false
