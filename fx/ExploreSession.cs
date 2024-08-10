using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using LibGit2Sharp;
using Terminal.Gui;
using File = System.IO.File;
using Application = Terminal.Gui.Application;
using Label = Terminal.Gui.Label;
using Repository = LibGit2Sharp.Repository;
using static Ctx;
using static fx.Props;
using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
namespace fx;
public class ExploreSession {
	public View root;
	public record SortMode(Func<PathItem, IComparable> f, bool reverse);
	public Dictionary<string, int> lastIndex = [];
	public string cwd = Environment.CurrentDirectory;
	public LinkedList<string> cwdPrev = [];
	public LinkedList<string> cwdNext = [];
	private ListMarker<PathItem> cwdData;
	public SortMode pathSort;
	Dictionary<string, GitItem> gitMap = [];
	private Label goPrev, goNext, goLeft, goTo;
	private TextField addressBar;
	public ListView pathList;
	//When we cd out of a repository, we immediately forget about it.
	private RepoPtr? git;
	private string? cwdRecall;
	Regex? nameFilter;
	Action<string> cwdChanged;
	public string? zipRoot;
	private Main main;
	private LibraryRoot libraryRoot;

	private Ctx ctx => main.ctx;
	private Fx fx => ctx.fx;
	public ExploreSession (Main main, string initCwd) {
		this.main = main;
		this.cwd = initCwd;

		var sortModes = new Func<PathItem, IComparable>[] {
			p => Path.GetFileName(p.path),
			p => p.strType,
			p => p.size,
			p => File.GetLastAccessTime(p.path).Ticks,
			p => main.ctx.fx.accessCount.GetOrAdd(p.path, 0),
			p => (gitMap.TryGetValue(p.path, out var v) ? 10 + (v.staged ? 1 : 0) : 0),
			//p => File.GetLastWriteTime(p.path),
		};

		/*

			sortName,
			sortSize,
			sortType,
			sortAccessDate,
			sortAccessFreq,
			sortGit
		*/
		main.FilesChanged += e => {
			RefreshCwd();
		};


		pathSort = new SortMode(sortModes[0], false);
		main.TermEnter += OnTermEnter;
		root = new View {
			Title = "Root",
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var tree = new QuickAccessTree { };
		var quickAccess = new TreeView<IFileTree> {
			Title = "Quick Access",
			BorderStyle = LineStyle.Single,
			X = 0,
			Y = 0,
			Width = 24,
			Height = Dim.Fill(),
			TreeBuilder = tree,
			AspectGetter = tree.AspectGetter
		};

		quickAccess.AddObjects(tree.GetRoots(main));
		foreach(var t in quickAccess.Objects)
			quickAccess.Expand(t);
		var pathPane = new View {
			//Title = "Directory",
			BorderStyle = LineStyle.Single,

			X = Pos.Right(quickAccess),
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(), //30,
		};
		cwdChanged += s => pathPane.Title = s;
		var filter = new TextField {
			
			X = 0,
			Y = 0,
			Width = Dim.Fill(12),
			Height = 1,
			ColorScheme = Application.Top.ColorScheme with {
				Focus = new(Color.White, new Color(31, 31, 31))
			},
			TabStop =  TabBehavior.NoStop
		};



		int refresh = 0;
		void Iteration (object? a, object e)=>
			refresh++;
		Application.Iteration += Iteration;
		CancellationTokenSource? filterCancel = null;
		filter.TextChanging += (a, e) => {
			Regex? MakeRegex(string s) {
				try {
					return new Regex(s);
				} catch {
					return null;
				}
			}
			if(e.NewValue is { Length: > 0 } s) {
				nameFilter = MakeRegex(s) ?? null;
			} else {
				nameFilter = null;
			}
			filterCancel?.Cancel();
			filterCancel = new();
			refresh = 0;
			Task.Run(() => {
				RefreshDirListing(cwd);
				//Auto-refresh does not work if processing takes more than one iteration
				if(refresh > 1) {
					Application.Refresh();
				}
			}, filterCancel.Token);
		};
		filter.KeyDownD(new() {
			[(int)Enter] = e => {
				pathList.SetFocus();
				if(pathList.SelectedItem == -1 && cwdData.Count > 0) {
					pathList.SelectedItem = 0;
				}
			}
		});
		goPrev = new(){ Title = "[<---]", X = Pos.AnchorEnd(24), TabStop = TabBehavior.TabStop };
		goNext = new() { Title = "[--->]", X = Pos.Right(goPrev), TabStop =  TabBehavior.TabStop };
		goLeft = new() { Title = "[/../]", X = Pos.Right(goNext), TabStop = TabBehavior.TabStop };
		goTo   = new () { Title = "[Goto]", X = Pos.Right(goLeft), TabStop = TabBehavior.TabStop };

		cwdData = new((p, i) => p.entry);

		var sortName = new Label {
			Text = "name",
			
			X = 7,
			Y = 1,
			Width = 32
		};
		var sortType = new Label {
			Text = "ext",
			
			X = Pos.Right(sortName),
			Y = 1,
			Width = 6
		};
		var sortSize = new Label {
			Text = "logSize",
			
			X = Pos.Right(sortType),
			Y = 1,
			Width = 8
		};
		var sortAccessDate = new Label {
			Text = "lastAccess",
			
			X = Pos.Right(sortSize),
			Y = 1,
			Width = 12
		};

		var sortAccessFreq = new Label {
			Text = "views",
			
			X = Pos.Right(sortAccessDate),
			Y = 1,
			Width = 6
		};
		var sortGit = new Label {
			Text = "repo",
			
			X = Pos.Right(sortAccessFreq),
			Y = 1,
			Width = 8
		};
		var sorts = Enumerable.Except([
			sortName,
			sortType,
			sortSize,
			sortAccessDate,
			sortAccessFreq,
			sortGit
		], [null]).ToArray();
		pathList = new ListView {
			//Title = "Directory Items",
			X = 0,
			Y = Pos.Bottom(sortName),
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			AllowsMultipleSelection = true,
			AllowsMarking = true,
			Source = cwdData,
			HasFocus = true,
			SelectedItem = 1
		};
		InitTreeLocal();
		InitEvents();
		InitCwd();
		RefreshPads();
		void InitTreeLocal() {
			SView.InitTree(
				[pathPane, filter, goPrev, goNext, goLeft, goTo, sortName, sortSize, sortType, sortAccessDate, sortAccessFreq, sortGit, pathList],
				[root, addressBar, quickAccess, pathPane]
				);
		}
		void InitEvents () {
			quickAccess.ObjectActivated += (o, e) => {
				if(e.ActivatedObject is IFilePath item) {
					Go(GetPathItem(item.path));
				}
			};
			quickAccess.KeyDownD(value: new() {
				[(int)Enter | (int)ShiftMask] = _ => {
					if(quickAccess.SelectedObject is IFilePath { path: { }p } && Directory.Exists(p)) {
						main.folder.AddTab("Expl", new ExploreSession(main, p).root, true, root);
					}
					return;
				},
				['"'] = _ => {
					if(quickAccess.SelectedObject is IFilePath item && File.Exists(item.path)) {
						ExploreSession.ShowPreview($"Preview: {item.path}", File.ReadAllText(item.path));
					}
				},
				['?'] = _ => {
					if(quickAccess.SelectedObject is IFilePath item) {
						ExploreSession.ShowProperties(ctx.GetPathItem(item.path, ExploreSession.GetStaticProps));
					}
				},
				['/'] = _ => {
					var rowObj = quickAccess.SelectedObject;
					var (row, col) = quickAccess.GetObjectPos(rowObj) ?? (0, 0);
					SView.ShowContext(quickAccess, [.. HomeSession.GetSpecificActions(quickAccess, main, row, rowObj)], row + 2, col + 2);
				},
				['<'] = e => {
					e.Handled = true;
					main.folder.SwitchTab(-1);
				},
				['>'] = e => {
					e.Handled = true;
					main.folder.SwitchTab(1);
				}
			});
			quickAccess.MouseEvD(new() {
				[(int)Button1Pressed] = e => {
					e.Handled = true;
					var y = e.MouseEvent.Position.Y;
					var row = y + quickAccess.ScrollOffsetVertical;
					var rowObj = quickAccess.GetObjectOnRow(row);
					if(rowObj == null) {
						return;
					}
					if(rowObj == quickAccess.SelectedObject) {
						if(quickAccess.IsExpanded(rowObj)) {
							quickAccess.Collapse(rowObj);
						} else {
							quickAccess.Expand(rowObj);
						}
					}
					quickAccess.SelectedObject = rowObj;
					quickAccess.SetNeedsDisplay();
				},
				[(int)Button1Clicked] = e => {
					e.Handled = true;
				},
				[(int)Button1Released] = e => {
					e.Handled = true;
					var y = e.MouseEvent.Position.Y;
					var row = y + quickAccess.ScrollOffsetVertical;
					var rowObj = quickAccess.GetObjectOnRow(row);
					if(rowObj != quickAccess.SelectedObject) {
						return;
					}
					if(quickAccess.IsExpanded(quickAccess.SelectedObject)) {
						quickAccess.Collapse(quickAccess.SelectedObject);
					} else {
						quickAccess.Expand(quickAccess.SelectedObject);
					}
				},
				[(int)Button3Pressed] = e => {
					e.Handled = true;
					var prevObj = quickAccess.SelectedObject;
					var y = e.MouseEvent.Position.Y;
					var row = y + quickAccess.ScrollOffsetVertical;
					var rowObj = quickAccess.GetObjectOnRow(row);
					var c = SView.ShowContext(quickAccess, [.. HomeSession.GetSpecificActions(quickAccess, main, row, rowObj)], y, e.MouseEvent.Position.X);
					if(row < main.ctx.fx.libraryData.Count) {
						c.MenuBar.MenuAllClosed += (a, e) => {
							if(main.ctx.fx.libraryData.Count == 0) {
								return;
							}
							if(quickAccess.GetParent(prevObj) != null) {
								quickAccess.SelectedObject = prevObj;
							} else {
								//TODO: Find suitable dest
							}
						};
					} else {
						return;
					}
				}
			});
			goPrev.MouseEvD(new() {
				[(int)Button3Clicked] = e => new ContextMenu {
					Position = e.MouseEvent.ScreenPosition,
					MenuItems = new([.. cwdPrev.Select((string p, int i) => new MenuItem(p, "", () => GoPrev(i + 1)))])
				}.Show(),
				[(int)Button1Pressed] = e => e.Handled = (GoPrev())
			});
			goNext.MouseEvD(new() {
				[(int)Button3Clicked] = e => new ContextMenu {
					Position = e.MouseEvent.ScreenPosition,
					MenuItems = new(
						[.. cwdNext.Select((string p, int i) => new MenuItem(p, "", () => GoNext(i + 1)))]),
				}.Show(),
				[(int)Button1Pressed] = e => e.Handled = (GoNext())
			});
			goLeft.MouseEvD(new() {
				[(int)Button3Clicked] = e => new ContextMenu {
					Position = e.MouseEvent.ScreenPosition,
					MenuItems = new([.. Up().Select(p => new MenuItem(p, "", () => TryGoPath(p)))])
				}.Show(),
				[(int)Button1Pressed] = e => e.Handled = (GoLeft())
			});
			void SetSort(int i) {
				var s = sortModes[i];
				pathSort = 
					pathSort.f == s ?
						pathSort with { reverse = !pathSort.reverse } :
						new SortMode(s, false);
				RefreshDirListing(cwd);
				pathList.SetNeedsDisplay();
			}
			foreach(var(i, c) in sorts.Select((c, i) => (i, c)))
				c.MouseClick += (a, e) => SetSort(i);
			pathList.MouseEvD(new() {
				[(int)Button1DoubleClicked] = e => {
					e.Handled = true;
					//cwdData.Clear();
					//pathList.SetNeedsDisplay();
					//cwdData.Toggle(pathList.SelectedItem);
				},
				[(int)Button1Pressed | (int)ButtonCtrl] = e => {
					e.Handled = true;
					var i = pathList.TopItem + e.MouseEvent.Position.Y;
					if(i >= cwdData.Count)
						return;
					cwdData.Toggle(i);

					pathList.SelectedItem = i;
					pathList.SetFocus();
					pathList.SetNeedsDisplay();
				},
				[(int)Button1Pressed] = e => {
					e.Handled = true;
					var i = pathList.TopItem + e.MouseEvent.Position.Y;
					if(i >= cwdData.Count)
						return;
					pathList.SelectedItem = i;
					pathList.SetFocus();
					pathList.SetNeedsDisplay();

				},
				[(int)Button1Released] = e => {
					e.Handled = true;
					var i = pathList.TopItem + e.MouseEvent.Position.Y;
					if(i >= cwdData.Count)
						return;
					cwdData.Toggle(pathList.SelectedItem);
					pathList.SetNeedsDisplay();
				},

				[(int)(Button1Released | ButtonCtrl)] = e => {
					e.Handled = true;
					cwdData.Toggle(pathList.SelectedItem);
					pathList.SetNeedsDisplay();
				},
				[(int)(Button1Clicked)] = e => {
				},
				[(int)Button3Pressed] = e => {
					e.Handled = true;
					var prev = pathList.SelectedItem;

					var i = pathList.TopItem + e.MouseEvent.Position.Y;
					if(i >= cwdData.Count) {
						ShowPathContext(GetPathItem(cwd), e.MouseEvent.Position.Y - 1, e.MouseEvent.Position.X - 1);
						return;
					}
					pathList.SelectedItem = i;
					var c = ShowPathContext(cwdData.list[i], e.MouseEvent.Position.Y - 1, e.MouseEvent.Position.X);
					c.MenuBar.MenuAllClosed += (object? _, EventArgs _) => {
						if(prev == -1) {
							return;
						}
						pathList.SelectedItem = prev;
					};
				},
				[(int)Button3Released] = e => {
					e.Handled = true;
				}
			});
			pathList.OpenSelectedItem += (a, e) => GoItem();
			pathList.KeyDownF(e => {
				if(!pathList.HasFocus) return null;
				return ((int)e.KeyCode) switch {
					(int)Enter or (int)CursorRight => () => GoItem(),
					(int)CursorLeft => () => TryGoPath(Path.GetDirectoryName(cwd)),
					(int)Esc => () => { },
					'A' | (int)CtrlMask => () => {
						quickAccess.SetFocus();
					},
					'F' | (int)CtrlMask => () => {
						filter.SetFocus();
					},
					'R' | (int)CtrlMask => () => {
						RefreshCwd();
					},
					'G' | (int)CtrlMask => () => {
						//Grep
					},
					' ' => () => { },
					'[' => () => GoPrev(),
					']' => () => GoNext(),
					'\\' => () => GoLeft(),
					'<' => () => { main.folder.SwitchTab(-1); },
					'>' => () => { main.folder.SwitchTab(1); },
					',' => () => {
						if(cwdRecall != null) SetCwd(cwdRecall);
					},
					'.' => () => {
						var context = ShowPathContext(GetPathItem(cwd), 0);
						context.MenuBar.KeyDownD(value: new() {
							{ '.', _ => context.Hide() }
						});
					},
					':' => () => main.FocusTerm(pathList),
					';' => () => {
						cwdData.Toggle(pathList.SelectedItem);
						pathList.SetNeedsDisplay();
						return;
					},
					'\'' => () => {
						//Copy file
					},
					'"' => () => {
						if(!GetItem(out var p)) return;
						p.ShowPreview();
					},
					'?' => () => {
						if(!GetItem(out var p)) return;
						ShowProperties(p);
					},
					'/' => () => {
						if(!GetItem(out var p, out var ind)) return;
						var c = ShowPathContext(p, ind - pathList.TopItem + 1, 1);
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
						quickAccess.SetNeedsDisplay();
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
						var pairs = cwdData.list.Select((c, Index) => (Index, c));
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
				};
			});
			/*
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
			*/
			/*
			repoList.OpenSelectedItem += (a, e) => {
				var item = gitData[e.Item];
				if(item.staged) Commands.Unstage(git.repo, item.local);
				else Commands.Stage(git.repo, item.local);
				RefreshChanges();
				repoList.SelectedItem = e.Item;
			};
			repoList.KeyDownD(new() {
				[(int)Space] = e => {

				}
			});
			repoList.MouseEvD(new() {
				[(int)Button1Clicked] = e => {

					repoList.SelectedItem = repoList.TopItem + e.MouseEvent.Position.Y;
					repoList.SetNeedsDisplay();
				},
				[(int)Button3Clicked] = e => {
					var prev = repoList.SelectedItem;
					repoList.SelectedItem = repoList.TopItem + e.MouseEvent.Position.Y;

					repoList.SetNeedsDisplay();
				}
			});
			*/

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
			ContextMenu ShowPathContext (PathItem selected, int row = 0, int col = 0) {
				var (x, y) = pathList.GetCurrentLoc();
				var bar = new MenuBarItem("This", [
					.. GetSingleActions(main, selected)
				]);
				var marked = GetMarkedItems().ToArray();
				if(marked.Except([selected]).Any()) {
					bar = new MenuBarItem([
						new MenuItem("Cancel", null, () => { }),
						bar,
						new MenuBarItem("Selected", [..GetGroupActions(main, marked)])
					]);
				}
				var c = new ContextMenu {
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
			}
		}
		void InitCwd () {
			SetCwd(cwd);
			if(cwdData.Count > 0)
				pathList.SelectedItem = 0;
			pathList.SetFocus();
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
		
		//Replace {0} with the first marked path
		string[] selected = cwdData.list.Where((item, index) => pathList.Source.IsMarked(index)).Select(item => item.local).ToArray();

		var args =
			selected.Any() ?
				selected :
				[..from item in cwdData.list select item.local];
		var cmd = e.text;
		cmd =	string.Format(cmd, args)
				.Replace("%select%", string.Join(" ", from s in selected select $@"""{s}"""))
				.Replace("%this%", $@"""{cwdData.list[pathList.SelectedItem].local}""")
				.Replace("%all%", string.Join(" ", [.. from item in cwdData.list select $@"""{item.local}"""]));
		if(cmd.MatchArray("cd (?<dest>.+)", 1) is [{ } dest]) {
			dest = Environment.ExpandEnvironmentVariables(dest);
			if(!Path.IsPathRooted(dest)) {
				dest = Path.GetFullPath(Path.Combine(cwd, dest));
			}
			if(!TryGoPath(dest)) {
			
			}
			goto Handled;
		}
		if(cmd is { Length:0 } or null) {
			return;
		}
		main.ctx.fx.commandCount.GetOrAdd(cwd, []).AddOrUpdate(cmd, 1, (_, n) => n + 1);
		//bool readProc = false;
		var pi = new ProcessStartInfo("cmd.exe") {
			WorkingDirectory = cwd,
			Arguments = @$"/c {cmd} & pause", 
			UseShellExecute = true
		};
		var p = Process.Start(pi);
		Handled:
		e.term.Text = "";
		e.Handled = true;
		Task.Run(() => {

		});
	}
	public IEnumerable<int> GetMarkedIndex () =>
		Enumerable.Range(0, cwdData.Count).Where(pathList.Source.IsMarked);
	public IEnumerable<PathItem> GetMarkedItems () =>
		GetMarkedIndex().Select(i => cwdData.list[i]);

	public static void ShowProperties (PathItem item) {
		var d = new Dialog {
			Title= $"Properties: {item.path}",
			Width = Dim.Absolute(Math.Min(108, Application.Top.Frame.Width - 8)),
		};
		d.KeyDownD(new() {
			[(int)Enter] = _ => d.RequestStop()
		});

		var tv = new TextView {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			ReadOnly = true,
			Text = string.Join("\n", item.propSet.Select(p => p.desc)),
		};
		tv.ColorScheme = d.ColorScheme with {
			Focus = new Terminal.Gui.Attribute(Color.White, Color.Black)
		};
		d.Add(tv);

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
				//WshShell shell = new WshShell(); //Create a new WshShell Interface
				//IWshShortcut link = (IWshShortcut)shell.CreateShortcut(path); //Link the interface to our shortcut
				//yield return IS_LINK_TO.Make(link.TargetPath);
			}
			if(path.EndsWith(".zip")) {
				yield return IS_ZIP;
			}
			if(path.EndsWith(".py"))
				yield return IS_PYTHON;

		} else {
			throw new Exception("Oops");
		}
	}

	public IEnumerable<IProp> GetInstanceProps(string path) {
		if(fx.locked.Contains(path)) {
			yield return IS_LOCKED;
		}
		if(Path.GetDirectoryName(path) is { } par && HasRepo(GetPathItem(par), out var root)) {
			yield return IN_REPOSITORY.Make(CalcRepoItem(root, path));
		}
		if(gitMap.Any()) {
			if(gitMap.TryGetValue(path, out var p)) {

				if(p.ignored) {
					yield return IS_GIT_IGNORED;
				} else if(p.unchanged) {
					yield return IS_GIT_UNCHANGED;
				} else if(p.staged) {
					yield return IS_GIT_STAGED;
				} else {
					yield return IS_GIT_UNSTAGED;
				}
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
			yield return new MenuItem("Extract", null, () => {
				RequestName($"Extract {item.path} to", t => { return true; });
			});
			yield break;
		}
		var RunCommand =
			new MenuItem("Command", null, () => {
				RequestName("Command", cmd => {
					cmd = string.Format(cmd, item.path);
					main.ctx.fx.commandCount.GetOrAdd(item.path, []).AddOrUpdate(cmd, 1, (_, n) => n + 1);
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
				main.folder.AddTab($"Term", session.root, true, main.root.FirstOrDefault()?.Focused);
			}) {
				Children = [
					UseSystemTerminal(item.path),
					RunCommand
				]
			};
		} else if(item.HasProp(IS_FILE)) {
			yield return new MenuBarItem("Edit", null, () => {
				main.folder.AddTab($"Edit", new EditSession(main, item.path).root, true, main.root.FirstOrDefault()?.Focused);
			}) {
				Children = [
					new MenuItem("Copy text", null, () => {
						Clipboard.TrySetClipboardData(File.ReadAllText(item.path));
					})
				]
			};
		}
		yield return new MenuBarItem("Copy Path", "", () => Clipboard.TrySetClipboardData(item.path)) {
			Children = [
				new MenuItem("Show in System", "", () => RunCmd(@$"explorer.exe /select, ""{item.path}"""))
			]
		};
		if(Path.GetDirectoryName(item.path) is { } par) {
			yield return new MenuBarItem("Parent", [
				new MenuBarItem("Explore", null, () => {
					main.folder.AddTab("Expl", new ExploreSession(main, par).root, true);
				}){
					Children = [
						new MenuItem("Show in System", "", () => RunCmd(@$"explorer.exe /select, ""{par}"""))
					]
				},

				new MenuBarItem("Term", null, () => {
					var session = new TermSession(main, item.path);
					main.folder.AddTab($"Term", session.root, true, main.root.FirstOrDefault()?.Focused);
					}) {
						Children = [
							UseSystemTerminal(item.path),
							RunCommand
						]
				}
			]);
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
					var l = new LibraryRoot(name);
					l.links.Add(new LibraryItem(item.path, true));
					main.ctx.fx.libraryData.Add(l);
					return true;
				});
			})
		]);

		if(item.HasProp(IS_REPOSITORY)) {
			using var repo = new Repository(item.path);
			var item_status = new MenuItem("status", null, () => {
				using var repo = new Repository(item.path);
				repo.RetrieveStatus();
			});
			var item_github = repo.Network.Remotes.Select((Remote r) => {
				if(Regex.Match(r.Url, "git@github.com:(?<user>[a-zA-Z]+)/(?<repo>[a-zA-Z]+).git") is { Success:true} m) {
					return new MenuItem("View on GitHub", null, () => {
						Process.Start("explorer", $"https://github.com/{m.Groups["user"].Value}/{m.Groups["repo"].Value}");
					});
				}
				return null;
			}).FirstOrDefault(r => r != null);
			yield return new MenuBarItem("Repository", [..Enumerable.Except([
				item_status,
				/*
				new MenuBarItem("Remotes", null, () => { }){
					Children = [
						//..repo.Network.Remotes.SelectMany(GetRemoteActions)
					]
				},
				*/
				item_github,
			], [null])]);
		}
		/*
		if(item.GetProp(IN_REPOSITORY, out RepoItem ri)) {
			yield return new MenuItem("cd repo root", null, () => {
				
			});
		}
		*/

		yield return new MenuBarItem("Fx", [.. GetFxItems()]);
		IEnumerable<MenuItem> GetFxItems() {
			var pins = main.ctx.fx.pins;
			var isPin = pins.Contains(item.path);
			yield return new MenuItem(isPin ? "Unpin" : "Pin", null, () =>
				((Action<string>)(isPin ? p => pins.Remove(p) : pins.Add))(item.path)
				);
			var locked = main.ctx.fx.locked;
			var isLocked = locked.Contains(item.path);
			yield return new MenuItem(isLocked ? "Unlock" : "Lock", null, () =>
				((Func<string, bool>)(isLocked ? locked.Remove : locked.Add))(item.path)
			);
			var hidden = main.ctx.fx.hidden;
			var isHidden = hidden.Contains(item.path);
			yield return new MenuItem(isHidden ? "Show" : "Hide", null, () => {
				((Func<string, bool>)(isHidden ? hidden.Remove : hidden.Add))(item.path);
				main.FilesChanged([item.path]);
			});
		}
		if(item.HasProp(IS_DIRECTORY)) {
			//yield return new MenuItem("Open in System", "", () => RunCmd($"explorer.exe {item.path}"));
			yield return new MenuItem("Delete Dir", null, () => {
				if(RequestConfirm($"Delete {item.path} [{item.local}]", item.local)) {
					Do();
					void Do() {
						try {

							Directory.Delete(item.path);
							main.FilesChanged([item.path]);
						} catch(UnauthorizedAccessException e) {
							if(RequestConfirm($"Unauthorized Access. Failed to delete {item.path}. Retry?")) {
								Do();
							}
						} catch(IOException e) {
							if(RequestConfirm($"IO Exception. Failed to delete {item.path}. Retry?")) {
								Do();
							}
						}
					}
				}
			});
		} else {
			yield return new MenuItem("Delete File", null, () => {
				if(RequestConfirm($"Delete {item.path} [{item.local}]", item.local)) {
					Do();
					void Do () {
						try {
							File.Delete(item.path);
							main.FilesChanged([item.path]);
						} catch(UnauthorizedAccessException e) {
							if(RequestConfirm($"Access denied. Retry?")) {
								Do();
							}
						}
					}
				}
			});
		}
		yield return new MenuItem("Properties", null, () => ShowProperties(item));
	}
	/// <summary>
	/// This should be refactored so that ctx handles path updates
	/// </summary>
	public IEnumerable<MenuItem> GetInstanceActions(Main main, PathItem item) {
		if(item.GetProp<ZipItem>(IN_ZIP, out var zi)) {

			var (zipRoot, zipEntry) = zi;

			if(item.HasProp(IS_DIRECTORY)) {
				yield return new MenuItem("Extract Dir", null, () => RequestName($"Extract {item.path}", dest => {
					if(!File.Exists(dest)) {
						Directory.CreateDirectory(dest);
						using var zip = ZipFile.OpenRead(zipRoot);
						var entries = zip.Entries
							.Where(e => !e.FullName.EndsWith('/') && Path.GetFullPath($"{zipRoot}/{e.FullName}").StartsWith(item.path))
							.ToArray();
						foreach(var entry in entries) {
							var entryPath = Path.GetFullPath($"{zipRoot}/{entry.FullName}");
							var sub = entryPath.Replace(Path.GetFullPath($"{item.path}/"), null);
							var entryDest = $"{dest}/{sub}";
							Directory.CreateDirectory(Path.GetDirectoryName(entryDest));
							entry.ExtractToFile(entryDest, true);
						}
						return true;
					}
					return false;
				}, $"{Path.GetDirectoryName(zipRoot)}/{Path.GetFileNameWithoutExtension(zipRoot)}"));
			} else {
				yield return new MenuItem("Extract File", null, () => RequestName($"Extract {item.path}", dest => {
					using var zip = ZipFile.OpenRead(zipRoot);
					var entry = zip.GetEntry(zipEntry);
					entry.ExtractToFile(dest);
					return true;
				}, $"{Path.GetDirectoryName(zipRoot)}/{Path.GetFileNameWithoutExtension(zipRoot)}"));
			}
			yield break;
		}
		if(item.HasProp(IS_DIRECTORY)) {
			yield return new MenuItem("Remember", null, () => cwdRecall = item.path);

			yield return new MenuBarItem("New", null, () => { }) {
				Children = [
					new MenuItem("File", null, () => RequestName("New File", name => {
						if(Path.Combine(item.path, name) is { } f && !Path.Exists(f)) {
							File.Create(f);
							if(item.path.StartsWith(cwd)) {
								RefreshCwd();
								pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
							}
							return true;
						}
						return false;
					}), canExecute: () => !item.HasProp(IS_LOCKED)),
					new MenuItem("Dir", null, () => RequestName("New Directory", name => {
						if(Path.Combine(item.path, name) is { }f && !Path.Exists(f)) {
							Directory.CreateDirectory(f);
							if(item.path.StartsWith(cwd)) {
								RefreshCwd();
								pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
							}
							return true;
						}
						return false;
					}), canExecute: () => !item.HasProp(IS_LOCKED))
				]
			};

			yield return new MenuBarItem("Zip To", null, () => RequestName($"Zip {item.path}", name => {
				return true;
			}));

			//Midnight Commander multi-move
			yield return new MenuBarItem("Copy To", null, () => RequestName($"Copy {item.path}", name => {
				if((Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.Combine(item.path, name)) is { } f && !Path.Exists(f)) {

					if(File.Exists(f)) {
						File.Copy(item.path, f);
					} else if(Directory.Exists(f)) {
					}
					
					if(item.path.StartsWith(cwd)) {
						RefreshCwd();
						pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
					}
					return true;
				}
				return false;
			}, item.path)) {
				Children = [
					new MenuItem("Move To", null, () => RequestName($"Move {item.path}", name => {
						if((Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.Combine(item.path, name)) is { } f && !Path.Exists(f)) {

							if(File.Exists(item.path)){
								File.Move(item.path, f);
							} else if(Directory.Exists(item.path)){
								Directory.Move(item.path, f);
							}
							if(item.path.StartsWith(cwd)) {
								RefreshCwd();
								var i = cwdData.list.FindIndex(p => p.path == f);
								//pathList.SelectedItem = i;
							}
							return true;
						}
						return false;
					}, item.path))
				]
			};
		} else if(item.HasProp(IS_FILE)) {
			if(Path.GetDirectoryName(item.path) is { } par && HasRepo(GetPathItem(par), out string root)) {
				string local = GetRepoLocal(root, item.path);


				var unstaged = item.HasProp(IS_GIT_UNSTAGED);
				var staged = item.HasProp(IS_GIT_STAGED);
				var changed = unstaged || staged;

				MenuItem[] git_bar = [..A()];
				if(git_bar.Any()) {
					yield return new MenuBarItem("Git", git_bar);
				}
				IEnumerable<MenuItem> A() {
					if(!changed) {
						yield break;
					}
					yield return new MenuItem("Diff", "", () => {
						using var repo = new Repository(root);
						ShowPreview($"Diff: {item.path}", repo.Diff.Compare<Patch>([local]).Content);
					});
					if(unstaged) {
						yield return new MenuItem("Stage", "", () => {
							using var repo = new Repository(root);
							Commands.Stage(repo, local);
							item.forceReload = true;
							RefreshCwd();
						});
					} else if(staged) {
						yield return new MenuItem("Unstage", "", () => {
							using var repo = new Repository(root);
							Commands.Unstage(repo, local);
							item.forceReload = true;
							RefreshCwd();
						});
					}
				}
			}
			yield return new MenuBarItem("Copy To", null, () => RequestName($"Copy {item.path}", name => {
				if((Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.Combine(item.path, name)) is { } f && !Path.Exists(f)) {
					File.Copy(item.path, f);
					if(item.path.StartsWith(cwd)) {
						RefreshCwd();
						pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
					}
					return true;
				}
				return false;
			}, item.path)) {
				Children = [
					new MenuItem("Move To", null, () => RequestName($"Move {item.path}", name => {
						if((Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.Combine(item.path, name)) is { } f && !Path.Exists(f)) {
							File.Move(item.path, f);
							if(item.path.StartsWith(cwd)) {
								RefreshCwd();
								pathList.SelectedItem = cwdData.list.FindIndex(p => p.path == f);
							}
							return true;
						}
						return false;
					}, item.path))
				]
			};

		}
	}
	public IEnumerable<MenuItem> GetSingleActions (Main main, PathItem item) => [
		new MenuItem("Cancel", "", () => { }),
		.. GetInstanceActions(main, item),
		.. ctx.GetCommands(item),
		.. GetStaticActions (main, item)
	];
	public IEnumerable<MenuItem> GetGroupActions (Main main, PathItem[] items) => [
		new MenuItem("Cancel", null, () => { }),
		new MenuItem("Deselect", null, () => {
			cwdData.marked.Clear();
			pathList.SetNeedsDisplay();
		}),
		.. GetStaticGroupActions(main, items)
	];
	public static IEnumerable<MenuItem> GetStaticGroupActions(Main main, PathItem[] items) {
		yield return new MenuItem("Compress", null, () => {
			RequestName("Archive Name", name => {
				using var f = File.Create(name);
				using var zip = new ZipArchive(f, ZipArchiveMode.Create);
				var par = Directory.GetParent(items.First().path).FullName;
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
		yield return new MenuItem("Hide All", null, () => {
			main.ctx.fx.hidden.UnionWith(from item in items select item.path);
			main.FilesChanged([..from item in items select item.path]);
		});
		yield return new MenuItem("Show All", null, () => {
			main.ctx.fx.hidden.ExceptWith(from item in items select item.path);
			main.FilesChanged([.. from item in items select item.path]);
		});
	}
	public static bool RequestConfirm (string title, string password = "") {
		var confirm = new Button() {
			Title = "Confirm",
			X = 1,
			Y = 1,
		};
		var cancel = new Button() {
			Title = "Cancel",
			X = Pos.Right(confirm),
			Y = 1,
		};
		var d = new Dialog() {
			Title = title,
			Width = 80,
			Height = 4,
		};
		var tv = new TextField() {
			X = 1,
			Y = 0,
			Width = Dim.Fill(1),
			Height = 1,
			HasFocus = true
		};
		tv.KeyDownD(new() {
			[(int)Enter] = e => {
				Confirm();
			}
		});
		d.Add([tv, confirm, cancel]);
		bool result = false;
		void Confirm () {
			if(tv.Text != password) {
				return;
			}
			result = true;
			d.RequestStop();
		}
		d.KeyDownD(new() {
			[(int)Enter] = e => {
				Confirm();
			}
		});
		confirm.MouseClick += (a, e) => Confirm();
		cancel.MouseClick += (a, e) => d.RequestStop();
		tv.SetFocus();
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
			Width = 80, Height = 6,
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
			[(int)Enter] = _ => {	
				Confirm();
			}
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
		//TO DO: clean up spaghetti
		var item = GetPathItem(path);
		if(git is { root: { } root }) {
			var stillInRepo = path.StartsWith(root);
			if(stillInRepo) {
				//If we're in the repo but not at root, remember that this directory is within the repository
				if(path != root && !item.HasProp(IN_REPOSITORY)) {

					using var r = new Repository(git.root);

					var next = IN_REPOSITORY.Make(r.CalcRepoItem(path));
					//TO DO: fix adding attributes
					ctx.cache.item[path] = new(item.local, item.path, new([.. item.propSet, next]));
				}
				RefreshChanges();
			} else {
				git = default;
			}
		} else if(Directory.Exists($"{path}/.git")) {
			//Mark this directory as a known repository.
			ctx.cache.item[path] = new(item.local, item.path, new([.. item.propSet, IS_REPOSITORY]));
			SetRepo(path);
			RefreshChanges();
		} else if(item.GetProp<RepoItem>(IN_REPOSITORY, out var repoItem, out var prop)) {
			if(Directory.Exists($"{repoItem.root}/.git")) {
				//We already know that this directory is within some repository from a previous visit.
				SetRepo(repoItem.root);
				RefreshChanges();
			} else {
				//Error
				ctx.cache.item[path] = new(item.local, item.path, new(item.propSet.Except([prop])));
			}
		}
	}
	string GenerateEntry(PathItem p) {
		var len = new {
			name = 32
		};
		var localName =
			p.local.Length <= len.name ?
				p.local :
				$"{p.local[..(len.name - 2)]}..";
		string GetSize(PathItem p) {
			//Add option to skip calculating dir size
			if(p.dir) {
				return "";
			}
			return fmt(p.size);
			string fmt(long l) {
				var log = l == 0 ? 0 : (int)(Math.Log2(l) / 10);
				var b = (int)Math.Pow(2, 10 * log);
				return $"{log,2} {$"{l / b}".PadLeft(4, '0')}";
			}
		}
		if(zipRoot != null) {
			using var z = ZipFile.OpenRead(zipRoot);
			var local = $"{p.path[(zipRoot.Length + 1)..]}{(p.dir ? "/" : "")}".Replace("\\", "/");
			var e = z.GetEntry(local);
			var name = localName.PadRight(len.name);
			var type = p.strType[..Math.Min(6, p.strType.Length)].PadRight(6);
			var size = $"{e.Length/1024}".PadRight(8);
			var lastWrite = e.LastWriteTime.ToString("MM/dd HH:mm").PadRight(12);
			var timesOpened = 0.ToString().PadLeft(6);
			return $"{name}{type}{size}{lastWrite}{timesOpened}";
		} else {
			var name = localName.PadRight(len.name);
			var type = p.strType[..Math.Min(6, p.strType.Length)].PadRight(6);
			var size = $"{GetSize(p)}".PadRight(8);
			var lastWrite = p.lastWrite.ToString("MM/dd HH:mm").PadRight(12);
			var openFreq = main.ctx.fx.accessCount.GetValueOrDefault(p.path, 0).ToString().PadLeft(4).PadRight(6);
			var gitInfo =
				(p.HasProp(IS_GIT_UNSTAGED) ?
					$"{new GlyphDefinitions().CheckStateUnChecked}" ://"\u2574" :
				p.HasProp(IS_GIT_STAGED) ?
					$"{new GlyphDefinitions().CheckStateChecked}" :
				p.HasProp(IS_GIT_IGNORED) ?
					$"i" :
				p.HasProp(IS_GIT_UNCHANGED) ?
					"." :

				//p.HasProp(IS_REPOSITORY) ? "*" :
					"").PadRight(8);
			return $"{name}{type}{size}{lastWrite}{openFreq}{gitInfo}";
		}
	}
	void RefreshDirListing (string s) {
		try {
			if(s == Path.GetFullPath("%APPDATA%/fx/libraries/")) {
				return;
			}
			PathItem transform (PathItem item) {
				var path = Path.GetFullPath(item.path);
				try {
					if(path == null || 0 != (File.GetAttributes(path) & FileAttributes.System)) {
						return null;
					}
					while(Directory.Exists(path) && Directory.GetFileSystemEntries(path) is [string sub])
						path = sub;
				} catch(UnauthorizedAccessException e) { return null; }
				catch(DirectoryNotFoundException e) { return null; }
				if(item.path == path) return item;
				var l = Path.GetDirectoryName(item.path).Length + 1;
				return new PathItem(path[l..].Replace("\\", "/"), path, [.. GetProps(path)]);
			}
			const string LIBRARIES = "fx:/libraries/";
			if(s.StartsWith(LIBRARIES)) {
				var libraryRoot = main.ctx.fx.libraryData[int.Parse(s[LIBRARIES.Length..])];
				var libraryItems = libraryRoot.links.Select(l => l.path);
				PathItem[] items = [
					..libraryItems.SelectMany(l => Directory.GetDirectories(l)
						.Select(GetPathItem)
						.Select(transform))
						.Except([null])
						.OrderPath(pathSort),
					..libraryItems.SelectMany(l => Directory.GetFiles(l)
						.Select(GetPathItem))
						.OrderPath(pathSort)
				];
			} else {
				if(0 != (File.GetAttributes(s) & FileAttributes.System)) {
					return;
				}
				var up = new Lazy<PathItem>(() => {
					return Path.GetDirectoryName(s) is { } u ? new PathItem("..", u, [.. GetProps(u)]) : null;
				}).Value;
				var upper = new Lazy<PathItem>(() => {
					var item = new PathItem("../..", up?.path ?? ".", []);
					if(Path.GetDirectoryName(up?.path) is { }p && Directory.GetFileSystemEntries(p) is { Length: 1 }) {
						while(Directory.GetFileSystemEntries(item.path) is { Length: 1 } && Path.GetDirectoryName(item.path) is { } next)
							item = new PathItem($"{item.local}/..", next, []);
						return item with { propSet = [.. GetProps(p)] };
					}
					return null;
				}).Value;
				Func<PathItem, bool>? f =
					nameFilter is { } nf ?
						s => nf.IsMatch(s.local) :
						null;
				var (dirs, files) = main.ctx.GetSubPaths(s);
				IEnumerable<PathItem> items = [
					up, upper,
					..dirs
						.Select(GetPathItem)
						.Select(transform)
						.Except([null])
						.Where(p => !main.ctx.fx.hidden.Contains(p.path))
						.MaybeWhere(f)
						.OrderPath(pathSort),
					..files
						.Select(GetPathItem)
						.Where(p => !main.ctx.fx.hidden.Contains(p.path))
						.MaybeWhere(f)
						.OrderPath(pathSort)
				];
				var _items = items.Except([null]).ToList();
				foreach(var (index, item) in _items.Select((c, i) => (i, c))) {
					/*
					if(item.entry is { Length: > 0 })
						continue;
					*/
					//TODO: fix file number
					item.entry = $"{index, 3}| {GenerateEntry(item)}";
				}

				cwdData.list = _items;
				pathList.SetNeedsDisplay();
			}
		}catch(UnauthorizedAccessException e) {
		}
	}
	public void RefreshChanges () {
		using var repo = new Repository(git.root);
		IEnumerable<GitItem> GetItems () {
			var status = repo.RetrieveStatus();
			
			return from item
				   in status
				   select new GitItem(item.FilePath, Path.GetFullPath($"{git.root}/{item.FilePath}"), item.State);
		}
		var items = GetItems().ToList();
		gitMap = items.ToDictionary(item => item.path);
	}
	void RefreshAddressBar () {
		var userProfile = Ctx.USER_PROFILE;
		var showCwd = cwd;
		if(fx.workroot is { } root) {
			showCwd = showCwd.Replace(root, Fx.WORK_ROOT);
			userProfile = userProfile.Replace(root, Fx.WORK_ROOT);
		}
		showCwd = showCwd.Replace(userProfile, USER_PROFILE_MASK);
		cwdChanged?.Invoke(showCwd);
		Console.Title = $"[fx] {showCwd}";
		pathList.SelectedItem = Math.Min(Math.Max(0, pathList.Source.Count - 1), lastIndex.GetValueOrDefault(cwd, 0));
		pathList.SetNeedsDisplay();
	}
	void RefreshCwd () {
		var path = cwd;
		lastIndex[path] = pathList.SelectedItem;
		//Refresh the repo in case it got deleted for some reason
		RefreshRepo(path);
		RefreshDirListing(path);
		if(ctx.cache.item[cwd].HasProp(IN_REPOSITORY)) {
			RefreshChanges();
		}
		RefreshAddressBar();
	}

	void ShowZip (string path) {
		PathItem GetZipItem (string path, HashSet<IProp> props) => new PathItem(Path.GetFileName(path), Path.GetFullPath($"{zipRoot}/{path}"), props);
		PathItem GetZipFile (string path) => GetZipItem(path, [IN_ZIP.Make(ZipItem.From(zipRoot, path)), IS_FILE]);
		PathItem GetZipDir (string path) => GetZipItem(path, [IN_ZIP.Make(ZipItem.From(zipRoot, path)), IS_DIRECTORY]);

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

		return;
	}
	void SetCwd (string dest) {
		var path = Path.GetFullPath(dest);


		bool isZip() {
			if(zipRoot is { } r && path.StartsWith(r) == true) {
				return true;
			}
			if(GetPathItem(path).HasProp(IS_ZIP)) {
				zipRoot = path;
				return true;
			}
			return false;

		}
		if(isZip()) {
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
	void Go (PathItem i) {
		if(i.HasProp(IS_LOCKED)) {
			return;
		}
		if(i.HasProp(IN_ZIP) && i.dir) {
			GoPath(i.path);
			return;
		}
		if(i.HasProp(IS_ZIP)) {
			GoPath(i.path);
			return;
		}
		zipRoot = null;

		if(i.GetProp<string>(IS_LINK_TO, out var dest)) {
			var destItem = GetPathItem(dest);
			Go(destItem);
			return;
		}
		if(i.dir) {
			TryGoPath(i.path);
			return;
		}
		main.ctx.fx.accessCount.AddOrUpdate(i.path, 1, (p, n) => n + 1);
		main.ctx.fx.lastOpened[i.path] = DateTime.Now;
		RunCmd(@$"cd {cwd} & ""{i.path}""", cwd);
	}
	void GoPath(string dest) {
		cwdNext.Clear();
		cwdPrev.AddLast(cwd);
		SetCwd(dest);
		RefreshPads();


		main.ctx.fx.accessCount.AddOrUpdate(dest, 1, (p, n) => n + 1);
		main.ctx.fx.lastOpened[dest] = DateTime.Now;
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
	public static void ShowPreview (string title, string content) {
		var d = new Dialog() {
			Title = title,

			Width = Dim.Absolute(Math.Min(108, Application.Top.Frame.Width - 8)),
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
		tv.ColorScheme = d.ColorScheme with {
			Focus = new Terminal.Gui.Attribute(Color.White, Color.Black)
		};
		d.Add(tv);
		Application.Run(d);
	}
	private void SetRepo(string root) {
		git = new(root);
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
	public string staged => HasProp(IS_GIT_STAGED) ? "+" : HasProp(IS_GIT_UNSTAGED) ? "*" : "";
	public string str => $"{tag,-24}{locked,-2}{staged,-2}";

	private string _strType = null;
	public string strType =>
		_strType ??= 
			HasProp(IS_REPOSITORY) ?
				"dir/repo" :
			dir ?
				"dir" :
			GetProp(IS_LINK_TO, out string linkto) ?
				"lnk" :
			Path.GetExtension(path) is { Length:>0 }ext ?
				ext.ToLower() :
			"file";
	public bool forceReload = false;


	public static DateTime GetLastWrite(string path) =>
		Directory.Exists(path) ?
			Directory.GetLastWriteTime(path) :
			File.GetLastWriteTime(path);

	public bool unchanged => lastWrite >= GetLastWrite(path);
	public bool isCurrent => unchanged && !forceReload;
	public DateTime lastWrite = GetLastWrite(path);
	public DateTime lastAccess = File.GetLastAccessTime(path);
	private long _size = -1;
	public long size => _size != -1 ? _size : _size = GetSize();
	private long GetSize () {
		long GetDirSize (string path) {
			try {
				return Directory.GetFileSystemEntries(path).Select(p =>
					Directory.Exists(p) ?
						GetDirSize(p) :
						new FileInfo(p).Length).Sum();
			} catch(UnauthorizedAccessException e) {
				return 0;
			}
		}
		return dir ? GetDirSize(path) : new FileInfo(path).Length;
	}
	public override string ToString () => str;
	public void ShowPreview () {
		ExploreSession.ShowPreview($"Preview: {path}",
			dir ?
				string.Join("\n",
					Directory.GetFileSystemEntries(path)
						.Select(e => e[(path.Length + 1)..])
					) :
				File.ReadAllText(path));
	}
	public string entry = "";
}
public record GitItem (string local, string path, FileStatus status) {
	public override string ToString () => $"{Path.GetFileName(local)}";

	public bool ignored => status == FileStatus.Ignored;

	public bool unchanged => status == FileStatus.Unaltered;
	public bool staged => Enumerable.Contains([
		FileStatus.ModifiedInIndex,
		FileStatus.DeletedFromIndex,
		FileStatus.RenamedInIndex,
		FileStatus.TypeChangeInIndex
		], status);
}
public record RepoPtr (string root) {
}
