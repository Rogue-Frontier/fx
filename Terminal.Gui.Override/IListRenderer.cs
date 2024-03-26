using fx;
using System.Collections;
public interface IListRenderer {
	int Count { get; }
	int Length { get; }
	void Render (ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0);
	bool IsMarked (int item);
	void SetMark (int item, bool value);
	IList ToList ();
}
