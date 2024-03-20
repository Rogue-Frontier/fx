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


var userprofileHide = "%USERPROFILE";
var userprofile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

var cwd = Environment.CurrentDirectory;
var restricted = new HashSet<string>();
var lastIndex = new Dictionary<string, int>();
var cwdPrev = new Stack<string>();
var cwdNext = new Stack<string>();
(string root, Repository repo, Patch patch) git = default;


var g = (cwdPrev, cwdNext);

var h = g with {
	cwdNext = null
};
var j = new {
	a = 5
};
var l = j with {
	a = 2
};

string save = "fx.state.yaml";
void Save () {
	File.WriteAllText(save, new Serializer().Serialize(new State() {
		cwd = cwd,
		restricted= restricted.Select(r => r.Replace(cwd, "%CWD%")).ToHashSet(),
		lastIndex = lastIndex
	}).Replace(userprofile, userprofileHide));
}
#if false
File.Delete(save);
#endif
void Load () {
	if(File.Exists(save)) {
		try {
			(cwd, restricted, lastIndex) = new Deserializer().Deserialize<State>(File.ReadAllText(save).Replace(userprofileHide, userprofile));

			//File.Delete(save);

			restricted = new(restricted.Select(s => s.Replace("%CWD%", cwd)));
			lastIndex = new(lastIndex);
			//Debug.Assert(lastIndex != null);
		} catch(Exception e) {
		} finally {

		}
	}
}
AppDomain.CurrentDomain.ProcessExit += (a, e) => {
	Save();
};
Command[] commands;
void InitCommands () {
	var bindings = Path.GetFullPath("Bindings.xml");
	
	commands = [.. XElement.Load(bindings).Elements().Select(Command.Parse)];
#if false
	Console.WriteLine(bindings);
	foreach(var item in commands) {
		Console.WriteLine($"{item.name}: {item.exe}");
    }
	Console.ReadLine();
#endif
}
InitCommands();

Application.Init();
try {
	Load();
	var w = Create();
	Application.Top.Add(w);
	Application.Run();
} finally {
	Application.Shutdown();
}

View[] Create () {
	var favData = new List<PathItem>();
	var cwdData = new List<PathItem>();
	var cwdRecall = (string)null;
	var procData = new List<ProcItem>();

	var window = new Window() { 
		X = 0,
		Y = 0,
		Width = Dim.Fill(0),
		Height = Dim.Fill(0),
		Modal = false,
		Border = { BorderStyle = BorderStyle.Single, Effect3D = false, DrawMarginFrame = true },
		TextAlignment = TextAlignment.Left,
		Title = "fx",
	};
	
	var goPrev = new Button("<-") { X = 0, };
	var goNext = new Button("->") { X = 6, };
	var goLeft = new Button("..") { X = 12, };

	var heading = new TextField() {
		X = Pos.Percent(25) + 1,
		Y = 0,
		Width = Dim.Fill() - 1,
		Height =1 ,
		Text = cwd,
		ReadOnly = true
	};
	

	#region left
	var freqWind = new FrameView("Recents", new() { BorderStyle = BorderStyle.Single, DrawMarginFrame = true, Effect3D = false}) {
		X = 0,
		Y = 1,
		Width = Dim.Percent(25),
		Height = Dim.Percent(50) - 1,
		//Title = "Recents"
	};
	var freqList = new ListView() {
		X = 0,
		Y = 0,
		Width = Dim.Fill(),
		Height = Dim.Fill(),
		Source = new ListWrapper(favData),
		ColorScheme = new() {
			HotNormal = new(Color.Black, Color.White),
			Normal = new(Color.White, Color.Blue),
			HotFocus = new(Color.Black, Color.White),
			Focus = new(Color.White, Color.Black),
			Disabled = new(Color.Red, Color.Black)
		}
	};
	var clipWind = new FrameView("Clipboard", new() { BorderStyle = BorderStyle.Single, DrawMarginFrame = true, Effect3D = false }) {
		X = 0,
		Y = Pos.Percent(50),
		Width = Dim.Percent(25),
		Height = Dim.Percent(50),
		//Title = "Clipboard"
	};
	var clipTab = new TabView() {
		X = 0,
		Y = 0,
		Width = Dim.Fill(),
		Height = Dim.Fill()
	};
	/*
	var clipCutList = new ListView() {
		X = 0,
		Y = 0,
		Width = Dim.Fill(),
		Height = Dim.Fill()
	};
	var clipHistList = new ListView() {
		X = 0,
		Y = 0,
		Width = Dim.Fill(),
		Height = Dim.Fill()
	};
	clipTab.AddTab(new("Cut", clipCutList), false);
	clipTab.AddTab(new("History", clipHistList), false);
	*/
	#endregion
	#region center
	var pathWind = new FrameView("Directory") {
		X = Pos.Percent(25),
		Y = 1,
		Width = Dim.Percent(50),
		Height = 28,
	};
	var pathList = new ListView() {
		X = 0,
		Y = 0,
		Width = Dim.Fill(),
		Height = Dim.Fill(),
		AllowsMultipleSelection = true,
		AllowsMarking = true,
		Source = new ListWrapper(cwdData),
		ColorScheme = new() {
			HotNormal = new(Color.Black, Color.White),
			Normal = new(Color.White, Color.Blue),
			HotFocus = new(Color.Black, Color.White),
			Focus = new(Color.White, Color.Black),
			Disabled = new(Color.Red, Color.Black)
		}
	};
	var properties = new FrameView("Properties") {
		X = Pos.Percent(25),
		Y = 29,
		Width = Dim.Percent(50),
		Height = Dim.Fill(1),
	};

	#endregion
	#region right
	var procWind = new FrameView("Processes", new() { BorderStyle = BorderStyle.Single, DrawMarginFrame = true, Effect3D = false }) {
		X = Pos.Percent(75),
		Y = 1,
		Width = Dim.Percent(25),
		Height = Dim.Fill(1),
		//Title = "Programs"
	};
	var procList = new ListView() {
		X = 0,
		Y = 0,
		Width = Dim.Fill(),
		Height = Dim.Fill(),
		Source = new ListWrapper(procData),
		ColorScheme = new() {
			HotNormal = new(Color.Black, Color.White),
			Normal = new(Color.White, Color.Blue),
			HotFocus = new(Color.Black, Color.White),
			Focus = new(Color.White, Color.Black),
			Disabled = new(Color.Red, Color.Black)
		}
	};
	#endregion
	#region bottom
	var term = new TextField() {
		X = 0,
		Y = Pos.AnchorEnd(1),
		Width = Dim.Fill(0),
		Height = 1,
		DesiredCursorVisibility = CursorVisibility.Box,
		ColorScheme = new() {
			Normal = new(Color.Red, Color.Black),
			Focus = new(Color.White, Color.Black),
			HotFocus = new(Color.Red, Color.Black),
			Disabled = new(Color.Red, Color.Black),
			HotNormal = new(Color.Red, Color.Black),
		}
	};
	#endregion
	Init();

	var tree = new Dictionary<View, View[]>() {
		{freqWind, [freqList]},

		
		{clipWind, []},
		{pathWind, [pathList]},
		{procWind, [procList]},
		{window,   [heading, /*goPrev, goNext, goLeft,*/ freqWind, clipWind, pathWind, properties, procWind, term]},
	};
	foreach(var (parent, children) in tree) {
		parent.Add(children);
	}

	var windowMenuBar = new MenuBar() {
		Visible = true,
		Enabled = true,
		Menus = [
			new MenuBarItem("File", [
				new MenuItem("Reload", "", InitCommands)
			]){
				CanExecute = () => true
			}
		]
	};

	return [window, windowMenuBar];
	void Init () {
		term.SetFocus();
		SetCwd(cwd);
		UpdateButtons();
		foreach(var b in new[] { goPrev, goNext, goLeft }) {
			b.Enabled = true;
			b.TabStop = false;
		}
		SetListeners();
		ListProcesses();

		void SetListeners(){
			
			goPrev.MouseClick += e => {
				e.Handled = true;
				((Action)(e.MouseEvent.Flags switch {
					MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
						[.. cwdPrev.Select((p, i) => new MenuItem(p, "", () => GoPrev(i + 1)))])).Show,
					MouseFlags.Button1Clicked => () => e.Handled = GoPrev(),
					_ => delegate { e.Handled = false; }
				}))();
			};
			goNext.MouseClick += e => {
				e.Handled = true;
				((Action)(e.MouseEvent.Flags switch {
					MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
						[.. cwdNext.Select((p, i) => new MenuItem(p, "", () => GoNext(i + 1)))])).Show,
					MouseFlags.Button1Clicked => () => e.Handled = GoNext(),
					_ => delegate { e.Handled = false; }
				}))();
			};
			goLeft.MouseClick += e => {
				e.Handled = true;
				((Action)(e.MouseEvent.Flags switch {
					MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
						[.. Up().Select(p => new MenuItem(p, "", () => GoPath(p)))])).Show,
					MouseFlags.Button1Clicked => () => e.Handled = GoLeft(),
					_ => delegate { e.Handled = false; }
				}))();
			};
			pathList.OpenSelectedItem += e => GoItem();
			pathList.KeyPress += e => {

				if(!pathList.HasFocus) return;

				e.Handled = true;
				(e.KeyEvent.Key switch {
					Key.Enter or Key.CursorRight => () => GoItem(),
					Key.CursorLeft => () => GoPath(Path.GetDirectoryName(cwd)),
					Key.Tab => () => {
						if(!GetItem(out var p)) return;
						term.Text = p.name;
						term.SetFocus();
					}
					,

					/*
					Key.Space => () => {
						if(!(pathList.SelectedItem < cwdData.Count)) {
							return;
						}
						var item = cwdData[pathList.SelectedItem];
						var t = term.Text.ToString();
						term.Text = $"{t[..(t.LastIndexOf(" ") + 1)]}{item.name}";
						term.SetFocus();
						term.PositionCursor();
					},
					*/
					_ => (Action)(e.KeyEvent.KeyValue switch {
						'[' => () => {
							GoPrev();
						},
						']' => () => { 
							GoNext();
						},
						'\\' => () => {
							GoLeft();
						},
						',' => () => {
							//Create new tab
							if(cwdRecall != null) SetCwd(cwdRecall);
						},
						'.' => () => {
							//cwdRecall = cwd;

							ShowContext(cwd);
						},
						':' => () => {
							term.SetFocus();
						},
						'\'' => () => {
							//Copy file
							return;
						},
						'"' => () => {
							if(!GetItem(out var p) || p.dir) return;
							var initial = File.ReadAllText(p.path);
							var d = new Dialog("View", []);
							d.Add(new TextView() {
								X = 0,
								Y = 0,
								Width = Dim.Fill(),
								Height = Dim.Fill(),
								Text = initial,
								ReadOnly = true,
							});


							Application.Run(d);
						},
						'?' => () => {
							if(!GetItem(out var p)) return;
							var d = new Dialog("Properties", []) {
								Text = $"{p.name}"
							};
							d.KeyPress += e => {
								e.Handled = true;
								d.Running = false;
							};
							Application.Run(d);
						},
						'/' => () => {
							if(!GetItem(out var p)) return;
							ShowContext(p.path);
						},
						'~' => () => {
							//set do-not-read

							//C# solution viewer
							//Git viewer
							if(!GetItem(out var p)) return;
							
							if(!restricted.Remove(p.path)) {
								restricted.Add(p.path);
							}
							SetCwd(cwd);
							pathList.SetNeedsDisplay();
						},
						'!' => () => {
							//need option to collapse single-directory chains / single-non-empty-directory chains
							if(!GetItem(out var p)) {
								return;
							}
							if(!favData.Remove(p)) {
								favData.Add(p);
							}
							freqList.SetNeedsDisplay();
						}
						,
						'@' =>  () => {
							//Copy file path


						}
						,
						>='a' and <='z' => () => {
							var c = $"{(char)e.KeyEvent.KeyValue}";
							var index = pathList.SelectedItem;

							bool P ((int index, PathItem item) pair) =>
								pair.index > index && StartsWith(pair);
							bool StartsWith ((int index, PathItem item) pair) =>
								pair.item.name.StartsWith(c, StringComparison.CurrentCultureIgnoreCase);

							var pairs = cwdData.Select((item, index) => (index, item));
							var dest = pairs.FirstOrDefault(P, pairs.FirstOrDefault(StartsWith, (-1, null)));
							if(dest.index == -1) return;
							pathList.SelectedItem = dest.index;
							pathList.SetNeedsDisplay();
						},
						>='A' and <='Z' => () => {
							var index = e.KeyEvent.KeyValue - 'A' + pathList.TopItem;
							if(pathList.SelectedItem == index) {
								GoItem();
								return;
							}
							pathList.SelectedItem = index;
							pathList.OnSelectedChanged();
							pathList.SetNeedsDisplay();
						}
						,
						_ => () => e.Handled = false
					})
				})();
			};
			/*
			TableView listing = new() { 
				X = 0, Y=0, 
				Width = Width, Height = Height-2,
				Style = {   ShowHorizontalHeaderOverline = false, ShowHorizontalHeaderUnderline = false,

				ShowVerticalHeaderLines = false,
				ShowVerticalCellLines = false,
				},

			};

			listing.Table = new();

			listing.Table.Columns.AddRange(new DataColumn[] {
				new("Name", typeof(string)){MaxLength=16 },
				new("app") });
			listing.Table.LoadDataRow(new[] { "world.exe", "Exe" }, true);
			listing.Table.LoadDataRow(new[] { "world.txt", "txt" }, true);
			listing.MouseClick += m =>{
				Title = $"{m.MouseEvent.X} {m.MouseEvent.Y}";
			};
			//Add(listing);
			*/
			//var cd = new Regex("cd (?<dest>.+)");

			procList.KeyPress += e => {
				e.Handled = true;
				((Action)(e.KeyEvent.Key switch {
					Key.CursorLeft or Key.CursorRight => () => { },

					Key.DeleteChar => () => {
						if(!(procList.SelectedItem < procData.Count))
							return;
						var p = procData[procList.SelectedItem];
						p.p.Kill();
						procData.Remove(p);
						procList.SetNeedsDisplay();
					},

					_ => e.KeyEvent.KeyValue switch {

						'.' => ListProcesses,
						_ => delegate { e.Handled = false; }
					}
				}))();
			};

			term.KeyPress += e => {
				e.Handled = true;
				var t = (string)term.Text;
				((Action)(e.KeyEvent.Key switch {
					Key.Enter when t.MatchArray("cd (?<dest>.+)") is [_, { } dest] => delegate {
						if(!GoPath(Path.GetFullPath(Path.Combine(cwd, dest)))) {
							return;
						}
						term.Text = "";
					},
					Key.Enter when t == "cut" => () => {
						var items = GetMarkedItems().ToArray();
					},
					Key.Enter when t.Any() => delegate {

						var cmd = $"{t} & pause";
						var pi = new ProcessStartInfo("cmd.exe") {
							WorkingDirectory = cwd,
							Arguments = @$"/c ""{cmd}""",
							UseShellExecute = true,
						};
						var p = new Process() { StartInfo = pi };
						p.Start();
						IEnumerable<Process> P (int Id) =>
							new ManagementObjectSearcher(
								$"Select * From Win32_Process Where ParentProcessID={Id}")
							.Get().Cast<ManagementObject>()
							.Select(m => Process.GetProcessById(Convert.ToInt32(m["ProcessID"])));
						//Process.GetProcesses().Where(p=>p.)
						/*
						IEnumerable<ProcItem> All() =>
							new ManagementObjectSearcher(
								$"Select * From Win32_Process")
							.Get().Cast<ManagementObject>()
							.Select(m => Process.GetProcessById(Convert.ToInt32(m["ProcessID"])))
							.Select(c => new ProcItem(t, c.Id, c.ProcessName));
						IEnumerable<ProcItem> ToItem (IEnumerable<Process> p) =>
							p.Select(c => new ProcItem(t, c.Id, c.ProcessName));
						*/
						//var conhost = P(p.Id).Single();
						//var ch = P(conhost.pid).ToList();
						//procData.AddRange(ch);

						//procData.AddRange(All());

						//var ch = P(p.Id).Single();
						//var ch2 = P(ch.Id).Single();

						//procList.MoveHome();
						//p.Kill();
					}
					,
					_ => delegate { e.Handled = false; }
				}))();
			};


			window.KeyPress += e => {
				e.Handled = true;
				((Action)(e.KeyEvent.KeyValue switch {
					':' => () => {
						if(term.HasFocus)
							return;
						term.SetFocus();
					},
					_ => delegate { e.Handled = false; }
				}))();
			};
		}
		void ShowContext(string path) {
			IEnumerable<MenuItem> GetCommon () {
				yield return new MenuItem("Properties", "", () => { });
				if(Directory.Exists(path)) {
					yield return new MenuItem("Remember", "", () => cwdRecall = path);
					yield return new MenuItem("Open in Explorer", "", () => Run($"explorer.exe {path}"));
					yield return new MenuItem("New File", "", () => {

						var create = new Button("Create") { Enabled = false };
						var cancel = new Button("Cancel");
						var d = new Dialog("New File", [
							create, cancel
							]) { Width = 32, Height = 5 };
						d.Border.Effect3D = false;
						var name = new TextField() {
							X = 0,
							Y = 0,
							Width = Dim.Fill(2),
							Height = 1,
						};
						name.TextChanging += e => {
							create.Enabled = e.NewText.Any();
						};
						create.Clicked += delegate {
							var f = Path.Combine(path, name.Text.ToString());
							File.Create(f);
							//select this file
							RefreshCwd();
							pathList.SelectedItem = cwdData.FindIndex(p => p.path == f);
							d.RequestStop();
						};
						cancel.Clicked += () => {
							d.RequestStop();
						};

						d.Add(name);
						name.SetFocus();
						Application.Run(d);

					}, canExecute: () => !restricted.Contains(path));
				} else {
					if(git is { patch: { } patch }) {
						var local = path.Replace(git.root + Path.DirectorySeparatorChar, "");
						var p = patch[local];
						if(p?.Status == ChangeKind.Modified) {
							var index = git.repo.Index;
							var entry = index[local];

							bool canUnstage = false;
							bool canStage = true;
							if(entry != null) {


								var blob = git.repo.Lookup<Blob>(entry.Id);
								var b = blob.GetContentText();
								var f = File.ReadAllText(path).Replace("\r", "");
								if(b == f) {
									canUnstage = true;
									canStage = false;
								}
							}
							if(canStage) {
								yield return new MenuItem("Stage", "", () => {
									index.Add(local);
								});
							}
							if(canUnstage) {
								yield return new MenuItem("Unstage", "", () => {
									index.Remove(local);
								});
							}
						}
					}
				}
				yield return new("Copy Path", "", () => { Clipboard.TrySetClipboardData(path); });
				foreach(var c in commands) {
					if(c.Accept(path))
						yield return new(c.name, "", () => Run(c.GetCmd(path)));
				}
			}

			var c = new ContextMenu(5, 5, new([.. GetCommon()])) { };
			c.Show();
		}
		bool GetItem (out PathItem p) {
			p = null;
			if(pathList.SelectedItem >= cwdData.Count) {
				return false;
			}
			p = cwdData[pathList.SelectedItem];
			return true;
		}
		bool GetIndex (out int i) =>
			(i = Math.Min(cwdData.Count - 1, pathList.SelectedItem)) != -1;
		IEnumerable<int> GetMarkedIndex() =>
			Enumerable.Range(0, cwdData.Count).Where(pathList.Source.IsMarked);
		IEnumerable<PathItem> GetMarkedItems () =>
			GetMarkedIndex().Select(i => cwdData[i]);
		void RefreshCwd () {
			SetCwd(cwd);
		}
		void SetCwd (string s) {
			try {

				var paths = new List<PathItem>([
					..Directory.GetDirectories(s).Select(
							f => new PathItem(Path.GetFileName(f), f, true, restricted.Contains(f))),
						..Directory.GetFiles(s).Select(
							f => new PathItem(Path.GetFileName(f), f, false, restricted.Contains(f) ))]);


				/*
				if(s == cwd) {
					goto UpdateListing;
				}
				*/


				lastIndex[cwd] = pathList.SelectedItem;
				cwd = Path.GetFullPath(s);

				if(git is {root:{}root, repo:{}repo}) {
					if(cwd.StartsWith(root)) {
						git = git with { patch = repo.Diff.Compare<Patch>() };
					} else {
						repo.Dispose();
						git = default;
					}
				} else {
					if(Repository.IsValid(cwd)) {
						var re = new Repository(cwd);
						git = (cwd, re, re.Diff.Compare<Patch>());
					}
				}


					cwdData.Clear();
				cwdData.AddRange(paths);
				pathList.Source = new ListWrapper(cwdData);

				var anonymize = true;
				var showCwd = cwd;
				if(anonymize) {
					showCwd = showCwd.Replace(userprofile, "%USERPROFILE%");
				}
				//Anonymize
				heading.Text = showCwd;
				Console.Title = showCwd;

				pathList.SelectedItem = Math.Min(Math.Max(0, cwdData.Count - 1), lastIndex.GetValueOrDefault(cwd, 0));


			} catch(UnauthorizedAccessException e) {

			}
		}
		IEnumerable<string> Up () {
			string
				p = cwd,
				prev = p;
			Up:
			p = Path.Combine(p, "..");
			if(!Directory.Exists(p))
				yield break;
			var full = Path.GetFullPath(p);
			if(full == prev)
				yield break;
			prev = full;
			yield return full;
			goto Up;
		}
		void UpdateButtons () {
			foreach(var (b, st) in new[] { (goLeft, Up()), (goNext, cwdNext), (goPrev, cwdPrev) }) {
				b.Enabled = st.Any();
			}
		}
		bool GoPath (string? dest) {

			var f = Path.GetFileName(cwd);
			if(Directory.Exists(dest) is { } b && b) {
				cwdNext.Clear();
				cwdPrev.Push(cwd);
				SetCwd(dest);
				UpdateButtons();

				if(cwdData.FirstOrDefault(p => p.name == f) is { } item) {
					pathList.SelectedItem = cwdData.IndexOf(item);
				}
			}
			return b;
		}
		bool GoPrev (int times = 1) => Enumerable.Range(0, times).All(_ => {
			if(cwdPrev.TryPop(out var prev) is { } b && b) {
				cwdNext.Push(cwd);
				SetCwd(prev);
				UpdateButtons();
			}
			return b;
		});
		bool GoNext (int times = 1) => Enumerable.Range(0, times).All(_ => {
			if(cwdNext.TryPop(out var next) is { } b && b) {
				cwdPrev.Push(cwd);
				SetCwd(next);
				UpdateButtons();
			}
			return b;
		});
		bool GoLeft (int times = 1) =>
			Enumerable.Range(0, times).All(
				_ => Path.GetFullPath(Path.Combine(cwd, "..")) is { } s && s != cwd && GoPath(s));
		void GoItem () {
			if(!(pathList.SelectedItem < cwdData.Count)) {
				return;
			}
			var i = cwdData[pathList.SelectedItem];
			if(i.restricted) return;
			if(i.dir) {
				GoPath(i.path);
			} else {
				Run(i.path);
			}
		}
		void ListProcesses () {
			procData.Clear();
			procData.AddRange(Process.GetProcesses().Where(
				p => p.MainWindowHandle != 0
				).Select(p => new ProcItem(p)));

			procList.SetNeedsDisplay();
		}
		Process Run (string cmd) {
			//var setPath = $"set 'PATH=%PATH%;{Path.GetFullPath("Programs")}'";

			var cmdArgs = @$"/c {cmd} & pause";
			var pi = new ProcessStartInfo("cmd.exe") {
				WorkingDirectory = cwd,
				Arguments = $"{cmdArgs}",
				UseShellExecute = true,
			};
			var p = new Process() { StartInfo = pi };
			p.Start();
			return p;
		}
	}
}
public static class Parse {
	public static void Init<T>(this XElement source) {
		T dest = (T)Activator.CreateInstance(typeof(T));
		typeof(T).GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
			.Select(property => (property, set: property.SetMethod))
			.Where(pair => pair.set != null)
			.ToList().ForEach(p => {
				p.set.Invoke(dest, [source.Attributes(p.property.Name)]);
			});
	}
	public static string A (this XElement e, string key) => e.Attribute(key).Value;
	public static string? TA (this XElement e, string key) => e.Attribute(key)?.Value;
	public static bool TA (this XElement e, string key, out string value) {
		if(e.Attribute(key)is{Value:{}v}) {
			value = v;
			return true;
		} else {
			value = null;
			return false;
		}
	}
}
public record Command(string name, string exe, ITarget[] targets) {
	public static Command Parse(XElement e) {
		var name = e.A("name");
		var cmd = "";
			cmd =
				e.TA("fmt", out cmd) ?
					$@"""{cmd}""" :
				e.TA("xref", out cmd) ?
					@$"""{Path.GetFullPath(File.ReadAllText(cmd))}"" {{0}}" :
					throw new Exception();
		return new Command(name, cmd, [..e.Element("Target").Elements().Select(e => (ITarget)(e.Name.LocalName switch {
			"Dir" => TargetDir.Parse(e),
			"File" => TargetFile.Parse(e),
			_ => throw new Exception()
		}))]);
	}
	public bool Accept(string path) =>
		targets.Any(t => t.Accept(path));
	public string GetCmd (string target) => string.Format(exe, target);
}
public interface ITarget {
	public bool Accept(string path);
}
public record TargetFile([StringSyntax("Regex")] string pattern = ".+") : ITarget {
	public static TargetFile Parse(XElement e) {
		return new(e.TA("pattern") ?? $"[^\\.]*\\.{e.TA("ext")}$");
	}
	public bool Accept (string path) {
		return !Conditions().Contains(false);
		IEnumerable<bool> Conditions () {
			yield return File.Exists(path);
			var f = Path.GetFileName(path);
			var b = Regex.IsMatch(f, pattern);
			yield return b;
		}
	}
}
public record TargetDir([StringSyntax("Regex")]string pattern = ".+") : ITarget {
	public TargetFile[] subFile = [];
	public TargetDir[] subDir = [];
	public static TargetDir Parse(XElement e) {
		return new(
			e.TA("name") is { }s ?
				Regex.Escape(s) :
				e.TA(nameof(pattern)) ??
				".+") {
			subDir = [.. e.Elements("Dir").Select(TargetDir.Parse)],
			subFile = [.. e.Elements("File").Select(TargetFile.Parse)]
		};
	}
	public bool Accept (string path) {
		return !Conditions().Contains(false);
		IEnumerable<bool> Conditions () {
			yield return Directory.Exists(path);
			yield return Regex.IsMatch(Path.GetFileName(path), pattern);

			var d = Directory.GetDirectories(path);
			yield return subDir.All(s => d.Any(s.Accept));
			
			var f = Directory.GetFiles(path);
			yield return subFile.All(s => f.Any(s.Accept));
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
public record PathItem (string name, string path, bool dir, bool restricted) {
	public string type => dir ? "📁" : "📄";
	public string locked => restricted ? "🔒" : " ";
	public string tag => $"{name}{(dir ? "/" : " ")}";
	public string str => $"{tag,-24}{(restricted ? "X" : " ")}";
	public override string ToString () => str;
}
public record State (string cwd, HashSet<string> restricted, Dictionary<string, int> lastIndex) {
	public State () : this(null, null, null) { }
}