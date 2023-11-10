using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public struct AbstractSyntaxTreeNode {
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

		private void _ready() {
				player = GetNode<Node3D>("/root/Player");
				ghost = GetNode<Node3D>("/root/Ghost");

				GD.Print("Initialized interpreter with values player: ", player, " ghost: ", ghost);
		}

	private Dictionary<string, string> tokenTypes = new Dictionary<string, string>
	{
		{"VERB", @"(moveTo|emitSound|manifest|speakAsGhost|speakAsSpiritBox|toggleLights|flickerLights|explodeLights|toggleTVs|flickerTVs|explodeTVs|ringPhones|throwPhysicsObject|knockWindows|knockDoors|openDoors|closeDoors|lockDoors)"},
		{"MODIFIER", @"(at|random|all)"},
		{"ENTITY", @"(player|ghost|room)"},
		{"NAME", @"[a-zA-Z][a-zA-Z0-9]*"},
		{"SPACE", @"\s+"},
		{"LPAREN", @"\("},
		{"RPAREN", @"\)"}
	};

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
		List<AbstractSyntaxTreeNode> ast = new ();
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

						ast.Add(new() {
							verb = verbToken.Value,
							arguments = arguments,
						});

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
						foreach (string argument in node.arguments)
						{
								GD.Print(argument);
						}
				}
		}
}
