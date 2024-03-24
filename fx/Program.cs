﻿using LibGit2Sharp;
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
using static Terminal.Gui.View;
using Command = fx.Command;
using System.Reflection.Emit;
using Label = Terminal.Gui.Label;
using static Terminal.Gui.TabView;
try {
	Application.Init();
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
	public Folder folder;
	public bool readProc = false;
	private ExploreSession exploreSession;
	public void ReadProc(Process proc) {
		//TerminalView.cs
	}
	public void EditFile (FindLine line) {

	}
	public bool GoPath(string path) {
		return false;
	}
	public void FindIn (string path) {
		var find = new FindSession(this);
		folder.AddTab($"Find({path})", find.root, true);
		find.rootBar.Text = path;
		find.rootBar.ReadOnly = true;
		find.FindDirs();
		find.tree.ExpandAll();
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
		term.OnKeyPress(e => {
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
				},
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
		folder = new Folder(new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(3),
		});
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
		
		foreach(var(name, view) in
		new Dictionary<string, View>() {
			["Home"] = homeSession.root,
			["Expl"] = exploreSession.root
		}) {
			folder.AddTab(name, view);
		}
		var window = new Window() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(0),
			Height = Dim.Fill(0),
			Border = { BorderStyle = BorderStyle.None, Effect3D = false, DrawMarginFrame = false },
			Title = "fx",
		};

		window.AddKey(value: new() {
			['{'] = () => {

				return;
			},
			['}'] = folder.NextTab
		});

		InitTree([
			[window, folder.root, termBar]
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
//public record Session(Fx state, Ctx temp);
public record Fx {
	public const string				SAVE_PATH = "fx.state.yaml";
	public const string				WORK_ROOT = "%WORKROOT%";
	public string					cwd =		Environment.CurrentDirectory;
	public LinkedList<string>		cwdPrev =	new();
	public LinkedList<string>		cwdNext =	new();
	public HashSet<string>			locked =	new();
	public Dictionary<string, int>	lastIndex = new();
	public List<string>				pinned =	new();

	public string					workroot =	null;
	public Fx () { }
	public Fx (Ctx ctx) => Load(ctx);
	public void Load (Ctx ctx) {

		if(File.Exists(SAVE_PATH)) {
			try {
				var o = new Deserializer().Deserialize<Fx>(File.ReadAllText(SAVE_PATH).Replace(Ctx.USER_PROFILE_MASK, ctx.USER_PROFILE));
				foreach(var f in GetType().GetFields( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
					f.Copy(this, o);
				}
				lastIndex = new(lastIndex);
			}
#if false
			catch {
				File.Delete(SAVE_PATH);
			}
#endif
			finally {

			}
		}
	}
}
public record Ctx {
	public const string USER_PROFILE_MASK = "%USERPROFILE%";
	public string USER_PROFILE { get; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	private Deserializer de { get; } = new Deserializer();
	private Serializer se { get; } = new Serializer();
	public Command[] Commands { get; private set; }
	public Sln sln;
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
		public string root => repo.GetRoot();
		public Repository repo { get; }
		public Patch patch { get; private set; }
		public Git (string path) {
			repo = new(path);
		}
		public void RefreshPatch () {
			patch = repo.Diff.Compare<Patch>();
		}
	}




	public record Sln {
		public string root;
		public Sln (string path) {
			root = Path.GetDirectoryName(path);
		}
	}
}
public static class SView {
	public static Delegate Bind<T, U>(this T del, params object[] args) where T: System.Delegate{
		Type[] par = [..typeof(T).GetMethod("Invoke").GetParameters().Select(par => par.ParameterType)];
		Type[] unbound = par[args.Length..];

		var name = typeof(T).GetType().Name;

		typeof(Action<,,,,>).MakeGenericType();
		return null;
	}
	public static (int, int) GetCurrentLoc(this View v) {
		var (x, y) = (0, 0);
		while(v != null) {
			var f = v.Frame;
			(x, y) = (f.X+x, f.Y+y);
			v = v.SuperView;
		}
		return (x,y);
	}
	public static void AddKey (this View v, Dictionary<Key, Action> key = null, Dictionary<int, Action> value = null) =>
		v.KeyPress += e => {
			var action =
				key?.TryGetValue(e.KeyEvent.Key, out var a) == true ?
					a :
				value?.TryGetValue(e.KeyEvent.KeyValue, out a) == true ?
					a :
					null;
			e.Set(action != null);
			action ?.Invoke ();
		};
	public static void AddMouse (this View v, Dictionary<MouseFlags, Action<MouseEventArgs>> dict) =>
		v.MouseClick += e => {
			var action =
				dict.TryGetValue(e.MouseEvent.Flags, out var a) ?
					a :
					null;
			e.Set(action != null);
			action?.Invoke(e);

		};
	public static void OnKeyPress (this View v, KeyEv f) =>
		v.KeyPress += DoRun(d => f(d));
	public static void OnMouseClick (this View v, MouseEv f) =>
		v.MouseClick += DoRun(d=>f(d));

	private static Action<dynamic> DoRun(Func<dynamic, Action> f) =>
		e => Run(e, f);
	private static void Run (dynamic e, Func<dynamic, Action> f) {
		Action a = f(e);
		e.Handled = (a != null);
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
	public static void Set (this MouseEventArgs e, bool b = true) => e.Handled = b;
	public static void Set (this KeyEventEventArgs e, bool b = true) => e.Handled = b;
	/*
	public static void Handle (this MouseEventArgs e, Func<bool> f) => e.Handled = f();
	public static void Handle (this KeyEventEventArgs e, Func<bool> f) => e.Handled = f();
	public static void Handle (this MouseEventArgs e, Func<MouseEventArgs, bool> f) => e.Handled = f(e);
	public static void Handle (this KeyEventEventArgs e, Func<KeyEventEventArgs, bool> f) => e.Handled = f(e);
	*/
	public static void Copy<T> (this FieldInfo field, T dest, T source) => field.SetValue(dest, field.GetValue(source));
}

public record Folder {
	public View root, head, body;
	private List<View> bars = new();
	private List<Tab> tabsList = new();
	private Dictionary<View, Tab> tabs = new();
	public Folder(View root, params(string name, View view)[] tabs) {
		this.root = root;
		head = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1
		};
		body = new View() {
			X = 0,
			Y = 1,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		foreach(var (name, view) in tabs) {
			var tab = new Tab(name, view);
			tabsList.Add(tab);
			this.tabs[view] = tab;
		}
		Refresh();
		/*
		var barLeft = new Label(" ") {
			X = 0,
			Y = 0,
			Width = 1,
			Height = 1
		};
		head.Add(barLeft);

		foreach(var (name, view) in tabs) {
			AddTab(name, view);
		}
		*/
		InitTree([[root, head, body]]);
	}
	public void Refresh () {
		head.RemoveAll();
		var barLeft = new View(" ") {
			X = 0,
			Y = 0,
			Width = 1,
			Height = 1
		};
		head.Add(barLeft);
		foreach(var tab in tabs.Values) {
			tab.Place(this);
		}
		head.SetNeedsDisplay();
	}
	public Tab AddTab(string name, View view, bool show = false) {
		var tab = new Tab(name, view);
		tab.Place(this);
		tabs[view] = tab;
		tabsList.Add(tab);
		if(show) {
			FocusTab(tab);
		}
		return tab;
	}
	public void RemoveTab(View view) {
		if(tabs.Remove(view, out var tab)) {
			int index = tabsList.IndexOf(tab);
			tabsList.Remove(tab);
			if(body.Subviews.SingleOrDefault() is { } v) {
				if(v == view) {
					body.RemoveAll();

					//Show next tab
					if(tabsList.Any()) {
						FocusTab(tabsList[Math.Clamp(index, 0, tabsList.Count - 1)]);
					}
				}else {
					//FocusTab(tabs[v]);
				}
			}
			Refresh();
		}
	}

	public void FocusTab(Tab tab) {
		SelectTab(tab);
		SetBody(tab.view);
	}
	private void SelectTab(Tab tab) {
		foreach(var t in tabs.Values) {
			t.Refresh();
		}
		tab.Refresh(true);
	}
	public void NextTab () {
		if(tabsList.Count == 0) {
			return;
		}
		if(body.Subviews.SingleOrDefault() is { } v) {
			var tab = tabs[v];
			if(tabsList.Count == 1) {
				return;
			}
			var next = tabsList[(tabsList.IndexOf(tab) + 1) % tabsList.Count];
			FocusTab(next);
		} else {
			FocusTab(tabsList[0]);
		}
	}
	public void SetBody (View view) {
		body.RemoveAll();
		body.Add(view);
	}
}
public record Tab {
	public string name;
	public View view;

	public View tab;
	public View leftBar, rightBar;
	public Tab (string name, View view) {
		this.name = name;
		this.view = view;
	}
	public void Place (Folder folder) {
		//context menu
		//- Kill all to left
		//- Kill all to right

		var head = folder.head;
		leftBar = head.Subviews.Last();
		tab = new Lazy<View>(() => {
			var root = new Pad(name) {
				X = Pos.Right(leftBar),
				Y = 0,
				Height = 1,
				Width = name.Length + 3,
			};
			root.MouseClick += e => {
				if(e.MouseEvent.Flags == MouseFlags.Button1Pressed) {
					folder.FocusTab(this);
				}
			};

			var kill = new Pad("[X]") {
				X = Pos.AnchorEnd(3),
				Y = 0,
				Width = 3,
				Height = 1
			};
			kill.Clicked += () => {
				folder.RemoveTab(view);
			};
			InitTree([[root, kill]]);
			return root;
		}).Value;

		rightBar = new View(" ") {
			X = Pos.Right(tab),
			Y = 0,
			Height = 1,
		};
		InitTree([[head, tab, rightBar]]);
		Refresh(folder.body.Subviews.SingleOrDefault() == view);
	}
	public void Refresh(bool open = false) {
		if(open) {
			leftBar.Text = "<";
			rightBar.Text = ">";
		} else {
			leftBar.Text = rightBar.Text = " ";
		}
	}
	public static implicit operator View (Tab t) => t.tab;
}

public delegate Action? KeyEv (KeyEventEventArgs e);
public delegate Action? MouseEv (MouseEventArgs e);