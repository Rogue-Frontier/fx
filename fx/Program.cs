using LibGit2Sharp;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Terminal.Gui;
using YamlDotNet.Serialization;
using View = Terminal.Gui.View;
using static SView;
using File = System.IO.File;
using fx;
using Command = fx.Command;
using Folder = fx.Folder;
using System.Collections.Concurrent;
using System.Drawing;
using Attribute = Terminal.Gui.Attribute;

using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
using Type = System.Type;
using Google.Apis.Auth.OAuth2;
using Color = Terminal.Gui.Color;
using static fx.ExploreSession;
using System.Runtime.Serialization;
using Repo = LibGit2Sharp.Repository;
using App = Terminal.Gui.Application;
using static Fx;
using YamlDotNet.Serialization.Utilities;

bool expl = true;
if(args is [string cwd]) {
	Environment.CurrentDirectory = cwd;
	expl = true;
}

Environment.SetEnvironmentVariable("PATH",$"{Environment.GetEnvironmentVariable("PATH")
	}{Path.PathSeparator
	}{Command.EXECUTABLES_PATH
	}");

var path = Environment.GetEnvironmentVariable("PATH");

#if false
foreach(var a in args) {
	Console.WriteLine(a);
}
Console.WriteLine($"cwd: {Environment.CurrentDirectory}");
Console.ReadLine();
#endif

Start:
App.Init();
bool crash = false, restart = false;
var main = new Main();
if(true) {
	if(expl)
		main.folder.AddTab("Expl", new ExploreSession(main, Environment.CurrentDirectory).root, true);
	//main.folder.AddTab("Github", new GithubSession(main, new RepoUrl("godotengine", "godot-builds")).root, true);
}
AppDomain.CurrentDomain.ProcessExit += (a, e) => {
	main.ctx.Save();
};
Run:
App.Top.Add(main.root);

if(false) {

	CmdInfo[] cmdList = [.. CmdStd.GetCmds()];
	Dialog d = new Dialog() {
		Title = "Command Builder",
	};
	d.ColorScheme = App.Top.ColorScheme with {
		Normal = new Attribute(Color.White, Color.Black),
		Focus = new Attribute(Color.White, Color.Black)
	};



	Pos X = 1;
	Pos Y = 1;

	var _cmd = new Button() {
		Text = "$ <command>",
		AutoSize = false,
		X = X,
		Y = Y,
		Width = 12,
		Height = 1,
		NoDecorations = true,
		NoPadding = true,
		TextAlignment = TextAlignment.Left
	};
	d.Add([_cmd]);

	_cmd.MouseClick += (a, e) => {
		CmdStd.ICmdModel result = null;
		var context = new ContextMenu() {
			Position = new(d.Frame.X + _cmd.Frame.X + 1, d.Frame.Y + _cmd.Frame.Y + 1),
			MenuItems = new([ ..from cmd in cmdList select new MenuItem(cmd.name, "", () => {
						result = (CmdStd.ICmdModel)cmd.t.GetConstructor(BindingFlags.Instance | BindingFlags.Public, [])!.Invoke([]);
						_cmd.Width = cmd.name.Length + 2;
						_cmd.Text = $"$ {cmd.name}";
						View[] _v = [..d.Subviews.Take(d.Subviews.IndexOf(_cmd) + 1)];
						d.RemoveAll();
						d.Add(_v);
						Pos X = _cmd.X + 2;
						Pos Y = Pos.Bottom(_cmd);
						foreach(var part in cmd.parts){
							((Action)(part switch {
								FlagInfo flag => () => {
									var b = new CheckBox(){
										Text = flag.att.name,
										X = X,
										Y = Y,
									};
									/*
									var label = new Label(){
										Text = flag.doc,
										AutoSize = false,
										X = 16,
										Y = Y,
										Width = Dim.Fill(),
										Height = flag.doc.Count(c => c == '\n') + 1
									};
									*/
									b.Toggled += (a, e) => flag.field.SetValue(result, e.NewValue);
									d.Add([b, /*label*/]);
									Y = Pos.Bottom(b);
								},
								RadioInfo radio => () => {
									var radioresult = radio.field.FieldType.GetConstructor([]).Invoke([]);
									radio.field.SetValue(result, radioresult);
									FlagInfo[] flags = [..
										from field
										in radio.field.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
										select new FlagInfo(field.GetCustomAttribute<FlagAttribute>()!, field)
									];
									var b = new RadioGroup() {
										X = X,
										Y = Y,
										RadioLabels = [..from flag in flags select flag.field.GetCustomAttribute<FlagAttribute>()!.name],
										Orientation = Orientation.Vertical,
									};
									b.SelectedItemChanged += (a, e) => {
										if(e.PreviousSelectedItem is { }i and not -1) {
											var ff = flags[i].field;
											var t = ff.FieldType;
											//todo: do not modify struct
											ff.SetValue(radioresult, t == typeof(bool) ? false : null);
										}
										if(e.SelectedItem is { }j and not -1){

											var ff = flags[j].field;
											var t = ff.FieldType;
											ff.SetValue(radioresult, t  == typeof(bool) ? true : t.GetConstructor([]).Invoke([]));
										}
									};
									foreach(var flag in flags) {
										var _X = X;
										X += flag.name.Length + 4;
										if(!flag.field.FieldType.IsPrimitive){
											var subfields = flag.field.FieldType.GetFields( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
											var subresult = flag.field.GetValue(radioresult);
											foreach(var sub in subfields){
												var name = sub.GetCustomAttribute<FlagAttribute>()?.name ?? sub.GetCustomAttribute<ArgAttribute>()?.name;
												var b_sub = new CheckBox() {
													Text = name,
													AutoSize = false,
													X = X,
													Y = Y,
													Width = name.Length + 4,
													Height = 1,
												};

												b_sub.Toggled += (a, e) => {
													//TODO: replace with dict lookup
													if(subresult == null){
														flag.field.SetValue(radioresult, subresult = Activator.CreateInstance(flag.field.FieldType));
													}
													sub.SetValue(subresult, e.NewValue);
												};
												d.Add(b_sub);
												X = Pos.Right(b_sub);
											}
										}
										X = _X;
										Y += 1;
									}
									d.Add(b);
								},
								ArgInfo arg => () => {
									/*
									var l = new Label() {
										Text = $"<{arg.name}>:",
										AutoSize = false,
										X = X,
										Y = Y,
										Width = arg.name.Length + 3,
										Height = 1
									};
									*/
									var hint = $"{arg.name}";
									var b = new TextField(){
										Text = hint,
										X = X,
										Y = Y,
										AutoSize = false,
										Width = 64,
										Height = 1
									};
									b.Enter += (a, e) => {
										if(b.Text == hint){
											b.Text = "";
										}
									};
									b.Leave += (a, e) => {
										if(b.Text.Length == 0){
											b.Text = hint;
										}
									};


									d.Add([b]);
									Y = Pos.Bottom(b);
								},
								CmdInfo cmd => () => {

								}

							}))();
						}
						d.SetNeedsDisplay();
					})
			])
		};
		context.Show();
		e.Handled = true;
	};
}
try {
	App.Run();
} catch (Exception e) {
	main.ctx.Save();
	App.Shutdown();

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
	/*
	App.Init();
	goto Run;
	*/
	goto Start;
	
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
	App.Shutdown();
}
goto Start;
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


			ColorScheme = App.Top.ColorScheme with {
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
			[(int)Delete] = _ => {
				if(folder.currentBody == homeSession.root) {
					return;
				}
				folder.RemoveTab();
			},
			['<'] = _ => folder.SwitchTab(-1),
			['>'] = _ => folder.SwitchTab(1),
			[':'] = _ => FocusTerm(window.Focused),
		});
		InitTree([
			[window, folder.root, termBar]
		]);
		IEnumerable<MenuBarItem> GetBarItems () {
			yield return new MenuBarItem("_Fx", [
				new MenuItem("Reload", null, ctx.ResetCommands),
				new MenuItem("Preferences", null, () => {
					folder.AddTab("Pref", new EditSession(this, Fx.SAVE_PATH).root, true, window);
				})
			]) { CanExecute = () => true };
			yield return new MenuBarItem("_Switch", [
				..folder.tabs.Values.Select(t => new MenuItem(t.name, null, () => folder.FocusTab(t)))
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
				e.CurrentMenu.Children.First(t => t.Title == folder.currentTab.name);
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
		$"{Command.ASSEMBLY}/fx_state.yaml";
	public const string		WORK_ROOT = "%WORKROOT%";

	public HashSet<string> hidden = new();
	public HashSet<string> locked = new();
	public string[] fxIgnore = [];
	public List<LibraryRoot> libraryData = new();
	public List<string> pins = new();
	public ConcurrentDictionary<string, ConcurrentDictionary<string, int>> commandCount = new();
	public ConcurrentDictionary<string, int> accessCount = new();
	public Dictionary<string, DateTime> lastOpened = new();

	public string workroot = null; //move to ExplorerSession
	//public List<OAuth> accounts;
	public Fx () { }
	public Fx (Ctx ctx) =>	Load(ctx);
	public void Load (Ctx ctx) {
		if(File.Exists(SAVE_PATH)) {
			try {
				var data = File.ReadAllText(SAVE_PATH).Replace(Ctx.USER_PROFILE_MASK, Ctx.USER_PROFILE);
				var o = new Deserializer().Deserialize<Fx>(data);
				foreach(var f in GetType().GetFields( BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
					f.Copy(this, o);
				}
			} catch(Exception e) {
				throw;
			}
#if false
			catch {
				File.Delete(SAVE_PATH);
			}
#endif
			finally {}
		}
	}

	public record DirIndex (string path, string[] dir, string[] file) {
		DateTime lastWrite = PathItem.GetLastWrite(path);
		public bool IsCurrent => lastWrite >= PathItem.GetLastWrite(path);

		public void Deconstruct (out string[] dir, out string[] file) =>
			(dir, file) = (this.dir, this.file);
	}
}
public record Ctx {
	public static string Anonymize (string path) => path.Replace(USER_PROFILE, USER_PROFILE_MASK);
	[IgnoreDataMember]
	public static string USER_PROFILE_MASK => "%userprofile%";
	[IgnoreDataMember]
	public static string USER_PROFILE => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

	public Cache cache = new();
	private Deserializer de { get; } = new ();
	private Serializer se { get; } = new ();
	public Command[] Commands { get; private set; } = [];
	public Sln sln;
	public Fx fx = new();
	public bool save = true;
	public Ctx () =>
		ResetCommands();
	public void Load () =>
		fx.Load(this);
	public void Save () {
		if(!save) {
			return;
		}
		Directory.CreateDirectory(Path.GetDirectoryName(Fx.SAVE_PATH));
		File.WriteAllText(Fx.SAVE_PATH, se.Serialize(fx).Replace(USER_PROFILE, USER_PROFILE_MASK));
	}
	public void ResetCommands () {
		var loc = Command.EXECUTABLES_PATH;
		Directory.CreateDirectory(loc);
		var executables = de.Deserialize<Dictionary<string, string>>(File.ReadAllText($"{loc}.yaml"));
		foreach((var name, var path) in executables) {
			File.WriteAllText($"{loc}/{name}.{Command.ext}", path);
		}
		Commands = de.Deserialize<Command[]>(File.ReadAllText($"{Command.ASSEMBLY}/fx_commands.yaml"));
	}
	public IEnumerable<MenuItem> GetCommands (PathItem item) => Commands
		.Where(c => c.Accept(item.path))
		.Select(c => new MenuItem(c.name, "", () =>
			RunCmd(c.GetCmd(item.path), c.cd ? item.path : null)
		));
	public delegate IEnumerable<IProp> GetProps (string path);
	public bool IsCurrent(string path, [NotNullWhen(true)] out PathItem? item) =>
		cache.item.TryGetValue(path, out item) && item.isCurrent;
	public PathItem UpdatePathCache (string path, GetProps GetProps) => cache.item[path] = new PathItem(Path.GetFileName(path), path, [.. GetProps(path)]);
	public PathItem GetPathItem(string path, GetProps GetProps) =>
		IsCurrent(path, out var item) ?
			item :
			UpdatePathCache(path, GetProps);
	public (string[] dir, string[] file) GetSubPaths (string path) {
		DirIndex r;
		if(cache.dir.TryGetValue(path, out var entry) && entry.IsCurrent) {
			r = entry;
		} else {
			r = cache.dir[path] = new(path, Directory.GetDirectories(path), Directory.GetFiles(path));
		}
		var (dir, file) = r;
		return (dir, file);
	}
	public record Cache {


		public static string SAVE_PATH { get; } =
			$"{Command.ASSEMBLY}/fx_cache.yaml";

		public ConcurrentDictionary<string, DirIndex> dir = [];
		public ConcurrentDictionary<string, PathItem> item = [];
		public ConcurrentDictionary<string, string[]> content = [];
	}
	public record Git {
		public string root => repo.GetRoot();
		public Repo repo { get; }
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
	public static Lazy<T> Lazy<T> (this Func<T> f) => new Lazy<T>(f);
	public static Lazy<IEnumerable<T>> Lazy<T> (this IEnumerable<T> e) => new Lazy<IEnumerable<T>>(() => e);
	public static IEnumerable<T> MaybeWhere<T>(this IEnumerable<T> e, Func<T, bool>? f) {
		return f == null ? e : e.Where(f);
	}

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