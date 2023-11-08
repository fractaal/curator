extends Node3D

var voices = DisplayServer.tts_get_voices_for_language("en")
var voice_id = voices[0]

var lastSpeechTime = Time.get_ticks_msec()

func _physics_process(delta):
	if (Time.get_ticks_msec() - lastSpeechTime > 2000):
		lastSpeechTime = Time.get_ticks_msec()
		
		DisplayServer.tts_speak("Kill yourself", voice_id)
		
	
