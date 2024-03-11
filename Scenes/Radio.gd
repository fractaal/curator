extends Node3D

@export var statusLabel: Label3D;

var isOn = true;
var isPlaying = false;

# Called when the node enters the scene tree for the first time.
func _ready():
	_updateStatusLabel()
	
func _updateStatusLabel():
	var out = "";
	if isOn:
		out += "ON, "
	else:
		out += "OFF, "
	
	if isPlaying:
		out += "PLAYING"
	else:
		out += "STATIC"
	
	statusLabel.text = out
	
func turnOn():
	isOn = true

func turnOff():
	isOn = false
	
func play():
	isPlaying = true
	
func stop():
	isPlaying = false

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	pass
