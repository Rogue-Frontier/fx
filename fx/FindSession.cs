using LibGit2Sharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui;

using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
using GDShrapt.Reader;
namespace fx;
public class FindSession {
	public View root;
	public TreeView<IFind> tree;
	FindFilter filter = new();
	public TextField rootBar;
	private Ctx ctx;

	private Main main;



	public FindSession (Main main, string path) {
		ctx = main.ctx;
		this.main = main;
		Fx fx = ctx.fx;
		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
		};
		int w = 8;
		int y = 0;
		var rootLabel = new Label() {
			AutoSize = false,
			Title= "Dir",
			X = 0,
			Y = y,
			Width = w,
			Height = 1
		};
		rootBar = new TextField() {
			AutoSize = false,
			X = w,
			Y = y,
			Width = Dim.Fill(24),
			Height = 1,
			Text = path
		};

		var rootShowButton = new Button() {
			AutoSize = false,
			X = Pos.Right(rootBar),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "[Find]",
			NoDecorations = true,
			NoPadding = true
		};
		y++;
		var filterLabel = new Label() {
			AutoSize = false,
			X = 0,
			Y = y,
			Width = w,
			Height = 1,
			Text = "File"
		};
		var filterBar = new TextField() {
			AutoSize = false,
			X = w,
			Y = y,
			Width = Dim.Fill(24),
			Height = 1,
		};
		var filterShowButton = new Button() {
			AutoSize = false,
			X = Pos.Right(filterBar),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "[Find]",
			NoDecorations = true,
			NoPadding = true
		};
		y++;
		var findLabel = new Label() {
			AutoSize = false,
			X = 0,
			Y = y,
			Width = w,
			Height = 1,
			Text = "Line"
		};
		var findBar = new TextField() {
			AutoSize = false,
			X = w,
			Y = y,
			Width = Dim.Fill(24),
			Height = 1,
		};
		var findAllButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findBar),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "[Find]",
			NoDecorations = true,
			NoPadding = true
		};
		var findPrevButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "[<-]",
			NoDecorations = true,
			NoPadding = true
		};
		var findNextButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "[->]",
			NoDecorations = true,
			NoPadding = true
		};
		y++;
		/*
		var replaceLabel = new Label() {
			AutoSize = false,
			X = 0,
			Y = y,
			Width = w,
			Height = 1,
			Text = "Replace",
		};
		var replaceBar = new TextField() {
			AutoSize = false,
			X = w,
			Y = y,
			Width = Dim.Fill(24),
			Height = 1
		};
		var replaceAllButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findBar),
			Y = y,
			Width = 6,
			Height=1,
			Text = "[Here]",
			NoDecorations = true,
			NoPadding = true
		};
		var replacePrevButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6,
			Height=1,
			Text = "[<-]",
			NoDecorations = true,
			NoPadding = true
		};
		var replaceNextButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6,
			Height=1,
			Text = "[->]",
			NoDecorations = true,
			NoPadding = true
		};
		y++;
		*/

		var bar = new LineView() {
			X = 0,
			Y = y,
			Width = Dim.Fill(),
			Height = 1,
			Orientation = Orientation.Horizontal
		};
		y++;
		tree = new TreeView<IFind>(new TreeFinder(filter)) {

			X = 0,
			Y = y,
			Width = Dim.Fill(0),
			Height = Dim.Fill(0),
			AspectGetter = f => f switch {
				FindDir d => $"{d.name}/",
				FindFile ff => ff.name,
				FindLine l => $"|{l.row,4}|{l.line.Replace("\r",null).Replace("\t", "    ")}"
			}
		};
		tree.ObjectActivated += (a, e) => {
			if(e.ActivatedObject is FindLine l) {
				//main.EditFile(l);
			}
		};
		root.KeyDownD(new() {
			[(int)Delete] = _ => {
				int i = 0;
			},
			[(int)Esc] = _ => {
				main.folder.RemoveTab(root, out var _);
			}
		});
		root.KeyDownD(new() {
			[(int)Esc] = _=> {
				main.folder.RemoveTab(root, out var _);
			},
			['>'] = _ => {
				main.folder.SwitchTab();
			}
		});
		rootBar.KeyDownD(new() {
			[(int)Enter] = _ => FindDirs()
		});
		filterBar.KeyDownD(new() {
			[(int)Enter] = _ => FindFiles()
		});
		findBar.KeyDownD(new() {
			[(int)Enter] = _ => FindLines()
		});

		rootShowButton.MouseEvD(new() {
			[(int)Button1Pressed] = _ => FindDirs()
		});
		filterShowButton.MouseEvD(new() {
			[(int)Button1Pressed] = _ => FindFiles()
		});
		tree.MouseClickD(new() {
			[Button3Clicked] = e => {
				var prev = tree.SelectedObject;
				var row = e.MouseEvent.Y;
				if(tree.GetObjectOnRow(row) is not { } o)
					return;
				tree.SelectedObject = tree.GetObjectOnRow(row);
				var c = ShowContext(tree.SelectedObject, row - 1, e.MouseEvent.X);
				/*
				c.MenuItems.Children.ToList().ForEach(it => it.Action += () => {
					int i = 0;
				});
				*/
				c.MenuBar.MenuAllClosed += (object? a, EventArgs e) => {
					tree.SelectedObject = prev;
				};
				tree.SetNeedsDisplay();
			},
		});
		tree.KeyDownD(new() {
			[(int)CursorRight] = _ => {
				if(!tree.IsExpanded(tree.SelectedObject)) {
					if(!tree.CanExpand(tree.SelectedObject)) {
						return;
					}
					tree.Expand(tree.SelectedObject);
					tree.SetNeedsDisplay();
					return;
				}
				if(tree.GetChildren(tree.SelectedObject).FirstOrDefault() is { } c) {
					tree.SelectedObject = c;
				}
				tree.SetNeedsDisplay();
			},
			['/'] = _ => {
				var (row, col) = tree.GetObjectPos(tree.SelectedObject) ?? (0, 0);
				var c = ShowContext(tree.SelectedObject, row+1, col+1);
			}
		});
		void FindFiles () {
			filter = filter with {
				filePattern = new(filterBar.Text.ToString()),
				linePattern = null
			};
			SetFilter(filter);
		}
		void FindLines () {
			filter = filter with {
				filePattern = new(filterBar.Text.ToString()),
				linePattern = new(findBar.Text.ToString())
			};
			SetFilter(filter);
		}
		findAllButton.MouseEvD(new() {
			[(int)Button1Pressed] = _ => FindLines()
		});
		SView.InitTree([root,
			rootLabel, rootBar, rootShowButton,
			filterLabel, filterBar, filterShowButton,
			findLabel, findBar, findAllButton, findPrevButton, findNextButton,
			//replaceLabel, replaceBar, replaceAllButton, replacePrevButton, replaceNextButton,
			bar,
			tree
			]);
	}
	ContextMenu ShowContext (IFind item, int row, int col = 0) {
		var (x, y) = tree.GetCurrentLoc();
		var c = new ContextMenu() {
			MenuItems = new(null, [
			.. GetActions(item)]),
			Position = new(x + col, y + row)
		};
		c.Show();
		c.ForceMinimumPosToZero = true;
		return c;
	}
	IEnumerable<MenuItem> GetActions (IFind item) => [
		new MenuItem("Cancel", null, () => { }),

		..(item switch {
			FindDir d => GetActions(d),
			FindFile f => GetActions(f),
			FindLine l => GetActions(l),
			_ => throw new Exception()
		}),
		
		.. item is not FindLine ?
			ExploreSession.GetStaticActions(main, ctx.GetCachedPathItem(item.path, ExploreSession.GetStaticProps)) :
			[]
	];
	IEnumerable<MenuItem> GetActions (FindDir d) {
		yield return new MenuItem("Explore", "", () => {
			main.folder.AddTab($"Expl {d.name}", new ExploreSession(main, d.path).root, true, root.Focused);
		});
	}
	IEnumerable<MenuItem> GetActions (FindFile f) {
		/*
		yield return new MenuItem("Edit", "", () => {
			main.folder.AddTab($"Edit {f.name}", new EditSession(f.path).root, true);
		});
		*/
		yield break;
	}

	private void RequestReplace(string title, string original, Action<string> accept) {
		var confirm = new Button() { Title = "Confirm", };
		var cancel = new Button() { Title = "Cancel", };
		var d = new Dialog() {
			Title = title,
			Buttons = [confirm, cancel],
			Width = 96,
			Height = 6,
		};
		var input = new TextView() {
			X = 1,
			Y = 1,
			Width = Dim.Fill(2),
			Height = 1,
			Multiline = false,
			PreserveTrailingSpaces = true,
			TabWidth = 4,
			AllowsTab = true,

			Text = original,
		};
		input.KeyDownD(new() {
			[(int)Enter] = _ => Confirm()
		});
		void Confirm() {
			accept(input.Text.ToString());
			d.RequestStop();
		}
		confirm.MouseClick += (a, e) => Confirm();
		cancel.MouseClick += (a, e) => d.RequestStop();
		d.Add(input);
		input.SetFocus();
		Application.Run(d);
	}

	IEnumerable<MenuItem> GetActions(FindLine l) {
		yield return new MenuItem("Replace Line", null, () =>
			RequestReplace($"Edit {l.path}", l.line, result => {
				var lines = File.ReadAllLines(l.path);
				lines[l.row] = result;
				File.WriteAllLines(l.path, lines);
			})
		);

		yield return new MenuItem("Replace Match", null, () =>
			RequestReplace($"Edit {l.path}", l.capture, result => {
				var lines = File.ReadAllLines(l.path);
				ref var line = ref lines[l.row];
				line = line[..l.col] + result + line[(l.col + l.capture.Length)..];
				File.WriteAllLines(l.path, lines);
			})
		);

		yield return new MenuItem("Edit", "", () => {
			main.folder.AddTab($"Edit {l.path}", new EditSession(main, l.path, l.row, l.col).root, true, root.Focused);
		});

		yield return new MenuItem("Copy Group", "", () => { });
		yield return new MenuItem("Copy Line", "", () => {});

		yield break;
	}
	public void FindDirs () {
		filter = filter with {
			filePattern = null,
			linePattern = null,
		};
		SetFilter(filter);
	}
	void SetFilter (FindFilter filter) {
		tree.ClearObjects();
		var tf = new TreeFinder(filter);
		tree.TreeBuilder = tf;

		var root = GetRoot();

		var dialog = new Dialog() {
			Title = $"Finding in {root}"
		};

		var tv = new TextView() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		dialog.Add(tv);

		tf.FileScanned += f => {
			tv.Text += $"{f}\n";
		};

		var c = new CancellationTokenSource();
		var cancel = new Button() { Title = "Cancel", };
		dialog.AddButton(cancel);
		cancel.MouseClick += (a, e) => {
			dialog.RequestStop();
			c.Cancel();
		};

		Task.Run(() => {
			tf.CreateTree(root);
			dialog.RequestStop();
		}, c.Token);

		Application.Run(dialog);

		var i = IFind.New(root);
		if(i is FindDir d) {
			d.name = d.path;
		} else if(i is FindFile f) {
			f.name = f.path;
		}
		tree.AddObject(i);

		/*
		int row = 0;
		while(tree.GetObjectOnRow(row) is { } o) {
			tree.Expand(o);
			row++;
		}
		*/
		
		tree.SetNeedsDisplay();
	}
	string GetRoot () =>
		rootBar.Text.ToString().Replace(Ctx.USER_PROFILE_MASK, ctx.USER_PROFILE);
}
public enum LeafType {
	Dir, File, Line
}
/// <summary>
/// 
/// </summary>
/// <param name="filePattern">If <c>null</c>, then do not show files</param>
/// <param name="linePattern">If <c>null</c>, then do not show lines </param>
/// <param name="replace">Format</param>
public record FindFilter (Regex filePattern, Regex linePattern, string replace) {
	public FindFilter () : this(null, null, null) { }
	public bool Accept (FindFile f) => filePattern?.Match(f.name).Success ?? false;
	public bool Accept (string line) => linePattern?.Match(line).Success ?? false;
	public void Replace (string line) => linePattern.Replace(line, replace);
	public LeafType LeafType =>
		linePattern == null && filePattern == null ?
			LeafType.Dir :
		linePattern == null ?
			LeafType.File :
		LeafType.Line;
}
public record TreeFinder (FindFilter filter) : ITreeBuilder<IFind> {
	ConcurrentDictionary<string, IFindPath[]> node = new();
	ConcurrentDictionary<string, FindLine[]> line = new();

	public bool SupportsCanExpand => true;
	public bool CanExpand (IFind i) => i switch {
		FindDir d => node.GetOrAdd(d.path, []).Any(),
		FindFile f => line.GetOrAdd(f.path, []).Any(),
		FindLine => false
	};
	public IEnumerable<IFind> GetChildren (IFind i) => i switch {
		FindDir d => node[d.path],
		FindFile f => line[f.path],
		FindLine => []
	};

	public Action<string> FileScanned;
	public void CreateTree(string root) {
		if(filter.LeafType == LeafType.Dir) {
			int i = 0;
			List<string> q = [root];
			while(i < q.Count) {
				Traverse(q[i]);
				i++;
			}
			void Traverse (string dir) {
				var sub = Directory.GetDirectories(dir);

				var next = sub.Select(d => new FindDir(d));
				node.AddOrUpdate(dir, _ => [.. next], (key, val) => [.. val, .. next]);
				//node[dir] = [..next];
				foreach(var d in sub) {
					q.Add(d);
				}
			}
		} else if(filter.LeafType == LeafType.File) {
			IEnumerable<string> matchFiles = GetAllSubFiles(root);
			if(filter.filePattern is { } fp && fp.ToString().Any()) {
				matchFiles = matchFiles.Where(f => filter.filePattern.IsMatch(f));
			}
			MakeNodes(matchFiles);

		} else if(filter.LeafType == LeafType.Line) {
			IEnumerable<string> matchFiles = GetAllSubFiles(root);
			if(filter.filePattern is { } fp && fp.ToString().Any()) {
				matchFiles = matchFiles.Where(s => fp.IsMatch(s));
			}

			//Handle empty pattern
			foreach(var f in matchFiles) {
				FindLine[] l = [.. File.ReadLines(f)
					.Select((line, row) => (row, line, match: filter.linePattern.Match(line)))
					.Where(p => p.match.Success)
					.Select(p => new FindLine(f, p.row, p.match.Index, p.line, p.match.Value))
					];
				FileScanned?.Invoke(f);
				if(l.Length == 0) {
					continue;
				}
				line[f] = l;
			}
			MakeNodes(line.Keys);
			//Nested dirs
		}
		void MakeNodes(IEnumerable<string> files) {
			//Filter out files before making the tree
			ConcurrentDictionary<string, HashSet<string>> subfiles = new(files
				.GroupBy(Path.GetDirectoryName)
				.Select(g => (g.Key, new HashSet<string>(g)))
				.ToDictionary()
				);

			var activeSubdir = subfiles.Keys;
			while(activeSubdir.Any()) {
				var dict = activeSubdir
					.GroupBy(Path.GetDirectoryName)
					.Where(g => g.Key != null)
					.Select(g => (g.Key, g.ToArray()))
					.ToDictionary();
				foreach(var (dir, subdir) in dict) {
					subfiles.GetOrAdd(dir, []).UnionWith(subdir);
				}
				activeSubdir = dict.Keys;
			}
			foreach(var (dir, file) in subfiles) {
				var next = file.Select(f => new FindFile(f));
				//node[dir] = [.. next];
				node.AddOrUpdate(dir, _ => [.. next], (key, val) => [.. val, .. next]);

			}
		}
		IEnumerable<string> GetAllSubFiles (string path) => [
			.. Directory.GetDirectories(path).SelectMany(d => GetAllSubFiles(d)),
			.. Directory.GetFiles(path)
		];
	}
}
public interface IFind {

	string path { get; }
	public static IFind New (string path) =>
		Directory.Exists(path) ?
			new FindDir(path) :
			new FindFile(path);
}
public interface IFindPath : IFind {
}
public record FindDir (string path) : IFindPath {
	public string name = Path.GetFileName(path);
	public static FindDir New (string path) => new(path);
}
public record FindFile (string path) : IFindPath {
	public string name = Path.GetFileName(path);
	public static FindFile New (string path) => new(path);
}
public record FindLine (string path, int row, int col, string line, string capture) : IFind {}
