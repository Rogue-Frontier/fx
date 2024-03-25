using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace fx {
	public record TermEvent(TextField term) {
		public string text => term.Text.ToString();
		public bool Handled = false;
	}
}
