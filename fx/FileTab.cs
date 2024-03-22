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
using GamerLib;
namespace fx;
public class FileTab {

	public View root;
	public FileTab (Main main) {
		var ctx = main.ctx;
		var fx = ctx.fx;
		var Prop = new {
			IS_LOCKED = new Prop("locked", "Locked"),
			IS_DIRECTORY = new Prop("directory", "Directory"),
			IS_STAGED = new Prop("gitStagedChanges", "Staged Changes"),
			IS_UNSTAGED = new Prop("gitUnstagedChanges", "Unstaged Changes"),
			IS_SOLUTION = new Prop("visualStudioSolution", "Visual Studio Solution"),
			IS_REPOSITORY = new Prop("gitRepository", "Git Repository"),
			IS_ZIP = new Prop("zipArchive", "Zip Archive"),

			IS_LINK_TO = new PropGen<string>("link", dest => $"Link To: {dest}"),
			IN_REPOSITORY = new PropGen<Repository>("gitRepositoryItem", repo => $"In Repository: {repo.Info.Path}"),
			IN_LIBRARY = new PropGen<Library>("libraryItem", library => $"In Library: {library.name}"),
			IN_SOLUTION = new PropGen<string>("solutionItem", solutionPath => $"In Solution: {solutionPath}"),
			IN_ZIP = new PropGen<string>("zipItem", zipRoot => $"In Zip: {zipRoot}"),
		};
		var favData = new List<PathItem>();
		var cwdData = new List<PathItem>();
		var cwdRecall = (string)null;
		var procData = new List<ProcItem>();
		var gitData = new List<GitItem>();

		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var goPrev = new Button("<-") { X = 0, TabStop = false };
		var goNext = new Button("->") { X = 6, TabStop = false };
		var goLeft = new Button("..") { X = 12, TabStop = false };
		var addressBar = new TextField() {
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
		var pathList = new ListView() {
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

		var gitList = new ListView() {
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
					var c = ShowContext(cwdData[i].path);
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
							ShowContext(fx.cwd);
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
							var initial = File.ReadAllText(p.path);
							var d = new Dialog("Preview", []) {
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
					Text = string.Join("\n", path.properties.Select(p => p.desc)),
				});

				Application.Run(d);
			}
			ContextMenu ShowContext (string path) {
				IEnumerable<MenuItem> GetCommon () {

					yield return new MenuItem(Path.GetFileName(path), null, null, () => false);
					yield return new MenuItem("----", null, null, () => false);
					yield return new MenuItem("Properties", null, () => { });

					yield return new MenuItem("Find", null, () => {
						main.FindIn(path);
					});
					if(Directory.Exists(path)) {
						yield return new MenuItem("Remember", "", () => cwdRecall = path);
						yield return new MenuItem("Open in Explorer", "", () => RunProc($"explorer.exe {path}"));
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

						}, canExecute: () => !fx.locked.Contains(path));


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

						}, canExecute: () => !fx.locked.Contains(path));

					} else {
						//git.repo.RetrieveStatus("lll") == FileStatus.
						if(ctx.git is { patch: { } patch }) {
							var local = path.Replace(ctx.git.root + Path.DirectorySeparatorChar, "");
							var p = patch[local];
							if(p?.Status == ChangeKind.Modified) {
								var index = ctx.git.repo.Index;
								var entry = index[local];

								bool canUnstage = false;
								bool canStage = true;
								if(entry != null) {
									var blob = ctx.git.repo.Lookup<Blob>(entry.Id);
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
					foreach(var c in ctx.Commands) {
						if(c.Accept(path))
							yield return new(c.name, "", () => RunProc(c.GetCmd(path)));
					}
				}

				var c = new ContextMenu(pathList, new(Path.GetFileName(path),
					[.. GetCommon()]));
				c.Show();
				return c;
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
				var f = Path.GetFileName(fx.cwd);
				if(Directory.Exists(dest) is { } b && b) {
					fx.cwdNext.Clear();
					fx.cwdPrev.Push(fx.cwd);
					SetCwd(dest);
					UpdateButtons();

					if(cwdData.FirstOrDefault(p => p.local == f) is { } item) {
						pathList.SelectedItem = cwdData.IndexOf(item);
					}
				}
				return b;
			}
			bool GoPrev (int times = 1) => Enumerable.Range(0, times).All(_ => {
				if(fx.cwdPrev.TryPop(out var prev) is { } b && b) {
					fx.cwdNext.Push(fx.cwd);
					SetCwd(prev);
					UpdateButtons();
				}
				return b;
			});
			bool GoNext (int times = 1) => Enumerable.Range(0, times).All(_ => {
				if(fx.cwdNext.TryPop(out var next) is { } b && b) {
					fx.cwdPrev.Push(fx.cwd);
					SetCwd(next);
					UpdateButtons();
				}
				return b;
			});
			bool GoLeft (int times = 1) =>
				Enumerable.Range(0, times).All(
					_ => Path.GetFullPath(Path.Combine(fx.cwd, "..")) is { } s && s != fx.cwd && GoPath(s));
			void GoItem () {
				if(!(pathList.SelectedItem < cwdData.Count)) {
					return;
				}
				var i = cwdData[pathList.SelectedItem];
				Go(i);
				void Go (PathItem i) {

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
					RunProc(i.path);
				}

			}
			void RefreshCwd () {
				SetCwd(fx.cwd);
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
		void UpdateButtons () =>
			SView.ForTuple((Button button, IEnumerable<string> items) => {
				button.Enabled = items.Any();
			}, [
				(goLeft, Up()), (goNext, fx.cwdNext), (goPrev, fx.cwdPrev)
			]);
		void UpdateProcesses () {
			procData.Clear();
			procData.AddRange(Process.GetProcesses().Where(p => p.MainWindowHandle != 0).Select(p => new ProcItem(p)));
			procList.SetNeedsDisplay();
		}
		void UpdateGit () {
			if(ctx.git is { root: { } root, repo: { } repo }) {
				if(fx.cwd.StartsWith(root)) {
					ctx.git.RefreshPatch();
					UpdateChanges();
				} else {
					gitData.Clear();
					repo.Dispose();
					ctx.git = null;
				}
			} else {
				if(Repository.IsValid(fx.cwd)) {
					ctx.git = new(fx.cwd);
					UpdateChanges();
				}
			}

			void UpdateChanges () {

				gitData.Clear();

				var index = ctx.git.repo.Index;
				var changes = ctx.git.patch.Select(patch => {
					var local = patch.Path;
					var entry = index[local];

					bool staged = false;
					if(entry != null) {
						var blob = ctx.git.repo.Lookup<Blob>(entry.Id);
						var b = blob.GetContentText();
						var f = File.ReadAllText($"{ctx.git.root}/{local}").Replace("\r", "");
						staged = f == b;
					}
					var item = new GitItem(local, patch, staged);
					return item;
				}).OrderByDescending(item => item.staged ? 1 : 0);
				gitData.AddRange([.. changes]);
				gitList.SetSource(gitData);
				foreach(var (i, it) in gitData.Index()) {
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
			if(fx.locked.Contains(path)) {
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


				fx.lastIndex[fx.cwd] = pathList.SelectedItem;
				fx.cwd = Path.GetFullPath(s);

				UpdateGit();


				var anonymize = true;
				var showCwd = fx.cwd;
				if(anonymize) {
					showCwd = showCwd.Replace(ctx.USER_PROFILE, Ctx.USER_PROFILE_MASK);
				}
				//Anonymize
				addressBar.Text = showCwd;
				Console.Title = showCwd;

				pathList.SelectedItem = Math.Min(Math.Max(0, cwdData.Count - 1), fx.lastIndex.GetValueOrDefault(fx.cwd, 0));


			} catch(UnauthorizedAccessException e) {

			}
		}
		IEnumerable<string> Up () {
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
	}
}