import requests  # Import the requests library
from RealtimeSTT import AudioToTextRecorder

# The URL of your web server
URL = "http://localhost:6900/"

if __name__ == '__main__':
    recorder = AudioToTextRecorder(spinner=False, model="tiny.en", language="en", silero_sensitivity=0.4, webrtc_sensitivity=2, post_speech_silence_duration=0.4, min_length_of_recording=0, min_gap_between_recordings=0, enable_realtime_transcription=True)

    print("Say something...")
    while True:
        # Capture the text from your voice recognition
        text = recorder.text()
        
        # Print the recognized text to the console
        print(text, end=" ", flush=True)
        
        # Check if text is not empty
        if text.strip() != "":
            try:
                # Send the text to the web server via POST request
                response = requests.post(URL, data=text.encode('utf-8'), headers={"Content-Type": "text/plain"})
                
                # Optional: Print the response from the server
                print("\nServer response:", response.text)
            except requests.exceptions.RequestException as e:
                # Handle any errors that occur during the request
                print("\nFailed to send data:", e)
