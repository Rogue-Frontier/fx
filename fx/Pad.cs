using NStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using Terminal.Gui;
using Application = Terminal.Gui.Application;

namespace fx;

public class Pad : View {
	//
	// Summary:
	//     Clicked System.Action, raised when the user clicks the primary mouse button within
	//     the Bounds of this Terminal.Gui.View or if the user presses the action key while
	//     this view is focused. (TODO: IsDefault)
	//
	// Remarks:
	//     Client code can hook up to this event, it is raised when the button is activated
	//     either with the mouse or the keyboard.
	public event Action Clicked;
	public Pad (ustring text)
	: base(text) {
	}
	//
	// Summary:
	//     Method invoked when a mouse event is generated
	//
	// Parameters:
	//   mouseEvent:
	//
	// Returns:
	//     true, if the event was handled, false otherwise.
	public override bool OnMouseEvent (MouseEvent mouseEvent) {
		MouseEventArgs args = new MouseEventArgs(mouseEvent);
		if(OnMouseClick(args)) {
			return true;
		}

		if(MouseEvent(mouseEvent)) {
			return true;
		}

		if(mouseEvent.Flags == MouseFlags.Button1Clicked) {
			if(!HasFocus && SuperView != null) {
				if(!SuperView.HasFocus) {
					SuperView.SetFocus();
				}

				SetFocus();
				SetNeedsDisplay();
			}

			OnClicked();
			return true;
		}

		return false;
	}

	public override bool OnEnter (View view) {
		Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);
		return base.OnEnter(view);
	}
	public virtual void OnClicked () =>
		Clicked?.Invoke();
}
