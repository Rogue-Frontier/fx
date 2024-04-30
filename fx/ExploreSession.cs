using IWshRuntimeLibrary;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;
using fx;
using File = System.IO.File;
using System.Collections;
using System.Reflection;
using static Ctx;
using System.Threading;
using Microsoft.Build.Construction;
using System.Reflection.Metadata.Ecma335;
using static fx.Props;
using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;
using Application = Terminal.Gui.Application;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.IO;


using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
using System.Text.RegularExpressions;
namespace fx;
public class ExploreSession {
	public View root;
	private Ctx ctx => main.ctx;
	private Fx fx => ctx.fx;


	public Dictionary<string, int> lastIndex = new();
	public string cwd = Environment.CurrentDirectory;
	public LinkedList<string> cwdPrev = new();
	public LinkedList<string> cwdNext = new();
	private ListMarker<PathItem> cwdData;
	Dictionary<string, GitItem> gitMap = new();
	private List<GitItem> gitData = new();
	private Label goPrev, goNext, goLeft;
	private TextField addressBar;
	public ListView pathList;
	private ListView repoList;
	//When we cd out of a repository, we immediately forget about it.
	private RepoPtr? git;
	private string cwdRecall = null;

	public string zipRoot;

	private Main main;
	public ExploreSession (Main main, string initCwd) {
		this.main = main;
		this.cwd = initCwd;
		cwdData = new(new(), (p, i) => p.local);
		var favData = new List<PathItem>();
		var procData = new List<WindowItem>();

		main.TermEnter += OnTermEnter;

		root = new View() {
			Title = "Root",
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		goPrev = new Label() { Title= "[<---]",  X = 1, TabStop = false };
		goNext = new Label() { Title= "[--->]", X = 7, TabStop = false };
		goLeft = new Label() { Title= "[/../]",  X = 13, TabStop = false };
		addressBar = new TextField() {
			Title = "Address",
			X = Pos.Percent(25) + 1,
			Y = 0,
			Width = Dim.Fill() - 1,
			Height = 1,
			ReadOnly = true,
			CanFocus = false,


			ColorScheme = Application.Top.ColorScheme with {
				Focus = new(Color.White, new Color(31, 31, 31))
			}
		};
		var freqPane = new View() {
			Title = "Recents",
			BorderStyle= LineStyle.Single,
			X = 0,
			Y = 1,
			Width = Dim.Percent(25),
			Height = Dim.Percent(50) - 1,
		};
		var freqList = new ListView() {
			Title = "Recents List",
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Source= new ListWrapper(new List<string>())
		};
		var clipPane = new Lazy<View>(() => {
			var view = new View() {
				Title = "Clipboard",
				BorderStyle = LineStyle.Single,
				X = 0,
				Y = Pos.Percent(50),
				Width = Dim.Percent(25),
				Height = Dim.Percent(50),
			};
			var clipTab = new Lazy<View>(() => {
				var view = new TabView() {
					Title = "Clipboard",
					X = 0,
					Y = 0,
					Width = Dim.Fill(),
					Height = Dim.Fill()
				};
				var clipCutList = new ListView() {
					Title = "Cut",
					X = 0,
					Y = 0,
					Width = Dim.Fill(),
					Height = Dim.Fill()
				};
				var clipHistList = new ListView() {
					Title = "History",
					X = 0,
					Y = 0,
					Width = Dim.Fill(),
					Height = Dim.Fill()
				};
				return view;
			}).Value;
			SView.InitTree([view, clipTab]);
			return view;
		}).Value;

		var pathPane = new View() {
			Title = "Directory",
			BorderStyle = LineStyle.Single,

			X = Pos.Percent(25),
			Y = 1,
			Width = Dim.Percent(50),
			Height = 28,
		};

		pathList = new ListView() {
			Title = "Directory Items",
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			AllowsMultipleSelection = true,
			AllowsMarking = true,
			Source = cwdData,
			HasFocus = true,
		};
		var properties = new FrameView() {
			Title = "Properties",
			X = Pos.Percent(25),
			Y = 29,
			Width = Dim.Percent(50),
			Height = Dim.Fill(1),
		};
		var procPane = new View() {
			Title = "Processes",
			BorderStyle = LineStyle.Single,

			X = Pos.Percent(75),
			Y = 1,
			Width = Dim.Percent(25),
			Height = Dim.Percent(50) - 1,
		};
		var procList = new ListView() {
			Title = "Process List",
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Source = new ListMarker<WindowItem>(procData, (w, index) => $"{Path.GetFileName(w.path), -20} {w.name}"),
		};
		var repoPane = new View() {
			Title = "Repository",
			BorderStyle = LineStyle.Single,
			X = Pos.Percent(75),
			Y = Pos.Percent(50),
			Width = Dim.Percent(25),
			Height = Dim.Percent(50),
		};
		repoList = new ListView() {
			Title = "Repository Items",
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			AllowsMarking = true,
			AllowsMultipleSelection = true,
		};
		InitTreeLocal();
		InitEvents();
		InitCwd();
		RefreshPads();
		UpdateProcesses();
		void InitTreeLocal () {
			SView.InitTree(
				[freqPane, freqList],
				[clipPane],
				[pathPane, pathList],
				[procPane, procList],
				[repoPane, repoList],
				[root, addressBar, goPrev, goNext, goLeft, freqPane, clipPane, pathPane, /*properties,*/ procPane, repoPane]
				);
		}
		void InitEvents () {
			goPrev.MouseEvD(new() {
				[(int)Button3Clicked] = e => new ContextMenu() {
					Position = e.MouseEvent.ScreenPosition,
					MenuItems = new([.. cwdPrev.Select((string p, int i) => new MenuItem(p, "", () => GoPrev(i + 1)))])
				}.Show(),
				[(int)Button1Pressed] = e => e.Handled = (GoPrev())
			});
			goNext.MouseEvD(new() {
				[(int)Button3Clicked] = e => new ContextMenu() {
					Position = e.MouseEvent.ScreenPosition,
					MenuItems = new(
						[.. cwdNext.Select((string p, int i) => new MenuItem(p, "", () => GoNext(i + 1)))]),
				}.Show(),
				[(int)Button1Pressed] = e => e.Handled = (GoNext())
			});
			goLeft.MouseEvD(new() {
				[(int)Button3Clicked] = e => new ContextMenu() {
					Position = e.MouseEvent.ScreenPosition,
					MenuItems = new([.. Up().Select(p => new MenuItem(p, "", () => TryGoPath(p)))])
				}.Show(),
				[(int)Button1Pressed] = e => e.Handled = (GoLeft())
			});
			pathList.MouseEvD(new() {
				[(int)Button1Pressed] = e => {
					var i = pathList.TopItem + e.MouseEvent.Y;
					if(i >= cwdData.Count)
						return;
					pathList.SelectedItem = i;
					pathList.SetNeedsDisplay();

					pathList.SetFocus();
					e.Handled = true;
				},
				[(int)Button1Released] = e => {
					cwdData.SetMark(pathList.SelectedItem, !cwdData.IsMarked(pathList.SelectedItem));
					e.Handled = true;
				},

				[(int)Button3Pressed] = e => {
					var prev = pathList.SelectedItem;

					var i = pathList.TopItem + e.MouseEvent.Y;
					if(i >= cwdData.Count)
						return;
					pathList.SelectedItem = i;
					var c = ShowContext(cwdData.list[i], e.MouseEvent.Y, e.MouseEvent.X);
					c.MenuBar.MenuAllClosed += (object? _, EventArgs _) => {
						if(prev == -1) {
							return;
						}
						pathList.SelectedItem = prev;
					};
					e.Handled = true;
				}
			});
			pathList.OpenSelectedItem += (a, e) => GoItem();
			pathList.KeyDownF(e => {
				if(!pathList.HasFocus) return null;
				return (Action?)((int)e.KeyCode switch {
					(int)Enter or (int)CursorRight => () => GoItem(),
					(int)CursorLeft => () => TryGoPath(Path.GetDirectoryName(cwd)),
					(int)Esc => () => { },
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
					'F' | (int)CtrlMask => () => {
						var find = new FindSession(main, cwd);
						main.folder.AddTab($"Find {cwd}", find.root, true, root.Focused);
						find.rootBar.SetLock(true);
						find.FindDirs();
					},
					'[' => () => GoPrev(),
					']' => () => GoNext(),
					'\\' => () => GoLeft(),
					'<' => () => { main.folder.SwitchTab(-1); },
					'>' => () => { main.folder.SwitchTab(1); },
					',' => () => {
						//Create new tab
						if(cwdRecall != null) SetCwd(cwdRecall);
					},
					'.' => () => {
						var cc = ShowContext(GetPathItem(cwd), 0);
						cc.MenuBar.KeyDownD(value: new() {
							{ '.', _ => cc.Hide() }
						});
					},
					':' => () => main.FocusTerm(pathList),
					'\'' => () => {
						//Copy file
						return;
					},
					'"' => () => {
						if(!GetItem(out var p) || p.dir) return;
						Preview($"Preview: {p.path}", File.ReadAllText(p.path));
					},
					'?' => () => {
						if(!GetItem(out var p)) return;
						ShowProperties(p);
					},
					'/' => () => {
						if(!GetItem(out var p, out var ind)) return;
						var c = ShowContext(p, ind - pathList.TopItem + 2, 2);
					},
					'~' => () => {
						if(!GetItem(out var p)) return;
						if(!fx.locked.Remove(p.path)) {
							fx.locked.Add(p.path);
						}
						RefreshCwd();
					},
					'!' => () => {
						//need option to collapse single-directory chains / single-non-empty-directory chains
						if(!GetItem(out var p))
							return;
						if(!ctx.fx.pins.Remove(p.path)) {
							ctx.fx.pins.Add(p.path);
						}
						freqList.SetNeedsDisplay();
					},
					'@' => () => {
						//set dir as workroot
						fx.workroot = fx.workroot != cwd ? cwd : null;
						RefreshCwd();
					},
					'#' => () => {
						main.FocusTerm(pathList);
						main.term.Text += $"{{{pathList.SelectedItem}}}";
					},
					>= 'a' and <= 'z' => () => {
						var c = $"{(char)e.AsRune.Value}";
						var index = pathList.SelectedItem;
						bool P ((int index, PathItem item) pair) =>
							pair.index > index && StartsWith(pair);
						bool StartsWith ((int index, PathItem item) pair) =>
							pair.item.local.StartsWith(c, StringComparison.CurrentCultureIgnoreCase);
						var pairs = cwdData.list.Index();
						var dest = pairs.FirstOrDefault(P, pairs.FirstOrDefault(StartsWith, (-1, null)));
						if(dest.Index == -1) return;
						pathList.SelectedItem = dest.Index;
						pathList.SetNeedsDisplay();
					},
					>= 'A' and <= 'Z' => () => {
						var index = (int)e.KeyCode - 'A' + pathList.TopItem;
						if(pathList.SelectedItem == index) {
							GoItem();
							return;
						}
						if(index >= pathList.Source.Count) {
							return;
						}

						pathList.SelectedItem = index;
						pathList.OnSelectedChanged();
						pathList.SetNeedsDisplay();
					},
					_ => null
				});
			});
			procList.KeyDownD(new() {
				[(int)CursorLeft] = default,
				[(int)CursorRight] = default,
				[(int)Enter] = _ => {
					var item = procData[procList.SelectedItem];
					WinApi.SwitchToWnd(item.window, true);
				},
				[(int)Backspace] = _ => {
					if(!(procList.SelectedItem < procData.Count))
						return;
					var p = procData[procList.SelectedItem];
					Process.GetProcessById((int)p.pid).Kill();
					procData.Remove(p);
					procList.SetNeedsDisplay();
				},
				['\''] = _ => UpdateProcesses(),
				['/'] = _ => {
					var ind = procList.SelectedItem;
					//procData.RemoveAll(w => Process.GetProcessById((int)w.pid) == null);
					if(!(ind > -1 && ind < procData.Count)) {
						return;
					}
					var item = procData[ind];
					IEnumerable<MenuItem> GetActions () {
						yield return new MenuItem("Cancel", null, () => { });
						yield return new MenuItem("Switch To", null, () => {
							WinApi.SwitchToWnd(item.window, true);
						});
						yield return new MenuItem("Kill", null, () => {
							Process.GetProcessById((int)item.pid).Kill();
							UpdateProcesses();
						});
					}

					SView.ShowContext(procList, [.. GetActions()], ind + 2, 0);
					//Context menu
				},
			});
			repoList.OpenSelectedItem += (a, e) => {
				var item = gitData[e.Item];
				if(item.staged) Commands.Unstage(git.repo, item.local);
				else Commands.Stage(git.repo, item.local);
				RefreshChanges();
				repoList.SelectedItem = e.Item;
			};
			repoList.MouseEvD(new() {
				[(int)Button1Clicked] = e => {

					repoList.SelectedItem = repoList.TopItem + e.MouseEvent.Y;
					repoList.SetNeedsDisplay();
				},
				[(int)Button3Clicked] = e => {
					var prev = repoList.SelectedItem;
					repoList.SelectedItem = repoList.TopItem + e.MouseEvent.Y;

					repoList.SetNeedsDisplay();
				}
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
			ContextMenu ShowContext (PathItem selected, int row = 0, int col = 0) {
				var (x, y) = pathList.GetCurrentLoc();
				var bar = new MenuBarItem("Selected Item", [
					.. GetSingleActions(main, selected)
				]);
				var marked = GetMarkedItems().ToArray();
				if(marked.Except([selected]).Any()) {
					bar = new MenuBarItem([
						bar,
						new MenuBarItem("Marked Items", [..GetGroupActions(main, marked)])
					]);
				}
				var c = new ContextMenu() {
					Position = new(x + col, y+row),
					MenuItems = bar
				};
				c.Show();
				c.ForceMinimumPosToZero = true;
				return c;
			}
			bool GoPrev (int times = 1) => Enumerable.Range(0, times).All(_ => {
				if(cwdPrev is { Last: { Value: { }prev } }l) {
					l.RemoveLast();
					cwdNext.AddLast(cwd);
					SetCwd(prev);
					RefreshPads();
					return true;
				}
				return false;
			});
			bool GoNext (int times = 1) => Enumerable.Range(0, times).All(_ => {
				if(cwdNext is { Last:{Value:{ }next} }l) {
					l.RemoveLast();
					cwdPrev.AddLast(cwd);
					SetCwd(next);
					RefreshPads();
					return true;
				}
				return false;
			});
			bool GoLeft (int times = 1) =>
				Enumerable.Range(0, times).All(
					_ => Path.GetFullPath(Path.Combine(cwd, "..")) is { } s && s != cwd && TryGoPath(s));
			void GoItem () {
				var ind = pathList.SelectedItem;
				if(!(ind > -1 && ind < cwdData.Count)) {
					return;
				}
				Go(cwdData.list[pathList.SelectedItem]);
				void Go (PathItem i) {
					if(i.HasProp(IS_LOCKED)) {
						return;
					}
					if(i.GetProp<string>(IN_ZIP, out var root)) {
						zipRoot = root;
						if(i.HasProp(IS_DIRECTORY)) {
							GoPath(i.path);
						} else {

						}
						return;
					}
					if(i.HasProp(IS_ZIP)) {
						zipRoot = i.path;
						GoPath(i.path);
						return;
					}
					zipRoot = null;

					if(i.GetProp<string>(IS_LINK_TO, out var dest)) {
						var destItem = GetPathItem(dest);
						Go(destItem);
						return;
					}
					if(i.HasProp(IS_DIRECTORY)) {
						TryGoPath(i.path);
						return;
					}
					RunCmd($"cd {cwd} & {i.path}");
				}
			}
		}
		void InitCwd () {
			SetCwd(cwd);
		}
		void UpdateProcesses () {
			procData.Clear();
			procData.AddRange(WinApi.GetOpenWindows());
			//procList.SetSource(procData);
			procList.SetNeedsDisplay();
		}
		/*
		//void AddListenersMulti ((View view, MousePressGen MouseClick, KeyPressGen KeyPress)[] pairs) => pairs.ToList().ForEach(pair => AddListeners(pair.view, pair.MouseClick, pair.KeyPress));
		void AddListeners(View view, MousePressGen MouseClick = null, KeyPressGen KeyPress = null) {

			if(KeyPress != null) AddKeyPress(view, KeyPress);
			if(MouseClick != null) AddMouseClick(view, MouseClick);
		}
		*/
	}
	public void OnTermEnter(TermEvent e) {
		if(main.folder.currentBody != root) {
			return;
		}
		var cmd = e.text;
		//Replace {0} with the first marked path
		string[] args = cwdData.list.Where((item, index) => pathList.Source.IsMarked(index)).Select(item => item.local).ToArray();
		if(args.Any()) {
			cmd = string.Format(cmd, args);
		} else {
			//cmd = string.Format(cmd, cwdData[pathList.SelectedItem].local);
			cmd = string.Format(cmd, [..cwdData.list.Select(p => p.local)]);
		}

		if(cmd.MatchArray("cd (?<dest>.+)", 1) is [{ } dest]) {
			if(!TryGoPath(Path.GetFullPath(Path.Combine(cwd, dest)))) {
			}
			goto Handled;
		}

		bool readProc = false;
		var pi = new ProcessStartInfo("cmd.exe") {
			WorkingDirectory = cwd,
			Arguments = @$"/c {cmd} & pause", 
			UseShellExecute = true
		};
		var p = Process.Start(pi);

		Handled:
		e.term.Text = "";
		e.Handled = true;
	}
	public IEnumerable<int> GetMarkedIndex () =>
		Enumerable.Range(0, cwdData.Count).Where(pathList.Source.IsMarked);
	public IEnumerable<PathItem> GetMarkedItems () =>
		GetMarkedIndex().Select(i => cwdData.list[i]);

	public static void ShowProperties (PathItem item) {
		var d = new Dialog() {
			Title= $"Properties: {item.path}",
		};
		d.KeyDownD(new() {
			[(int)Enter] = _ => d.RequestStop()
		});

		d.Add(new TextView() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			ReadOnly = true,
			Text = string.Join("\n", item.propSet.Select(p => p.desc)),
		});

		Application.Run(d);
	}
	/// <summary>
	/// 
	/// </summary>
	/// <param name="cmd">Should not require cwd</param>
	/// <returns></returns>
	public static Process RunCmd (string cmd, string cwd = null) {
		//var setPath = $"set 'PATH=%PATH%;{Path.GetFullPath("Executables")}'";
		var cmdArgs = @$"/c {cmd} & pause";
		var pi = new ProcessStartInfo("cmd.exe") {
			Arguments = $"{cmdArgs}",
			UseShellExecute = true,
			//WindowStyle = ProcessWindowStyle.Hidden,
		};
		if(cwd != null) {
			pi.WorkingDirectory = cwd;
		}
		return Process.Start(pi);
	}

	public static Process StartCmd (string path) {
		//var setPath = $"set 'PATH=%PATH%;{Path.GetFullPath("Executables")}'";
		var pi = new ProcessStartInfo("cmd.exe") {
			Arguments = $"/k cd {path}",
			WorkingDirectory = path,
			UseShellExecute = true,
		};
		return Process.Start(pi);
	}
	public static IEnumerable<IProp> GetStaticProps (string path) {
		if(Directory.Exists(path)) {
			yield return IS_DIRECTORY;
			if(Directory.Exists($"{path}/.git")) {
				yield return IS_REPOSITORY;
			}
		} else if(File.Exists(path)) {
			yield return IS_FILE;
			if(path.EndsWith(".sln")) {
				yield return IS_SOLUTION;
			}
			if(path.EndsWith(".lnk")) {
				// WshShellClass shell = new WshShellClass();
				WshShell shell = new WshShell(); //Create a new WshShell Interface
				IWshShortcut link = (IWshShortcut)shell.CreateShortcut(path); //Link the interface to our shortcut
				yield return IS_LINK_TO.Make(link.TargetPath);
			}
			if(path.EndsWith(".zip")) {
				yield return IS_ZIP;
			}
			if(path.EndsWith(".py"))
				yield return IS_PYTHON;

		} else {
			throw new Exception("What the hell is wrong with you?");
		}
	}

	public IEnumerable<IProp> GetInstanceProps(string path) {
		if(fx.locked.Contains(path)) {
			yield return IS_LOCKED;
		}
		if(Path.GetDirectoryName(path) is { } par && HasRepo(GetPathItem(par), out var root)) {
			yield return IN_REPOSITORY.Make(CalcRepoItem(root, path));
		}
		if(gitMap.TryGetValue(path, out var p)) {
			if(p.staged) {
				yield return IS_STAGED;
			} else {
				yield return IS_UNSTAGED;
			}
		}
	}
	/// <param name="path">This is strictly a child of cwd.</param>
	/// <remarks>Strictly called after <see cref="RefreshRepo(string)"/>, so we already know repo props for the parent directory.</remarks>
	IEnumerable<IProp> GetProps (string path) =>
		[..GetInstanceProps(path), .. GetStaticProps(path)];


	public static IEnumerable<MenuItem> GetStaticActions (Main main, PathItem item) {
		//yield return new MenuItem(item.local, null, null, () => false);
		//yield return new MenuItem("----", null, null, () => false);

		if(item.HasProp(IN_ZIP)) {
			yield break;
		}

		var RunCommand =
			new MenuItem("Command", null, () => {
				RequestName("Command", cmd => {
					cmd = string.Format(cmd, item.path);
					RunCmd(cmd);
					return true;
				});
			});
		MenuItem UseSystemTerminal (string path) =>
			new MenuItem("Use system terminal", null, () => {
				StartCmd(path);
			});


		if(item.HasProp(IS_PYTHON)) {
			yield return new MenuBarItem("Run in Python3", null, () => {
			});
			yield return new MenuItem("Args", null, () => {
				RequestName("Args", args => {
					return true;
				});
			});
		}

		if(item.HasProp(IS_DIRECTORY)) {
			yield return new MenuBarItem("Explore", null, () => {
				main.folder.AddTab("Expl", new ExploreSession(main, item.path).root, true);
			}) {
				Children = [new MenuItem("Use system viewer", null, () => {
					RunCmd($"explorer.exe {item.path}");
				})]
			};
			yield return new MenuBarItem("Term", null, () => {
				var session = new TermSession(main, item.path);
				main.folder.AddTab($"Term {item.path}", session.root, true, main.root.FirstOrDefault()?.Focused);
			}) {
				Children = [
					UseSystemTerminal(item.path),
					RunCommand
				]
			};
		} else if(item.HasProp(IS_FILE)) {
			yield return new MenuItem("Edit", null, () => {
				main.folder.AddTab($"Edit", new EditSession(main, item.path).root, true, main.root.FirstOrDefault()?.Focused);
			});
		}

		if(Path.GetDirectoryName(item.path) is { } par) {
			yield return new MenuBarItem("Explore parent", null, () => {
				main.folder.AddTab("Expl", new ExploreSession(main, par).root, true);
			}) {
				Children = [
					new MenuItem("Show in System", "", () => RunCmd(@$"explorer.exe /select, ""{par}"""))
				]
			};
			yield return new MenuBarItem("Term parent", null, () => {
				var session = new TermSession(main, item.path);
				main.folder.AddTab($"Term {par}", session.root, true, main.root.FirstOrDefault()?.Focused);
			}) {
				Children = [
					UseSystemTerminal(item.path),
					RunCommand
				]
			};
			
		}

		yield return new MenuBarItem("Find", null, () => {
			var find = new FindSession(main, item.path);
			main.folder.AddTab($"Find {item.path}", find.root, true);
			find.rootBar.SetLock(true);
			find.FindDirs();
		}) {
			Children = [
				new MenuItem("Grep", null, () => {
					RequestName("Grep", pattern => {
						var reg = new Regex(pattern);
						return true;
					});
				})
			]
		};
		yield return new MenuBarItem("Library", [
			..main.ctx.fx.libraryData.Select(l => {
				var link = l.links.FirstOrDefault(link => link.path == item.path);
				return new MenuItem(l.name, null, () => {
					if(link != null){
						l.links.Remove(link);
					} else {
						l.links.Add(new LibraryItem(item.path, true));
					}
				}){ CheckType = MenuItemCheckStyle.Checked, Checked = link != null };
			}),
			new MenuItem("New Library", null, () => {
				RequestName("New Library", name => {
					if(main.ctx.fx.libraryData.Any(l => l.name == name)){
						return false;
					}
					var l = new Library(name);
					l.links.Add(new LibraryItem(item.path, true));
					main.ctx.fx.libraryData.Add(l);
					return true;
				});
			})
		]);
		var pins = main.ctx.fx.pins;
		var pin = pins.Contains(item.path);
		yield return new MenuItem(pin ? "Unpin" : "Pin", null, () =>
			((Action<string>)(pin ? p => pins.Remove(p) : pins.Add))(item.path)
			);
		var locked = main.ctx.fx.locked;
		var isLocked = locked.Contains(item.path);
		yield return new MenuItem(isLocked ? "Unlock" : "Lock", null, () =>
			((Func<string, bool>)(isLocked ? locked.Remove : locked.Add))(item.path)
		);
		if(item.HasProp(IS_DIRECTORY)) {
			//yield return new MenuItem("Open in System", "", () => RunCmd($"explorer.exe {item.path}"));
			yield return new MenuItem("Delete", null, () => {
				if(RequestConfirm($"Delete {item.path}")) {
					Directory.Delete(item.path);
				}
			});
		} else {
			yield return new MenuItem("Delete", null, () => {
				if(RequestConfirm($"Delete {item.path}")) {
					File.Delete(item.path);
				}
			});
		}
		yield return new MenuItem("Copy Path", "", () => Clipboard.TrySetClipboardData(item.path));
		
		yield return new MenuItem("Properties", null, () => ShowProperties(item));
	}
	//
	/// <summary>
	/// This should be refactored so that ctx handles path updates
	/// </summary>
	public IEnumerable<MenuItem> GetInstanceActions(Main main, PathItem item) {

		if(item.GetProp<string>(IN_ZIP, out var zipR)) {

			var zipPath = item.path.Replace(Path.GetFullPath($"{zipR}/"), "");

			if(item.HasProp(IS_DIRECTORY)) {
				yield return new MenuItem("Extract Dir", null, () => RequestName($"Extract {item.path}", dest => {

					if(!File.Exists(dest)) {
						Directory.CreateDirectory(dest);
						using var zip = ZipFile.OpenRead(zipR);

						var entries = zip.Entries
							.Where(e => !e.FullName.EndsWith('/') && Path.GetFullPath($"{zipR}/{e.FullName}").StartsWith(item.path)).ToArray();
						foreach(var entry in entries) {
							var entryPath = Path.GetFullPath($"{zipR}/{entry.FullName}");
							var sub = entryPath.Replace(Path.GetFullPath($"{item.path}/"), null);
							var entryDest = $"{dest}/{sub}";
							Directory.CreateDirectory(Path.GetDirectoryName(entryDest));
							entry.ExtractToFile(entryDest, true);
						}
						return true;
					}
					return false;
				}, $"{Path.GetDirectoryName(zipR)}/{Path.GetFileNameWithoutExtension(zipR)}"));
			} else {
				yield return new MenuItem("Extract File", null, () => RequestName($"Extract {item.path}", dest => {
					using var zip = ZipFile.OpenRead(zipR);
					var entry = zip.GetEntry(zipPath);
					entry.ExtractToFile(dest);
					return true;
				}, $"{Path.GetDirectoryName(zipR)}/{Path.GetFileNameWithoutExtension(zipR)}"));
			}

			yield break;
		}

		if(item.HasProp(IS_DIRECTORY)) {
			yield return new MenuItem("Remember", null, () => cwdRecall = item.path);
			yield return new MenuItem("New File", null, () => RequestName("New File", name => {
				if(Path.Combine(item.path, name) is { } f && !Path.Exists(f)) {
					File.Create(f);
					if(item.path.StartsWith(cwd)) {
						RefreshCwd();
						pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
					}
					return true;
				}
				return false;
			}), canExecute: () => !item.HasProp(IS_LOCKED));
			yield return new MenuItem("New Dir", null, () => RequestName("New Directory", name => {
				if(Path.Combine(item.path, name) is { }f && !Path.Exists(f)) {
					Directory.CreateDirectory(f);
					if(item.path.StartsWith(cwd)) {
						RefreshCwd();
						pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
					}
					return true;
				}
				return false;
			}), canExecute: () => !item.HasProp(IS_LOCKED));
			if(Path.GetDirectoryName(item.path) is { } par && HasRepo(GetPathItem(par), out string root)) {
				string local = GetRepoLocal(root, item.path);
				yield return new MenuItem("Diff", null, () => {
					using var repo = new Repository(root);
					Preview($"Diff: {item.path}", repo.Diff.Compare<Patch>([local]).Content);
				});
			}


			//Midnight Commander multi-move


			yield return new MenuItem("Copy", null, () => RequestName($"Copy {item.path}", name => {
				if((Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.Combine(item.path, name)) is { } f && !Path.Exists(f)) {
					File.Copy(item.path, f);
					if(item.path.StartsWith(cwd)) {
						RefreshCwd();
						pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
					}
					return true;
				}
				return false;
			}, item.path));
			yield return new MenuItem("Move", null, () => RequestName($"Move {item.path}", name => {
				if((Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.Combine(item.path, name)) is { } f && !Path.Exists(f)) {
					File.Move(item.path, f);
					if(item.path.StartsWith(cwd)) {
						RefreshCwd();
						pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
					}
					return true;
				}
				return false;
			}, item.path));
		} else if(item.HasProp(IS_FILE)) {
			if(Path.GetDirectoryName(item.path) is { } par && HasRepo(GetPathItem(par), out string root)) {
				string local = GetRepoLocal(root, item.path);
				var unstaged = item.HasProp(IS_UNSTAGED);
				var staged = item.HasProp(IS_STAGED);
				if(unstaged || staged) {
					yield return new MenuItem("Diff", "", () => {
						using var repo = new Repository(root);
						Preview($"Diff: {item.path}", repo.Diff.Compare<Patch>([local]).Content);
					});
					if(unstaged) {
						yield return new MenuItem("Stage", "", () => {
							using var repo = new Repository(root);
							Commands.Stage(repo, local);
							RefreshCwd();
						});
					} else if(staged) {
						yield return new MenuItem("Unstage", "", () => {
							using var repo = new Repository(root) ;
							Commands.Unstage(repo, local);
							RefreshCwd();
						});
					}
				}
			}


			yield return new MenuItem("Copy", null, () => RequestName($"Copy {item.path}", name => {
				if((Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.Combine(item.path, name)) is { } f && !Path.Exists(f)) {
					File.Copy(item.path, f);
					if(item.path.StartsWith(cwd)) {
						RefreshCwd();
						pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
					}
					return true;
				}
				return false;
			}, item.path));
			yield return new MenuItem("Move", null, () => RequestName($"Move {item.path}", name => {
				if((Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.Combine(item.path, name)) is { } f && !Path.Exists(f)) {
					File.Move(item.path, f);
					if(item.path.StartsWith(cwd)) {
						RefreshCwd();
						pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
					}
					return true;
				}
				return false;
			}, item.path));

		}
	}
	public IEnumerable<MenuItem> GetSingleActions (Main main, PathItem item) =>
		[new MenuItem("Cancel", "", () => {
		return;
		}),
		.. GetInstanceActions(main, item),
		.. ctx.GetCommands(item),
		.. GetStaticActions (main, item)];

	public IEnumerable<MenuItem> GetGroupActions (Main main, PathItem[] items) =>
		[new MenuItem("Cancel", "", () => {
			return;
		}),
		.. GetStaticGroupActions(main, items)
	];
	public IEnumerable<MenuItem> GetStaticGroupActions(Main main, PathItem[] items) {
		yield return new MenuItem("Compress", null, () => {
			RequestName("Archive Name", name => {
				using var f = File.Create(name);
				using var zip = new ZipArchive(f, ZipArchiveMode.Create);
				var par = Directory.GetParent(items[0].path).FullName;
				foreach(var item in items) {
					if(item.dir) {
						var subitems =
							from path in Directory.GetFileSystemEntries(item.path, "*", SearchOption.AllDirectories)
							where File.Exists(path)
							select path;
						foreach(var sub in subitems) {
							zip.CreateEntryFromFile(sub, sub.Replace(par, ""));
						}
					} else {
						zip.CreateEntryFromFile(item.path, item.path.Replace(par, ""));
					}
				}
				return true;
			}, $"{items[0].path}/{items[0].local}.zip");
		});
	}

	public static bool RequestConfirm (string title) {
		var confirm = new Button() {
			Title = "Confirm",
		};
		var cancel = new Button() {
			Title = "Cancel",
		};
		var d = new Dialog() {
			Title = title,
			Buttons = [confirm, cancel],
			Width = 32,
			Height = 3,
		};
		bool result = false;
		void Confirm () {
			result = true;
			d.RequestStop();
		}
		confirm.MouseClick += (a, e) => Confirm();
		cancel.MouseClick += (a, e) => d.RequestStop();
		Application.Run(d);
		return result;
	}
	public static void RequestName (string title, Predicate<string> accept, string first = null) {
		var confirm = new Button() {
			Title = "Confirm",
			Enabled = false
		};
		var cancel = new Button() {
			Title = "Cancel",
		};
		var d = new Dialog() {
			Title = title,
			Buttons = [confirm, cancel],
			Width = 48, Height = 6,
		};
		var input = new TextField() {
			X = 1,
			Y = 1,
			Width = Dim.Fill(2),
			Height = 1,
			Text = first ?? ""
		};
		input.TextChanging += (_,e) => confirm.Enabled = e.NewValue.Any();
		input.KeyDownD(new() {
			[(int)Enter] = _ => Confirm()
		});
		void Confirm () {
			if(accept(input.Text.ToString())) {
				d.RequestStop();
			}
		}
		confirm.MouseClick += (a,e) => Confirm();
		cancel.MouseClick+= (a,e)=>d.RequestStop();
		d.Add(input);
		input.SetFocus();
		Application.Run(d);
	}
	PathItem GetPathItem (string path) => ctx.GetPathItem(path, GetProps);
	/// <summary>
	/// Do not check directory properties here as this is where we assign properties to begin with.
	/// </summary>
	/// <param name="path"></param>
	private void RefreshRepo (string path) {
		//TODO: Setup File System Watcher
		var item = GetPathItem(path);
		if(git is { root: { } root }) {
			var stillInRepo = path.StartsWith(root);
			if(stillInRepo) {
				//If we're in the repo but not at root, remember that this directory is within the repository
				if(path != root && !item.HasProp(IN_REPOSITORY)) {
					var next = IN_REPOSITORY.Make<RepoItem>(git.repo.CalcRepoItem(path));
					//TO DO: fix adding attributes
					ctx.pathData[path] = new(item.local, item.path, new([.. item.propSet, next]));
				}
				RefreshChanges();
			} else {
				gitData.Clear();
				git?.Clear();
				git = default;
			}
		} else if(Directory.Exists($"{path}/.git")) {
			//Mark this directory as a known repository.

			ctx.pathData[path] = new(item.local, item.path, new([.. item.propSet, IS_REPOSITORY]));
			SetRepo(path);
			RefreshChanges();
		} else if(item.GetProp<RepoItem>(IN_REPOSITORY, out var repoItem, out var prop)) {
			if(Directory.Exists($"{repoItem.root}/.git")) {
				//We already know that this directory is within some repository from a previous visit.
				SetRepo(repoItem.root);
				RefreshChanges();
			} else {
				//Error
				ctx.pathData[path] = new(item.local, item.path, new(item.propSet.Except([prop])));
			}
		}
	}
	void RefreshDirListing (string s) {
		try {
			cwdData.list.Clear();
			cwdData.list.AddRange(Directory.GetDirectories(s).Select(GetPathItem).Concat(Directory.GetFiles(s).Select(GetPathItem)).OrderByDescending(p => File.GetLastWriteTimeUtc(p.path)));
		}catch(UnauthorizedAccessException e) {
		}
	}
	public void RefreshChanges () {
		IEnumerable<GitItem> GetItems () {
			foreach(var item in git.repo.RetrieveStatus()) {
				GitItem GetItem (bool staged) => new GitItem(item.FilePath, Path.GetFullPath($"{git.root}/{item.FilePath}"), staged);
				if(item.State switch {
					FileStatus.ModifiedInIndex => GetItem(true),
					FileStatus.ModifiedInWorkdir => GetItem(false),
					_ => null
				} is { } it) {
					yield return it;
				}
			}
		}
		var items = GetItems().ToList();
		gitMap = items.ToDictionary(item => item.path);
		gitData = items;
		repoList.SetSource(gitData);
		foreach(var (i, it) in gitData.Index()) {
			repoList.Source.SetMark(i, it.staged);
		}
	}
	void RefreshAddressBar () {
		var anonymize = true;
		var userProfile = ctx.USER_PROFILE;
		var showCwd = cwd;
		if(fx.workroot is { } root) {
			showCwd = showCwd.Replace(root, Fx.WORK_ROOT);
			userProfile = userProfile.Replace(root, Fx.WORK_ROOT);
		}
		if(anonymize) {
			showCwd = showCwd.Replace(userProfile, USER_PROFILE_MASK);
		}
		//Anonymize
		addressBar.Text = showCwd;
		Console.Title = showCwd;
		pathList.SelectedItem = Math.Min(Math.Max(0, pathList.Source.Count - 1), lastIndex.GetValueOrDefault(cwd, 0));
		pathList.SetNeedsDisplay();
	}
	void RefreshCwd () {
		var path = cwd;
		lastIndex[path] = pathList.SelectedItem;
		//Refresh the repo in case it got deleted for some reason
		RefreshRepo(path);
		RefreshDirListing(path);
		if(ctx.pathData[cwd].HasProp(IN_REPOSITORY)) {
			RefreshChanges();
		}
		RefreshAddressBar();
	}

	void ShowZip (string path) {
		PathItem GetZipItem (string path, HashSet<IProp> props) => new PathItem(Path.GetFileName(path), Path.GetFullPath($"{zipRoot}/{path}"), props);
		PathItem GetZipFile (string path) => GetZipItem(path, [IN_ZIP.Make(zipRoot), IS_FILE]);
		PathItem GetZipDir (string path) => GetZipItem(path, [IN_ZIP.Make(zipRoot), IS_DIRECTORY]);

		using var zip = ZipFile.OpenRead(zipRoot);
		var curr = path.Replace($"{zipRoot}", null).TrimStart('/', '\\');
		cwdData.list.Clear();
		HashSet<string> dirs = [];
		HashSet<string> files = [];
		foreach(var f in zip.Entries) {
			var par = Path.GetDirectoryName(f.FullName);
			if(par == curr) {

				if(Path.GetFullPath($"{zipRoot}/{f.FullName}/") is { }l && Path.GetFullPath($"{zipRoot}/{curr}/") is { }r && l == r) {
					continue;
				}
				files.Add(f.FullName);
				continue;
			}
			if(Path.GetDirectoryName(par) == curr) {
				dirs.Add(par);
				continue;
			}
		}
		cwdData.list.AddRange([.. dirs.Select(GetZipDir), .. files.Select(GetZipFile)]);
	}
	void SetCwd (string dest) {
		var path = Path.GetFullPath(dest);
		if(zipRoot is { } r && path.StartsWith(r) == true) {
			ShowZip(path);
			int i = 0;
			//cwdData.list.AddRange(Directory.GetDirectories(s).Select(GetPathItem).Concat(Directory.GetFiles(s).Select(GetPathItem)).OrderByDescending(p => File.GetLastWriteTimeUtc(p.path)));
		} else {
			zipRoot = null;
			RefreshRepo(path);
			RefreshDirListing(path);
		}
		lastIndex[cwd] = pathList.SelectedItem;
		cwd = path;
		RefreshAddressBar();
	}

	void GoPath(string dest) {
		cwdNext.Clear();
		cwdPrev.AddLast(cwd);
		SetCwd(dest);
		RefreshPads();
	}
	bool TryGoPath (string dest) {
		var f = Path.GetFileName(cwd);
		if(fx.workroot is { } root && !dest.Contains(root)) {
			return false;
		}

		if(Directory.Exists(dest)) {
			GoPath(dest);
			if(cwdData.list.FindIndex(p => p.local == f) is {}ind and not -1) {
				pathList.SelectedItem = ind;
			}
			return true;
		} else if(zipRoot is { }r && File.Exists(r) && dest.StartsWith(r) == true) {
			GoPath(dest);
			return true;
		}
		return false;
	}
	void RefreshPads () =>
		SView.ForTuple((Label pad, IEnumerable<string> items) => {
			pad.Enabled = items.Any();
		}, [
			(goLeft, Up()), (goNext, cwdNext), (goPrev, cwdPrev)
		]);
	private IEnumerable<string> Up () {
		var curr = Path.GetDirectoryName(cwd);
		while(curr != null) {
			yield return curr;
			curr = Path.GetDirectoryName(curr);
		}
	}
	bool GetItem (out PathItem p) {
		if(pathList.SelectedItem >= cwdData.Count) {
			p = null;
			return false;
		}
		p = cwdData.list[pathList.SelectedItem];
		return true;
	}
	bool GetItem (out PathItem p, out int index) {

		var ind = pathList.SelectedItem;
		if(!(ind > -1 && ind < cwdData.Count)) {
			p = null;
			index = -1;
			return false;
		}
		p = cwdData.list[index = ind];
		return true;
	}
	public static void Preview (string title, string content) {
		var d = new Dialog() {
			Title = title
		};
		d.KeyDownD(new() {
			[(int)Enter] = _ => d.RequestStop()
		});
		var tv = new TextView() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Text = content,
			ReadOnly = true,
		};
		d.ColorScheme = d.ColorScheme with {
			Focus = new Terminal.Gui.Attribute(Color.White, Color.Black)
		};
		tv.ColorScheme = d.ColorScheme;
		d.Add(tv);
		Application.Run(d);
	}
	private void SetRepo(string root) {
		git = new(root, new Repository(root));
	}
}
public record ProcItem (Process p) {
	public override string ToString () => $"{p.ProcessName,-24}{p.Id,-8}";
}
public record PathItem (string local, string path, HashSet<IProp> propSet) {
	public readonly Dictionary<string, IProp> propDict =
		propSet.ToDictionary(p => p.id, p => p);
	public bool HasProp (IProp p) => propSet.Contains(p);
	public bool HasProp (IPropGen p) => propDict.ContainsKey(p.id);
	public bool HasPropData<T> (IPropGen p, T data) where T : notnull => propDict.TryGetValue(p.id, out var prop) && data.Equals(((Prop<T>)prop).data);
	public bool GetProp (IPropGen p, out IProp prop) => propDict.TryGetValue(p.id, out prop);
	public bool GetProp<T> (IPropGen p, out T data) {
		if(propDict.TryGetValue(p.id, out var prop)) {
			data = ((Prop<T>)prop).data;
			return true;
		} else {
			data = default(T);
			return false;
		}
	}
	public bool GetProp<T> (IPropGen p, out T data, out IProp prop) {
		if(propDict.TryGetValue(p.id, out prop)) {
			data = ((Prop<T>)prop).data;
			return true;
		} else {
			data = default(T);
			return false;
		}
	}
	public T GetProp<T> (IPropGen p) => ((Prop<T>)propDict[p.id]).data;
	public bool dir => HasProp(IS_DIRECTORY);
	public bool isLocked => HasProp(IS_LOCKED);
	//public string type => dir ? "📁" : "📄";
	//public string locked => restricted ? "🔒" : " ";
	public string tag => $"{local}{(dir ? "/" : " ")}";
	public string locked => isLocked ? "~" : "";
	public string staged => HasProp(IS_STAGED) ? "+" : HasProp(IS_UNSTAGED) ? "*" : "";
	public string str => $"{tag,-24}{locked,-2}{staged,-2}";
	public override string ToString () => str;
}
public record GitItem (string local, string path, bool staged) {
	public override string ToString () => $"{Path.GetFileName(local)}";
}
public record RepoPtr (string root, Repository repo) {
	public void Clear () {
		repo.Dispose();
	}
}
