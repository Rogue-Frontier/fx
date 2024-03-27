using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
				BorderStyle = LineStyle.Single,
				Title = "Terminal"
			};
			var output = new TextView() {
				X = 0,
				Y = 0,
				Width = Dim.Fill(),
				Height = Dim.Fill(),

				ReadOnly = true,
				CanFocus = true
			};

			main.TermEnter += e => {
				if(main.folder.currentBody != root) {
					return;
				}
				output.Text += $"{cwd}>{e.text}\n";

				var cmd = e.text;
				cmd = @$"/c {cmd}";
				// & echo !fx{{%cd%}}
				var pi = new ProcessStartInfo("cmd.exe") {
					Arguments = $"{cmd}",
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
						output.Text += text + '\n';
						output.ScrollTo(output.Lines - output.Bounds.Height - 1);
					});
				}

			};
			SView.InitTree([root, output]);
		}

		private void P_ErrorDataReceived (object sender, DataReceivedEventArgs e) {
			throw new NotImplementedException();
		}
	}
}
