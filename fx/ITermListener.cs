using Terminal.Gui;

namespace fx {
	public record TermEvent(TextField term) {
		public string text = term.Text.ToString();
		public bool Handled = false;
	}
}
