using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace fx;
public class FindSession : ITab {
	public string TabName => "Find";
	public View TabView => root;
	public FindFilter filter = new FindFilter(null, null, null);
	public View root;
	public TreeView<IFind> tree;
	public TextField rootBar;
	private Ctx ctx;
	public FindSession (Main main) {
		ctx = main.ctx;
		Fx fx = ctx.fx;
		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			CanFocus = true,
		};
		var finder = new TreeFinder();
		int w = 8;
		int y = 0;
		var rootLabel = new Label("Dir") {
			X = 0,
			Y = y,
			Width = w
		};
		rootBar = new TextField(fx.cwd.Replace(ctx.USER_PROFILE, Ctx.USER_PROFILE_MASK)) {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};

		var rootShowButton = new Button("All", false) {
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
		var filterShowButton = new Button("All", false) {
			X = Pos.Right(filterBar),
			Y = y,
			Width = 6
		};
		y++;
		var findLabel = new Label("Text") {
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
		y++;
		tree = new TreeView<IFind>(finder) {
			X = 0,
			Y = y,
			Width = Dim.Fill(0),
			Height = Dim.Fill(0),
			AspectGetter = f => f switch {
				FindDir d => $"{d.name}/",
				FindFile ff => ff.name,
				FindLine l => $"{l.row,3}|{l.line}"
			}
		};
		tree.ObjectActivated += e => {
			if(e.ActivatedObject is FindLine l) {
				main.EditFile(l);
			}
		};
		root.AddKey(new() {
			[Key.DeleteChar] = () => {
				int i = 0;
			},
			[Key.Delete] = () => {
				var tab = main.tabs.Tabs.First(t => t.View == root);
				main.tabs.RemoveTab(tab);
			}
		});
		root.AddKeyPress(e => {
			return null;
		});

		rootBar.AddKey(new() {
			[Key.Enter] = FindDirs
		});
		filterBar.AddKey(new() {
			[Key.Enter] = FindFiles
		});
		findBar.AddKey(new() {
			[Key.Enter] = FindLines
		});


		rootShowButton.Clicked += FindDirs;
		filterShowButton.Clicked += FindFiles;


		tree.AddMouse(new() {
			[MouseFlags.Button3Clicked] = e => {
				
				tree.SelectedObject = tree.GetObjectOnRow(e.MouseEvent.Y);
				tree.SetNeedsDisplay();
			},
		});
		tree.AddKey(new() {
			[Key.CursorRight] = () => {
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
		findAllButton.Clicked += FindLines;
		//Print button (no replace)
		FindLines();
		SView.InitTree([root,
			rootLabel, rootBar, rootShowButton,
			filterLabel, filterBar, filterShowButton,
			findLabel, findBar, findAllButton, findPrevButton, findNextButton,
			replaceLabel, replaceBar, replaceAllButton, replacePrevButton, replaceNextButton,
			tree
			]);
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
		var root = GetRoot();
		tree.AddObject(
			Directory.Exists(root) ?
				new FindDir(filter, root) :
				new FindFile(filter, root)
				);
		//tree.ExpandAll();
	}
	string GetRoot () =>
		rootBar.Text.ToString().Replace(Ctx.USER_PROFILE_MASK, ctx.USER_PROFILE);
}

public record FindFilter (Regex filePattern, Regex linePattern, string replace) {
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
public interface IFind {
	bool IsMatch () => false;
	IEnumerable<IFind> GetChildren ();
	IEnumerable<FindLine> GetLeaves ();
}
public record FindDir (FindFilter filter, string path) : IFind {
	public string name => Path.GetFileName(path);


	private IEnumerable<IFind> GetDescendants (bool excludeFiles = false) {
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
			if(m is { Success: true })
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