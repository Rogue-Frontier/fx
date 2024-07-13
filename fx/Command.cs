using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace fx;
public record Config (Dictionary<string, string> programs = null, Command[] commands = null) {
	public Config () : this(null, null) { }
}
public record Command () {
	public string name;
	public string exe;
	public TargetAny targetAny;

	public bool cd = false;

	public static string ASSEMBLY = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
	public static string EXECUTABLES_PATH = Path.GetFullPath($"{ASSEMBLY}/executables");
	public string fmt { set => exe = @$"""{value}"""; }
	public string program { set => exe = @$"""{File.ReadAllText($"{EXECUTABLES_PATH}/{value}")}"" {{0}}"; }
	public bool Accept (string path) => targetAny.Accept(path);
	public string GetCmd (string target) => $"{string.Format(exe, target)}";
}
public interface ITarget {
	public bool Accept (string path);
}
public record TargetAny () : ITarget {
	public TargetDir[] dir = [];
	public TargetFile[] file = [];
	public bool Accept (string path) =>
		Directory.Exists(path) ?
			dir.Any(d => d.Accept(path)) :
			file.Any(f => f.Accept(path));
}
public record TargetFile () : ITarget {
	[StringSyntax("Regex")]
	public string pattern = ".+";
	public string ext { set => pattern = $"[^\\.]*\\.({Regex.Escape(value)})$"; }

	public string name { set => pattern = $"({Regex.Escape(value)})$"; }
	public bool Accept (string path) {
		return !Conditions().Contains(false);
		IEnumerable<bool> Conditions () {
			yield return File.Exists(path);
			yield return Regex.IsMatch(Path.GetFileName(path), pattern);
		}
	}
}
public record TargetDir () : ITarget {
	[StringSyntax("Regex")]
	public string pattern = ".+";
	public string name { set => pattern = Regex.Escape(value); }
	public TargetFile[] file = [];
	public TargetDir[] dir = [];
	public bool Accept (string path) {
		return !Conditions().Contains(false);
		IEnumerable<bool> Conditions () {
			yield return Directory.Exists(path);
			yield return Regex.IsMatch(Path.GetFileName(path), pattern);
			var d = Directory.GetDirectories(path);
			yield return dir.All(s => d.Any(s.Accept));
			var f = Directory.GetFiles(path);
			yield return file.All(s => f.Any(s.Accept));
		}
	}
}
public record TargetCombo (ITarget[] targets) {
	public bool Accept (string[] paths, out string[] args) {
		var remaining = new HashSet<string>(paths);
		args = [.. BindTargets()];
		return args.Length == targets.Length;
		IEnumerable<string> BindTargets () {
			foreach(var t in targets) {
				if(remaining.FirstOrDefault(t.Accept) is { } p) {
					remaining.Remove(p);
					yield return p;
				} else {
					yield break;
				}
			}
		}
	}
}