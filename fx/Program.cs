using LibGit2Sharp;
using Microsoft.VisualBasic;
using System;
using System.CodeDom;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Terminal.Gui;
using YamlDotNet;
using YamlDotNet.Serialization;
using View = Terminal.Gui.View;
using static SView;
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
using Folder = fx.Folder;
using System.Collections.Concurrent;
using System.Drawing;
using Attribute = Terminal.Gui.Attribute;

using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
var a = fx.Monitor.GetOpenWindows();
var b = 0;
try {
	Application.Init();
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

	public View termPrev;
	public TextField term;
	public Folder folder;
	public bool readProc = false;
	private ExploreSession exploreSession;
	public Action<TermEvent> TermEnter = default;
	public void ReadProc(Process proc) {
		//TerminalView.cs
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
			TabStop = false
		};
		term.Leave += (a, e) => {
			term.SetLock();
		};
		term.MouseClick += (a, e) => {
			if(e.MouseEvent.Flags == Button1Clicked) {
				if(!term.HasFocus) {
					FocusTerm();
					e.Handled = true;
				}
			}
		};
		term.KeyDownD(new() {
			[(int)Esc] = e => {
				term.SetLock();
				e.Handled = true;
			},
			[(int)Enter] = e => {
				var ev = new TermEvent(term);
				foreach(var listen in TermEnter.GetInvocationList()) {
					listen.DynamicInvoke(ev);
					if(ev.Handled) {
						e.Handled = true;
						return;
					}
				}
			},
			[(int)CursorUp] = e => e.Handled = true,
			[(int)CursorDown] = e => e.Handled = true,
			[(int)Backspace] = e => {
				if(term.Text.Any()) {
					e.Handled = false;
					return;
				}
				term.SetLock(true);
				term.SuperView.SetFocus();

				if(termPrev.IsAdded) {
					termPrev.SetFocus();
				}

				e.Handled = true;
			}
		});
		var termBar = new Lazy<View>(() => {
			var view = new View() {
				Title = "Command",
				X = 0,
				Y = Pos.AnchorEnd(3),
				Width = Dim.Fill(),
				Height = 3,
				CanFocus = false,
				BorderStyle = LineStyle.Single,
			};
			InitTree([view, term]);
			return view;
		}).Value;
		var homeSession = new HomeSession(this);
		//Add context button to switch to Find with root at dir
		//Add button to treat dir as root
		//Add option for treeview
		folder = new Folder(new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(3),
		});
		exploreSession = new ExploreSession(this, Environment.CurrentDirectory);


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
		foreach(var(name, view) in new Dictionary<string, View>() {
			["Home"] = homeSession.root,
			["Expl"] = exploreSession.root,
		}) {
			folder.AddTab(name, view);
		}
		folder.SwitchTab();
		var window = new Window() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(0),
			Height = Dim.Fill(0),
			BorderStyle = LineStyle.Single,
			Title = "fx",
		};
		window.KeyDownD(new() {
			[(int)Delete] = _ => folder.RemoveTab(),
			['<'] = _ => folder.SwitchTab(-1),
			['>'] = _ => folder.SwitchTab(1),
			[':'] = _ => FocusTerm(window.Focused),
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
			}]
		};
		root = [window, windowMenuBar];
	}
	public void FocusTerm (View termPrev = null) {
		if(term.ReadOnly) {
			term.SetLock(false);
		}
		term.SetFocus();
		this.termPrev = termPrev;
	}
}
//public record Session(Fx state, Ctx temp);
public record Fx {
	public const string		SAVE_PATH = "fx.state.yaml";
	public const string		WORK_ROOT = "%WORKROOT%";
	public HashSet<string>	locked =	new();
	public List<string>		pins =	new();
	public string			workroot =	null; //move to ExplorerSession

	public List<Library>	libraryData = new();
	public Fx () { }
	public Fx (Ctx ctx) =>	Load(ctx);
	public void Load (Ctx ctx) {
		if(File.Exists(SAVE_PATH)) {
			try {
				var o = new Deserializer().Deserialize<Fx>(File.ReadAllText(SAVE_PATH).Replace(Ctx.USER_PROFILE_MASK, ctx.USER_PROFILE));
				foreach(var f in GetType().GetFields( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
					f.Copy(this, o);
				}
			}
#if false
			catch {
				File.Delete(SAVE_PATH);
			}
#endif
			finally {}
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
	public ConcurrentDictionary<string, PathItem> pathData = new();
	public Ctx () =>
		ResetCommands();
	public void Load () =>
		fx.Load(this);
	public void Save () =>
		File.WriteAllText(Fx.SAVE_PATH, se.Serialize(fx).Replace(USER_PROFILE, USER_PROFILE_MASK));
	public void ResetCommands () {

		var dir = Command.EXECUTABLES_DIR;
		Directory.CreateDirectory(dir);
		var executables = de.Deserialize<Dictionary<string, string>>(File.ReadAllText($"{dir}.yaml"));
		foreach((var name, var path) in executables) {
			File.WriteAllText($"{dir}/{name}", path);
		}
		Commands = de.Deserialize<Command[]>(File.ReadAllText("Commands.yaml"));
	}
	public IEnumerable<MenuItem> GetCommands (PathItem item) =>
		Commands
			.Where(c => c.Accept(item.path))
			.Select(c => new MenuItem(c.name, "", () => ExploreSession.RunCmd(c.GetCmd(item.path))));

	public delegate IEnumerable<IProp> GetProps (string path);
	public PathItem GetPathItem(string path, GetProps GetProps) =>
		pathData[path] =
			pathData.TryGetValue(path, out var item) ?
				new PathItem(item.local, item.path, new(GetProps(path))) :
				new PathItem(Path.GetFileName(path), path, new(GetProps(path)));
	public PathItem GetCachedPathItem (string path, GetProps GetProps) =>
		pathData[path] =
			pathData.TryGetValue(path, out var item) ?
				item :
				new PathItem(Path.GetFileName(path), path, new(GetProps(path)));
	public record Git {
		public string root => repo.GetRoot();
		public Repository repo { get; }
		public Patch patch { get; private set; }
		public Git (string path) =>
			repo = new(path);
		public void RefreshPatch ()=>
			patch = repo.Diff.Compare<Patch>();
	}
	public record Sln {
		public string root;
		public Sln (string path) {
			root = Path.GetDirectoryName(path);
		}
	}
}
public static class SView {
	public static void SetLock(this TextView v, bool locked = true) {
		v.ReadOnly = locked;
		v.CanFocus = !locked;
	}
	public static void SetLock (this TextField v, bool locked = true) {
		v.ReadOnly = locked;
		v.CanFocus = !locked;
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
	public static Point GetCurrentPos (this View v) {
		var (x, y) = (0, 0);
		while(v != null) {
			var f = v.Frame;
			(x, y) = (f.X + x, f.Y + y);
			v = v.SuperView;
		}
		return new(x, y);
	}
	public static void KeyDownD (this View v, Dictionary<int, Action<Key>> value) =>
		v.KeyDown += (_, e) => {
			var action = value?.GetValueOrDefault((int)e.KeyCode);
			e.Handled = action != null;
			action ?.Invoke (e);
		};
	public static void MouseEvD (this View v, Dictionary<int, Action<MouseEventEventArgs>> dict) =>
		v.MouseEvent += (_, e) => {
			var action = dict.GetValueOrDefault((int)e.MouseEvent.Flags);
			e.Handled = action != null;
			action?.Invoke(e);
		};
	public static void MouseClickD (this View v, Dictionary<MouseFlags, Action<MouseEventEventArgs>> dict) =>
		v.MouseClick += (_, e) => {
			var action = dict.GetValueOrDefault(e.MouseEvent.Flags);
			e.Handled = action != null;
			action?.Invoke(e);
		};
	public static void KeyDownF (this View v, KeyEv f) =>
		v.KeyDown += DoRun<Key>(d => f(d));
	public static void MouseEvF (this View v, MouseEv f) =>
		v.MouseEvent += DoRun<MouseEventEventArgs>(d=>f(d));
	private static EventHandler<T> DoRun<T>(Func<T, Action> f) =>
		(_, e) => Run(e, d => f(d));
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
	/*
	public static void Handle (this MouseEventArgs e, Func<bool> f) => e.Handled = f();
	public static void Handle (this KeyEventEventArgs e, Func<bool> f) => e.Handled = f();
	public static void Handle (this MouseEventArgs e, Func<MouseEventArgs, bool> f) => e.Handled = f(e);
	public static void Handle (this KeyEventEventArgs e, Func<KeyEventEventArgs, bool> f) => e.Handled = f(e);
	*/
	public static void Copy<T> (this FieldInfo field, T dest, T source) => field.SetValue(dest, field.GetValue(source));
	public static ContextMenu ShowContext (this View view, MenuItem[] actions, int row = 0, int col = 0) {
		var (x, y) = view.GetCurrentLoc();
		var c = new ContextMenu() {
			Position = new(x + col, y + row),
			MenuItems = new MenuBarItem(null, actions)
		};
		c.Show();
		c.ForceMinimumPosToZero = true;
		return c;
	}
}
public delegate Action? KeyEv (Key e);
public delegate Action? MouseEv (MouseEventEventArgs e);