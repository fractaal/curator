extends Node3D

var locked = false
var isOpen = false
var isAnimating = false

func open():
	if locked: return
	if isOpen: return
	if isAnimating: return

	isAnimating = true
	isOpen = true
	
	var tween = create_tween()
	await tween.tween_method(_openStep, 0.0, 1.0, 1.0).set_ease(Tween.EASE_OUT_IN).finished

	isAnimating = false


func close():
	if locked: return
	if not isOpen: return
	if isAnimating: return
	
	isOpen = false
	isAnimating = true

	var tween = create_tween()
	await tween.tween_method(_closeStep, 0.0, 1.0, 1.0).set_ease(Tween.EASE_OUT_IN).finished

	isAnimating = false

func toggle():
	if isOpen:
		close()
	else:
		open()
	
	

func lock():
	locked = true

func unlock():
	locked = false

func _openStep(progress: float):
	var y = lerp(0, 135, progress)
	rotation.y = deg_to_rad(y)
	# print(y)
	# rotation.y = y

func _closeStep(progress: float):
	var y = lerp(135, 0, progress)
	rotation.y = deg_to_rad(y)
