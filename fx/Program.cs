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
var Prop = new {
	IS_LOCKED =		new Prop("locked",					"Locked"),
	IS_DIRECTORY =	new Prop("directory",				"Directory"),
	IS_STAGED =		new Prop("gitStagedChanges",		"Staged Changes"),
	IS_UNSTAGED =	new Prop("gitUnstagedChanges",		"Unstaged Changes"),
	IS_SOLUTION =	new Prop("visualStudioSolution",	"Visual Studio Solution"),
	IS_REPOSITORY =	new Prop("gitRepository",			"Git Repository"),
	IS_ZIP =		new Prop("zipArchive",				"Zip Archive"),

	IS_LINK_TO =	new Prop<string>.Gen("link", dest => $"Link To: {dest}"),
	IN_REPOSITORY =	new Prop<Repository>.Gen("gitRepositoryItem", repo => $"In Repository: {repo.Info.Path}"),
	IN_LIBRARY =	new Prop<Library>.Gen("libraryItem", library => $"In Library: {library.name}"),
	IN_SOLUTION =	new Prop<string>.Gen("solutionItem", solutionPath => $"In Solution: {solutionPath}"),
	IN_ZIP =		new Prop<string>.Gen("zipItem", zipRoot => $"In Zip: {zipRoot}"),
};

var userprofileAlias = "%USERPROFILE%";
var userprofile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

var cwd = Environment.CurrentDirectory;
var locked = new HashSet<string>();
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
		restricted= locked.Select(r => r.Replace(cwd, "%CWD%")).ToHashSet(),
		lastIndex = lastIndex
	}).Replace(userprofile, userprofileAlias));
}
#if false
File.Delete(save);
#endif
void Load () {
	if(File.Exists(save)) {
		try {
			(cwd, locked, lastIndex) = new Deserializer().Deserialize<State>(File.ReadAllText(save).Replace(userprofileAlias, userprofile));

			//File.Delete(save);

			locked = new(locked.Select(s => s.Replace("%CWD%", cwd)));
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
	Application.UseSystemConsole = true;
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

	var gitData = new List<GitItem>();

	bool readProcess = false;
	Action<Process>? ProcessStarted = default;

	Action<FindLine> EditFile = default;

	var term = new TextField() {
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

	var homeView = new Lazy<View>(() => {

		var view = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
		};
		var FILL = Dim.Fill();
		var w = Dim.Percent(25);
		//Libraries
		var libraries = new FrameView("Libraries") {
			X = 0,
			Y = 0,
			Width = w,
			Height = Dim.Fill(),
		};
		var librariesList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL,
		};

		//Pinned folders, pinned files
		var pins = new FrameView("Pinned") {
			X = Pos.Right(libraries),
			Y = 0,
			Width = w,
			Height = FILL,
		};
		var pinsList = new ListView() {
			X=0,
			Y=0,
			Width=FILL,
			Height=FILL
		};
		var recent = new FrameView("Recent") {
			X = Pos.Right(pins),
			Y = 0,
			Width = w,
			Height = Dim.Fill()
		};
		var recentList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL
		};


		var repos = new FrameView("Repositories") {
			X = Pos.Right(recent),
			Y = 0,
			Width = w,
			Height = Dim.Fill()
		};
		var repoList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL
		};

		InitTree(
			[view, libraries, pins, recent, repos],
			[libraries, librariesList],
			[pins, pinsList],
			[recent, recentList],
			[repos, repoList]
			);
		return view;
		//https://stackoverflow.com/questions/13079569/how-do-i-get-the-path-name-from-a-file-shortcut-getting-exception/13079688#13079688
	}).Value;
	//Add context button to switch to Find with root at dir
	//Add button to treat dir as root
	//Add option for treeview
	var fileView = new Lazy<View>(() => {
		var fileView = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var goPrev = new Button("<-") { X = 0, TabStop = false };
		var goNext = new Button("->") { X = 6, TabStop = false };
		var goLeft = new Button("..") { X = 12, TabStop = false };
		var addressBar = new TextField(cwd) {
			X = Pos.Percent(25) + 1,
			Y = 0,
			Width = Dim.Fill() - 1,
			Height = 1,
			ReadOnly = true
		};
		var freqPane = new FrameView("Recents", new() { BorderStyle = BorderStyle.Single, DrawMarginFrame = true, Effect3D = false }) {
			X = 0,
			Y = 1,
			Width = Dim.Percent(25),
			Height = Dim.Percent(50) - 1,
		};
		var freqList = new ListView(favData) {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			ColorScheme = new() {
				HotNormal = new(Color.Black, Color.White),
				Normal = new(Color.White, Color.Blue),
				HotFocus = new(Color.Black, Color.White),
				Focus = new(Color.White, Color.Black),
				Disabled = new(Color.Red, Color.Black)
			}
		};
		var clipPane = new FrameView("Clipboard", new() { BorderStyle = BorderStyle.Single, DrawMarginFrame = true, Effect3D = false }) {
			X = 0,
			Y = Pos.Percent(50),
			Width = Dim.Percent(25),
			Height = Dim.Percent(50),
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
		var pathPane = new FrameView("Directory") {
			X = Pos.Percent(25),
			Y = 1,
			Width = Dim.Percent(50),
			Height = 28,
		};
		var pathList = new ListView(cwdData) {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			AllowsMultipleSelection = true,
			AllowsMarking = true,
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
		var procPane = new FrameView("Processes", new() { BorderStyle = BorderStyle.Single, DrawMarginFrame = true, Effect3D = false }) {
			X = Pos.Percent(75),
			Y = 1,
			Width = Dim.Percent(25),
			Height = Dim.Percent(50) - 1,
		};
		var procList = new ListView(procData) {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			ColorScheme = new() {
				HotNormal = new(Color.Black, Color.White),
				Normal = new(Color.White, Color.Blue),
				HotFocus = new(Color.Black, Color.White),
				Focus = new(Color.White, Color.Black),
				Disabled = new(Color.Red, Color.Black)
			}
		};

		var gitPane = new FrameView("Changes", new() { BorderStyle = BorderStyle.Single, DrawMarginFrame = true, Effect3D = false }) {
			X = Pos.Percent(75),
			Y = Pos.Percent(50),
			Width = Dim.Percent(25),
			Height = Dim.Percent(50),
		};

		var gitList = new ListView(gitData) {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			AllowsMarking = true,
			AllowsMultipleSelection = true,
			ColorScheme = new() {
				HotNormal = new(Color.Black, Color.White),
				Normal = new(Color.White, Color.Blue),
				HotFocus = new(Color.Black, Color.White),
				Focus = new(Color.White, Color.Black),
				Disabled = new(Color.Red, Color.Black)
			}
		};

		InitTreeLocal();
		InitEvents();
		InitCwd();
		UpdateButtons();
		UpdateProcesses();

		return fileView;

		void InitTreeLocal () {
			InitTree(
				[freqPane, freqList],
				[clipPane],
				[pathPane, pathList],
				[procPane, procList],
				[gitPane, gitList],
				[fileView, addressBar, goPrev, goNext, goLeft, freqPane, clipPane, pathPane, /*properties,*/ procPane, gitPane]
				);
		}
		void InitEvents () {
			goPrev.AddMouseClick(e => e.MouseEvent.Flags switch {
				MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
					[.. cwdPrev.Select((p, i) => new MenuItem(p, "", () => GoPrev(i + 1)))])).Show,
				MouseFlags.Button1Clicked => () => e.Handled = GoPrev(),
				_ => null,
			});
			goNext.AddMouseClick(e => e.MouseEvent.Flags switch {
				MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
					[.. cwdNext.Select((p, i) => new MenuItem(p, "", () => GoNext(i + 1)))])).Show,
				MouseFlags.Button1Clicked => () => e.Handled = GoNext(),
				_ => null
			});
			goLeft.AddMouseClick(e => e.MouseEvent.Flags switch {
				MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
					[.. Up().Select(p => new MenuItem(p, "", () => GoPath(p)))])).Show,
				MouseFlags.Button1Clicked => () => e.Handled = GoLeft(),
				_ => null
			});
			pathList.OpenSelectedItem += e => GoItem();
			pathList.AddKeyPress(e => {
				if(!pathList.HasFocus) return null;
				return e.KeyEvent.Key switch {
					Key.Enter or Key.CursorRight => () => GoItem(),
					Key.CursorLeft => () => GoPath(Path.GetDirectoryName(cwd)),
					Key.Tab => () => {
						if(!GetItem(out var p)) return;
						term.Text = p.local;
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
					_ => e.KeyEvent.KeyValue switch {
						'[' => () => {
							GoPrev();
						}
						,
						']' => () => {
							GoNext();
						}
						,
						'\\' => () => {
							GoLeft();
						}
						,
						',' => () => {
							//Create new tab
							if(cwdRecall != null) SetCwd(cwdRecall);
						}
						,
						'.' => () => {
							ShowContext(cwd);
						}
						,
						':' => term.SetFocus,
						'\'' => () => {
							//Copy file
							return;
						}
						,
						'"' => () => {
							if(!GetItem(out var p) || p.dir) return;
							var initial = File.ReadAllText(p.path);
							var d = new Dialog("Preview", []) {
								Border = {Effect3D=false}
							};
							d.AddKeyPress(e => e.KeyEvent.Key switch {
								Key.Enter => d.RequestStop
							});
							d.Add(new TextView() {
								X = 0,
								Y = 0,
								Width = Dim.Fill(),
								Height = Dim.Fill(),
								Text = initial,
								ReadOnly = true,
							});
							Application.Run(d);
						}
						,
						'?' => () => {
							if(!GetItem(out var p)) return;
							ShowProperties(p);
						}
						,
						'/' => () => {
							if(!GetItem(out var p)) return;
							ShowContext(p.path);
						}
						,
						'~' => () => {
							if(!GetItem(out var p)) return;
							if(!locked.Remove(p.path)) {
								locked.Add(p.path);
							}
							SetCwd(cwd);
							pathList.SetNeedsDisplay();
						}
						,
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
						'@' => () => {
							//Copy file path
						}
						,
						'#' => () => {
							//treat dir as root, disallow cd out
						}
						,
						>= 'a' and <= 'z' => () => {
							var c = $"{(char)e.KeyEvent.KeyValue}";
							var index = pathList.SelectedItem;
							bool P ((int index, PathItem item) pair) =>
								pair.index > index && StartsWith(pair);
							bool StartsWith ((int index, PathItem item) pair) =>
								pair.item.local.StartsWith(c, StringComparison.CurrentCultureIgnoreCase);
							var pairs = cwdData.Index();
							var dest = pairs.FirstOrDefault(P, pairs.FirstOrDefault(StartsWith, (-1, null)));
							if(dest.Index == -1) return;
							pathList.SelectedItem = dest.Index;
							pathList.SetNeedsDisplay();
						}
						,
						>= 'A' and <= 'Z' => () => {
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
						_ => null
					}
				};
			});
			procList.AddKeyPress(e => e.KeyEvent.Key switch {
				Key.CursorLeft or Key.CursorRight => () => { }
				,
				Key.Backspace => () => {
					if(!(procList.SelectedItem < procData.Count))
						return;
					var p = procData[procList.SelectedItem];
					p.p.Kill();
					procData.Remove(p);
					procList.SetNeedsDisplay();
				}
				,
				_ => e.KeyEvent.KeyValue switch {
					'\'' => UpdateProcesses,
					_ => null
				}
			});
			term.AddKeyPress(e => {
				var t = (string)term.Text;
				return e.KeyEvent.Key switch {
					Key.Enter when t.MatchArray("cd (?<dest>.+)") is [_, { } dest] => delegate {
						if(!GoPath(Path.GetFullPath(Path.Combine(cwd, dest)))) {
							return;
						}
						term.Text = "";
					}
					,
					Key.Enter when t == "cut" => () => {
						var items = GetMarkedItems().ToArray();
					}
					,
					Key.Enter when t.Any() => delegate {
						var cmd = $"{t}";
						var pi = new ProcessStartInfo("cmd.exe") {
							WorkingDirectory = cwd,
							Arguments = @$"/c {cmd} & pause",
							UseShellExecute = !readProcess,
							RedirectStandardOutput = readProcess,
							RedirectStandardError=readProcess,
						};


						var p = new Process() {
							StartInfo = pi
						};

						Task.Run(() => {
							p.Start();
							if(readProcess) {
								ProcessStarted(p);
							}
						});
						
					}
					,
					_ => null
				};
			});
			/*
			window.AddKeyPress(e => e.KeyEvent.KeyValue switch {
				':' => () => {
					if(term.HasFocus)
						return;
					term.SetFocus();
				},
				_ => null
			});
			*/
			void ShowProperties(PathItem path) {
				var d = new Dialog("Properties", []) {
					Border = {
						Effect3D = false
					}
				};
				d.KeyPress += e => {
					e.Handled = true;
					d.Running = false;
				};

				d.Add(new TextView() {
					X=0,Y=0,
					Width=Dim.Fill(),Height=Dim.Fill(),
					ReadOnly = true,
					Text = string.Join("\n", path.properties.Select(p => p.desc)),
				});

				Application.Run(d);
			}
			void ShowContext (string path) {
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

						}, canExecute: () => !locked.Contains(path));


						yield return new MenuItem("New Directory", "", () => {

							var create = new Button("Create") { Enabled = false };
							var cancel = new Button("Cancel");
							var d = new Dialog("New Directory", [
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
								Directory.CreateDirectory(f);
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

						}, canExecute: () => !locked.Contains(path));

					} else {
						//git.repo.RetrieveStatus("lll") == FileStatus.
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

				var c = new ContextMenu(pathList.Bounds.X, pathList.Bounds.Y, new([.. GetCommon()])) { };
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
			IEnumerable<int> GetMarkedIndex () =>
				Enumerable.Range(0, cwdData.Count).Where(pathList.Source.IsMarked);
			IEnumerable<PathItem> GetMarkedItems () =>
				GetMarkedIndex().Select(i => cwdData[i]);
			bool GoPath (string? dest) {
				var f = Path.GetFileName(cwd);
				if(Directory.Exists(dest) is { } b && b) {
					cwdNext.Clear();
					cwdPrev.Push(cwd);
					SetCwd(dest);
					UpdateButtons();

					if(cwdData.FirstOrDefault(p => p.local == f) is { } item) {
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
				Go(i);
				void Go(PathItem i) {

					if(i.properties.Contains(Prop.IS_LOCKED)) {
						return;
					}
					if(i.propertyDict.TryGetValue(Prop.IS_LINK_TO.id, out var link)) {
						var dest = ((Prop<string>)link).data;
						var destItem = CreatePathItem(dest);
						Go(destItem);
						return;
					}
					if(i.propertyDict.ContainsKey(Prop.IS_ZIP.id)) {
						using(ZipArchive zip = ZipFile.Open(i.path, ZipArchiveMode.Read)) {
							foreach(ZipArchiveEntry entry in zip.Entries) {
								Debug.Print(entry.FullName);
							}
						}
						return;
					}
					if(i.properties.Contains(Prop.IS_DIRECTORY)) {
						GoPath(i.path);
						return;
					}
					Run(i.path);
				}
				
			}
			void RefreshCwd () {
				SetCwd(cwd);
			}
			Process Run (string cmd) {
				//var setPath = $"set 'PATH=%PATH%;{Path.GetFullPath("Programs")}'";
				var cmdArgs = @$"/c {cmd} & pause";
				var pi = new ProcessStartInfo("cmd.exe") {
					WorkingDirectory = cwd,
					Arguments = $"{cmdArgs}",
					UseShellExecute = true,
					RedirectStandardOutput = false,
				};
				var p = new Process() {
					StartInfo = pi
				
				};
				p.Start();

				ProcessStarted(p);
				return p;
			}
		}
		void InitCwd () {
			SetCwd(cwd);
		}
		void UpdateButtons () {
			foreach(var (b, st) in new[] { (goLeft, Up()), (goNext, cwdNext), (goPrev, cwdPrev) }) {
				b.Enabled = st.Any();
			}
		}
		void UpdateProcesses () {
			procData.Clear();
			procData.AddRange(Process.GetProcesses().Where(p => p.MainWindowHandle != 0).Select(p => new ProcItem(p)));
			procList.SetNeedsDisplay();
		}
		void UpdateGit() {
			if(git is { root: { } root, repo: { } repo }) {
				if(cwd.StartsWith(root)) {
					git = git with { patch = repo.Diff.Compare<Patch>() };
					UpdateChanges();
				} else {
					gitData.Clear();
					repo.Dispose();
					git = default;
				}
			} else {
				if(Repository.IsValid(cwd)) {
					var re = new Repository(cwd);
					git = (cwd, re, re.Diff.Compare<Patch>());
					UpdateChanges();
				}
			}

			void UpdateChanges () {

				gitData.Clear();

				var index = git.repo.Index;
				var changes = git.patch.Select(patch => {
					var local = patch.Path;
					var entry = index[local];

					bool staged = false;
					if(entry != null) {
						var blob = git.repo.Lookup<Blob>(entry.Id);
						var b = blob.GetContentText();
						var f = File.ReadAllText($"{git.root}/{local}").Replace("\r", "");
						staged = f == b;
					}
					var item = new GitItem(local, patch, staged);
					return item;
				}).OrderByDescending(item=>item.staged ? 1 : 0);
				gitData.AddRange([..changes]);
				gitList.SetSource(gitData);
				foreach(var(i, it) in gitData.Index()) {
					gitList.Source.SetMark(i, it.staged);
				}
				gitList.OpenSelectedItem += e => {
					//Stage/Unstage
					int i = 0;
				};
			}
		}
		/*
		//void AddListenersMulti ((View view, MousePressGen MouseClick, KeyPressGen KeyPress)[] pairs) => pairs.ToList().ForEach(pair => AddListeners(pair.view, pair.MouseClick, pair.KeyPress));
		void AddListeners(View view, MousePressGen MouseClick = null, KeyPressGen KeyPress = null) {

			if(KeyPress != null) AddKeyPress(view, KeyPress);
			if(MouseClick != null) AddMouseClick(view, MouseClick);
		}
		*/
		PathItem CreatePathItem (string f) =>
			new PathItem(Path.GetFileName(f), f, Directory.Exists(f), new(GetProps(f)));
		IEnumerable<IProp> GetProps (string path) {
			if(locked.Contains(path)) {
				yield return Prop.IS_LOCKED;
			}
			if(Directory.Exists(path)) {
				yield return Prop.IS_DIRECTORY;
			} else {
				if(path.EndsWith(".sln")) {
					yield return Prop.IS_SOLUTION;
				}
				if(path.EndsWith(".lnk")) {
					// WshShellClass shell = new WshShellClass();
					WshShell shell = new WshShell(); //Create a new WshShell Interface
					IWshShortcut link = (IWshShortcut)shell.CreateShortcut(path); //Link the interface to our shortcut
					yield return Prop.IS_LINK_TO.Generate(link.TargetPath);
				}
				if(path.EndsWith(".zip")) {
					yield return Prop.IS_ZIP;
				}
			}
		}
		void SetCwd (string s) {
			try {
				var paths = new List<PathItem>([
					..Directory.GetDirectories(s).Select(CreatePathItem),
					..Directory.GetFiles(s).Select(CreatePathItem)
				]);
				cwdData.Clear();
				cwdData.AddRange(paths);
				pathList.SetSource(cwdData);
				/*
				if(s == cwd) {
					goto UpdateListing;
				}
				*/


				lastIndex[cwd] = pathList.SelectedItem;
				cwd = Path.GetFullPath(s);

				UpdateGit();


				var anonymize = true;
				var showCwd = cwd;
				if(anonymize) {
					showCwd = showCwd.Replace(userprofile, userprofileAlias);
				}
				//Anonymize
				addressBar.Text = showCwd;
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
	}).Value;
	var findView = new Lazy<View>(() => {
		var view = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var filter = new FindFilter(new(""), new(""), null);
		var finder = new TreeFinder();
		int w = 8;
		int y = 0;
		var rootLabel = new Label("Root") {
			X = 0,
			Y = y,
			Width = w
		};
		var rootBar = new TextField(cwd.Replace(userprofile, userprofileAlias)) {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};
		var rootShowButton = new Button("Find Directories", false) {
			X = Pos.Right(rootBar),
			Y = y,
			Width = 6
		};
		y++;
		var filterLabel = new Label("File") {
			X = 0,
			Y = y,
			Width = w
		};
		var filterBar = new TextField() {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};
		var filterShowButton = new Button("Find Files", false) {
			X = Pos.Right(filterBar),
			Y = y,
			Width = 6
		};
		y++;
		var findLabel = new Label("Find") {
			X = 0,
			Y = y,
			Width = w
		};
		var findBar = new TextField() {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};
		var findAllButton = new Button("All", false) {
			X = Pos.Right(findBar),
			Y = y,
			Width = 6
		};
		var findPrevButton = new Button("<-", false) {
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6
		};
		var findNextButton = new Button("->", false) {
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6
		};
		y++;
		var replaceLabel = new Label("Replace") {
			X = 0,
			Y = y,
			Width = w
		};
		var replaceBar = new TextField() {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};
		var replaceAllButton = new Button("All", false) {
			X = Pos.Right(findBar),
			Y = y,
			Width = 6
		};
		var replacePrevButton = new Button("<-", false) {
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6
		};
		var replaceNextButton = new Button("->", false) {
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6
		};
		y++;
		var tree = new TreeView<IFind>(finder) {
			X = 0,
			Y = y,
			Width = Dim.Fill(0),
			Height = Dim.Fill(0),
			AspectGetter = f => f switch {
				FindDir d => $"{d.name}/",
				FindFile ff => ff.name,
				FindLine l => $"{l.row, 3}|{l.line}"
			}
		};
		tree.ObjectActivated += e => {
			if(e.ActivatedObject is FindLine l) {
				EditFile(l);
			}
		};
		rootShowButton.Clicked += FindDirs;
		filterShowButton.Clicked += FindFiles;
		string GetRoot() =>
			rootBar.Text.ToString().Replace(userprofileAlias, userprofile);
		void FindDirs () {
			filter = filter with {
				filePattern = null,
				linePattern = null,
			};
			SetFilter(filter);
		}
		void FindFiles() {
			filter = filter with {
				filePattern = new(filterBar.Text.ToString()),
				linePattern = null
			};
			SetFilter(filter);
		}
		findAllButton.Clicked += FindLines;
		void FindLines() {
			filter = filter with {
				filePattern = new(filterBar.Text.ToString()),
				linePattern = new(findBar.Text.ToString())
			};
			SetFilter(filter);
		}
		void SetFilter(FindFilter filter) {
			tree.ClearObjects();
			tree.AddObject(new FindDir(filter, GetRoot()));
			//tree.ExpandAll();
		}
		//Print button (no replace)
		FindLines();
		InitTree([view,
			rootLabel, rootBar, rootShowButton,
			filterLabel, filterBar, filterShowButton,
			findLabel, findBar, findAllButton, findPrevButton, findNextButton,
			replaceLabel, replaceBar, replaceAllButton, replacePrevButton, replaceNextButton,
			tree
			]);
		return view;
	}).Value;
	var editView = new Lazy<View>(() => {
		var view = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var addressBar = new TextField("FILE") {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1
		};
		var textView = new TextView() {
			X = 0,
			Y = 2,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		InitTree([view, addressBar, textView]);
		return view;
	}).Value;
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
		ProcessStarted += (Process p) => {
			//if(!view.Visible) return;
			/*
			p.OutputDataReceived += (a, e) => {
				text.Text += $"{e.Data}\n";
			};
			p.ErrorDataReceived += (a, e) => {
				text.Text += $"{e.Data}\n";
			};
			p.BeginOutputReadLine();
			p.BeginErrorReadLine();
			p.WaitForExit();
			*/
		};
		InitTree([view, text]);
		return view;
	}).Value;
	var mainTabs = new Lazy<TabView>(() => {
		var tv = new TabView() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(3),
			Border = new() { Effect3D = false, DrawMarginFrame = false, BorderStyle = BorderStyle.None}
		};
		foreach(var(name, view) in new[] { ("Home", homeView), ("File", fileView), ("Find", findView), ("Edit", editView), ("Term", termView), ("Git", null) }) {
			tv.AddTab(new TabView.Tab(name, view), false);
		}
		/*
		tv.AddTab(new TabView.Tab("File", fileView), false);
		tv.AddTab(new TabView.Tab("Term", termView), false);
		*/
		tv.SelectedTabChanged += (a, e) => {
			readProcess = e.NewTab.View == termView;
		};
		return tv;
	}).Value;
	var window = new Window() {
		X = 0,
		Y = 0,
		Width = Dim.Fill(0),
		Height = Dim.Fill(0),
		Border = { BorderStyle = BorderStyle.None, Effect3D = false, DrawMarginFrame = false },
		Title = "fx",
	};

	InitTree([
		[window, mainTabs, termBar]
	]);
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
	void InitTree (params View[][] tree) {
		foreach(var row in tree) {
			row[0].Add(row[1..]);
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
public interface IFind {
	IEnumerable<IFind> GetChildren ();
	IEnumerable<FindLine> GetLeaves ();
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
public record Prop (string id, string desc) : IProp {}
public record Prop<T>(string id, string desc, T data) : IProp {
	public delegate string GetDesc (T args);
	public record Gen(string id, GetDesc getDesc) {
		public Prop<T> Generate (T args) => new Prop<T>(id, getDesc(args), args);
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
public record State (string cwd, HashSet<string> restricted, Dictionary<string, int> lastIndex) {
	public State () : this(null, null, null) { }
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
}
public delegate Action? KeyEvent (View.KeyEventEventArgs e);
public delegate Action? MouseEvent (View.MouseEventArgs e);

public static class SEnumerable {
	public static IEnumerable<U> Construct<T, U>(IEnumerable<T> seq) {
		var con = typeof(U).GetConstructor([typeof(T)]);
		Debug.Assert(con != null);
		foreach(var item in seq) {
			yield return (U)con.Invoke([item]);
		}
	}
}