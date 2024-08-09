using Terminal.Gui;




using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util;
using Event = Google.Apis.Calendar.v3.Data.Event;

namespace fx {
	public class Calendar {
		public View root;
		public Calendar (Main main) {
			var cal = "primary";

			var fx = main.ctx.fx;
			OAuth[] accounts = [
				new("achen115@ucr.edu",
				"40405526811-rql4l5dflr4nqu01u0olqtkipn59fmfg.apps.googleusercontent.com",
				"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")
			];
			var auth = accounts[0];
			string[] scopes = { CalendarService.Scope.Calendar };
			var credentials = GoogleWebAuthorizationBroker
				.AuthorizeAsync(auth.secrets, scopes, "user", CancellationToken.None).Result;
			if(credentials.Token.IsExpired(SystemClock.Default)) {
				var b = credentials.RefreshTokenAsync(CancellationToken.None).Result;
			}
			var service = new CalendarService(new BaseClientService.Initializer() {
				HttpClientInitializer = credentials
			});
			root = new View() {
				AutoSize = false,
				X = 0,
				Y = 0,
				Width = Dim.Fill(),
				Height = Dim.Fill(),
			};
			var evData = new ListMarker<Event>((e, i) => $"{DateOnly.FromDateTime(e.Start.DateTimeDateTimeOffset.Value.DateTime).ToString("yyyy/MM/dd")}: {e.Summary}" ?? "<untitled>");
			RefreshEvents();
			var evList = new ListView() {
				AutoSize = false,
				X = 0,
				Y = 1,
				Width = Dim.Percent(25),
				Height = Dim.Fill(1),
				Title = "Events",
				BorderStyle = LineStyle.Single,
				AllowsMarking = true,
				AllowsMultipleSelection = true,
				Source = evData
			};
			evList.KeyDownD(new() {
				['.'] = k => {
					RefreshEvents();
				},
				[(int)KeyCode.Delete] = k => {
					k.Handled = true;
					evData.UpdateMarked();
					if(evData.marked.Any()) {
						Task.Run(() => {
							foreach(var e in evData.marked) {
								string id = e.Id;
								service.Events.Delete(cal, id).Execute();
								Application.Invoke(() => {
									evData.marked.Remove(e);
									evData.list.Remove(e);
									evList.SetNeedsDisplay();
								});
							}
						});
					} else if(evData.list.ElementAtOrDefault(evList.SelectedItem) is { } e) {
						//service.Events.Delete(cal, e.RecurringEventId);
						Task.Run(() => {
							service.Events.Delete(cal, e.Id).Execute();
							Application.Invoke(() => {

								evData.list.Remove(e);
								evList.SetNeedsDisplay();
							});
						});
					}
				}
			});
			var evView = new View() {
				Title = "Details",
				AutoSize = false,
				X = Pos.Right(evList),
				Y = 0,
				Width = Dim.Fill(),
				Height = Dim.Fill(),
				BorderStyle = LineStyle.Single,
			};
			var create = new Button() {
				Title = "New Event",
				AutoSize = false,
				X = 0,
				Y = 0,
				Width = Dim.Percent(25),
				Height = 1,
				NoDecorations = true,
				NoPadding = true
			};
			create.MouseClick += (a, e) => {
				var confirm = new Button() { Title = "Confirm", };
				var cancel = new Button() { Title = "Cancel", };
				var d = new Dialog() {
					Title = "New Event",
					Buttons = [confirm, cancel],
					Width = 96,
					Height = 16,
				};
				int x = 0;
				int y = 0;
				var timeLabel = new Label() {
					AutoSize = false,
					X = x,
					Y = y,
					Width = 8,
					Height = 1,
					Text = "Time",
				};
				x = 8;
				var start = DateTime.Now;
				var end = DateTime.Now + TimeSpan.FromHours(1);
				var duration = end - start;
				Action setStart = default, setEnd = default, setDur = default;
				View[] timeFieldStart = [
					tf(ref x, 4, ()=>start.Year, year => SetStart(start.With(year:year)), ref setStart),
					tl(ref x, 1, '/'),
					tf(ref x, 2, ()=>start.Month, month => SetStart(start.With(month:month)), ref setStart),
					tl(ref x, 1, '/'),
					tf(ref x, 2, ()=>start.Day, day => SetStart(start.With(day:day)), ref setStart),
					tl(ref x, 1, ' '),
					tf(ref x, 2, ()=>start.Hour, hour => SetStart(start.With(hour:hour)), ref setStart),
					tl(ref x, 1, ':'),
					tf(ref x, 2, ()=>start.Minute, minute => SetStart(start.With(minute:minute)), ref setStart),
					tl(ref x, 1, ':'),
					tf(ref x, 2, ()=>start.Second, second => SetStart(start.With(second:second)), ref setStart),
					];
				x = 8;
				y++;
				View[] timeFieldEnd = [
					tf(ref x, 4, ()=>end.Year, year => SetEnd(end.With(year:year)), ref setEnd),
					tl(ref x, 1, '/'),
					tf(ref x, 2, ()=>end.Month, month => SetEnd(end.With(month:month)), ref setEnd),
					tl(ref x, 1, '/'),
					tf(ref x, 2, ()=>end.Day, day => SetEnd(end.With(day:day)), ref setEnd),
					tl(ref x, 1, ' '),
					tf(ref x, 2, ()=>end.Hour, hour => SetEnd(end.With(hour:hour)), ref setEnd),
					tl(ref x, 1, ':'),
					tf(ref x, 2, ()=>end.Minute, minute => SetEnd(end.With(minute:minute)), ref setEnd),
					tl(ref x, 1, ':'),
					tf(ref x, 2, ()=>end.Second, second => SetEnd(end.With(second:second)), ref setEnd)
					];
				x = 19;
				y++;
				View[] timeFieldDur = [
					tf(ref x, 2, ()=>duration.Hours, hour => SetDur(duration.With(hour:hour)), ref setDur),
					tl(ref x, 1, ':'),
					tf(ref x, 2, ()=>duration.Minutes, minute => SetDur(duration.With(minute:minute)), ref setDur),
					tl(ref x, 1, ':'),
					tf(ref x, 2, ()=>duration.Seconds, second => SetDur(duration.With(second:second)), ref setDur),
					];
				void SetStart (DateTime d) {
					start = d;
					duration = end - start;
					setStart();
					setDur();
				}
				void SetEnd (DateTime d) {
					end = d;
					duration = end - start;
					setEnd();
					setDur();
				}
				void SetDur (TimeSpan t) {
					duration = t;
					end = start + duration;
					setDur();
					setEnd();
				}
				TextField tf (ref int x, int width, Func<object> getValue, Action<int> input, ref Action changed) {
					var t = new TextField() {
						Text = $"{getValue()}",
						X = x,
						Y = y,
						Width = width,
						Height = 1,
						AutoSize = false,
						TabStop = true,
						CanFocus = true,
						ReadOnly = false
					}.Constrain(width);
					changed += () => {
						var next = $"{getValue()}".PadLeft(width, '0');
						if(t.Text == next) {
							return;
						}
						t.Text = next;
					};
					bool busy = false;
					if(input != null)
						t.TextChanged += (a, e) => {
							if(busy) {
								return;
							}
							try {
								busy = true;
								input(int.Parse(e.NewValue));
							} catch(Exception c) {
								e.Cancel = true;
								t.Text = e.OldValue;
							} finally {
								busy = false;
							}
						};
					x += width;
					return t;
				}
				TextField tl (ref int x, int width, object value) {
					var t = new TextField() {
						Text = $"{value}",
						X = x,
						Y = y,
						Width = width,
						Height = 1,
						AutoSize = false,
						TabStop = false,
						CanFocus = false,
						ReadOnly = true
					}.Constrain(width);
					x += width;
					return t;
				}
				y++;
				var repeatLabel = new Label() {
					AutoSize = false,
					X = 0,
					Y = y,
					Width = 8,
					Height = 1,
					Text = "Freq",
				};
				x = 8;
				var repeatGroup = new RadioGroup() {
					X = x,
					Y = y,
					RadioLabels = ["None", "Day", "Week", "Month"],
					Orientation = Orientation.Vertical,
				};
				var interval = 1;
				Action intervalChanged = default;
				var intervalField = tf(ref x, 2, () => interval, i => interval = i, ref intervalChanged);
				x = 16;
				Dictionary<string, CheckBox> days = new() {
					["SU"] = new CheckBox() { X = x, Y = y, Text = "Sunday" },
					["MO"] = new CheckBox() { X = x, Y = 1 + y, Text = "Monday" },
					["TU"] = new CheckBox() { X = x, Y = 2 + y, Text = "Tuesday" },
					["WE"] = new CheckBox() { X = x, Y = 3 + y, Text = "Wednesday" },
					["TH"] = new CheckBox() { X = x, Y = 4 + y, Text = "Thursday" },
					["FR"] = new CheckBox() { X = x, Y = 5 + y, Text = "Friday" },
					["SA"] = new CheckBox() { X = x, Y = 6 + y, Text = "Saturday" }
				};
				x = 32;
				y = 0;
				var nameLabel = new Label() {
					AutoSize = false,
					X = x,
					Y = y,
					Width = 8,
					Height = 1,
					Text = "Name",
				};
				var nameField = new TextField() {
					AutoSize = false,
					X = Pos.Right(nameLabel),
					Y = y,
					Width = Dim.Fill(),
					Height = 1,
				};
				y++;
				var descLabel = new Label() {
					AutoSize = false,
					X = x,
					Y = y,
					Width = 8,
					Height = 1,
					Text = "Desc",
				};
				var descField = new TextField() {
					AutoSize = false,
					X = Pos.Right(descLabel),
					Y = y,
					Width = Dim.Fill(),
					Height = 1,
				};


				SView.InitTree([d, timeLabel, .. timeFieldStart, .. timeFieldEnd, .. timeFieldDur, repeatLabel, repeatGroup, .. days.Values, nameLabel, nameField, descLabel, descField]);
				var ev = new Event();
				ev.Summary = nameField.Text;
				ev.Description = descField.Text;
				nameField.TextChanged += (a, e) => {
					ev.Summary = e.NewValue;
				};
				descField.TextChanged += (a, e) => {
					ev.Description = e.NewValue;
				};
				confirm.MouseClick += (a, e) => {
					var _interval = interval > 1 ? $"INTERVAL={interval};" : "";
					int? count = null;
					int? until = null;
					var _term =
						count != null ?
							$"COUNT={count};" :
						until != null ?
							$"UNTIL={until};" :
							"";
					var _wkst = "";

					string rrule = "";
					switch(repeatGroup.SelectedItem) {
						case 0://None

							break;
						case 1: {//Daily
								var _freq = $"FREQ=DAILY;";
								rrule = $"RRULE:{_freq}{_interval}{_term}";
							}
							break;
						case 2: {//Weekly
								var _freq = $"FREQ=WEEKLY;";
								var _byday = string.Join(",", days
									.Where(pair => pair.Value.Checked == true)
									.Select(pair => pair.Key)) is { Length: > 0 } str ?
										$"BYDAY={str};" :
										"";
								rrule = $"RRULE:{_freq}{_byday}{_interval}{_term}";
							}
							break;
						case 3: {//Monthly
								var _freq = $"FREQ=MONTHLY;";
								rrule = $"RRULE{_freq}{_interval}{_term}";
							}
							break;
						case 4: {
								var _freq = $"FREQ=YEARLY;";
								rrule = $"RRULE:{_freq}{_interval}{_term}";
							}
							break;
					}
					if(rrule.Any()) {
						rrule = rrule[..(rrule.Length - 1)];
						ev.Recurrence = [rrule];
					}
					if(!TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out var id))
						throw new Exception();
					var tz = TimeZone.CurrentTimeZone;
					ev.Start = new EventDateTime() {
						DateTime = start,
						TimeZone = id
					};
					ev.End = new EventDateTime() {
						DateTime = end,
						TimeZone = id
					};
					Event result = service.Events.Insert(ev, cal).ExecuteAsync().Result;
					d.RequestStop();
				};
				cancel.MouseClick += (a, e) => d.RequestStop();
				Application.Run(d);
			};
			{
				var y = 0;
				var nameLabel = new Label() {
					Text = "Name",
					AutoSize = false,
					X = 0,
					Y = y,
					Width = 8,
					Height = 1
				};
				var nameField = new Label() {
					AutoSize=false,
					X = Pos.Right(nameLabel),
					Y = y,
					Width = Dim.Fill(),
					Height = 1
				};
				y++;
				var locLabel = new Label() {
					Text = "Loc",
					AutoSize = false,
					X = 0,
					Y = y,
					Width = 8,
					Height = 1
				};
				var locField = new Label() {
					AutoSize = false,
					X = Pos.Right(locLabel),
					Y = y,
					Width = Dim.Fill(),
					Height = 1
				};
				y++;
				var descLabel = new Label() {
					Text = "Desc",
					AutoSize = false,
					X = 0,
					Y = y,
					Width = 8,
					Height = 1
				};
				var descField = new Label() {
					AutoSize=false,
					X = Pos.Right(descLabel),
					Y = y,
					Width = Dim.Fill(),
					Height = 16,
				};
				y++;
				

				evList.SelectedItemChanged += (a, e) => {
					var item = evData.list[e.Item];
					nameField.Text = item.Summary ?? "N/A";
					descField.Text = item.Description ?? "N/A";
					locField.Text = item.Location ?? "N/A";

				};

				SView.InitTree([evView, nameLabel, nameField, descLabel, descField, locLabel, locField]);
			}
			SView.InitTree([root, evList, evView, create]);
			try {
				//Console.WriteLine("Event created: %s\n", result.HtmlLink);
				return;
			} catch(GoogleApiException ex) {
				Console.WriteLine(ex.ToString());
			}
			void RefreshEvents () {
				evData.list.Clear();
				var it = service.Events.List(cal).Execute().Items.Where(e =>
					e.Summary != null //&& (e.Recurrence?.Any() == true || e.End.DateTimeDateTimeOffset > DateTime.Now)
					).OrderBy(e => e.Start.DateTimeDateTimeOffset.Value.DateTime);
				evData.list.AddRange(it);
			}
		}
	}
}
public static class SField {
	public static TimeSpan With (this TimeSpan t, int? hour = null, int? minute = null, int? second = null) =>
		new TimeSpan(hour ?? t.Hours, minute ?? t.Minutes, second ?? t.Seconds);
	public static DateTime With(this DateTime d, int? year=null, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null) =>
		new DateTime(year??d.Year, month??d.Month, day??d.Day, hour??d.Hour, minute??d.Minute, second??d.Second);
	public static TextField Constrain (this TextField f, int width) {
		f.Enter += (a, e) => {
			f.CursorPosition = 0;
		};
		f.KeyDown += (a, k) => {

			switch(k.KeyCode) {
				case KeyCode.CursorLeft:
					if(f.CursorPosition == 0) {
						f.FocusPrev();
					} else {
						f.CursorPosition--;
					}
					k.Handled = true;
					return;
				case KeyCode.CursorRight:
					if(f.CursorPosition == width - 1) { f.FocusNext(); } else {
						f.CursorPosition++;
					}
					k.Handled = true;
					return;
			}
			var c = (char)k.AsRune.Value;
			if(c == 0) {
				return;
			}
			if(!char.IsDigit(c)) {
				k.Handled = true;
				return;
			}
			f.Text = f.Text.Remove(f.CursorPosition, 1).Insert(f.CursorPosition, $"{c}");
			if(f.CursorPosition < width - 1) {
				f.CursorPosition++;
			} else {
				f.FocusNext();
			}
			k.Handled = true;
		};
		f.Text = f.Text.PadLeft(width, '0');
		return f;
	}
}