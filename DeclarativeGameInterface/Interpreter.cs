using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

public struct AbstractSyntaxTreeNode
{
	public string verb;
	public List<string> arguments;
}

public class Token
{
	public string Type { get; private set; }
	public string Value { get; private set; }

	public Token(string type, string value)
	{
		Type = type;
		Value = value;
	}

	public override string ToString()
	{
		return $"Token({Type}, {Value})";
	}
}

public partial class Interpreter : Node
{
	private Node3D player = null;
	private Node3D ghost = null;

	private void _ready()
	{
		player = (Node3D)GetTree().CurrentScene.FindChild("Player");
		ghost = (Node3D)GetTree().CurrentScene.FindChild("Ghost");

		GD.Print("Initialized interpreter with values player: ", player, " ghost: ", ghost);
	}

	private Dictionary<string, string> tokenTypes = new Dictionary<string, string>
	{
		{
			"VERB",
			@"(moveTo|emitSound|manifest|speakAsGhost|speakAsSpiritBox|toggleLights|turnOffLights|flickerLights|explodeLights|restoreLights|toggleTVs|flickerTVs|explodeTVs|ringPhones|throwPhysicsObject|knockWindows|knockDoors|openDoors|closeDoors|lockDoors)"
		},
		{ "MODIFIER", @"(at|random|all|in)" },
		{ "ENTITY", @"(player|ghost|room)" },
		{ "NAME", @"[a-zA-Z][a-zA-Z0-9]*" },
		{ "SPACE", @"\s+" },
		{ "LPAREN", @"\(" },
		{ "RPAREN", @"\)" }
	};

	private List<string> objectInteractionVerbPrefixes =
		new() { "flicker", "explode", "restore", "turnOff" };

	public List<Token> Lexer(string code)
	{
		List<Token> tokens = new List<Token>();
		while (!string.IsNullOrEmpty(code))
		{
			bool match = false;
			foreach (var tokenType in tokenTypes)
			{
				Match regexMatch = Regex.Match(code, "^" + tokenType.Value);
				if (regexMatch.Success)
				{
					match = true;
					if (tokenType.Key != "SPACE")
					{
						tokens.Add(new Token(tokenType.Key, regexMatch.Value));
					}
					code = code.Substring(regexMatch.Length);
					break;
				}
			}

			if (!match)
			{
				throw new ArgumentException($"Illegal character: {code[0]}");
			}
		}
		return tokens;
	}

	public List<AbstractSyntaxTreeNode> Parser(List<Token> tokens)
	{
		List<AbstractSyntaxTreeNode> ast = new();
		while (tokens.Count > 0)
		{
			Token verbToken = tokens[0];
			tokens.RemoveAt(0);
			if (verbToken.Type != "VERB")
			{
				throw new ArgumentException("Command must start with a verb");
			}

			if (tokens.Count == 0 || tokens[0].Type != "LPAREN")
			{
				throw new ArgumentException("Expected '(' after verb");
			}
			tokens.RemoveAt(0);

			List<string> arguments = new List<string>();
			while (tokens.Count > 0 && tokens[0].Type != "RPAREN")
			{
				Token token = tokens[0];
				tokens.RemoveAt(0);
				if (token.Type == "MODIFIER" || token.Type == "ENTITY" || token.Type == "NAME")
				{
					arguments.Add(token.Value);
				}
				else
				{
					throw new ArgumentException("Unexpected token type in arguments");
				}
			}

			if (tokens.Count == 0 || tokens[0].Type != "RPAREN")
			{
				throw new ArgumentException("Expected ')' after arguments");
			}
			tokens.RemoveAt(0);

			ast.Add(new() { verb = verbToken.Value, arguments = arguments, });
		}

		return ast;
	}

	public void Interpret(string code)
	{
		List<Token> tokens = Lexer(code);
		List<AbstractSyntaxTreeNode> ast = Parser(tokens);
		foreach (AbstractSyntaxTreeNode node in ast)
		{
			GD.Print(node.verb);

			var searchSpace = getObjectSearchSpace(node.arguments);

			string prefix = null;

			try
			{
				prefix = objectInteractionVerbPrefixes.First(p =>
				{
					var res = node.verb.Contains(p);
					GD.Print(node.verb, " contains ", p, "? - ", res);
					return res;
				});
			}
			catch (Exception e)
			{
				GD.Print(node.verb, " did not have a valid object interaction verb prefix");
			}

			if (prefix != null)
			{
				GD.Print("was object interaction command!");
				foreach (Node3D n in searchSpace)
				{
					n.Call(prefix);
				}
			}
		}
	}

	private List<Node3D> getObjectSearchSpace(List<string> arguments)
	{
		int inKeywordIndex = arguments.IndexOf("in");
		string roomName = null;

		var lights = GetTree().GetNodesInGroup("lights").Cast<Node3D>().ToList();
		var tvs = GetTree().GetNodesInGroup("tvs").Cast<Node3D>().ToList();

		if (inKeywordIndex != -1)
		{
			roomName = arguments[inKeywordIndex + 1];
		}

		if (roomName == null)
		{
			List<Node3D> nodes = new();

			nodes.AddRange(lights);
			nodes.AddRange(tvs);

			return nodes;
		}

		var rooms = GetTree().GetNodesInGroup("rooms");

		Area3D selectedRoom = null;

		for (int i = 0; i < rooms.Count; i++)
		{
			if (rooms[i].Name.ToString().ToLower().Contains(roomName.ToLower()))
			{
				selectedRoom = (Area3D)rooms[i];
				break;
			}
		}

		if (selectedRoom == null)
		{
			GD.Print(roomName, " apparently does not exist. Defaulting to all");
			List<Node3D> nodes = new();

			nodes.AddRange(lights);
			nodes.AddRange(tvs);
			return nodes;
		}

		var roomNodes = selectedRoom.GetOverlappingBodies().ToList();

		List<Node3D> validNodes = new();

		foreach (Node3D roomNode in roomNodes)
		{
			Node currentNode = roomNode;

			while (currentNode.GetParent() != null)
			{
				if (currentNode.IsInGroup("lights") || currentNode.IsInGroup("tvs"))
				{
					validNodes.Add((Node3D)currentNode);
				}
				currentNode = currentNode.GetParent();
			}
		}

		validNodes.ForEach(n => GD.Print(n.Name));

		return validNodes;
	}
}
