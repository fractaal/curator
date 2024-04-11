import re
import pathlib
import pprint

# Open and read the log file content
log_file_path = '/Applications/Godot_mono.app/Contents/MacOS/Game Log 2024-04-11 145416.txt'

# Read the content of the file
with open(log_file_path, 'r') as file:
    log_content = file.read()

# Split the log file content based on newlines
log_lines = log_content.split('\n')

# Initialize variables to hold processed log entries
processed_log_entries = []
current_entry = []

# Iterate over each line in the log file
for line in log_lines:
    # Check if the line starts with a timestamp (digits followed by "|")
    if re.match(r'^\d+\|', line):
        # If current_entry is not empty, it means we've encountered a new entry
        # Add the current entry to the processed list and start a new entry
        if current_entry:
            processed_log_entries.append(current_entry)
            current_entry = [line]
        else:
            current_entry.append(line)
    else:
        # If the line does not start with a timestamp, it's part of the current entry
        current_entry.append(line)

# Add the last entry to the list if it's not empty
if current_entry:
    processed_log_entries.append(current_entry)



# Process the log entries to condense multi-line messages into the third position of each entry
condensed_log_entries = []

for entry in processed_log_entries:
    # Timestamp and event name will always be in the first element, split by "|"
    timestamp, event_type, *message_parts = entry[0].split('|', 2)
    # Combine any additional lines from the entry into the message
    full_message = ' '.join([message_parts[0]] + entry[1:]) if message_parts else ' '.join(entry[1:])
    # Append the structured entry to the condensed log entries list
    condensed_log_entries.append([timestamp, event_type, full_message.strip()])

def into_words(text: str):
    return text.split(" ")

def has_matching_words(word_list: list[str], text: str):
    for word in word_list:
        if word.lower() in text.lower():
            return True
    return False

def has_all_matching_words(word_list: list[str], text: str):

    result = True

    for word in word_list:
        if not word.lower() in text.lower():
            result = False
            break
        
    return result

total = 0
successes = []

base_failures = []
object_resolution_failures = []
target_resolution_failures = []
narrative_integrity_failures = []
undefined_failures = []

def collate_commands(index: int, log_entry):
    if log_entry[1] == "InterpreterCommandRecognized":
        words = into_words(log_entry[2])
        success = True
        system_feedback_entry = None

        for following in condensed_log_entries[index+1:]:
            if following[1] == "SystemFeedback" and has_matching_words(words, following[2]) and has_all_matching_words(["Command", "does", "NOT", "exist."], following[2]):
                success = False
                system_feedback_entry = following
                break
            
        if success:
            pass
            # print("Command \"" + log_entry[2] + "\" was recognized")
            # Don't do anything here because it may still fail later on 
        else:
            # print("Command \"" + log_entry[2] + "\" was not recognized")
            # print("System Feedback Entry: ", system_feedback_entry)
            base_failures.append([log_entry, system_feedback_entry])

    elif log_entry[1] == "ObjectInteraction":
        global total
        total = total + 1
        words = into_words(log_entry[2])

        acknowledged = False
        failure_mode = None

        for following in condensed_log_entries[index+1:]:
            if following[1] == "ObjectInteractionAcknowledged" and has_matching_words(words, following[2]):
              acknowledged = True
              break
            elif following[1] == "SystemFeedback" and has_matching_words(words, following[2]):
              acknowledged = False
              message = following[2].lower()
              if "object" in message:
                  failure_mode = "Object Resolution Failure Mode"
                  object_resolution_failures.append([log_entry, following])
              elif "target" in message:
                  failure_mode = "Target Resolution Failure Mode"
                  target_resolution_failures.append([log_entry, following])
              else:
                  failure_mode = "Undefined Failure Mode"
                  undefined_failures.append([log_entry, following])
              break
        
        if acknowledged:
          # print(log_entry[2], " was successfully executed")
          successes.append(log_entry)
          pass
        else:
          pass
          # print(log_entry[2], " failed - ", failure_mode)
      
                

for index, entry in enumerate(condensed_log_entries):
    collate_commands(index, entry)

results = {
    "Command Success Rate (%)": (len(successes) / (len(successes) + len(base_failures) + len(object_resolution_failures) + len(target_resolution_failures) + len(narrative_integrity_failures) + len(undefined_failures))) * 100,
    "Total Commands": total,
    "Commands Succeeded": len(successes),
    "Object Resolution Failures": len(object_resolution_failures),
    "Target Resolution Failures": len(target_resolution_failures),
    "Narrative Integrity Failures": len(narrative_integrity_failures)
}

pprint.pp(results)

# pprint.pp(condensed_log_entries[:10])