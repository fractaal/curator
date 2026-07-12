extends Node

var _refresh_elapsed := 0.0

func _ready() -> void:
	%HostGameButton.pressed.connect(on_host_game_button_pressed)
	%JoinGameButton.pressed.connect(on_join_game_button_pressed)
	%LobbyList.item_activated.connect(on_lobby_activated)

	LanDiscovery.StartDiscovery()

func _process(delta: float) -> void:
	if not %MultiplayerHUD.visible:
		return

	_refresh_elapsed += delta
	if _refresh_elapsed < 0.5:
		return
	_refresh_elapsed = 0.0

	_refresh_lobby_list()

func _refresh_lobby_list():
	var lobby_list: ItemList = %LobbyList

	var selected_address := ""
	var selected := lobby_list.get_selected_items()
	if selected.size() > 0:
		selected_address = lobby_list.get_item_metadata(selected[0])

	lobby_list.clear()

	for lobby in LanDiscovery.GetLobbies():
		var index = lobby_list.add_item("%s  (%d/%d players)" % [lobby["address"], lobby["players"], lobby["max"]])
		lobby_list.set_item_metadata(index, lobby["address"])
		if lobby["address"] == selected_address:
			lobby_list.select(index)

func on_host_game_button_pressed():
	MultiplayerManager.start_host()
	_close()

func on_join_game_button_pressed():
	var address: String = %AddressField.text.strip_edges()

	if address == "":
		var selected := (%LobbyList as ItemList).get_selected_items()
		if selected.size() > 0:
			address = %LobbyList.get_item_metadata(selected[0])

	if address == "":
		address = "127.0.0.1"

	MultiplayerManager.join_server(address)
	_close()

func on_lobby_activated(index: int):
	MultiplayerManager.join_server(%LobbyList.get_item_metadata(index))
	_close()

func _close():
	LanDiscovery.StopDiscovery()
	%MultiplayerHUD.visible = false
