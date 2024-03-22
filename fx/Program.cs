using LibGit2Sharp;
using Microsoft.VisualBasic;
using System;
using System.CodeDom;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Management;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Terminal.Gui;
using YamlDotNet;
using YamlDotNet.Serialization;
using View = Terminal.Gui.View;
using static SView;
using Terminal.Gui.Trees;
using System.IO;
using System.IO.Compression;
using IWshRuntimeLibrary;
using File = System.IO.File;
using fx;
using System.Reflection.Metadata;
Application.Init();
try {
	Application.UseSystemConsole = true;

	var main = new Main();
	AppDomain.CurrentDomain.ProcessExit += (a, e) => {
		main.ctx.Save();
	};

	Application.Top.Add(main.root);
	Application.Run();
} finally {
	Application.Shutdown();
}
public class Main {
	public Ctx ctx;
	public View[] root;
	public TextField term;
	public TabView tabs;
	public bool readProc = false;


	private ExploreSession exploreSession;

	public void ReadProc(Process proc) {

	}
	public void EditFile (FindLine line) {

	}
	public bool GoPath(string path) {
		return false;
	}
	public void FindIn (string path) {
		var find = new FindSession(this);
		tabs.AddTab(new TabView.Tab("Find", find.root), true);
		find.rootBar.Text = path;
		find.FindDirs();
		find.tree.ExpandAll();
	}
	public void AddTab(TabView.Tab tab) {
		tabs.AddTab(tab, false);
	}

	public void FocusTerm () {
		term.SetFocus();
	}
	public Main () {
		ctx = new Ctx();
		ctx.Load();

		var fx = ctx.fx;

		term = new TextField() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1,
			DesiredCursorVisibility = CursorVisibility.Box,
			ColorScheme = new() {
				Normal = new(Color.Red, Color.Black),
				Focus = new(Color.White, Color.Black),
				HotFocus = new(Color.Red, Color.Black),
				Disabled = new(Color.Red, Color.Black),
				HotNormal = new(Color.Red, Color.Black),
			},
		};

		term.AddKeyPress(e => {
			var t = (string)term.Text;
			return e.KeyEvent.Key switch {
				Key.Enter when t.MatchArray("cd (?<dest>.+)") is [_, { } dest] => delegate {
					if(!GoPath(Path.GetFullPath(Path.Combine(fx.cwd, dest)))) {
						return;
					}
					term.Text = "";
				}
				,
				Key.Enter when t == "cut" => () => {
					var items = exploreSession.GetMarkedItems().ToArray();
				}
				,
				Key.Enter when t.Any() => delegate {
					var cmd = $"{t}";
					var pi = new ProcessStartInfo("cmd.exe") {
						WorkingDirectory = fx.cwd,
						Arguments = @$"/c {cmd} & pause",
						UseShellExecute = !readProc,
						RedirectStandardOutput = readProc,
						RedirectStandardError = readProc,
					};
					var p = new Process() {
						StartInfo = pi
					};
					Task.Run(() => {
						p.Start();
						if(readProc) {
							ReadProc(p);
						}
					});
				},
				_ => null
			};
		});
		var termBar = new Lazy<View>(() => {
			var view = new FrameView("Term", new Border() { BorderStyle = BorderStyle.Single, Effect3D = false, DrawMarginFrame = true }) {
				X = 0,
				Y = Pos.AnchorEnd(3),
				Width = Dim.Fill(),
				Height = 3
			};
			InitTree([view, term]);
			return view;
		}).Value;
		var homeSession = new HomeSession();
		//Add context button to switch to Find with root at dir
		//Add button to treat dir as root
		//Add option for treeview
		tabs = new Tabs(this).root;
		exploreSession = new ExploreSession(this);
		//https://github.com/HicServices/RDMP/blob/a57076c0d3995e687d15558d21071299b6fb074d/Tools/rdmp/CommandLine/Gui/Windows/RunnerWindows/RunEngineWindow.cs#L176
		//https://github.com/gui-cs/Terminal.Gui/issues/1404
		var termView = new Lazy<View>(() => {
			var view = new View();
			var text = new TextView() {
				X = 0,
				Y = 0,
				Width = Dim.Fill(),
				Height = Dim.Fill(),
				Multiline = true,
				PreserveTrailingSpaces = true,
				ReadOnly = true
			};
			InitTree([view, text]);
			return view;
		}).Value;
		new List<ITab>([homeSession, exploreSession]).ForEach(s => AddTab(s.GetTab()));

		var window = new Window() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(0),
			Height = Dim.Fill(0),
			Border = { BorderStyle = BorderStyle.None, Effect3D = false, DrawMarginFrame = false },
			Title = "fx",
		};

		InitTree([
			[window, tabs, termBar]
		]);
		var windowMenuBar = new MenuBar() {
			Visible = true,
			Enabled = true,
			Menus = [
				new MenuBarItem("File", [
				new MenuItem("Reload", "", ctx.ResetCommands)
			]){
				CanExecute = () => true
			}
			]
		};
		root = [window, windowMenuBar];
	}
}
public record FindFilter(Regex filePattern, Regex linePattern, string replace) {
	public bool Accept (FindFile f) => filePattern?.Match(f.name).Success ?? true;
	public bool Accept (string line) => linePattern?.Match(line).Success ?? true;

	public void Replace (string line) => linePattern.Replace(line, replace);
}
public record TreeFinder () : ITreeBuilder<IFind> {
	public bool SupportsCanExpand => true;
	public bool CanExpand (IFind f) => f switch {
		FindDir or FindFile => f.GetLeaves().Any(),
		_ => false
	};
	public IEnumerable<IFind> GetChildren (IFind f) {
		return f.GetChildren();
	}
}
public interface IFind {
	bool IsMatch () => false;
	IEnumerable<IFind> GetChildren ();
	IEnumerable<FindLine> GetLeaves ();
}
public record FindDir (FindFilter filter, string path) : IFind {
	public string name => Path.GetFileName(path);


	private IEnumerable<IFind> GetDescendants(bool excludeFiles = false) {
		foreach(var d in Directory.GetDirectories(path)) {
			var fd = new FindDir(filter, d);
			if(fd.GetLeaves().Any())
				yield return fd;
		}
		if(excludeFiles) {
			yield break;
		}
		foreach(var f in Directory.GetFiles(path)) {
			var ff = new FindFile(filter, f);
			if(filter.Accept(ff) && ff.GetLeaves().Any())
				yield return ff;
		}
	}
	public IEnumerable<IFind> GetChildren () => GetDescendants(filter.filePattern == null);
	public IEnumerable<FindLine> GetLeaves () {
		foreach(var c in GetDescendants()) {
			foreach(var l in c.GetLeaves()) {
				yield return l;
			}
		}
	}
}
public record FindFile (FindFilter filter, string path) : IFind {
	public string name => Path.GetFileName(path);
	public IEnumerable<IFind> GetChildren () {
		if(filter.linePattern != null) {
			return GetLeaves();
		}
		return Enumerable.Empty<IFind>();
		
	}
	public IEnumerable<FindLine> GetLeaves () {
		int row = 0;
		foreach(var l in File.ReadLines(path)) {
			row++;
			var m = filter.linePattern?.Match(l);
			if(m is { Success:true })
				yield return new FindLine(path, row, m.Index, l.Replace("\t", "    "), m.Value);
			else if(m == null) {
				yield return null;
			}
		}
	}
}
public record FindLine (string path, int row, int col, string line, string capture) : IFind {
	public IEnumerable<IFind> GetChildren () {
		yield break;
	}
	public IEnumerable<FindLine> GetLeaves () {
		yield return this;
	}
}
public interface IProp {
	string id { get; }
	string desc { get; }
}

public static class Props {
	public static IProp
		IS_LOCKED = new Prop("locked", "Locked"),
		IS_DIRECTORY = new Prop("directory", "Directory"),
		IS_STAGED = new Prop("gitStagedChanges", "Staged Changes"),
		IS_UNSTAGED = new Prop("gitUnstagedChanges", "Unstaged Changes"),
		IS_SOLUTION = new Prop("visualStudioSolution", "Visual Studio Solution"),
		IS_REPOSITORY = new Prop("gitRepository", "Git Repository"),
		IS_ZIP = new Prop("zipArchive", "Zip Archive");
	public static IPropGen
		IS_LINK_TO = new PropGen<string>("link", dest => $"Link To: {dest}"),
		IN_REPOSITORY = new PropGen<Repository>("gitRepositoryItem", repo => $"In Repository: {repo.Info.Path}"),
		IN_LIBRARY = new PropGen<Library>("libraryItem", library => $"In Library: {library.name}"),
		IN_SOLUTION = new PropGen<string>("solutionItem", solutionPath => $"In Solution: {solutionPath}"),
		IN_ZIP = new PropGen<string>("zipItem", zipRoot => $"In Zip: {zipRoot}");
		
}
public record Prop (string id, string desc) : IProp {
}
public record Prop<T>(string id, string desc, T data) : IProp {

}

public interface IPropGen {
	string id { get; }
}
public record PropGen<T> (string id, PropGen<T>.GetDesc getDesc) : IPropGen {
	public delegate string GetDesc (T args);
	public Prop<T> Generate (T args) => new Prop<T>(id, getDesc(args), args);
}
public record Command() {
	public string name;
	public string exe;
	public TargetAny targetAny;
	public string fmt { set => exe = @$"""{value}"""; }
	public string program { set => exe = @$"""{File.ReadAllText($"Programs/{value}")}"" {{0}}"; }

	public bool Accept (string path) => targetAny.Accept(path);
	public string GetCmd (string target) => string.Format(exe, target);
}
public interface ITarget {
	public bool Accept(string path);
}
public record TargetAny() : ITarget {
	public TargetDir[] dir = [];
	public TargetFile[] file = [];
	public bool Accept (string path) =>
		Directory.Exists(path) ?
			dir.Any(d => d.Accept(path)) :
			file.Any(f => f.Accept(path));
}
public record TargetFile() : ITarget {
	[StringSyntax("Regex")]
	public string pattern = ".+";
	public string ext { set => pattern = $"[^\\.]*\\.{value}$"; }
	public bool Accept (string path) {
		return !Conditions().Contains(false);
		IEnumerable<bool> Conditions () {
			yield return File.Exists(path);
			yield return Regex.IsMatch(Path.GetFileName(path), pattern);
		}
	}
}
public record TargetDir() : ITarget {
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
		args = [..BindTargets()];
		return args.Length == targets.Length;
		IEnumerable<string> BindTargets() {
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
public record ProcItem(Process p) {
	public override string ToString () => $"{p.ProcessName,-24}{p.Id, -8}";
}
public record PathItem (string local, string path, bool dir, HashSet<IProp> properties = null, string linkTarget = null) {
	public readonly Dictionary<string, IProp> propertyDict =
		(properties ?? new())
		.ToDictionary(p => p.id, p => p);
	public bool dir => propertyDict.ContainsKey("directory");
	public bool isLocked => propertyDict.ContainsKey("locked");

	//public string type => dir ? "📁" : "📄";
	//public string locked => restricted ? "🔒" : " ";
	public string tag => $"{local}{(dir ? "/" : " ")}";
	public string str => $"{tag,-24}{(isLocked ? "X" : " ")}";
	public override string ToString () => str;
}
public record GitItem (string path, PatchEntryChanges patches, bool staged) {
	public override string ToString () => $"{Path.GetFileName(path)}";
}
public record Library(string name) {
	public List<Link> links = new();
	public record Link(string path, bool visible, bool expand);
}

public record Session(Fx state, Ctx temp);
public record Fx {
	public const string SAVE_PATH = "fx.state.yaml";

	public string cwd = Environment.CurrentDirectory;
	public Stack<string> cwdPrev = new();
	public Stack<string> cwdNext = new();
	public HashSet<string> locked = new();
	public Dictionary<string, int> lastIndex = new();
	public List<string> pinned = new();

	public Fx () { }
	public Fx (Ctx ctx) => Load(ctx);
	public void Load (Ctx ctx) {

		if(File.Exists(SAVE_PATH)) {
			try {
				var o = new Deserializer().Deserialize<Fx>(File.ReadAllText(SAVE_PATH).Replace(Ctx.USER_PROFILE_MASK, ctx.USER_PROFILE));
				foreach(var f in GetType().GetFields()) {
					f.Copy(this, o);
				}
				lastIndex = new(lastIndex);
			} finally {

			}
		}
	}
}
public record Ctx {
	public const string USER_PROFILE_MASK = "%USERPROFILE%";
	public string USER_PROFILE { get; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	public Git git;
	public Command[] Commands { get; private set; }
	private Deserializer de { get; } = new Deserializer();
	private Serializer se { get; } = new Serializer();

	public Fx fx = new();
	public Ctx () {
		ResetCommands();
	}
	public void Load () {
		fx.Load(this);
	}
	public void Save () {
		File.WriteAllText(Fx.SAVE_PATH, se.Serialize(fx).Replace(USER_PROFILE, USER_PROFILE_MASK));
	}
	public void ResetCommands () {
		var d = Directory.CreateDirectory($"Programs");
		var programs = de.Deserialize<Dictionary<string, string>>(File.ReadAllText($"{d.FullName}.yaml"));
		foreach((var name, var path) in programs) {
			File.WriteAllText($"{d.FullName}/{name}", path);
		}
		Commands = de.Deserialize<Command[]>(File.ReadAllText("Commands.yaml"));
	}
	public record Git {
		public string root => repo.Info.Path;
		public Repository repo { get; }
		public Patch patch { get; private set; }
		public Git (string path) {
			repo = new(path);
			RefreshPatch();
		}
		public void RefreshPatch () {
			patch = repo.Diff.Compare<Patch>();
		}
	}
}
public record Config (Dictionary<string, string> programs = null, Command[] commands = null) {
	public Config () : this(null, null) { }
}
public static class SView {
	public static void AddKeyPress (this View v, KeyEvent f) {
		/*
		v.KeyPress += e => {
			var a = f(e);
			e.Handled = a != null;
			a?.Invoke();
		};
		*/
		v.KeyPress += e => Run(e, d => f(d));
	}
	public static void AddMouseClick (this View v, MouseEvent f) {
		/*
		v.MouseClick += e => {
			var a = f(e);
			e.Handled = a != null;
			a?.Invoke();
		};
		*/
		v.MouseClick += e => Run(e, d => f(d));
	}
	private static void Run (dynamic e, Func<dynamic, Action> f) {
		Action a = f(e);
		e.Handled = a != null;
		a?.Invoke();
	}
	public static void InitTree (params View[][] tree) {
		foreach(var row in tree) {
			row[0].Add(row[1..]);
		}
	}
	public static void ForTuple<T, U> (T action, params U[] arr) where T : Delegate {

		var fields = typeof(U).GetFields();
		Type[] pars = [.. fields.Select(field => field.GetType())];
		foreach(var row in arr) {
			object[] args = [.. fields.Select(field => field.GetValue(row))];
			action.DynamicInvoke(args);
		}
	}

	public static void Copy<T> (this FieldInfo field, T dest, T source) => field.SetValue(dest, field.GetValue(source));
}
public delegate Action? KeyEvent (View.KeyEventEventArgs e);
public delegate Action? MouseEvent (View.MouseEventArgs e);