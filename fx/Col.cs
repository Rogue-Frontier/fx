using System.Numerics;
using Terminal.Gui;

namespace fx {
	public record Ext<T>(T min, T max) where T:IComparisonOperators<T, T, bool> {
        public Ext (T c) : this(c, c) { }
        public T Clamp (T x) =>
            x < min ? min :
            x > max ? max :
            x;
    }
    public record Length(float weight = 1) {
        public Ext<int>? absolute;
        public Ext<float> fraction = new(0, 1);
	}
    public interface IDisplay {
        bool IsVisible { get; }
        public static View Full => new View() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        void Refresh ();
        View GetView ();
    }
    public interface ICol : IDisplay {
		Length Width { get; }
        public float weight => Width.weight;
	}
    public interface IRow : IDisplay {
        Length Height { get; }
        public float weight => Height.weight;
    }
    public record Row(Length Height) : IRow {
        public List<ICol> Cols = new();

        private Dictionary<ICol, View> Subviews = new();
        public bool IsVisible => Cols.Any(c => c.IsVisible);
        public void Refresh () => Cols.ForEach(c => c.Refresh());
        public View GetView () {
            var items = Cols.Where(c => c.IsVisible);
            var weightSum = (float)items.Sum(c => c.weight);
			var view = IDisplay.Full;

            var x = 0f;
			foreach(var i in items) {
                var v = i.GetView();
				v.X = Pos.Percent((int)x);
				var frac = i.Width.fraction.Clamp(i.weight / weightSum);
                x += frac;
				v.Width = Dim.Percent((int)(frac * 100));
                view.Add(v);
				Subviews[i] = v;
			}
			return view;
        }
    }
    public record Col(Length Width) : IDisplay, ICol {
        public List<IRow> Rows = new();
		private Dictionary<IRow, View> Subviews = new();
		public bool IsVisible => Rows.Any(c => c.IsVisible);
		public void Refresh () => Rows.ForEach(c => c.Refresh());
		public View GetView () {
			var items = Rows.Where(c => c.IsVisible);
			var weightSum = (float)items.Sum(c => c.weight);
			var view = IDisplay.Full;
            var y = 0f;
			foreach(var i in items) {
				var v = i.GetView();
                v.Y = Pos.Percent((int)y);
				var frac = i.Height.fraction.Clamp(i.weight / weightSum);
                y += frac;
				v.Height = Dim.Percent((int)(frac * 100));
				view.Add(v);
                Subviews[i] = v;
			}
			return view;
		}


	}
	public record Box(View view) : IDisplay, IRow, ICol {
        public bool IsVisible { get; private set; } = true;
		public Length Width { get; } = new();
		public Length Height { get; } = new();
		public void Refresh () {
            IsVisible = true;
        }
		public View GetView() {
            return view;
		}
	}
}