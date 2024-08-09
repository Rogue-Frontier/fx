using ClangSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fx;

public interface IResource {
	public Dictionary<string, string> pairs { get; set; }
	public string id => pairs["id"];
	public string path => pairs["path"];
}
public record Resource : IResource {
	public Dictionary<string, string> pairs { get; set; }
}
public record PackedScene :IResource {
	public Dictionary<string, string> pairs { get; set; }
}
public record Script : IResource {
	public Dictionary<string, string> pairs { get; set; }
}
public record Node {
	public Dictionary<string, string> pairs;
	public string name => pairs["name"];
	public string _path =>
		_parent == null ?
			"." :
		_parent._path == "." ?
			name :
		$"{_parent._path}/{name}";
	public Node _parent;
	public List<Node> _children = new();
	public PackedScene _instance;
	public Script _script;
}

public class GodotProject {
	public string root;

	public string GetDiskPath (IResource r) => r.path.Replace("res://", root);
	public string GetResPath (IResource r) => r.path.Replace(root, "res://");
}
public class GodotScene {
	public GodotScene(string tscn) {
		var text = File.ReadAllText(tscn);
		//[gd_scene load_steps=4 format=3 uid="uid://cecaux1sm7mo0"]
		if(text.MatchArray("""^\[gd_scene load_steps=([^"]+) format=([^"]+) uid="([^"]+)"\]\s*""", 1, out var end)
			.Apply(s => int.Parse(s), s => int.Parse(s), null) is { }result && result is not
			[int load_steps, int format, string uid]) {
			throw new Exception();
		}
		text = text[end..];
		Dictionary<string, PackedScene> packedScene = [];
		Dictionary<string, Script> script = [];
		Dictionary<string, IResource> ext_resource = [];
		while(text.MatchMatrix("""^\[ext_resource(?<pair>\s[^=]+="[^"]+")+\]\s*""", 1, out end) is [{ }pairs]) {
			text = text[end..];
			var items = ParsePairs(pairs);
			while(text.MatchArray("""^(?<key>[^\s]+) = (?<val>[^\s]+)\s*""", 1, out end) is [{}key, { } val]) {
				items[key] = val;
				text = text[end..];
			}
			IResource s = new Resource() { pairs = items };
			switch(items["type"]) {
				case "PackedScene": {
					s = packedScene[s.id] = new PackedScene() {
						pairs = items,
					};
					break;
				}
				case "Script": {
					s = script[s.id] = new Script() {
						pairs = items,
					};
					break;
				}
			}
			ext_resource[s.id] = s;
		}
		List<Dictionary<string, string>> sub_resource = [];
		while(text.MatchMatrix("""^\[sub_resource(?<pair>\s[^=]+="[^"]+")+\]\s*""", 1, out end) is [{ } pairs]) {
			text = text[end..];
			var items = ParsePairs(pairs);
			while(text.MatchArray("""^(?<key>[^\s]+) = (?<val>[^\s]+)\s*""", 1, out end) is [{ } key, { } val]) {
				items[key] = val;
				text = text[end..];
			}
			sub_resource.Add(items);
		}
		Node root = null;
		Dictionary<string, Node> node = [];
		while(text.MatchMatrix("""^\[node(?<pair>\s[^=]+="[^"]+")+(?<instance>\sinstance=ExtResource\("[^"]+"\))?\]\s*""", 1, out end) is [{ } pairs, { }instance]) {
			text = text[end..];
			var items = ParsePairs(pairs);
			while(text.MatchArray("""^(?<key>[^\s]+) = (?<val>[^\n]+)\s*""", 1, out end) is [{ } key, { } val]) {
				items[key] = val;
				text = text[end..];
			}
			PackedScene _instance = null;
			if(instance is [{ }inst] && inst.MatchArray("""instance=ExtResource\("(?<id>[^"]+)"\)""", 1) is [{ } id]) {
				items["instance"] = $"""ExtResource("{id}")""";
				_instance = packedScene[id];
			}
			Script _script =
				items.GetValueOrDefault("script")?.MatchArray("""ExtResource\("(?<id>[^"]+)"\)""", 1) is [{ } scriptId] ?
					script[scriptId] :
					null;
			if(root == null) {
				node["."] = root = (new Node() {
					pairs = items,
					_instance = _instance,
					_script = _script,
					_parent = null
				});
			} else {
				var par = items["parent"];
				var name = items["name"];
				var path = par == "." ? name : $"{par}/{name}";
				var n = new Node() {
					pairs = items,
					_instance = _instance,
					_script = _script,
					_parent = node[par]
				};
				node[path] = n;
				n._parent._children.Add(n);
			}
		}

		//to do: animationplayer, skeleton, animation

		Dictionary<string, string> ParsePairs (string[] pairs) {
			Dictionary<string, string> items = new();
			foreach(var pair in pairs)
				if(pair.MatchArray("""^ (?<key>[^=]+)="(?<val>[^"]+)"$""", 1, out _) is [{ } key, { } val])
					items[key] = val;
			return items;
		}
	}
}
