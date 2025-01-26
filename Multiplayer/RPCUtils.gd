extends Node

# The server-side handler that calls the implementation
@rpc("authority", "call_local")
func handle_rpc(target_path: NodePath, function_name: String, args: Array = []):
	var target = get_tree().get_root().get_node(target_path)
	if target and target.has_method(function_name + "_impl"):
		target.call(function_name + "_impl", args)
	else:
		push_error("Missing implementation for " + function_name + "_impl in " + str(target))

# The client-side request that anyone can call
@rpc("any_peer", "call_local")
func request_rpc(target_path: NodePath, function_name: String, args: Array = []):
	if not get_tree().get_root().get_node(target_path).multiplayer.is_server():
		return
	handle_rpc.rpc(target_path, function_name, args)

# The main entry point that handles routing
func try_rpc_call(target: Node, function_name: String, args: Array = []) -> void:
	if not target.has_method(function_name + "_impl"):
		push_error("Missing implementation for " + function_name + "_impl in " + str(target))
		return
		
	if get_tree().get_root().get_node(target.get_path()).multiplayer.is_server():
		handle_rpc.rpc(target.get_path(), function_name, args)
	else:
		request_rpc.rpc_id(1, target.get_path(), function_name, args)
