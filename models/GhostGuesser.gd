extends VBoxContainer

var buttonChildren: Array[Node]

@export var confirmLabel: RichTextLabel

var evidenceDepositor: Node

func _ready():
	buttonChildren = find_children("*", "Button", true) as Array[Node]
	
	evidenceDepositor = get_tree().current_scene.get_node("Ghost/EvidenceDepositor")
	
	for button in buttonChildren:
		if button.text == "CONFIRM CHOICE":
			button.button_down.connect(func(): _on_button_down(button.text))
			continue

		var evidences = evidenceDepositor.GetEvidenceForGhost(button.text)
		if evidences == null:
			continue
		else:
			find_child(button.text + "Label").text = evidences
		button.button_down.connect(func(): _on_button_down(button.text))

var selectedGhost = ""
		
func _on_button_down(text):
	print("Player clicked", text)

	if text != "CONFIRM CHOICE":
		selectedGhost = text
		confirmLabel.text = selectedGhost
	else:
		if selectedGhost == "":
			print("Player has not selected a ghost type")
			confirmLabel.text = "Select a ghost type first"
			await get_tree().create_timer(2).timeout
			confirmLabel.text = ""
		else:
			print("Player has selected", selectedGhost)
			
			EventBus.emit_signal("PlayerDecidedGhostType", selectedGhost)
