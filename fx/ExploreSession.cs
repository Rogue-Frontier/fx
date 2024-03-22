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
namespace fx;
public class ExploreSession : ITab {
	public string TabName => "Explore";
	public View TabView => root;

	public View root;

	private Ctx ctx;
	private Fx fx => ctx.fx;

	//Persistent inbetween visits
	//Dictionary<string, PathItem> pathData = new();
	
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



	public ExploreSession (Main main) {

		ctx = main.ctx;
		var favData = new List<PathItem>();
		var cwdRecall = (string)null;
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
			goPrev.AddMouseClick(e => e.MouseEvent.Flags switch {
				MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
					[.. fx.cwdPrev.Select((p, i) => new MenuItem(p, "", () => GoPrev(i + 1)))])).Show,
				MouseFlags.Button1Clicked => () => e.Handled = GoPrev(),
				_ => null,
			});
			goNext.AddMouseClick(e => e.MouseEvent.Flags switch {
				MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
					[.. fx.cwdNext.Select((p, i) => new MenuItem(p, "", () => GoNext(i + 1)))])).Show,
				MouseFlags.Button1Clicked => () => e.Handled = GoNext(),
				_ => null
			});
			goLeft.AddMouseClick(e => e.MouseEvent.Flags switch {
				MouseFlags.Button3Clicked => new ContextMenu(e.MouseEvent.View, new(
					[.. Up().Select(p => new MenuItem(p, "", () => GoPath(p)))])).Show,
				MouseFlags.Button1Clicked => () => e.Handled = GoLeft(),
				_ => null
			});
			pathList.AddMouseClick(e => e.MouseEvent.Flags switch {
				MouseFlags.Button3Clicked => () => {
					var prev = pathList.SelectedItem;
					var i = pathList.TopItem + e.MouseEvent.Y;
					if(i >= cwdData.Count) {
						return;
					}
					var c = ShowContext(cwdData[i]);
				}
				,
				_ => null
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
							ShowContext(CreatePathItem(fx.cwd));
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
							if(!GetItem(out var p)) return;
							ShowContext(p);
						}
						,
						'~' => () => {
							if(!GetItem(out var p)) return;
							if(!fx.locked.Remove(p.path)) {
								fx.locked.Add(p.path);
							}
							SetCwd(fx.cwd);
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

			gitList.OpenSelectedItem += e => {
				var item = gitData[e.Item];
				if(item.staged) Commands.Unstage(ctx.git.repo, item.local);
				else Commands.Stage(ctx.git.repo, item.local);
				RefreshChanges();
				gitList.SelectedItem = e.Item;
			};

			gitList.AddMouseClick(e => e.MouseEvent.Flags switch {
				MouseFlags.Button1Clicked => () => {
					gitList.SelectedItem = gitList.TopItem + e.MouseEvent.Y;
					gitList.SetNeedsDisplay();
				}
				,
				_ => null
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
			void ShowProperties (PathItem path) {
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
					X = 0,
					Y = 0,
					Width = Dim.Fill(),
					Height = Dim.Fill(),
					ReadOnly = true,
					Text = string.Join("\n", path.propertySet.Select(p => p.desc)),
				});

				Application.Run(d);
			}
			ContextMenu ShowContext (PathItem item) {
				IEnumerable<MenuItem> GetCommon () {
					yield return new MenuItem(item.local, null, null, () => false);
					yield return new MenuItem("----", null, null, () => false);
					yield return new MenuItem("Properties", null, () => { });
					yield return new MenuItem("Find", null, () => {
						main.FindIn(item.path);
					});
					if(item.HasProp(Props.IS_DIRECTORY)) {
						yield return new MenuItem("Remember", "", () => cwdRecall = item.path);
						yield return new MenuItem("Open in Explorer", "", () => RunProc($"explorer.exe {item.path}"));
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
								var f = Path.Combine(item.path, name.Text.ToString());
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

						}, canExecute: () => !item.HasProp(Props.IS_LOCKED));
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
								var f = Path.Combine(item.path, name.Text.ToString());
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
						}, canExecute: () => !item.HasProp(Props.IS_LOCKED));
						goto Done;
					}
					if(item.GetProp<(Repository repo, string local)>(Props.IN_REPOSITORY, out var pair)) {
						var (repo, local) = pair;

						IEnumerable<string> Paths () {
							yield return local;
						}

						var diff = new MenuItem("Diff", "", () => {
							var patch = repo.Diff.Compare<Patch>(Paths());
							Preview($"Diff: {item.path}", patch.Content);
						});

						if(item.HasProp(Props.IS_UNSTAGED)) {
							yield return new MenuItem("Stage", "", () => {
								Commands.Stage(repo, local);
								RefreshCwd();
							});
							yield return diff;
						} else if(item.HasProp(Props.IS_STAGED)) {
							yield return new MenuItem("Unstage", "", () => {
								Commands.Unstage(repo, local);
								RefreshCwd();
							});
							yield return diff;
						}
					}
					Done:
					yield return new("Copy Path", "", () => { Clipboard.TrySetClipboardData(item.path); });
					foreach(var c in ctx.Commands) {
						if(c.Accept(item.path))
							yield return new(c.name, "", () => RunProc(c.GetCmd(item.path)));
					}
				}

				var c = new ContextMenu(pathList, new(Path.GetFileName(item.path),
					[.. GetCommon()]));
				c.Show();
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
					if(i.propertySet.Contains(Props.IS_LOCKED)) {
						return;
					}
					if(i.propertyDict.TryGetValue(Props.IS_LINK_TO.id, out var link)) {
						var dest = ((Prop<string>)link).data;
						var destItem = CreatePathItem(dest);
						Go(destItem);
						return;
					}
					if(i.propertyDict.ContainsKey(Props.IS_ZIP.id)) {
						using(ZipArchive zip = ZipFile.Open(i.path, ZipArchiveMode.Read)) {
							foreach(ZipArchiveEntry entry in zip.Entries) {
								Debug.Print(entry.FullName);
							}
						}
						return;
					}
					if(i.propertySet.Contains(Props.IS_DIRECTORY)) {
						GoPath(i.path);
						return;
					}
					RunProc(i.path);
				}

			}
			Process RunProc (string cmd) {
				//var setPath = $"set 'PATH=%PATH%;{Path.GetFullPath("Programs")}'";
				var cmdArgs = @$"/c {cmd}";
				var pi = new ProcessStartInfo("cmd.exe") {
					WorkingDirectory = fx.cwd,
					Arguments = $"{cmdArgs}",
					UseShellExecute = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					//CreateNoWindow = true,
				};
				var p = new Process() {
					StartInfo = pi

				};
				p.Start();

				main.ReadProc(p);
				return p;
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
	IEnumerable<IProp> GetProps (string path) {
		if(fx.locked.Contains(path)) {
			yield return Props.IS_LOCKED;
		}
		if(Directory.Exists(path)) {
			yield return Props.IS_DIRECTORY;

			if(ctx.git?.root == path) {
				yield return Props.IS_REPOSITORY;
			}
		} else {
			if(path.EndsWith(".sln")) {
				yield return Props.IS_SOLUTION;
			}
			if(path.EndsWith(".lnk")) {
				// WshShellClass shell = new WshShellClass();
				WshShell shell = new WshShell(); //Create a new WshShell Interface
				IWshShortcut link = (IWshShortcut)shell.CreateShortcut(path); //Link the interface to our shortcut
				yield return ((PropGen<string>)Props.IS_LINK_TO).Generate(link.TargetPath);
			}
			if(path.EndsWith(".zip")) {
				yield return Props.IS_ZIP;
			}
			if(ctx.git is { }) {
				yield return ((PropGen<(Repository repo, string local)>)Props.IN_REPOSITORY).Generate((ctx.git.repo, ctx.git.GetRepoLocal(path)));
			}
		}
		if(gitMap.TryGetValue(path, out var p)) {
			if(p.staged) {
				yield return Props.IS_STAGED;
			} else {
				yield return Props.IS_UNSTAGED;
			}
		}

	}
	PathItem CreatePathItem (string f) =>
		new PathItem(Path.GetFileName(f), f, new(GetProps(f)));

	void RefreshListing (string s) {
		try {
			pathList.SetSource(cwdData = new List<PathItem>([
				..Directory.GetDirectories(s).Select(CreatePathItem),
				..Directory.GetFiles(s).Select(CreatePathItem)
			]));
		}catch(UnauthorizedAccessException e) {
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
			showCwd = showCwd.Replace(userProfile, Ctx.USER_PROFILE_MASK);
		}
		//Anonymize
		addressBar.Text = showCwd;
		Console.Title = showCwd;

		pathList.SelectedItem = Math.Min(Math.Max(0, cwdData.Count - 1), fx.lastIndex.GetValueOrDefault(fx.cwd, 0));
		pathList.SetNeedsDisplay();
	}
	void RefreshCwd () {
		RefreshListing(fx.cwd);
		fx.lastIndex[fx.cwd] = pathList.SelectedItem;
		if(ctx.git != null) {
			RefreshChanges();
		}
		RefreshAddressBar();
	}
	void SetCwd (string s) {
		RefreshListing(s);

		/*
		if(s == cwd) {
			goto UpdateListing;
		}
		*/
		fx.lastIndex[fx.cwd] = pathList.SelectedItem;
		fx.cwd = Path.GetFullPath(s);
		RefreshGit();
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
		string
			p = fx.cwd,
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
	private void RefreshGit () {
		//Replace with property check?
		if(ctx.git is { root: { } root, repo: { } repo }) {
			if(fx.cwd.StartsWith(root)) {
				ctx.git.RefreshPatch();
				RefreshChanges();
			} else {
				gitData.Clear();
				repo.Dispose();
				ctx.git = null;
			}
			return;
		}
		if(Repository.IsValid(fx.cwd)) {
			ctx.git = new(fx.cwd);
			RefreshChanges();
		}
	}
	public void RefreshChanges () {
		
		IEnumerable<GitItem> GetItems () {
			foreach(var item in ctx.git.repo.RetrieveStatus()) {
				GitItem GetItem (bool staged) => new GitItem(item.FilePath, Path.GetFullPath($"{ctx.git.root}/{item.FilePath}"), staged);
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
	bool GetItem (out PathItem p) {
		p = null;
		if(pathList.SelectedItem >= cwdData.Count) {
			return false;
		}
		p = cwdData[pathList.SelectedItem];
		return true;
	}
	public void Preview (string title, string content) {
		var d = new Dialog(title, []) {
			Border = { Effect3D = false }
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
}