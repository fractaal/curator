class_name NodeUtils 

static func walk_up_until_node_of_type(type: String, node: Node) -> Node:
	while node and !node.is_class(type):
		node = node.get_parent()
	return node