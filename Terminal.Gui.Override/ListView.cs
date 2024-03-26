using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace fx;

public class ListView : View {
	private int top;

	private int left;

	private int selected;

	private IListDataSource source;

	private bool allowsMarking;

	private int lastSelectedItem = -1;

	private bool allowsMultipleSelection = true;

	//
	// Summary:
	//     Gets or sets the Terminal.Gui.IListDataSource backing this Terminal.Gui.ListView,
	//     enabling custom rendering.
	//
	// Value:
	//     The source.
	//
	// Remarks:
	//     Use Terminal.Gui.ListView.SetSource(System.Collections.IList) to set a new System.Collections.IList
	//     source.
	public IListDataSource Source {
		get {
			return source;
		}
		set {
			source = value;
			KeystrokeNavigator.Collection = source?.ToList()?.Cast<object>();
			top = 0;
			selected = 0;
			lastSelectedItem = -1;
			SetNeedsDisplay();
		}
	}
	public bool AllowsMarking {
		get {
			return allowsMarking;
		}
		set {
			allowsMarking = value;
			SetNeedsDisplay();
		}
	}

	//
	// Summary:
	//     If set to true more than one item can be selected. If false selecting an item
	//     will cause all others to be un-selected. The default is false.
	public bool AllowsMultipleSelection {
		get {
			return allowsMultipleSelection;
		}
		set {
			allowsMultipleSelection = value;
			if(Source != null && !allowsMultipleSelection) {
				for(int i = 0; i < Source.Count; i++) {
					if(Source.IsMarked(i) && i != selected) {
						Source.SetMark(i, value: false);
					}
				}
			}

			SetNeedsDisplay();
		}
	}

	//
	// Summary:
	//     Gets or sets the item that is displayed at the top of the Terminal.Gui.ListView.
	//
	//
	// Value:
	//     The top item.
	public int TopItem {
		get {
			return top;
		}
		set {
			if(source != null) {
				Debug.Assert(value > -1 && (source.Count == 0 || value < source.Count), "index out of range");

				top = value;
				SetNeedsDisplay();
			}
		}
	}
	//
	// Summary:
	//     Gets or sets the leftmost column that is currently visible (when scrolling horizontally).
	//
	//
	// Value:
	//     The left position.
	public int LeftItem {
		get {
			return left;
		}
		set {
			if(source != null) {
				Debug.Assert(value > -1 && (Maxlength == 0 || value < Maxlength), "index out of range");
				left = value;
				SetNeedsDisplay();
			}
		}
	}
	//
	// Summary:
	//     Gets the widest item in the list.
	public int Maxlength => source?.Length ?? 0;
	//
	// Summary:
	//     Gets or sets the index of the currently selected item.
	//
	// Value:
	//     The selected item.
	public int SelectedItem {
		get {
			return selected;
		}
		set {
			Debug.Assert(source != null && source.Count > 0, "Source is empty");
			Debug.Assert(value > -1 && value < source.Count, "index out of range");
			selected = value;
			OnSelectedChanged();
		}
	}
	//
	// Summary:
	//     Gets the Terminal.Gui.CollectionNavigator that searches the Terminal.Gui.ListView.Source
	//     collection as the user types.
	public CollectionNavigator KeystrokeNavigator { get; private set; } = new CollectionNavigator();


	//
	// Summary:
	//     This event is raised when the selected item in the Terminal.Gui.ListView has
	//     changed.
	public event Action<ListViewItemEventArgs> SelectedItemChanged;

	//
	// Summary:
	//     This event is raised when the user Double Clicks on an item or presses ENTER
	//     to open the selected item.
	public event Action<ListViewItemEventArgs> OpenSelectedItem;

	//
	// Summary:
	//     This event is invoked when this Terminal.Gui.ListView is being drawn before rendering.
	public event Action<ListViewRowEventArgs> RowRender;

	//
	// Summary:
	//     Sets the source of the Terminal.Gui.ListView to an System.Collections.IList.
	//
	//
	// Value:
	//     An object implementing the IList interface.
	//
	// Remarks:
	//     Use the Terminal.Gui.ListView.Source property to set a new Terminal.Gui.IListDataSource
	//     source and use custome rendering.
	public void SetSource (IList source) {
		if(source == null && (Source == null || !(Source is ListWrapper))) {
			Source = null;
		} else {
			Source = Wrap(source);
		}
	}

	//
	// Summary:
	//     Sets the source to an System.Collections.IList value asynchronously.
	//
	// Value:
	//     An item implementing the IList interface.
	//
	// Remarks:
	//     Use the Terminal.Gui.ListView.Source property to set a new Terminal.Gui.IListDataSource
	//     source and use custom rendering.
	public Task SetSourceAsync (IList source) {
		return Task.Factory.StartNew(delegate {
			if(source == null && (Source == null || !(Source is ListWrapper))) {
				Source = null;
			} else {
				Source = Wrap(source);
			}

			return source;
		}, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
	}

	private static IListDataSource Wrap (IList source) {
		return new ListWrapper(source);
	}

	//
	// Summary:
	//     Initializes a new instance of Terminal.Gui.ListView that will display the contents
	//     of the object implementing the System.Collections.IList interface, with relative
	//     positioning.
	//
	// Parameters:
	//   source:
	//     An System.Collections.IList data source, if the elements are strings or ustrings,
	//     the string is rendered, otherwise the ToString() method is invoked on the result.
	public ListView (IList source)
		: this(Wrap(source)) {
	}

	//
	// Summary:
	//     Initializes a new instance of Terminal.Gui.ListView that will display the provided
	//     data source, using relative positioning.
	//
	// Parameters:
	//   source:
	//     Terminal.Gui.IListDataSource object that provides a mechanism to render the data.
	//     The number of elements on the collection should not change, if you must change,
	//     set the "Source" property to reset the internal settings of the ListView.
	public ListView (IListDataSource source) {
		this.source = source;
		Initialize();
	}

	//
	// Summary:
	//     Initializes a new instance of Terminal.Gui.ListView. Set the Terminal.Gui.ListView.Source
	//     property to display something.
	public ListView () {
		Initialize();
	}

	//
	// Summary:
	//     Initializes a new instance of Terminal.Gui.ListView that will display the contents
	//     of the object implementing the System.Collections.IList interface with an absolute
	//     position.
	//
	// Parameters:
	//   rect:
	//     Frame for the listview.
	//
	//   source:
	//     An IList data source, if the elements of the IList are strings or ustrings, the
	//     string is rendered, otherwise the ToString() method is invoked on the result.
	public ListView (Rect rect, IList source)
		: this(rect, Wrap(source)) {
		Initialize();
	}

	//
	// Summary:
	//     Initializes a new instance of Terminal.Gui.ListView with the provided data source
	//     and an absolute position
	//
	// Parameters:
	//   rect:
	//     Frame for the listview.
	//
	//   source:
	//     IListDataSource object that provides a mechanism to render the data. The number
	//     of elements on the collection should not change, if you must change, set the
	//     "Source" property to reset the internal settings of the ListView.
	public ListView (Rect rect, IListDataSource source)
		: base(rect) {
		this.source = source;
		Initialize();
	}

	private void Initialize () {
		Source = source;
		CanFocus = true;
		AddCommand(Command.LineUp, () => MoveUp());
		AddCommand(Command.LineDown, () => MoveDown());
		AddCommand(Command.ScrollUp, () => ScrollUp(1));
		AddCommand(Command.ScrollDown, () => ScrollDown(1));
		AddCommand(Command.PageUp, () => MovePageUp());
		AddCommand(Command.PageDown, () => MovePageDown());
		AddCommand(Command.TopHome, () => MoveHome());
		AddCommand(Command.BottomEnd, () => MoveEnd());
		AddCommand(Command.OpenSelectedItem, () => OnOpenSelectedItem());
		AddCommand(Command.ToggleChecked, () => ToggleRowMark());
		AddKeyBinding(Key.CursorUp, Command.LineUp);
		AddKeyBinding(Key.P | Key.CtrlMask, Command.LineUp);
		AddKeyBinding(Key.CursorDown, default(Command));
		AddKeyBinding(Key.N | Key.CtrlMask, default(Command));
		AddKeyBinding(Key.PageUp, Command.PageUp);
		AddKeyBinding(Key.PageDown, Command.PageDown);
		AddKeyBinding(Key.V | Key.CtrlMask, Command.PageDown);
		AddKeyBinding(Key.Home, Command.TopHome);
		AddKeyBinding(Key.End, Command.BottomEnd);
		AddKeyBinding(Key.Enter, Command.OpenSelectedItem);
	}

	public override void Redraw (Rect bounds) {
		Attribute attribute = ColorScheme.Focus;
		View.Driver.SetAttribute(attribute);
		Move(0, 0);
		Rect rect = Frame;
		int num = top;
		bool flag = HasFocus;
		int num2 = (allowsMarking ? 2 : 0);
		int start = left;
		int num3 = 0;
		while(num3 < rect.Height) {
			bool flag2 = num == selected;
			Attribute attribute2 = ((!flag) ? (flag2 ? ColorScheme.HotNormal : GetNormalColor()) : (flag2 ? ColorScheme.Focus : GetNormalColor()));
			if((int)attribute2 != (int)attribute) {
				View.Driver.SetAttribute(attribute2);
				attribute = attribute2;
			}

			Move(0, num3);
			if(source == null || num >= source.Count) {
				for(int i = 0; i < rect.Width; i++) {
					View.Driver.AddRune(' ');
				}
			} else {
				ListViewRowEventArgs listViewRowEventArgs = new ListViewRowEventArgs(num);
				OnRowRender(listViewRowEventArgs);
				if(listViewRowEventArgs.RowAttribute.HasValue && (int)attribute != (int?)listViewRowEventArgs.RowAttribute) {
					attribute = listViewRowEventArgs.RowAttribute.Value;
					View.Driver.SetAttribute(attribute);
				}

				if(allowsMarking) {
					View.Driver.AddRune((!source.IsMarked(num)) ? (AllowsMultipleSelection ? View.Driver.UnChecked : View.Driver.UnSelected) : (AllowsMultipleSelection ? View.Driver.Checked : View.Driver.Selected));
					View.Driver.AddRune(' ');
				}

				Source.Render(this, View.Driver, flag2, num, num2, num3, rect.Width - num2, start);
			}

			num3++;
			num++;
		}
	}

	public override bool ProcessKey (KeyEvent kb) {
		if(source == null) {
			return base.ProcessKey(kb);
		}

		bool? flag = InvokeKeybindings(kb);
		if(flag.HasValue) {
			return flag.Value;
		}

		if(CollectionNavigator.IsCompatibleKey(kb)) {
			int? num = KeystrokeNavigator?.GetNextMatchingItem(SelectedItem, (char)kb.KeyValue);
			if(num is int && num != -1) {
				SelectedItem = num.Value;
				EnsureSelectedItemVisible();
				SetNeedsDisplay();
				return true;
			}
		}

		return false;
	}
	public virtual bool AllowsAll () {
		if(!allowsMarking) {
			return false;
		}

		if(!AllowsMultipleSelection) {
			for(int i = 0; i < Source.Count; i++) {
				if(Source.IsMarked(i) && i != selected) {
					Source.SetMark(i, value: false);
					return true;
				}
			}
		}

		return true;
	}
	public virtual bool ToggleRowMark () {
		if(AllowsAll()) {
			Source.SetMark(SelectedItem, !Source.IsMarked(SelectedItem));
			SetNeedsDisplay();
			return true;
		}
		return false;
	}
	public virtual bool MovePageUp () {
		int num = selected - Frame.Height;
		if(num < 0) {
			num = 0;
		}
		if(num != selected) {
			selected = num;
			top = selected;
			OnSelectedChanged();
			SetNeedsDisplay();
		}
		return true;
	}
	public virtual bool MovePageDown () {
		int num = selected + Frame.Height;
		if(num >= source.Count) {
			num = source.Count - 1;
		}
		if(num != selected) {
			selected = num;
			if(source.Count >= Frame.Height) {
				top = selected;
			} else {
				top = 0;
			}
			OnSelectedChanged();
			SetNeedsDisplay();
		}
		return true;
	}
	public virtual bool MoveDown () {
		if(source.Count == 0) {
			return false;
		}
		if(selected >= source.Count) {
			selected = source.Count - 1;
			OnSelectedChanged();
			SetNeedsDisplay();
		} else if(selected + 1 < source.Count) {
			selected++;
			if(selected >= top + Frame.Height) {
				top++;
			} else if(selected < top) {
				top = selected;
			} else if(selected < top) {
				top = selected;
			}
			OnSelectedChanged();
			SetNeedsDisplay();
		} else if(selected == 0) {
			OnSelectedChanged();
			SetNeedsDisplay();
		} else if(selected >= top + Frame.Height) {
			top = source.Count - Frame.Height;
			SetNeedsDisplay();
		}
		return true;
	}
	public virtual bool MoveUp () {
		if(source.Count == 0) {
			return false;
		}
		if(selected >= source.Count) {
			selected = source.Count - 1;
			OnSelectedChanged();
			SetNeedsDisplay();
		} else if(selected > 0) {
			selected--;
			if(selected > Source.Count) {
				selected = Source.Count - 1;
			}
			if(selected < top) {
				top = selected;
			} else if(selected > top + Frame.Height) {
				top = Math.Max(selected - Frame.Height + 1, 0);
			}
			OnSelectedChanged();
			SetNeedsDisplay();
		} else if(selected < top) {
			top = selected;
			SetNeedsDisplay();
		}
		return true;
	}
	public virtual bool MoveEnd () {
		if(source.Count > 0 && selected != source.Count - 1) {
			selected = source.Count - 1;
			if(top + selected > Frame.Height - 1) {
				top = selected;
			}
			OnSelectedChanged();
			SetNeedsDisplay();
		}
		return true;
	}
	public virtual bool MoveHome () {
		if(selected != 0) {
			selected = 0;
			top = selected;
			OnSelectedChanged();
			SetNeedsDisplay();
		}
		return true;
	}
	public virtual bool ScrollDown (int items) {
		top = Math.Max(Math.Min(top + items, source.Count - 1), 0);
		SetNeedsDisplay();
		return true;
	}
	public virtual bool ScrollUp (int items) {
		top = Math.Max(top - items, 0);
		SetNeedsDisplay();
		return true;
	}
	public virtual bool ScrollRight (int cols) {
		left = Math.Max(Math.Min(left + cols, Maxlength - 1), 0);
		SetNeedsDisplay();
		return true;
	}

	//
	// Summary:
	//     Scrolls the view left.
	//
	// Parameters:
	//   cols:
	//     Number of columns to scroll left.
	public virtual bool ScrollLeft (int cols) {
		left = Math.Max(left - cols, 0);
		SetNeedsDisplay();
		return true;
	}
	public virtual bool OnSelectedChanged () {
		if(selected != lastSelectedItem) {
			IListDataSource listDataSource = source;
			object value = ((listDataSource != null && listDataSource.Count > 0) ? source.ToList()[selected] : null);
			this.SelectedItemChanged?.Invoke(new ListViewItemEventArgs(selected, value));
			if(HasFocus) {
				lastSelectedItem = selected;
			}

			return true;
		}

		return false;
	}
	public virtual bool OnOpenSelectedItem () {
		if(source.Count <= selected || selected < 0 || this.OpenSelectedItem == null) {
			return false;
		}

		object value = source.ToList()[selected];
		this.OpenSelectedItem?.Invoke(new ListViewItemEventArgs(selected, value));
		return true;
	}
	public virtual void OnRowRender (ListViewRowEventArgs rowEventArgs) {
		this.RowRender?.Invoke(rowEventArgs);
	}
	public override bool OnEnter (View view) {
		Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);
		if(lastSelectedItem == -1) {
			EnsureSelectedItemVisible();
		}
		return base.OnEnter(view);
	}
	public override bool OnLeave (View view) {
		if(lastSelectedItem > -1) {
			lastSelectedItem = -1;
		}
		return base.OnLeave(view);
	}
	public void EnsureSelectedItemVisible () {
		base.SuperView?.LayoutSubviews();
		if(selected < top) {
			top = selected;
		} else if(Frame.Height > 0 && selected >= top + Frame.Height) {
			top = Math.Max(selected - Frame.Height + 1, 0);
		}
	}
	public override void PositionCursor () {
		if(allowsMarking) {
			Move(0, selected - top);
		} else {
			Move(base.Bounds.Width - 1, selected - top);
		}
	}
	public override bool MouseEvent (MouseEvent me) {
		if(!me.Flags.HasFlag(MouseFlags.Button1Clicked) && !me.Flags.HasFlag(MouseFlags.Button1DoubleClicked) && me.Flags != MouseFlags.WheeledDown && me.Flags != MouseFlags.WheeledUp && me.Flags != MouseFlags.WheeledRight && me.Flags != MouseFlags.WheeledLeft) {
			return false;
		}

		if(!HasFocus && CanFocus) {
			SetFocus();
		}

		if(source == null) {
			return false;
		}

		if(me.Flags == MouseFlags.WheeledDown) {
			ScrollDown(1);
			return true;
		}

		if(me.Flags == MouseFlags.WheeledUp) {
			ScrollUp(1);
			return true;
		}

		if(me.Flags == MouseFlags.WheeledRight) {
			ScrollRight(1);
			return true;
		}

		if(me.Flags == MouseFlags.WheeledLeft) {
			ScrollLeft(1);
			return true;
		}

		if(me.Y + top >= source.Count) {
			return true;
		}

		selected = top + me.Y;
		if(AllowsAll()) {
			Source.SetMark(SelectedItem, !Source.IsMarked(SelectedItem));
		}

		OnSelectedChanged();
		SetNeedsDisplay();
		if(me.Flags == MouseFlags.Button1DoubleClicked) {
			OnOpenSelectedItem();
		}

		return true;
	}
}
#if false // Decompilation log
'177' items in cache
------------------
Resolve: 'System.Runtime, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Runtime.dll'
------------------
Resolve: 'System.Collections, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Collections, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Collections.dll'
------------------
Resolve: 'NStack, Version=1.1.1.0, Culture=neutral, PublicKeyToken=b1bc8ab4b95b4a4a'
Found single assembly: 'NStack, Version=1.1.1.0, Culture=neutral, PublicKeyToken=b1bc8ab4b95b4a4a'
Load from: 'C:\Users\alexm\.nuget\packages\nstack.core\1.1.1\lib\netstandard2.0\NStack.dll'
------------------
Resolve: 'System.Console, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Console, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Console.dll'
------------------
Resolve: 'System.Threading, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Threading, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Threading.dll'
------------------
Resolve: 'Microsoft.Win32.Primitives, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'Microsoft.Win32.Primitives, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\Microsoft.Win32.Primitives.dll'
------------------
Resolve: 'System.ComponentModel.TypeConverter, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ComponentModel.TypeConverter, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.ComponentModel.TypeConverter.dll'
------------------
Resolve: 'System.ComponentModel.Primitives, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ComponentModel.Primitives, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.ComponentModel.Primitives.dll'
------------------
Resolve: 'System.Diagnostics.Process, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Diagnostics.Process, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Diagnostics.Process.dll'
------------------
Resolve: 'System.Management, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Management, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Users\alexm\.nuget\packages\system.management\8.0.0\lib\net8.0\System.Management.dll'
------------------
Resolve: 'System.Data.Common, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Data.Common, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Data.Common.dll'
------------------
Resolve: 'System.IO.FileSystem.Watcher, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.IO.FileSystem.Watcher, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.IO.FileSystem.Watcher.dll'
------------------
Resolve: 'System.Text.RegularExpressions, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Text.RegularExpressions, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Text.RegularExpressions.dll'
------------------
Resolve: 'System.Runtime.InteropServices, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.InteropServices, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Runtime.InteropServices.dll'
------------------
Resolve: 'System.Linq, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Linq, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Linq.dll'
------------------
Resolve: 'System.Threading.Thread, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Threading.Thread, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Threading.Thread.dll'
------------------
Resolve: 'System.ComponentModel.Primitives, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ComponentModel.Primitives, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.ComponentModel.Primitives.dll'
------------------
Resolve: 'System.ObjectModel, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ObjectModel, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.ObjectModel.dll'
------------------
Resolve: 'System.Runtime, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Runtime.dll'
------------------
Resolve: 'System.Runtime.CompilerServices.Unsafe, Version=7.0.0.0, Culture=neutral, PublicKeyToken=null'
Found single assembly: 'System.Runtime.CompilerServices.Unsafe, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '7.0.0.0', Got: '9.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\9.0.0-preview.2.24128.5\ref\net9.0\System.Runtime.CompilerServices.Unsafe.dll'
#endif

