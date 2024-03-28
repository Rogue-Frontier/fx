using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace fx {
	public class TermSession {
		public View root;
		public TermSession(Main main, string cwd) {
			root = new View() {
				AutoSize = false,
				X = 0,
				Y=0,
				Width=Dim.Fill(),
				Height=Dim.Fill(),
			};
			var tree = new FileTree();
			var fileTree = new TreeView<string>() {
				AutoSize = false,
				X = 0,
				Y = 1,
				Width = 32,
				Height = Dim.Fill(),
				BorderStyle = LineStyle.Single,
				Title = "Directory",
				TreeBuilder = tree,
				AspectGetter = tree.GetName
			};
			var cwdBar = new TextField() {
				AutoSize = false,
				X = 0,
				Y = 0,
				Width = Dim.Fill(),
				Height = 1,

				Text = cwd,

				ReadOnly = true,
				CanFocus = false
			};

			SetCwd(cwd);

			fileTree.AddObject(new("C:\\"));
			fileTree.AddObject(new("D:\\"));

			
			fileTree.ObjectActivated += (a, e) => {

				var path = e.ActivatedObject;
				if(Directory.Exists(path)) {
					SetCwd(path);
				} else {
					//Append path to cmd 
				}
				
			};


			var output = new TextView() {
				X = 32,
				Y = 1,
				Width = Dim.Fill(),
				Height = Dim.Fill(),

				BorderStyle = LineStyle.Single,

				ReadOnly = true,
				CanFocus = true
			};
			main.TermEnter += e => {
				if(main.folder.currentBody != root) {
					return;
				}
				output.Text+=($"{cwd}>{e.text}\n");
				var cmd = e.text;
				cmd = @$"/c {cmd}";
				// & echo !fx{{%cd%}}
				var pi = new ProcessStartInfo("cmd.exe") {
					Arguments = $"{cmd}",
					WorkingDirectory = cwd,
					WindowStyle = ProcessWindowStyle.Hidden,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					RedirectStandardInput = true,
				};
				e.term.Text = "";
				Application.Invoke(() => {
					using var p = Process.Start(pi);
					p.BeginOutputReadLine();
					p.BeginErrorReadLine();
					p.OutputDataReceived += (_, d) => {
						AppendLine(d.Data);
					};
					p.ErrorDataReceived += (_, d) => {
						AppendLine(d.Data);
					};
					p.StandardInput.Close();
					p.WaitForExit();
					p.CancelOutputRead();
					p.CancelErrorRead();
				});
				void AppendLine(string text) {
					Application.Invoke(() => {
						if(text == null && output.Text.EndsWith("\n\r\n")) {
							return;
						}
						/*
						if(text?.MatchArray("^!fx{(.+)}$") is [var msg]) {
							cwd = msg;
							return;
						}
						*/
						//output.Text += text + '\n';
						output.Text+=($"{text}\n");
						output.ScrollTo(output.Lines - output.Bounds.Height - 1);
					});
				}
			};

			IEnumerable<string> GetPathNodes (string src) {
				var node = Path.GetDirectoryName(src);
				while(node != null) {
					yield return node;
					node = Path.GetDirectoryName(node);
				}
			}
			foreach(var node in GetPathNodes(cwd).Reverse()) {
				fileTree.Expand(node);
			}
			fileTree.SelectedObject = cwd;


			SView.InitTree([root, fileTree, cwdBar, output]);

			void SetCwd(string s) {
				s = Path.GetFullPath(s);
				cwd = s;
				cwdBar.Text = s;
			}
		}
	}
}
public record FileTree : ITreeBuilder<string> {

	private ConcurrentDictionary<string, bool> canExpand = new();
	private ConcurrentDictionary<string, string[]> getChildren = new();
	private ConcurrentDictionary<string, string> name = new();

	public string GetName (string path) => name.GetOrAdd(path, _ =>
		(Path.GetFileName(path) is { Length: > 0 } n ? n : Path.GetFullPath(path)) + (Directory.Exists(path) ? "/" : "")
	);
	public bool SupportsCanExpand => true;
	public bool CanExpand (string path) => canExpand.GetOrAdd(path, _ => {
		try {
			return Directory.Exists(path) && Directory.GetDirectories(path).Any();
		} catch(Exception e) {
			return false;
		}
	});
	public IEnumerable<string> GetChildren (string path) => getChildren.GetOrAdd(path, _ => {
		try {
			return Directory.GetDirectories(path).ToArray();
		}catch(Exception e) {
			return [];
		}
	});
}