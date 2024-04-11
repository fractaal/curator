extends Node

static func GetGhostTypes() -> Array[String]:
    return ["Demon", "Wraith", "Phantom", "Banshee", "Shade", "Poltergeist"]

static func MatchCase(original: String, replacement: String) -> String:
    if original == original.to_upper():
        return replacement.to_upper()
    elif original.substr(0, 1) == original.substr(0, 1).to_upper() and original.substr(1) == original.substr(1).to_lower():
        return replacement.capitalize()
    else:
        return replacement.to_lower()

static func StripGhostTypesAndFeedback(text: String, action: String) -> String:
    var stripped_text = text
    var removed_ghost_types: Array[String] = []
    var ghost_types = GetGhostTypes()

    for ghost_type in ghost_types:
        var index = 0
        while index < stripped_text.length():
            var found_pos = stripped_text.findn(ghost_type, index)
            if found_pos == - 1:
                break
            var found_text = stripped_text.substr(found_pos, ghost_type.length())
            var replacement_text = MatchCase(found_text, "supernatural entity")
            stripped_text = stripped_text.replace(found_text, replacement_text)
            if !removed_ghost_types.has(ghost_type):
                removed_ghost_types.append(ghost_type)
            index = found_pos + replacement_text.length()

    if removed_ghost_types.size() > 0:
        var feedback_text = "NARRATIVE INTEGRITY FAILURE: During " + action + ", you divulged ghost types (" + ", ".join(removed_ghost_types) + "). This has been replaced with a generic term 'supernatural entity.' DO NOT divulge ghost types to the player!"
        EventBus.emit_signal("SystemFeedback", feedback_text)

    return stripped_text

static func StripGhostTypes(text: String) -> String:
    var stripped_text = text
    var removed_ghost_types: Array[String] = []
    var ghost_types = GetGhostTypes()

    for ghost_type in ghost_types:
        var index = 0
        while index < stripped_text.length():
            var found_pos = stripped_text.findn(ghost_type, index)
            if found_pos == - 1:
                break
            var found_text = stripped_text.substr(found_pos, ghost_type.length())
            var replacement_text = MatchCase(found_text, "supernatural entity")
            stripped_text = stripped_text.replace(found_text, replacement_text)
            if !removed_ghost_types.has(ghost_type):
                removed_ghost_types.append(ghost_type)
            index = found_pos + replacement_text.length()

    return stripped_text
