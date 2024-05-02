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
using ClangSharp;
using ClangSharp.Interop;
using Type = System.Type;
using Calendar = fx.Calendar;
using Google.Apis.Auth.OAuth2;
using Color = Terminal.Gui.Color;
using static fx.ExploreSession;

//var g = new GodotScene("C:\\Users\\alexm\\source\\repos\\Rogue-Frontier-Godot\\Main\\Mainframe.tscn");
//CppProject.ParseMake("C:\\Users\\alexm\\source\\repos\\IPC\\CMakeLists.txt");

bool expl = true;
if(args is [string cwd]) {
	Environment.CurrentDirectory = cwd;
	expl = true;

}
#if false
foreach(var a in args) {
	Console.WriteLine(a);
}
Console.WriteLine($"cwd: {Environment.CurrentDirectory}");
Console.ReadLine();
#endif

Run:

Application.Init();
var main = new Main();
bool crash = false, restart = false;
try {
	AppDomain.CurrentDomain.ProcessExit += (a, e) => {
		main.ctx.Save();
	};
	
	Application.Top.Add(main.root);
	if(expl)
		main.folder.AddTab("Expl", new ExploreSession(main, Environment.CurrentDirectory).root, true);

	Application.Run();	
} catch (Exception e){
	main.ctx.Save();
	Application.Shutdown();

	Console.Clear();
	Console.WriteLine(e.Message);
	Console.WriteLine(e.StackTrace);

	crash = true;

	/*
	var s = new CancellationTokenSource();
	new Task(() => {
		Console.ReadLine();
		restart = true;
	}, s.Token).Start();

	Thread.Sleep(5000);
	s.Cancel();
	if(restart) goto Run;
	*/

	Console.ReadLine();
	goto Run;
	
	throw;
} finally {
	/*
	if(crash) {
		Console.WriteLine("Restart? (Y/N)");
		if(Console.Read() == 'Y') {
			restart = true;
		}
	}
	if(!restart) {
		Environment.Exit(0);
	}
	*/
	main.ctx.Save();
	Application.Shutdown();
}
goto Run;
public class Main {
	public Ctx ctx;
	public View[] root;

	public View termPrev;
	public TextField term;
	public Folder folder;
	public bool readProc = false;
	public event Action<TermEvent> TermEnter = default;
	public Action<string[]> FilesChanged = _ => { };
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
			TabStop = false,


			ColorScheme = Application.Top.ColorScheme with {
				Focus = new(Color.White, new Color(31, 31, 31))
			}
		};
		term.Leave += (a, e) => term.SetLock();
		term.MouseClickD(new() {
			[Button1Clicked] = e => {
				if(!term.HasFocus) {
					FocusTerm();
					e.Handled = true;
				}
			}
		});
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
				if(termPrev?.IsAdded == true) {
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

		term.SetLock(true);
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
		//https://github.com/HicServices/RDMP/blob/a57076c0d3995e687d15558d21071299b6fb074d/Tools/rdmp/CommandLine/Gui/Windows/RunnerWindows/RunEngineWindow.cs#L176
		//https://github.com/gui-cs/Terminal.Gui/issues/1404
		/*
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
		*/
		foreach(var (name, view) in new Dictionary<string, View>() {
			["Home"] = homeSession.root,
			//["Expl"] = new ExploreSession(this, Environment.CurrentDirectory).root,
			//["Cal"] = new Calendar(this).root,
		}) {
			folder.AddTab(name, view, true);
		}
		folder.SwitchTab();
		var window = new Window() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(0),
			Height = Dim.Fill(0),

			BorderStyle = LineStyle.None,
			//Text = "fx"
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
		IEnumerable<MenuBarItem> GetBarItems () {
			yield return new MenuBarItem("_Fx", [
				new MenuItem("Reload", "", ctx.ResetCommands)
			]) { CanExecute = () => true };
			yield return new MenuBarItem("_Switch", [
				..folder.tabsList.Select(t => new MenuItem(t.name, null, () => folder.FocusTab(t)))
			]) { CanExecute = () => true };
		}
		var windowMenuBar = new MenuBar() {
			Visible = true,
			Enabled = true,
			Menus = [..GetBarItems()],


		};


		var b = new Attribute(Color.White, Color.Black);
		var w = new Attribute(Color.White, new Color(75, 75, 75));
		/*
		windowMenuBar.ColorScheme = windowMenuBar.ColorScheme with {
			Focus = w,
			Normal = b,
			Disabled = b,
			HotFocus = b,
			HotNormal = b
		};
		*/
		windowMenuBar.MenuOpening += (a, e) => {
			windowMenuBar.Menus = [..GetBarItems()];
			var c = e.CurrentMenu;

			if(e.CurrentMenu.Title == "_Switch") {

				//e.CurrentMenu.Children.First(t => t.Title == folder.currentTab.name)
			}
		};
		
		window.ColorScheme = window.ColorScheme with {
			Focus = w,
			Normal = b,
			Disabled = b,
			HotFocus = b,
			HotNormal = b
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
public record OAuth (string email, string clientId, string clientSecret) {
	public OAuth () : this(null, null, null) { }
	[YamlIgnore]
	public ClientSecrets secrets => new(){
		ClientId = clientId,
		ClientSecret = clientSecret
	};
}
public record Fx {
	public static string SAVE_PATH { get; } =
		$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/fx/state.yaml";
	public const string		WORK_ROOT = "%WORKROOT%";

	public HashSet<string> hidden = new();
	public HashSet<string> locked = new();
	public ConcurrentDictionary<string, int> timesOpened = new();
	public Dictionary<string, DateTime> lastOpened = new();

	public List<string> pins = new();
	public string workroot = null; //move to ExplorerSession
	//public List<OAuth> accounts;
	public List<LibraryRoot> libraryData = new();
	public Fx () { }
	public Fx (Ctx ctx) =>	Load(ctx);
	public void Load (Ctx ctx) {
		if(File.Exists(SAVE_PATH)) {
			try {
				var o = new Deserializer().Deserialize<Fx>(File.ReadAllText(SAVE_PATH).Replace(Ctx.USER_PROFILE_MASK, Ctx.USER_PROFILE));
				foreach(var f in GetType().GetFields( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
					f.Copy(this, o);
				}
			} catch(Exception e) {

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
	public static string Anonymize (string path) => path.Replace(USER_PROFILE, USER_PROFILE_MASK);
	public static string USER_PROFILE_MASK => "%USERPROFILE%";
	public static string USER_PROFILE => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
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
	public void Save () {
		Directory.CreateDirectory(Path.GetDirectoryName(Fx.SAVE_PATH));
		File.WriteAllText(Fx.SAVE_PATH, se.Serialize(fx).Replace(USER_PROFILE, USER_PROFILE_MASK));
	}
	public void ResetCommands () {
		var dir = Command.EXECUTABLES_DIR;
		Directory.CreateDirectory(dir);
		var executables = de.Deserialize<Dictionary<string, string>>(File.ReadAllText($"D:/fx/fx/{dir}.yaml"));
		foreach((var name, var path) in executables) {
			File.WriteAllText($"{dir}/{name}", path);
		}
		Commands = de.Deserialize<Command[]>(File.ReadAllText("D:/fx/fx/commands.yaml"));
	}
	public IEnumerable<MenuItem> GetCommands (PathItem item) => Commands
		.Where(c => c.Accept(item.path))
		.Select(c => new MenuItem(c.name, "", () =>
			ExploreSession.RunCmd(c.GetCmd(item.path), c.cd ? item.path : null)
		));
	public delegate IEnumerable<IProp> GetProps (string path);
	public PathItem GetPathItem(string path, GetProps GetProps) => pathData[path] =
		pathData.TryGetValue(path, out var item) ?
			new PathItem(item.local, item.path, new(GetProps(path))) :
			new PathItem(Path.GetFileName(path), path, new(GetProps(path)));
	public PathItem GetCachedPathItem (string path, GetProps GetProps) => pathData[path] =
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

	public static IEnumerable<PathItem> OrderPath(this IEnumerable<PathItem> e, SortMode s) =>
		s.reverse ?
			e.OrderByDescending(s.f) :
			e.OrderBy(s.f);
	public static void SetLock(this TextView v, bool locked = true) {
		v.ReadOnly = locked;
		v.CanFocus = !locked;

		v.SuperView.Enabled = !locked;
	}
	public static void SetLock (this TextField v, bool locked = true) {
		v.ReadOnly = locked;
		v.CanFocus = !locked;

		v.SuperView.Enabled = !locked;
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
	public static void KeyDownD (this View v, Dictionary<uint, Action<Key>> value) =>
		v.KeyDown += (_, e) => {
			var action = value?.GetValueOrDefault((uint)e.KeyCode);
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
		var p = view.GetCurrentPos();
		var (x, y) = (p.X, p.Y);
		var c = new ContextMenu() {
			Position = new(x + col, y + row - 1),
			MenuItems = new MenuBarItem(null, actions)
		};
		c.Show();
		c.ForceMinimumPosToZero = true;
		return c;
	}
}
public delegate Action? KeyEv (Key e);
public delegate Action? MouseEv (MouseEventEventArgs e);