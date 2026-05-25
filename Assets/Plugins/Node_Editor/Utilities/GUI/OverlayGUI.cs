using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using MenuFunction = UnityEditor.GenericMenu.MenuFunction;
using MenuFunctionData = UnityEditor.GenericMenu.MenuFunction2;
#else
using MenuFunction = NodeEditorFramework.Utilities.OverlayGUI.CustomMenuFunction;
using MenuFunctionData = NodeEditorFramework.Utilities.OverlayGUI.CustomMenuFunctionData;
#endif

namespace NodeEditorFramework.Utilities 
{
	public static class OverlayGUI 
	{
		public delegate void CustomMenuFunction();
		public delegate void CustomMenuFunctionData(object userData);

		private static string currentEditorUser;

		public static string openedPopupUser = "NONE";
		public static PopupMenu openedPopup;

		/// <summary>
		/// Returns if any popup currently has control.
		/// </summary>
		public static bool HasPopupControl () 
		{
			return openedPopup != null && currentEditorUser == openedPopupUser;
		}

		/// <summary>
		/// Starts the OverlayGUI (Call before any other GUI code!)
		/// </summary>
		public static void StartOverlayGUI (string editorUser) 
		{
			currentEditorUser = editorUser;
			if (HasPopupControl () && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
				openedPopup.Draw ();
		}

		/// <summary>
		/// Ends the OverlayGUI (Call after any other GUI code!)
		/// </summary>
		public static void EndOverlayGUI () 
		{
			if (HasPopupControl () && (Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint))
				openedPopup.Draw ();
		}

		/// <summary>
		/// Opens the specified popupMenu in the current editor users and closes all other popups
		/// </summary>
		public static void OpenPopup (PopupMenu popup)
		{
			openedPopup = popup;
			openedPopupUser = currentEditorUser;
		}

		/// <summary>
		/// Closes the popup in the current editor if existant
		/// </summary>
		public static void ClosePopup ()
		{
			if (HasPopupControl ())
			{
				openedPopup = null;
				openedPopupUser = "NONE";
			}
		}
	}

	/// <summary>
	/// A Generic Popupmenu. Used by GenericMenu, Popup (future), etc.
	/// </summary>
	public class PopupMenu 
	{
		public List<MenuItem> menuItems = new List<MenuItem> ();
		
		// State
		private Rect position;
		private string selectedPath;
		private MenuItem groupToDraw;
		private float currentItemHeight;
		private bool close;
		
		// GUI variables
		public static GUIStyle backgroundStyle;
		public static Texture2D expandRight;
		public static float itemHeight;
		public static GUIStyle selectedLabel;
		// Scaled copies of node label styles, kept local to the popup so we
		// don't enlarge knob/header labels on every node by enlarging nodeLabel.
		public static GUIStyle itemLabel;
		public static GUIStyle itemLabelSelected;

		public float minWidth;

		private const float minCloseDistance = 200;

		// Multiplier on font size, row height, and arrow column for legibility
		// on high-DPI displays.
		private const float MenuScale = 1.5f;
		private const float ExpandArrowSize = 12f * MenuScale;

		// Search-bar mode. searchEnabled adds a text field at the top of the
		// popup; forceFlatList keeps the popup in flat-list mode even when the
		// query is empty (used by the spacebar quick-add palette).
		public bool searchEnabled = false;
		public bool forceFlatList = false;
		private string searchQuery = "";
		private int flatSelectedIndex = 0;
		private int flatScrollIndex = 0;
		private MenuItem[] cachedFlatLeaves = null;
		private MenuItem[] cachedFiltered = null;
		private string cachedFilteredQuery = null;
		private const int MaxVisibleFlatRows = 14;
		// Track the frame Show was called on so we can eat the trailing
		// character event from the spacebar that opened the palette.
		private int showFrame = int.MinValue;
		private bool eatOpeningChar = false;
		// TextEditor drives text editing directly so we don't need IMGUI focus
		// (which doesn't work reliably inside OverlayGUI popups — the popup
		// draws at different points in the OnGUI flow depending on event type,
		// breaking IMGUI's focus state machine).
		private TextEditor textEditor;
		private static GUIStyle searchFieldStyle;

		public PopupMenu () 
		{
			SetupGUI ();
		}
		
		public void SetupGUI ()
		{
			backgroundStyle = new GUIStyle (GUI.skin.box);
			backgroundStyle.contentOffset = new Vector2 (2, 2);
			expandRight = ResourceManager.LoadTexture ("Textures/expandRight.png");

			GUIStyle baseLabel = NodeEditorGUI.nodeLabel ?? GUI.skin.label;
			GUIStyle baseSelected = NodeEditorGUI.nodeLabelSelected ?? GUI.skin.label;
			int basePx = baseLabel.fontSize > 0 ? baseLabel.fontSize : GUI.skin.label.fontSize;
			int scaledPx = Mathf.RoundToInt (basePx * MenuScale);
			itemLabel = new GUIStyle (baseLabel) { fontSize = scaledPx };
			itemLabelSelected = new GUIStyle (baseSelected) { fontSize = scaledPx };
			itemHeight = itemLabel.CalcHeight (new GUIContent ("text"), 100);

			selectedLabel = new GUIStyle (GUI.skin.label);
			selectedLabel.normal.background = RTEditorGUI.ColorToTex (1, new Color (0.4f, 0.4f, 0.4f));

			searchFieldStyle = new GUIStyle (GUI.skin.textField) { fontSize = scaledPx };
		}

		// Minimum width used by both right-click and spacebar popups when
		// search is enabled, so the search field and menu rows always agree.
		private const float SearchPopupMinWidth = 260f;

		public void Show (Vector2 pos, float MinWidth = 40)
		{
			minWidth = MinWidth;
			if (searchEnabled && forceFlatList)
			{
				// Flat mode recomputes its rect in DrawFlatList every frame;
				// only the anchor position matters here.
				position = new Rect (pos.x, pos.y, 0, 0);
			}
			else if (searchEnabled)
			{
				// Redo the up/down flip with the search header included so the
				// header never overlaps the menu and the whole popup fits.
				// Use a shared minimum width so the header and body match.
				Rect menuRect = calculateRect (pos, menuItems, minWidth);
				float unifiedWidth = Mathf.Max (menuRect.width, SearchPopupMinWidth);
				float headerHeight = SearchHeaderHeight ();
				float totalHeight = menuRect.height + headerHeight;
				bool down = (pos.y + totalHeight) <= Screen.height;
				position = new Rect (pos.x, down ? pos.y : pos.y - totalHeight, unifiedWidth, menuRect.height);
			}
			else
			{
				position = calculateRect (pos, menuItems, minWidth);
			}
			selectedPath = "";
			searchQuery = "";
			flatSelectedIndex = 0;
			flatScrollIndex = 0;
			cachedFiltered = null;
			cachedFilteredQuery = null;
			showFrame = Time.frameCount;
			eatOpeningChar = true;
			OverlayGUI.OpenPopup (this);
		}

		private static float SearchHeaderHeight ()
		{
			GUIStyle style = searchFieldStyle ?? GUI.skin.textField;
			float padY = backgroundStyle != null ? backgroundStyle.contentOffset.y : 2f;
			return style.CalcHeight (new GUIContent ("Mg"), 100) + 4 + padY * 2 + 2;
		}

		public Vector2 Position { get { return position.position; } }

#region Creation
		
		public void AddItem (GUIContent content, bool on, MenuFunctionData func, object userData)
		{
			string path;
			MenuItem parent = AddHierarchy (ref content, out path);
			if (parent != null)
				parent.subItems.Add (new MenuItem (path, content, func, userData));
			else
				menuItems.Add (new MenuItem (path, content, func, userData));
		}
		
		public void AddItem (GUIContent content, bool on, MenuFunction func)
		{
			string path;
			MenuItem parent = AddHierarchy (ref content, out path);
			if (parent != null)
				parent.subItems.Add (new MenuItem (path, content, func));
			else
				menuItems.Add (new MenuItem (path, content, func));
		}
		
		public void AddSeparator (string path)
		{
			GUIContent content = new GUIContent (path);
			MenuItem parent = AddHierarchy (ref content, out path);
			if (parent != null)
				parent.subItems.Add (new MenuItem ());
			else
				menuItems.Add (new MenuItem ());
		}
		
		private MenuItem AddHierarchy (ref GUIContent content, out string path) 
		{
			path = content.text;
			if (path.Contains ("/"))
			{ // is inside a group
				string[] subContents = path.Split ('/');
				string folderPath = subContents[0];
				
				// top level group
				MenuItem parent = menuItems.Find ((MenuItem item) => item.content != null && item.content.text == folderPath && item.group);
				if (parent == null)
					menuItems.Add (parent = new MenuItem (folderPath, new GUIContent (folderPath), true));
				// additional level groups
				for (int groupCnt = 1; groupCnt < subContents.Length-1; groupCnt++)
				{
					string folder = subContents[groupCnt];
					folderPath += "/" + folder;
					if (parent == null)
						Debug.LogError ("Parent is null!");
					else if (parent.subItems == null)
						Debug.LogError ("Subitems of " + parent.content.text + " is null!");
					MenuItem subGroup = parent.subItems.Find ((MenuItem item) => item.content != null && item.content.text == folder && item.group);
					if (subGroup == null)
						parent.subItems.Add (subGroup = new MenuItem (folderPath, new GUIContent (folder), true));
					parent = subGroup;
				}
				
				// actual item
				path = content.text;
				content = new GUIContent (subContents[subContents.Length-1], content.tooltip);
				return parent;
			}
			return null;
		}
		
#endregion
		
#region Drawing
		
		public void Draw ()
		{
			// Eat navigation keys before the search TextField sees them so the
			// arrow/enter/escape navigation works while focus is in the field.
			if (searchEnabled)
				HandleSearchKeyboard ();

			bool flat = searchEnabled && (forceFlatList || !string.IsNullOrEmpty (searchQuery));

			int inRect;
			if (flat)
			{
				inRect = DrawFlatList (position);
			}
			else if (searchEnabled)
			{
				// Search field above the existing hierarchical menu. The menu
				// itself is drawn at a shifted Y so it sits beneath the field.
				float fieldRowHeight;
				inRect = DrawSearchHeader (position, out fieldRowHeight);
				Rect bodyPos = new Rect (position.x, position.y + fieldRowHeight, position.width, position.height);
				inRect = Mathf.Max (inRect, DrawGroup (bodyPos, menuItems));

				while (groupToDraw != null && !close)
				{
					MenuItem group = groupToDraw;
					groupToDraw = null;
					if (group.group)
						inRect = Mathf.Max (inRect, DrawGroup (group.groupPos, group.subItems));
				}
			}
			else
			{
				inRect = DrawGroup (position, menuItems);

				while (groupToDraw != null && !close)
				{
					MenuItem group = groupToDraw;
					groupToDraw = null;
					if (group.group) // Draw group and find if the mouse is in group rect
						inRect = Mathf.Max(inRect, DrawGroup(group.groupPos, group.subItems));
				}
			}

			if (close || inRect < 2 || (Event.current.type == EventType.MouseDown && inRect < 3))
				OverlayGUI.ClosePopup ();

			NodeEditor.RepaintClients ();
		}

		// Renders a standalone search-field row above the hierarchical menu.
		// Returns the inRect hit-test state and outputs the row's total height
		// (including padding) so the caller can shift the body down.
		private int DrawSearchHeader (Rect pos, out float rowHeight)
		{
			GUIStyle style = searchFieldStyle ?? GUI.skin.textField;
			float fieldHeight = style.CalcHeight (new GUIContent ("Mg"), 100) + 4;
			float padX = backgroundStyle.contentOffset.x;
			float padY = backgroundStyle.contentOffset.y;
			float width = Mathf.Max (pos.width, 200f);
			rowHeight = fieldHeight + padY * 2 + 2;

			Rect headerRect = new Rect (pos.x, pos.y, width, rowHeight);
			GUI.BeginGroup (extendRect (headerRect, backgroundStyle.contentOffset), GUIContent.none, backgroundStyle);
			Rect fieldRect = new Rect (padX, padY, width - padX * 2, fieldHeight - 4);
			DrawRealSearchField (fieldRect, style);
			GUI.EndGroup ();

			return headerRect.Contains (Event.current.mousePosition) ? 3 : 1;
		}

		// Draws the search field. Text editing is driven by our owned
		// TextEditor in HandleSearchKeyboard (IMGUI focus doesn't work inside
		// OverlayGUI popups). The caret and selection are rendered as
		// overlay rects positioned via GUIStyle.GetCursorPixelPosition, so
		// the text never shifts as the caret blinks.
		private static readonly Color SearchSelectionColor = new Color (0.30f, 0.55f, 0.95f, 0.45f);
		private static readonly Color SearchCaretColor = new Color (0.95f, 0.95f, 0.95f, 1f);

		private void DrawRealSearchField (Rect rect, GUIStyle style)
		{
			GUI.Box (rect, GUIContent.none, style);

			Rect contentRect = new Rect (rect.x + 4, rect.y, rect.width - 8, rect.height);
			GUIContent content = new GUIContent (searchQuery);

			int cursor = textEditor != null
				? Mathf.Clamp (textEditor.cursorIndex, 0, searchQuery.Length)
				: searchQuery.Length;
			int select = textEditor != null
				? Mathf.Clamp (textEditor.selectIndex, 0, searchQuery.Length)
				: cursor;

			// Selection background (drawn before the text so the text sits on top).
			if (Event.current.type == EventType.Repaint && cursor != select)
			{
				int a = Mathf.Min (cursor, select);
				int b = Mathf.Max (cursor, select);
				Vector2 pa = style.GetCursorPixelPosition (contentRect, content, a);
				Vector2 pb = style.GetCursorPixelPosition (contentRect, content, b);
				Color prev = GUI.color;
				GUI.color = SearchSelectionColor;
				GUI.DrawTexture (new Rect (pa.x, pa.y, pb.x - pa.x, style.lineHeight), Texture2D.whiteTexture);
				GUI.color = prev;
			}

			GUI.Label (contentRect, searchQuery, style);

			// Blinking caret as a 1-pixel rect — doesn't shift the text.
			if (Event.current.type == EventType.Repaint && ((int)(Time.realtimeSinceStartup * 2) & 1) == 0)
			{
				Vector2 caretPos = style.GetCursorPixelPosition (contentRect, content, cursor);
				Color prev = GUI.color;
				GUI.color = SearchCaretColor;
				GUI.DrawTexture (new Rect (caretPos.x, caretPos.y, 1, style.lineHeight), Texture2D.whiteTexture);
				GUI.color = prev;
			}
		}

		// Walks the hierarchy and returns every executable leaf, sorted by
		// full path. Cached because the hierarchy is immutable once Show'd.
		private MenuItem[] GetFlatLeaves ()
		{
			if (cachedFlatLeaves != null) return cachedFlatLeaves;
			var leaves = new List<MenuItem> ();
			var stack = new Stack<MenuItem> ();
			for (int i = menuItems.Count - 1; i >= 0; i--) stack.Push (menuItems[i]);
			while (stack.Count > 0)
			{
				var it = stack.Pop ();
				if (it.separator) continue;
				if (it.group)
				{
					if (it.subItems != null)
						for (int i = it.subItems.Count - 1; i >= 0; i--) stack.Push (it.subItems[i]);
				}
				else
				{
					leaves.Add (it);
				}
			}
			leaves.Sort ((a, b) => string.Compare (a.path ?? "", b.path ?? "", StringComparison.OrdinalIgnoreCase));
			cachedFlatLeaves = leaves.ToArray ();
			return cachedFlatLeaves;
		}

		private MenuItem[] GetFilteredLeaves ()
		{
			if (cachedFiltered != null && cachedFilteredQuery == searchQuery)
				return cachedFiltered;
			var all = GetFlatLeaves ();
			if (string.IsNullOrEmpty (searchQuery))
			{
				cachedFiltered = all;
			}
			else
			{
				var result = new List<MenuItem> ();
				foreach (var leaf in all)
				{
					if (leaf.path != null && leaf.path.IndexOf (searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
						result.Add (leaf);
				}
				cachedFiltered = result.ToArray ();
			}
			cachedFilteredQuery = searchQuery;
			return cachedFiltered;
		}

		private TextEditor EnsureTextEditor ()
		{
			if (textEditor == null)
			{
				textEditor = new TextEditor ();
				textEditor.text = searchQuery;
				textEditor.cursorIndex = searchQuery.Length;
				textEditor.selectIndex = searchQuery.Length;
			}
			return textEditor;
		}

		private void SyncQueryFromEditor (TextEditor te)
		{
			if (te.text != searchQuery)
			{
				searchQuery = te.text;
				cachedFiltered = null;
				flatSelectedIndex = 0;
				flatScrollIndex = 0;
			}
		}

		private void HandleSearchKeyboard ()
		{
			var e = Event.current;
			if (e.type != EventType.KeyDown) return;

			// Eat the trailing character event from the spacebar that opened
			// the palette. IMGUI fires a separate character KeyDown right
			// after the keyCode one, on the same frame as Show was called.
			// Restricted to ' ' so we don't swallow a real first character if
			// the popup opens by some other means.
			if (eatOpeningChar && Time.frameCount == showFrame
			    && e.character == ' ' && e.keyCode == KeyCode.None)
			{
				eatOpeningChar = false;
				e.Use ();
				return;
			}
			if (Time.frameCount != showFrame)
				eatOpeningChar = false;

			// Navigation / dismissal: consume before TextEditor sees them, so
			// the down-arrow drives list navigation rather than caret movement.
			switch (e.keyCode)
			{
				case KeyCode.DownArrow:
				{
					var filtered = GetFilteredLeaves ();
					if (filtered.Length > 0)
						flatSelectedIndex = Mathf.Min (flatSelectedIndex + 1, filtered.Length - 1);
					e.Use ();
					return;
				}
				case KeyCode.UpArrow:
				{
					var filtered = GetFilteredLeaves ();
					if (filtered.Length > 0)
						flatSelectedIndex = Mathf.Max (flatSelectedIndex - 1, 0);
					e.Use ();
					return;
				}
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
				{
					var filtered = GetFilteredLeaves ();
					if (filtered.Length > 0 && flatSelectedIndex < filtered.Length)
					{
						filtered[flatSelectedIndex].Execute ();
						close = true;
						e.Use ();
					}
					return;
				}
				case KeyCode.Escape:
					close = true;
					e.Use ();
					return;
			}

			// Hand off to TextEditor for OS hotkeys: Shift+Home, Cmd+A,
			// Home/End, Cmd+Backspace, arrow-within-text, clipboard, etc.
			// HandleKeyEvent doesn't insert printable characters — that's a
			// separate event we handle manually below.
			var te = EnsureTextEditor ();
			if (te.text != searchQuery)
			{
				te.text = searchQuery;
				te.cursorIndex = te.selectIndex = searchQuery.Length;
			}
			if (te.HandleKeyEvent (e))
			{
				SyncQueryFromEditor (te);
				e.Use ();
				return;
			}

			// Insert printable characters via TextEditor so the cursor / any
			// active selection are respected. Skip when a non-typing modifier
			// is held so Cmd+V etc. don't accidentally type 'v'.
			bool hasModifier = (e.modifiers & (EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command)) != 0;
			char c = e.character;
			if (!hasModifier && c >= ' ' && c != 127)
			{
				te.Insert (c);
				SyncQueryFromEditor (te);
				e.Use ();
			}
		}

		private int DrawFlatList (Rect pos)
		{
			var filtered = GetFilteredLeaves ();
			if (filtered.Length == 0) flatSelectedIndex = 0;
			else flatSelectedIndex = Mathf.Clamp (flatSelectedIndex, 0, filtered.Length - 1);

			int visibleRows = Mathf.Max (1, Mathf.Min (filtered.Length, MaxVisibleFlatRows));
			// Keep the highlighted row in view as it moves past the visible window.
			if (flatSelectedIndex < flatScrollIndex) flatScrollIndex = flatSelectedIndex;
			if (flatSelectedIndex >= flatScrollIndex + visibleRows) flatScrollIndex = flatSelectedIndex - visibleRows + 1;
			flatScrollIndex = Mathf.Clamp (flatScrollIndex, 0, Mathf.Max (0, filtered.Length - visibleRows));

			float fieldHeight = (searchFieldStyle ?? GUI.skin.textField).CalcHeight (new GUIContent ("Mg"), 100) + 4;
			float rowsHeight = (filtered.Length == 0 ? 1 : visibleRows) * itemHeight;

			float width = CalcFlatWidth (filtered, fieldHeight);
			float padY = backgroundStyle.contentOffset.y;
			float height = fieldHeight + rowsHeight + padY * 2 + 2;

			bool down = (pos.position.y + height) <= Screen.height;
			Rect rect = new Rect (pos.position.x, pos.position.y - (down ? 0 : height), width, height);

			GUI.BeginGroup (extendRect (rect, backgroundStyle.contentOffset), GUIContent.none, backgroundStyle);

			float padX = backgroundStyle.contentOffset.x;
			Rect fieldRect = new Rect (padX, padY, width - padX * 2, fieldHeight - 4);
			DrawRealSearchField (fieldRect, searchFieldStyle ?? GUI.skin.textField);

			float rowY = fieldHeight + padY;
			if (filtered.Length == 0)
			{
				GUI.Label (new Rect (padX, rowY, width - padX * 2, itemHeight),
					new GUIContent ("(no matches)"), itemLabel);
			}
			else
			{
				for (int i = 0; i < visibleRows; i++)
				{
					int idx = flatScrollIndex + i;
					if (idx >= filtered.Length) break;
					var item = filtered[idx];
					Rect rowRect = new Rect (padX, rowY, width - padX * 2, itemHeight);

					if (rowRect.Contains (Event.current.mousePosition))
					{
						flatSelectedIndex = idx;
						if (Event.current.type == EventType.MouseDown ||
						    (Event.current.button != 1 && Event.current.type == EventType.MouseUp))
						{
							item.Execute ();
							close = true;
							Event.current.Use ();
						}
					}

					var style = (idx == flatSelectedIndex) ? itemLabelSelected : itemLabel;
					GUI.Label (rowRect, new GUIContent (item.path), style);
					rowY += itemHeight;
				}
			}

			GUI.EndGroup ();

			position = rect;

			int inRect = 1;
			if (rect.Contains (Event.current.mousePosition))
				inRect = 3;
			else
			{
				Rect clickRect = new Rect (rect.x - minCloseDistance, rect.y - minCloseDistance, rect.width + 2 * minCloseDistance, rect.height + 2 * minCloseDistance);
				if (clickRect.Contains (Event.current.mousePosition))
					inRect = 2;
			}
			return inRect;
		}

		private float CalcFlatWidth (MenuItem[] items, float fieldHeight)
		{
			float w = Mathf.Max (minWidth, 260f);
			GUIStyle measure = itemLabel ?? GUI.skin.label;
			for (int i = 0; i < items.Length; i++)
			{
				float candidate = measure.CalcSize (new GUIContent (items[i].path)).x + 16f;
				if (candidate > w) w = candidate;
			}
			return w;
		}
		
		private int DrawGroup (Rect pos, List<MenuItem> menuItems)
		{
			// Honor a caller-supplied width floor — search mode passes a unified
			// width so the body matches the header. Submenus pass 0 and keep
			// the natural item-derived width.
			Rect rect = calculateRect (pos.position, menuItems, Mathf.Max (minWidth, pos.width));

			// DRAW GROUP
			currentItemHeight = backgroundStyle.contentOffset.y;
			GUI.BeginGroup (extendRect (rect, backgroundStyle.contentOffset), GUIContent.none, backgroundStyle);
			for (int itemCnt = 0; itemCnt < menuItems.Count; itemCnt++)
			{
				DrawItem (menuItems[itemCnt], rect);
				if (close) break;
			}
			GUI.EndGroup ();

			// MOUSE POS RECT TEST
			int inRect = 1; // State 1: Outside of all rects
			if (rect.Contains(Event.current.mousePosition))
				inRect = 3; // State 3: Inside group rect
			else
			{
				Rect clickRect = new Rect(rect.x - minCloseDistance, rect.y - minCloseDistance, rect.width + 2 * minCloseDistance, rect.height + 2 * minCloseDistance);
				if (clickRect.Contains(Event.current.mousePosition))
					inRect = 2; // State 2: Inside extended click rect
			}

			return inRect;
		}
		
		private void DrawItem (MenuItem item, Rect groupRect) 
		{
			if (item.separator) 
			{
				if (Event.current.type == EventType.Repaint)
					RTEditorGUI.Seperator (new Rect (backgroundStyle.contentOffset.x+1, currentItemHeight+1, groupRect.width-2, 1));
				currentItemHeight += 3;
			}
			else 
			{
				Rect labelRect = new Rect (backgroundStyle.contentOffset.x, currentItemHeight, groupRect.width, itemHeight);

				if (labelRect.Contains (Event.current.mousePosition))
					selectedPath = item.path;

				bool selected = selectedPath == item.path || selectedPath.Contains (item.path + "/");
				GUI.Label (labelRect, item.content, selected? itemLabelSelected : itemLabel);

				if (item.group)
				{
					GUI.DrawTexture (new Rect (labelRect.x+labelRect.width-ExpandArrowSize, labelRect.y+(labelRect.height-ExpandArrowSize)/2, ExpandArrowSize, ExpandArrowSize), expandRight);
					if (selected)
					{
						item.groupPos = new Rect (groupRect.x+groupRect.width+4, groupRect.y+currentItemHeight-2, 0, 0);
						groupToDraw = item;
					}
				}
				else if (selected && (Event.current.type == EventType.MouseDown || (Event.current.button != 1 && Event.current.type == EventType.MouseUp)))
				{
					item.Execute ();
					close = true;
					Event.current.Use ();
				}
				
				currentItemHeight += itemHeight;
			}
		}
		
		private static Rect extendRect (Rect rect, Vector2 extendValue) 
		{
			rect.x -= extendValue.x;
			rect.y -= extendValue.y;
			rect.width += extendValue.x+extendValue.x;
			rect.height += extendValue.y+extendValue.y;
			return rect;
		}
		
		private static Rect calculateRect (Vector2 position, List<MenuItem> menuItems, float minWidth) 
		{
			Vector2 size;
			float width = minWidth, height = 0;
			
			GUIStyle measure = itemLabel ?? GUI.skin.label;
			float groupPad = ExpandArrowSize + 10f;
			float itemPad = 10f * MenuScale;
			for (int itemCnt = 0; itemCnt < menuItems.Count; itemCnt++)
			{
				MenuItem item = menuItems[itemCnt];
				if (item.separator)
					height += 3;
				else
				{
					width = Mathf.Max (width, measure.CalcSize (item.content).x + (item.group? groupPad : itemPad));
					height += itemHeight;
				}
			}
			
			size = new Vector2 (width, height);
			bool down = (position.y+size.y) <= Screen.height;
			return new Rect (position.x, position.y - (down? 0 : size.y), size.x, size.y);
		}
		
#endregion
		
#region Nested MenuItem
		
		public class MenuItem
		{
			public string path;
			// -!Separator
			public GUIContent content;
			// -Executable Item
			public MenuFunction func;
			public MenuFunctionData funcData;
			public object userData;
			// -Non-executables
			public bool separator = false;
			// --Group
			public bool group = false;
			public Rect groupPos;
			public List<MenuItem> subItems;
			
			public MenuItem ()
			{
				separator = true;
			}
			
			public MenuItem (string _path, GUIContent _content, bool _group)
			{
				path = _path;
				content = _content;
				group = _group;
				
				if (group)
					subItems = new List<MenuItem> ();
			}
			
			public MenuItem (string _path, GUIContent _content, MenuFunction _func)
			{
				path = _path;
				content = _content;
				func = _func;
			}
			
			public MenuItem (string _path, GUIContent _content, MenuFunctionData _func, object _userData)
			{
				path = _path;
				content = _content;
				funcData = _func;
				userData = _userData;
			}
			
			public void Execute ()
			{
				if (funcData != null)
					funcData (userData);
				else if (func != null)
					func ();
			}
		}
		
#endregion
	}

	/// <summary>
	/// Generic Menu which mimics UnityEditor.GenericMenu class pretty much exactly. Wrapper for the generic PopupMenu.
	/// </summary>
	public class GenericMenu
	{
#if UNITY_EDITOR
		private UnityEditor.GenericMenu editorMenu;
#endif
		private static PopupMenu popup;

		public Vector2 Position { get { return popup.Position; } }

		// Forward search-mode flags through to the underlying PopupMenu.
		// No-ops when the wrapper is using UnityEditor.GenericMenu.
		public bool searchEnabled
		{
			get { return popup != null && popup.searchEnabled; }
			set { if (popup != null) popup.searchEnabled = value; }
		}
		public bool forceFlatList
		{
			get { return popup != null && popup.forceFlatList; }
			set { if (popup != null) popup.forceFlatList = value; }
		}
		
		public GenericMenu (bool emulateEditor = false) 
		{
#if UNITY_EDITOR
			if (emulateEditor)
				editorMenu = new UnityEditor.GenericMenu();
			else
#endif
				popup = new PopupMenu ();
		}
		
		public void ShowAsContext ()
		{
#if UNITY_EDITOR
			if (editorMenu != null)
				editorMenu.ShowAsContext();
			else
#endif
				popup.Show (GUIScaleUtility.GUIToScreenSpace(Event.current.mousePosition));
		}

		public void Show(Vector2 pos, float MinWidth = 40)
		{
#if UNITY_EDITOR
			if (editorMenu != null)
				editorMenu.DropDown(new Rect (pos, Vector2.zero));
			else
#endif
				popup.Show(GUIScaleUtility.GUIToScreenSpace (pos), MinWidth);
		}

		public void DropDown(Rect rect, float MinWidth = 40)
		{
#if UNITY_EDITOR
			if (editorMenu != null)
				editorMenu.DropDown(rect);
			else
#endif
				popup.Show(GUIScaleUtility.GUIToScreenSpace (rect.position), Mathf.Max (rect.width, MinWidth));
		}

		public void AddItem (GUIContent content, bool on, MenuFunctionData func, object userData)
		{
#if UNITY_EDITOR
			if (editorMenu != null)
				editorMenu.AddItem (content, on, func, userData);
			else
#endif
				popup.AddItem (content, on, func, userData);
		}

		public void AddItem(GUIContent content, bool on, MenuFunction func)
		{
#if UNITY_EDITOR
			if (editorMenu != null)
				editorMenu.AddItem(content, on, func);
			else
#endif
				popup.AddItem(content, on, func);
		}

		public void AddDisabledItem(GUIContent content)
		{
#if UNITY_EDITOR
			if (editorMenu != null)
				editorMenu.AddDisabledItem(content);
			else
#endif
				popup.AddItem(content, false, null);
		}

		public void AddSeparator (string path)
		{
#if UNITY_EDITOR
			if (editorMenu != null)
				editorMenu.AddSeparator(path);
			else
#endif
				popup.AddSeparator (path);
		}
	}
}