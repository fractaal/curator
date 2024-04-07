extends Node3D

var spring_k = 750
var spring_b = 10

var target_rotation = Quaternion()
var local_rotation = Quaternion()

var noise_x_high = FastNoiseLite.new()
var noise_y_high = FastNoiseLite.new()

func _physics_process(_delta):
	# target_rotation = $"../Head/Camera3d".rotation + $"..".rotation
	target_rotation = $"../Head/Camera3d".global_transform.basis.get_rotation_quaternion()

	local_rotation = local_rotation.slerp(target_rotation, .15)

	var velocity = $"..".velocity
	var local_rotation_noised = local_rotation.get_euler() + Vector3(
		((noise_x_high.get_noise_1d((Time.get_ticks_msec() + velocity.length()) * 0.1) * 2) + (sin((Time.get_ticks_msec() + velocity.length()) * 0.01) * 0.5)) * velocity.length() * 0.01,
		0,
		((noise_y_high.get_noise_1d((Time.get_ticks_msec() + velocity.length()) * 0.1) * 2) + (cos((Time.get_ticks_msec() + velocity.length()) * 0.01) * 0.5)) * velocity.length() * 0.01
	)
	
	$"../SpotLight3D".global_rotation = local_rotation_noised
	
	$"../SpotLight3D".position.x = 0.357 + ((noise_x_high.get_noise_1d((Time.get_ticks_msec() + velocity.length()) * 0.05) * 2) + (sin((Time.get_ticks_msec() + velocity.length()) * 0.01) * 0.5)) * velocity.length() * 0.05
	$"../SpotLight3D".position.y = 0.6 + ((noise_y_high.get_noise_1d((Time.get_ticks_msec() + velocity.length()) * 0.05) * 2) + (cos((Time.get_ticks_msec() + velocity.length()) * 0.01) * 0.5)) * velocity.length() * 0.05
