using fx;
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

string save = "fx.json";
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

			File.Delete(save);

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


Command[] commands = [.. XElement.Load("Bindings.xml").Elements().Select(Command.Parse)];

Application.Init();
try {
	Load();
	Application.Run(Create());
} finally {
	Application.Shutdown();
}
Window Create () {


	var favData = new List<PathItem>();
	
	var cwdData = new List<PathItem>();
	var cwdPrev = new Stack<string>();
	var cwdNext = new Stack<string>();
	var cwdRecall = (string)null;
	var procData = new List<ProcItem>();

	var window = new Explorer();
	#region left
	var freqView = new View() {
		X = 0,
		Y = 0,
		Width = Dim.Percent(25)-1,
		Height = Dim.Percent(50)
	};
	
	var freqName = new Label() {
		X = 0,
		Y = 0,
		Width = Dim.Width(freqView),
		Height = Dim.Sized(1),
		Text = "[Recents]",
	};
	var freqList = new ListView() {
		X = 0,
		Y = Pos.Bottom(freqName),
		Width = Dim.Width(freqView),
		Height = Dim.Height(freqView) - 1,
		Source = new ListWrapper(favData),
		ColorScheme = new() {
			HotNormal = new(Color.Black, Color.White),
			Normal = new(Color.White, Color.Blue),
			HotFocus = new(Color.Black, Color.White),
			Focus = new(Color.White, Color.Black),
			Disabled = new(Color.Red, Color.Black)
		}
	};

	var clipView = new View() { };
	#endregion
	var lineLeft = new Window() {
		X = Pos.Right(freqView)-1,
		Y = 0,
		Width = 1,
		Height = Dim.Fill(1),
		
	};
	#region center
	var pathView = new View() {
		X = Pos.Percent(25),
		Y = 0,
		Width = Dim.Percent(50),
		Height = window.Height,
	};
	var pathName = new TextField() {
		X = 0,
		Y = 0,
		Width = Dim.Width(pathView),
		Height = Dim.Sized(1),
		Text = cwd,
		Enabled = false
	};
	var goPrev = new Button("<-") { X = Pos.Right(pathName) -18, };
	var goNext = new Button("->") { X = Pos.Right(goPrev), };
	var goLeft = new Button("..") { X = Pos.Right(goNext), };
	var pathList = new ListView() {
		X = 0,
		Y = Pos.Bottom(goNext),
		Width = Dim.Width(pathView),
		Height = Dim.Height(pathView) - 1,
		Source = new ListWrapper(cwdData),
		ColorScheme = new() {
			HotNormal = new(Color.Black, Color.White),
			Normal = new(Color.White, Color.Blue),
			HotFocus = new(Color.Black, Color.White),
			Focus = new(Color.White, Color.Black),
			Disabled = new(Color.Red, Color.Black)
		}
	};
	#endregion
	var lineRight = new Window() {
		X = Pos.Right(pathView) ,
		Y = 0,
		Width = 1,
		Height = Dim.Fill()
	};
	#region right
	var procView = new View() {
		X = Pos.Right(pathView),
		Y = 0,
		Width = Dim.Percent(25),
		Height = Dim.Fill(0)
	};
	var procName = new Label() {
		X = 0,
		Y = 0,
		Width = Dim.Width(procView),
		Height = 1,
		Text = "[Processes]"
	};
	var procList = new ListView() {
		X = 0,
		Y = 1,
		Width = Dim.Width(procView),
		Height = Dim.Height(procView),
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
	var termField = new TextField() {
		X = 0,
		Y = Pos.Bottom(pathView) - 1,
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
	var tree = new Dictionary<View, List<View>>() {
		[freqView] = [freqName, freqList],
		[pathView] = [pathName, goPrev, goNext, goLeft, pathList],
		[procView] = [procName, procList],
		[window] = [freqView, lineLeft, pathView, lineRight, procView, termField],
	};
	foreach(var (parent, children) in tree) {
		children.ForEach(parent.Add);
	}

	return window;
	void Init () {
		termField.SetFocus();
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
					Key.Space => () => {
						if(!(pathList.SelectedItem < cwdData.Count)) {
							return;
						}
						var item = cwdData[pathList.SelectedItem];
						var t = termField.Text.ToString();
						termField.Text = $"{t[..(t.LastIndexOf(" ") + 1)]}{item.name}";
						termField.SetFocus();
						termField.PositionCursor();
					},
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
						}
						,
						'.' => () => {
							//cwdRecall = cwd;

							ShowContext(cwd);
						},
						'"' => () => {
							if(!GetSelectedItem(out var p) || p.dir) return;
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
							if(!GetSelectedItem(out var p)) return;
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
							if(!GetSelectedItem(out var p)) return;
							ShowContext(p.path);
						},
						'~' => () => {
							//set do-not-read

							//C# solution viewer
							//Git viewer
							if(!GetSelectedItem(out var p)) return;
							
							if(!restricted.Remove(p.path)) {
								restricted.Add(p.path);
							}
							SetCwd(cwd);
							pathList.SetNeedsDisplay();
						},
						'!' => () => {
							//need option to collapse single-directory chains / single-non-empty-directory chains
							if(!GetSelectedItem(out var p)) {
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

			termField.KeyPress += e => {
				e.Handled = true;
				var t = (string)termField.Text;
				((Action)(e.KeyEvent.Key switch {
					Key.Enter when t.MatchArray("cd (?<dest>.+)") is [_, { } dest] => delegate {
						if(!GoPath(Path.GetFullPath(Path.Combine(cwd, dest)))) {
							return;
						}
						termField.Text = "";
					}
					,
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
						if(termField.HasFocus)
							return;
						termField.SetFocus();
					},
					_ => delegate { e.Handled = false; }
				}))();
			};
		}
		void ShowContext(string path) {
			IEnumerable<MenuItem> GetCommon () {
				yield return new MenuItem("Properties", "", () => { });
				if(Directory.Exists(path))
					yield return new MenuItem("Remember", "", () => cwdRecall = path);
				yield return new("Copy Path", "", () => { Clipboard.TrySetClipboardData(path); });
				foreach(var c in commands) {
					if(c.Accept(path))
						yield return new MenuItem(c.name, "", () => Run(c.GetCmd(path)));
				}
			}

			new ContextMenu(5, 5, new([..GetCommon()])) { }.Show();
		}
		bool GetSelectedItem (out PathItem p) {
			p = null;
			if(pathList.SelectedItem >= cwdData.Count) {
				return false;
			}
			p = cwdData[pathList.SelectedItem];
			return true;
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

				var anonymize = true;
				var showCwd = cwd;
				if(anonymize) {
					showCwd = showCwd.Replace(userprofile, "%USERPROFILE%");
				}
				//Anonymize
				pathName.Text = showCwd;
				Console.Title = showCwd;

				pathList.SelectedItem = Math.Min(cwdData.Count, lastIndex.GetValueOrDefault(cwd, 0));

				UpdateListing:
				cwdData.Clear();
				cwdData.AddRange(paths);


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
		Process Run(string cmd) {
			var pi = new ProcessStartInfo("cmd.exe") {
				WorkingDirectory = cwd,
				Arguments = @$"/c ""{cmd}""",
				UseShellExecute = true,
			};
			var p = new Process() { StartInfo = pi };
			p.Start();
			return p;
		}
		void ListProcesses () {
			procData.Clear();
			procData.AddRange(Process.GetProcesses().Where(
				p => p.MainWindowHandle != 0
				).Select(p => new ProcItem(p)));

			procList.SetNeedsDisplay();
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
}
public record Command(string name, string cmd, ITarget[] targets) {
	public static Command Parse(XElement e) {
		var name = e.A("name");
		var cmd = e.A("cmd");
		return new Command(name, cmd, [..e.Element("Target").Elements().Select(e => (ITarget)(e.Name.LocalName switch {
			"Dir" => TargetDir.Parse(e),
			"File" => TargetFile.Parse(e),
			_ => throw new Exception()
		}))]);
	}
	public bool Accept(string path) =>
		targets.Any(t => t.Accept(path));
	public string GetCmd (string path) => string.Format(cmd, path);
}
public interface ITarget {
	public bool Accept(string path);
}
public record TargetFile([StringSyntax("Regex")] string pattern = ".+") : ITarget {
	public static TargetFile Parse(XElement e) {
		return new(e.TA("pattern") ?? $"[^\\.]*\\.{e.TA("ext")}");
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
		return new(e.TA(nameof(pattern)) ?? ".+") {
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
	public override string ToString () => $"{$"{(restricted ? "~" : "")}{name}",-32}{(dir ? "D" : "F")}";
}

public record State (string cwd, HashSet<string> restricted, Dictionary<string, int> lastIndex) {
	public State () : this(null, null, null) { } }