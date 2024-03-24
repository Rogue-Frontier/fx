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
namespace fx;
public class ExploreSession : ITab {
	public string TabName => "Expl";
	public View TabView => root;

	public View root;

	private Ctx ctx;
	private Fx fx => ctx.fx;

	//Keep old data around since we might use the properties
	ConcurrentDictionary<string, PathItem> pathData = new();
	
	/// <summary>Temporary</summary>
	private List<PathItem> cwdData = new();

	/// <summary>Temporary</summary>
	Dictionary<string, GitItem> gitMap = new();
	
	/// <summary>Temporary</summary>		
	private List<GitItem> gitData = new();

	private Button goPrev, goNext, goLeft;
	private TextField addressBar;
	public ListView pathList;
	private ListView gitList;

	//When we cd out of a repository, we immediately forget about it.
	private RepoPtr? git;
	private string cwdRecall = null;
	public ExploreSession (Main main) {
		ctx = main.ctx;
		var favData = new List<PathItem>();
		
		var procData = new List<ProcItem>();

		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		goPrev = new Button("<-") { X = 0, TabStop = false };
		goNext = new Button("->") { X = 6, TabStop = false };
		goLeft = new Button("..") { X = 12, TabStop = false };
		addressBar = new TextField() {
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
		var freqList = new ListView() {
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
		var clipPane = new Lazy<View>(() => {
			var view = new FrameView("Clipboard", new() { BorderStyle = BorderStyle.Single, DrawMarginFrame = true, Effect3D = false }) {
				X = 0,
				Y = Pos.Percent(50),
				Width = Dim.Percent(25),
				Height = Dim.Percent(50),
			};
			var clipTab = new Lazy<View>(() => {
				var view = new TabView() {
					X = 0,
					Y = 0,
					Width = Dim.Fill(),
					Height = Dim.Fill()
				};
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

				if(false)
				SView.ForTuple((string name, View v) => view.AddTab(new TabView.Tab(name, v), false), [
					("Cut", clipCutList),
				("History", clipHistList)
					]);
				return view;
			}).Value;
			SView.InitTree([view, clipTab]);
			return view;
		}).Value;

		var pathPane = new FrameView("Directory") {
			X = Pos.Percent(25),
			Y = 1,
			Width = Dim.Percent(50),
			Height = 28,
		};
		pathList = new ListView() {
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
		var procList = new ListView() {
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

		gitList = new ListView() {
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
		RefreshButtons();
		UpdateProcesses();
		void InitTreeLocal () {
			SView.InitTree(
				[freqPane, freqList],
				[clipPane],
				[pathPane, pathList],
				[procPane, procList],
				[gitPane, gitList],
				[root, addressBar, goPrev, goNext, goLeft, freqPane, clipPane, pathPane, /*properties,*/ procPane, gitPane]
				);
		}
		void InitEvents () {
			goPrev.AddMouse(new() {
				[MouseFlags.Button3Clicked] = e => new ContextMenu(e.MouseEvent.View, new(
					[.. fx.cwdPrev.Select((p, i) => new MenuItem(p, "", () => GoPrev(i + 1)))])).Show(),
				[MouseFlags.Button1Clicked] = e => e.Set(GoPrev())
			});
			goNext.AddMouse(new() {
				[MouseFlags.Button3Clicked] = e => new ContextMenu(e.MouseEvent.View, new(
					[.. fx.cwdNext.Select((p, i) => new MenuItem(p, "", () => GoNext(i + 1)))])).Show(),
				[MouseFlags.Button1Clicked] = e => e.Set(GoNext())
			});
			goLeft.AddMouse(new() {
				[MouseFlags.Button3Clicked] = e => new ContextMenu(e.MouseEvent.View, new(
					[.. Up().Select(p => new MenuItem(p, "", () => GoPath(p)))])).Show(),
				[MouseFlags.Button1Clicked] = e => e.Set(GoLeft())
			});
			pathList.AddMouse(new() {
				[MouseFlags.Button3Clicked] = e => {
					var prev = pathList.SelectedItem;
					var i = pathList.TopItem + e.MouseEvent.Y;
					if(i >= cwdData.Count)
						return;
					pathList.SelectedItem = i;
					var c = ShowContext(cwdData[i], i);
					/*
					c.MenuItems.Children.ToList().ForEach(it => it.Action += () => {
						int i = 0;
					});
					*/
					c.MenuBar.MenuAllClosed += () => {
						pathList.SelectedItem = prev;
					};
				}
			});
			pathList.OpenSelectedItem += e => GoItem();
			pathList.AddKeyPress(e => {
				if(!pathList.HasFocus) return null;
				return e.KeyEvent.Key switch {
					Key.Enter or Key.CursorRight => () => GoItem(),
					Key.CursorLeft => () => GoPath(Path.GetDirectoryName(fx.cwd)),
					Key.Tab => () => {
						if(!GetItem(out var p)) return;
						main.term.Text = p.local;
						main.term.SetFocus();
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
							var c = ShowContext(GetPathItem(fx.cwd));
							c.MenuBar.AddKey(value: new() {
								{ '.', c.Hide }
							});
						}
						,
						':' => main.FocusTerm,
						'\'' => () => {
							//Copy file
							return;
						}
						,
						'"' => () => {
							if(!GetItem(out var p) || p.dir) return;
							Preview($"Preview: {p.path}", File.ReadAllText(p.path));
						}
						,
						'?' => () => {
							if(!GetItem(out var p)) return;
							ShowProperties(p);
						}
						,
						'/' => () => {

							if(!GetItem(out var p, out var ind)) return;
							var c = ShowContext(p, ind);
						},
						'~' => () => {
							if(!GetItem(out var p)) return;
							if(!fx.locked.Remove(p.path)) {
								fx.locked.Add(p.path);
							}
							RefreshCwd();
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
							//set dir as workroot
							fx.workroot = fx.workroot != fx.cwd ? fx.cwd : null;
							RefreshCwd();
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

			procList.AddKey(new() {
				[Key.CursorLeft] = default,
				[Key.CursorRight] = default,
				[Key.Backspace] = () => {
					if(!(procList.SelectedItem < procData.Count))
						return;
					var p = procData[procList.SelectedItem];
					p.p.Kill();
					procData.Remove(p);
					procList.SetNeedsDisplay();
				}
			}, new() {
				['\''] = UpdateProcesses
			});

			gitList.OpenSelectedItem += e => {
				var item = gitData[e.Item];


				if(item.staged) Commands.Unstage(git.repo, item.local);
				else Commands.Stage(git.repo, item.local);
				RefreshChanges();
				gitList.SelectedItem = e.Item;
			};


			gitList.AddMouse(new() {
				[MouseFlags.Button1Clicked] = e => {
					gitList.SelectedItem = gitList.TopItem + e.MouseEvent.Y;
					gitList.SetNeedsDisplay();
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
			
			ContextMenu ShowContext (PathItem item, int yInc = 0) {
				var (x, y) = pathList.GetCurrentLoc();
				var c = new ContextMenu(x, y+yInc, new MenuBarItem(Path.GetFileName(item.path), [
					.. GetActions(main, item)]));
				c.Show();
				c.ForceMinimumPosToZero = true;
				return c;
			}
			
			bool GetIndex (out int i) =>
				(i = Math.Min(cwdData.Count - 1, pathList.SelectedItem)) != -1;
			
			bool GoPrev (int times = 1) => Enumerable.Range(0, times).All(_ => {
				if(fx.cwdPrev is { Last: { Value: { }prev } }l) {
					l.RemoveLast();
					fx.cwdNext.AddLast(fx.cwd);
					SetCwd(prev);
					RefreshButtons();
					return true;
				}
				return false;
			});
			bool GoNext (int times = 1) => Enumerable.Range(0, times).All(_ => {
				if(fx.cwdNext is { Last:{Value:{ }next} }l) {
					l.RemoveLast();
					fx.cwdPrev.AddLast(fx.cwd);
					SetCwd(next);
					RefreshButtons();
					return true;
				}
				return false;
			});
			bool GoLeft (int times = 1) =>
				Enumerable.Range(0, times).All(
					_ => Path.GetFullPath(Path.Combine(fx.cwd, "..")) is { } s && s != fx.cwd && GoPath(s));
			void GoItem () {
				if(!(pathList.SelectedItem < cwdData.Count)) {
					return;
				}
				Go(cwdData[pathList.SelectedItem]);
				void Go (PathItem i) {
					if(i.propSet.Contains(IS_LOCKED)) {
						return;
					}
					if(i.propDict.TryGetValue(IS_LINK_TO.id, out var link)) {
						var dest = ((Prop<string>)link).data;
						var destItem = GetPathItem(dest);
						Go(destItem);
						return;
					}
					if(i.propDict.ContainsKey(IS_ZIP.id)) {
						using(ZipArchive zip = ZipFile.Open(i.path, ZipArchiveMode.Read)) {
							foreach(ZipArchiveEntry entry in zip.Entries) {
								Debug.Print(entry.FullName);
							}
						}
						return;
					}
					if(i.propSet.Contains(IS_DIRECTORY)) {
						GoPath(i.path);
						return;
					}
					RunCmd(main, i.path);
				}

			}
			
		}
		void InitCwd () {
			SetCwd(fx.cwd);
		}
		void UpdateProcesses () {
			procData.Clear();
			procData.AddRange(Process.GetProcesses().Where(p => p.MainWindowHandle != 0).Select(p => new ProcItem(p)));
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
	public IEnumerable<int> GetMarkedIndex () =>
		Enumerable.Range(0, cwdData.Count).Where(pathList.Source.IsMarked);
	public IEnumerable<PathItem> GetMarkedItems () =>
		GetMarkedIndex().Select(i => cwdData[i]);

	void ShowProperties (PathItem item) {
		var d = new Dialog($"Properties: {item.path}", []) {
			Border = {
				Effect3D = false
			}
		};
		d.KeyPress += e => {
			e.Set();
			d.Running = false;
		};

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
	Process RunCmd (Main main, string cmd) {
		//var setPath = $"set 'PATH=%PATH%;{Path.GetFullPath("Programs")}'";
		var cmdArgs = @$"/c {cmd}";
		var pi = new ProcessStartInfo("cmd.exe") {
			WorkingDirectory = fx.cwd,
			Arguments = $"{cmdArgs}",
			UseShellExecute = true,
			WindowStyle = ProcessWindowStyle.Hidden,
		};
		var p = Process.Start(pi);
		main.ReadProc(p);
		return p;
	}
	/// <param name="path">This is strictly a child of cwd.</param>
	/// <remarks>Strictly called after <see cref="RefreshRepo(string)"/>, so we already know repo props for the parent directory.</remarks>
	IEnumerable<IProp> GetProps (string path) {
		if(fx.locked.Contains(path)) {
			yield return IS_LOCKED;
		}
		if(Directory.Exists(path)) {
			yield return IS_DIRECTORY;
			if(Repository.IsValid(path)) {
				yield return IS_REPOSITORY;
			}
		} else {
			if(path.EndsWith(".sln")) {
				yield return IS_SOLUTION;
			}
			if(path.EndsWith(".lnk")) {
				// WshShellClass shell = new WshShellClass();
				WshShell shell = new WshShell(); //Create a new WshShell Interface
				IWshShortcut link = (IWshShortcut)shell.CreateShortcut(path); //Link the interface to our shortcut
				yield return ((PropGen<string>)IS_LINK_TO).Generate(link.TargetPath);
			}
			if(path.EndsWith(".zip")) {
				yield return IS_ZIP;
			}
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

	IEnumerable<MenuItem> GetActions (Main main, PathItem item) {
		//yield return new MenuItem(item.local, null, null, () => false);
		//yield return new MenuItem("----", null, null, () => false);
		yield return new MenuItem("Cancel", "", () => { });
		yield return new MenuItem("Find", null, () => {
			main.FindIn(item.path);
		});

		yield return new MenuItem("Show in Explorer", "", () => RunCmd(main, @$"explorer.exe /select, ""{item.path}"""));

		if(item.HasProp(IS_DIRECTORY)) {
			yield return new MenuItem("Remember", "", () => cwdRecall = item.path);
			yield return new MenuItem("Open in Explorer", "", () => RunCmd(main, $"explorer.exe {item.path}"));


			var createFile = () => {
				RequestName("New File", name => {
					var f = Path.Combine(item.path, name);
					File.Create(f);
					RefreshCwd();
					pathList.SelectedItem = cwdData.FindIndex(p => p.path == f);
					return true;
				});
			};
			yield return new MenuItem("New File", "", createFile, canExecute: () => !item.HasProp(IS_LOCKED));
			var createDir = () => {
				RequestName("New Directory", name => {
					var f = Path.Combine(item.path, name);
					Directory.CreateDirectory(f);
					RefreshCwd();
					pathList.SelectedItem = cwdData.FindIndex(p => p.path == f);
					return true;
				});
			};
			yield return new MenuItem("New Dir", "", createDir, canExecute: () => !item.HasProp(IS_LOCKED));
			goto Done;
		}
		if(Path.GetDirectoryName(item.path) is { }par && HasRepo(GetPathItem(par), out string root)) {
			string local = GetRepoLocal(root, item.path);
			IEnumerable<string> Paths () {
				yield return local;
			}
			var diff = new MenuItem("Diff", "", () => {
				using(var repo = new Repository(root)) {
					var patch = repo.Diff.Compare<Patch>(Paths());
					Preview($"Diff: {item.path}", patch.Content);
				}
			});
			if(item.HasProp(IS_UNSTAGED)) {
				yield return new MenuItem("Stage", "", () => {
					using(var repo = new Repository(root)) {
						Commands.Stage(repo, local);
					}
					RefreshCwd();
				});
				yield return diff;
			} else if(item.HasProp(IS_STAGED)) {
				yield return new MenuItem("Unstage", "", () => {
					using(var repo = new Repository(root)) {
						Commands.Unstage(repo, local);
					}
					RefreshCwd();
				});
				yield return diff;
			}
		}
		Done:
		yield return new("Copy Path", "", () => { Clipboard.TrySetClipboardData(item.path); });
		foreach(var c in ctx.Commands) {
			if(c.Accept(item.path))
				yield return new(c.name, "", () => RunCmd(main, c.GetCmd(item.path)));
		}

		yield return new MenuItem("Properties", null, () => { ShowProperties(item); });
	}


	void RequestName (string title, Predicate<string> accept) {
		var create = new Button("Create") { Enabled = false };
		var cancel = new Button("Cancel");
		var d = new Dialog(title, [
			create, cancel
			]) { Width = 32, Height = 5 };
		d.Border.Effect3D = false;
		var input = new TextField() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(2),
			Height = 1,
		};
		input.TextChanging += e => {
			create.Enabled = e.NewText.Any();
		};
		input.AddKey(new() {
			[Key.Enter] = create.OnClicked
		});
		create.Clicked += () => {
			if(accept(input.Text.ToString())) {
				d.RequestStop();
			}
		};
		cancel.Clicked += d.RequestStop;
		d.Add(input);
		input.SetFocus();
		Application.Run(d);
	}
	PathItem GetPathItem (string path) {
		return pathData[path] =
			pathData.TryGetValue(path, out var item) ?
				new PathItem(item.local, item.path, new(GetProps(path))) :
				new PathItem(Path.GetFileName(path), path, new(GetProps(path)));
	}
	/// <summary>
	/// Do not check directory properties here as this is where we assign properties to begin with.
	/// </summary>
	/// <param name="path"></param>
	private void RefreshRepo (string path) {
		var item = GetPathItem(path);
		if(git is { root: { } root }) {
			var stillInRepo = path.StartsWith(root);
			if(stillInRepo) {
				//If we're in the repo but not at root, remember that this directory is within the repository
				if(path != root && !item.HasProp(IN_REPOSITORY)) {
					var next = ((PropGen<RepoItem>)IN_REPOSITORY).Generate(git.repo.CalcRepoItem(path));
					//TO DO: fix adding attributes
					pathData[path] = new(item.local, item.path, new([.. item.propSet, next]));
				}
				RefreshChanges();
			} else {
				gitData.Clear();
				git?.Clear();
				git = default;
			}
		} else if(Repository.IsValid(path)) {
			//Mark this directory as a known repository.

			pathData[path] = new(item.local, item.path, new([.. item.propSet, IS_REPOSITORY]));
			SetRepo(path);
			RefreshChanges();
		} else if(item.GetProp<RepoItem>(IN_REPOSITORY, out var repoFile, out var prop)) {
			if(Repository.IsValid(repoFile.root)) {
				//We already know that this directory is within some repository from a previous visit.
				SetRepo(repoFile.root);
				RefreshChanges();
			} else {
				//Error
				pathData[path] = new(item.local, item.path, new(item.propSet.Except([prop])));
			}
		}
	}
	void RefreshListing (string s) {
		try {
			pathList.SetSource(cwdData = new List<PathItem>([
				..Directory.GetDirectories(s).Select(GetPathItem),
				..Directory.GetFiles(s).Select(GetPathItem)
			]));
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
		gitList.SetSource(gitData);
		foreach(var (i, it) in gitData.Index()) {
			gitList.Source.SetMark(i, it.staged);
		}
	}
	void RefreshAddressBar () {
		var anonymize = true;
		var userProfile = ctx.USER_PROFILE;
		var showCwd = fx.cwd;
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

		pathList.SelectedItem = Math.Min(Math.Max(0, cwdData.Count - 1), fx.lastIndex.GetValueOrDefault(fx.cwd, 0));
		pathList.SetNeedsDisplay();
	}
	void RefreshCwd () {
		var path = fx.cwd;
		fx.lastIndex[path] = pathList.SelectedItem;
		//Refresh the repo in case it got deleted for some reason
		RefreshRepo(path);
		RefreshListing(path);
		if(pathData[fx.cwd].HasProp(IN_REPOSITORY)) {
			RefreshChanges();
		}
		RefreshAddressBar();
	}
	void SetCwd (string dest) {
		var path = Path.GetFullPath(dest);
		RefreshRepo(path);
		RefreshListing(path);
		fx.lastIndex[fx.cwd] = pathList.SelectedItem;
		fx.cwd = path;
		RefreshAddressBar();
	}
	bool GoPath (string? dest) {
		var f = Path.GetFileName(fx.cwd);
		if(fx.workroot is { } root && !dest.Contains(root)) {
			return false;
		}
		if(Directory.Exists(dest)) {
			fx.cwdNext.Clear();
			fx.cwdPrev.AddLast(fx.cwd);
			SetCwd(dest);
			RefreshButtons();
			if(cwdData.FindIndex(p => p.local == f) is {}ind and not -1) {
				pathList.SelectedItem = ind;
			}
			return true;
		}
		return false;
	}
	void RefreshButtons () =>
		SView.ForTuple((Button button, IEnumerable<string> items) => {
			button.Enabled = items.Any();
		}, [
			(goLeft, Up()), (goNext, fx.cwdNext), (goPrev, fx.cwdPrev)
		]);
	private IEnumerable<string> Up () {
		var curr = Path.GetDirectoryName(fx.cwd);
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
		p = cwdData[pathList.SelectedItem];
		return true;
	}
	bool GetItem (out PathItem p, out int index) {
		if(pathList.SelectedItem >= cwdData.Count) {
			p = null;
			index = -1;
			return false;
		}
		p = cwdData[index = pathList.SelectedItem];
		return true;
	}
	public void Preview (string title, string content) {
		var d = new Dialog(title, []) {
			Border = { Effect3D = false },
		};
		d.AddKeyPress(e => e.KeyEvent.Key switch {
			Key.Enter => d.RequestStop
		});
		d.Add(new TextView() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Text = content,
			ReadOnly = true,
		});
		Application.Run(d);
	}
	private void SetRepo(string root) {
		git = new(root, new Repository(root));
	}
}
public interface IProp {
	string id { get; }
	string desc { get; }
}
/// <summary>
/// Standard properties used within fx
/// </summary>
/// <remarks>
/// Be careful not to assign <see cref="IS_REPOSITORY"/> or <see cref="IN_REPOSITORY"/> outside <see cref="ExploreSession.RefreshRepo(string)"/>. The repository is updated before the cwd listing.
/// </remarks>
public static class Props {
	public static IProp
		IS_LOCKED =		new Prop("locked", "Locked"),
		IS_DIRECTORY =	new Prop("directory", "Directory"),
		IS_STAGED =		new Prop("gitStagedChanges", "- Staged Changes"),
		IS_UNSTAGED =	new Prop("gitUnstagedChanges", "- Unstaged Changes"),
		IS_SOLUTION =	new Prop("visualStudioSolution", "Visual Studio Solution"),
		IS_REPOSITORY = new Prop("gitRepositoryRoot", "Git Repository"),
		IS_ZIP =		new Prop("zipArchive", "Zip Archive");
	public static IPropGen
		IN_REPOSITORY = new PropGen<RepoItem>("gitRepositoryItem", pair => $"In Repository: {pair.root}"),
		IS_LINK_TO =	new PropGen<string>	("link",				dest => $"Link To: {dest}"),
		IN_LIBRARY =	new PropGen<Library>("libraryItem",			library => $"In Library: {library.name}"),
		IN_SOLUTION =	new PropGen<string>	("solutionItem",		solutionPath => $"In Solution: {solutionPath}"),
		IN_ZIP =		new PropGen<string>	("zipItem",				zipRoot => $"In Zip: {zipRoot}");

	public static string GetRoot (this Repository repo) => Path.GetFullPath($"{repo.Info.Path}/..");
	public static string GetRepoLocal (this Repository repo, string path) => path.Replace(repo.GetRoot() + Path.DirectorySeparatorChar, null);
	public static string GetRepoLocal (string root, string path) => path.Replace(root + Path.DirectorySeparatorChar, null);
	public static RepoItem CalcRepoItem (this Repository repo, string path) => CalcRepoItem(repo.GetRoot(), path);
	public static RepoItem CalcRepoItem (string root, string path) =>
		new(root, GetRepoLocal(root, path));

	public static bool HasRepo(PathItem item, out string root) {
		if(item.HasProp(IS_REPOSITORY)) {
			root = item.path;
			return true;
		}
		if(item.GetProp<RepoItem>(IN_REPOSITORY, out var repoItem)) {
			root = repoItem.root;
			return true;
		}
		root = null;
		return false;
	}

	/// <summary>Identifies a repository-contained file by local path and repository root</summary>
	public record RepoItem (string root, string local) {}
}
public record Prop (string id, string desc) : IProp {}
public record Prop<T> (string id, string desc, T data) : IProp {}
public interface IPropGen {
	string id { get; }
	public Prop<T> Make<T> (T data) => ((PropGen<T>)this).Generate(data);

}
public record PropGen<T> (string id, PropGen<T>.GetDesc getDesc) : IPropGen {
	public delegate string GetDesc (T args);
	public Prop<T> Generate (T args) => new Prop<T>(id, getDesc(args), args);
}
public record ProcItem (Process p) {
	public override string ToString () => $"{p.ProcessName,-24}{p.Id,-8}";
}
public record PathItem (string local, string path, HashSet<IProp> propSet) {
	public readonly Dictionary<string, IProp> propDict =
		propSet?.ToDictionary(p => p.id, p => p);
	public bool HasProp (IProp p) => propSet.Contains(p);
	public bool HasProp (IPropGen p) => propDict.ContainsKey(p.id);
	public bool HasProp<T> (IPropGen p, T data) where T : notnull => propDict.TryGetValue(p.id, out var prop) && data.Equals(((Prop<T>)prop).data);
	public bool GetProp (IPropGen p, out IProp prop) => propDict.TryGetValue(p.id, out prop);
	public bool GetProp<T> (IPropGen p, out T data) {
		data =
			propDict.TryGetValue(p.id, out var prop) is { } b && b ?
				((Prop<T>)prop).data :
				default;
		return b;
	}
	public bool GetProp<T> (IPropGen p, out T data, out IProp prop) {
		data =
			propDict.TryGetValue(p.id, out prop) is { } b && b ?
				((Prop<T>)prop).data :
				default;
		return b;
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