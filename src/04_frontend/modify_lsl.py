import sys
import re

file_path = "MainWindow.xaml.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

queue_code = """
        // LSL Message Queue
        private System.Collections.Generic.Queue<string> _lslMessageQueue = new();
        private bool _isProcessingLslMessages = false;

        private async void QueueLslMessage(string message)
        {
            _lslMessageQueue.Enqueue(message);
            if (!_isProcessingLslMessages)
            {
                _isProcessingLslMessages = true;
                while (_lslMessageQueue.Count > 0)
                {
                    string msg = _lslMessageQueue.Dequeue();
                    if (TxtLslStatus != null) TxtLslStatus.Text = msg;
                    await Task.Delay(5000);
                }
                if (_lslProcess == null && TxtLslStatus != null) TxtLslStatus.Text = "Status: Idle";
                _isProcessingLslMessages = false;
            }
        }
"""

content = content.replace("public partial class MainWindow : Window\n    {", "public partial class MainWindow : Window\n    {" + queue_code)

content = content.replace("""TxtLslStatus.Text = $"Status: {status}";""", """QueueLslMessage($"Status: {status}");""")
content = content.replace("""TxtLslStatus.Text = $"Error: {ev.Data}";""", """QueueLslMessage($"Error: {ev.Data}");""")
content = content.replace("""TxtLslStatus.Text = "Status: Launched BlueMuse Protocol";""", """QueueLslMessage("Status: Launched BlueMuse Protocol");""")
content = content.replace("""TxtLslStatus.Text = "Status: Initializing...";""", """QueueLslMessage("Status: Initializing...");""")

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)
print("done LSL queue")
