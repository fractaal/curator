extends VBoxContainer

@onready var mic_player: AudioStreamPlayer = $MicPlayer
@onready var audio_to_text: CaptureStreamToText = $CaptureStreamToText # Called when the node enters the scene tree for the first time.

func _on_capture_stream_to_text_transcribed_msg(is_partial, text):
	EventBus.emit_signal("VoiceRecognition", is_partial, text)

func _ready():
	mic_player.bus = "Record"
	mic_player.play()
	audio_to_text.transcribed_msg.connect(_on_capture_stream_to_text_transcribed_msg)

func _physics_process(_delta):
	if Input.is_action_just_pressed("ToggleVoiceRecognition"):
		audio_to_text.recording = !audio_to_text.recording

		EventBus.emit_signal("ToastNotification", "Voice recognition " + ("enabled" if audio_to_text.recording else "disabled"))
