using fx;
using System;
using System.Diagnostics;
using System.Management;
using Terminal.Gui;

string cwd = null;
string save = "fx.txt";
void Save () {
	var state = $"{cwd}";
	File.WriteAllText(save, state);
}
void Load () {
	if(File.Exists(save)) {
		var state = File.ReadAllText(save);
		cwd = state;
	}
}
AppDomain.CurrentDomain.ProcessExit += (a, e) => {
	Save();
};

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
	var procData = new List<ProcItem>();

	var lastIndex = new Dictionary<string, int>();

	var window = new Explorer();

	var freqView = new View() {
		X = 0,
		Y = 0,
		Width = Dim.Percent(25),
		Height = window.Height
	};
	var freqName = new Label() {
		X = 0,
		Y = 0,
		Width = Dim.Width(freqView),
		Height = Dim.Sized(1),
		Text = "[Frequents]",
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


	var pathView = new View() {
		X = Pos.Percent(25),
		Y = 0,
		Width = Dim.Percent(50),
		Height = window.Height,
	};
	var pathName = new Label() {
		X = 0,
		Y = 0,
		Width = Dim.Width(pathView),
		Height = Dim.Sized(1),
		Text = cwd
	};
	var goPrev = new Button("<-") { X = Pos.Right(pathView) - 18, };
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

	Init();
	var tree = new Dictionary<View, List<View>>() {
		[freqView] = [freqName, freqList],
		[pathView] = [pathName, goPrev, goNext, goLeft, pathList],
		[procView] = [procName, procList],
		[window] = [freqView, pathView, procView, termField],
	};
	foreach(var (parent, children) in tree) {
		children.ForEach(parent.Add);
	}

	return window;
	void Init () {
		termField.SetFocus();
		SetCwd(cwd ??= Environment.CurrentDirectory);
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
						'"' => () => {
							if(!GetSelectedItem(out var p) || p.dir) return;
							var d = new Dialog("Edit", []);
							d.Add(new TextView() {
								X = 0,
								Y = 0,
								Width = Dim.Fill(),
								Height = Dim.Fill(),
								Text = File.ReadAllText(p.path)
							});

							Application.Run(d);
						},
						'?' => () => {
							if(!GetSelectedItem(out var p)) return;
							var d = new Dialog("Properties", []) { Text = $"{p.name}" };
							d.KeyPress += e => {
								e.Handled = true;
								d.Running = false;
							};
							Application.Run(d);
						},
						'/' => () => {
							new ContextMenu(pathList, new([
								new MenuItem("Properties", "View properties", () => { })
								
								])).Show();
						},
						'!' => () => {
							if(!GetSelectedItem(out var p)) {
								return;
							}
							if(!favData.Remove(p)) {
								favData.Add(p);
							}
							freqList.SetNeedsDisplay();
						},
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
							f => new PathItem(Path.GetFileName(f), f, true)),
						..Directory.GetFiles(s).Select(
							f => new PathItem(Path.GetFileName(f), f, false))]);

				lastIndex[cwd] = pathList.SelectedItem;
				cwd = s;
				pathName.Text = $"[{s}]";
				cwdData.Clear();
				cwdData.AddRange(paths);

				pathList.SelectedItem = lastIndex.TryGetValue(s, out int index) ? index : 0;


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

public record ProcItem(Process p) {
	public override string ToString () => $"{p.ProcessName,-24}{p.Id, -8}";
}
public record PathItem (string name, string path, bool dir) {
	public override string ToString () => $"{name,-32}{(dir ? "D" : "F")}";
}