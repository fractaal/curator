using System;
using System.Net;
using System.Threading;
using Godot;

public partial class VoiceWebServer : Node
{
    private HttpListener listener;
    private Thread listenerThread;

    public override void _Ready()
    {
        StartServer();
    }

    private void StartServer()
    {
        listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:6900/");
        listener.Start();
        GD.Print("Server started on http://localhost:6900/");

        listenerThread = new Thread(HandleRequests);
        listenerThread.Start();
    }

    private void EmitSaidSignal(string text)
    {
        EventBus
            .Get()
            .EmitSignal(EventBus.SignalName.NotableEventOccurred, "Player said: \"" + text + "\"");
    }

    private void HandleRequests()
    {
        while (listener.IsListening)
        {
            try
            {
                var context = listener.GetContext();
                var response = context.Response;
                var request = context.Request;

                // Process request here...
                // For example, read the text from voice recognition:
                using (
                    var reader = new System.IO.StreamReader(
                        request.InputStream,
                        request.ContentEncoding
                    )
                )
                {
                    string text = reader.ReadToEnd();
                    GD.Print("Received Text from Voice Recognition: " + text);
                    CallDeferred(nameof(EmitSaidSignal), text);
                }

                string responseString = "<html><body>OK</body></html>";
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception e)
            {
                GD.PrintErr("Request Handling Error: ", e.Message);
            }
        }
    }

    public override void _ExitTree()
    {
        listener.Stop();
        listener.Close();
        // listenerThread.Abort();
    }
}
