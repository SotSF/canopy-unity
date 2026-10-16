using System;
using System.Collections.Generic;
using UnityEngine;

using NodeEditorFramework.IO;

using GenericMenu = NodeEditorFramework.Utilities.GenericMenu;

namespace NodeEditorFramework.Standard
{
	public class NodeEditorInterface
	{
		public NodeEditorUserCache canvasCache;
		public Action<GUIContent> ShowNotificationAction;

		// GUI
		public float toolbarHeight = 17;

		// Modal Panel
		public bool showModalPanel;
		public Rect modalPanelRect = new Rect(20, 50, 250, 70);
		public Action modalPanelContent;

		// IO Format modal panel
		// Format behind File > Load/Save Canvas (XMLImportExport.FormatIdentifier)
		private const string CanvasFileFormat = "XML";
		private ImportExportFormat IOFormat;
		private object[] IOLocationArgs;
		private delegate bool? DefExportLocationGUI(string canvasName, ref object[] locationArgs);
		private delegate bool? DefImportLocationGUI(ref object[] locationArgs);
		private DefImportLocationGUI ImportLocationGUI;
		private DefExportLocationGUI ExportLocationGUI;

		public void ShowNotification(GUIContent message)
		{
			if (ShowNotificationAction != null)
				ShowNotificationAction(message);
		}

#region GUI

		public void DrawToolbarGUI(Rect rect)
		{
			rect.height = toolbarHeight;
			GUILayout.BeginArea (rect, NodeEditorGUI.toolbar);
			GUILayout.BeginHorizontal();
			float curToolbarHeight = 0;

			if (GUILayout.Button("File", NodeEditorGUI.toolbarDropdown, GUILayout.Width(50)))
			{
				GenericMenu menu = new GenericMenu(!Application.isPlaying);

				// A single New Canvas rather than a submenu of canvas types: canopy only uses
				// Calculation canvases. The framework's other built-in type (the Graph example)
				// only traverses from a root node and ignores node changes, so GUI edits on
				// non-ticking nodes never propagated there — the choice was only a trap.
				menu.AddItem(new GUIContent("New Canvas"), false, () => NewNodeCanvas(typeof(NodeEditorFramework.Standard.CalculationCanvasType)));
				menu.AddSeparator("");

				// Load / Save as canvas files (XML) — the format the built app uses, so it leads.
				// Direct items rather than Import/Export submenus: XML is the only format.
				menu.AddItem(new GUIContent("Load Canvas"), false, () => ImportCanvasCallback(CanvasFileFormat));
				if (canvasCache.nodeCanvas.allowSceneSaveOnly)
					menu.AddDisabledItem(new GUIContent("Save Canvas"));
				else
					menu.AddItem(new GUIContent("Save Canvas"), false, () => ExportCanvasCallback(CanvasFileFormat));
				menu.AddSeparator("");

				// Load / Save as Unity assets (editor only)
#if UNITY_EDITOR
				menu.AddItem(new GUIContent("Load Canvas Asset"), false, LoadCanvas);
				menu.AddItem(new GUIContent("Reload Canvas Asset"), false, ReloadCanvas);
				if (canvasCache.nodeCanvas.allowSceneSaveOnly)
				{
					menu.AddDisabledItem(new GUIContent("Save Canvas Asset"));
					menu.AddDisabledItem(new GUIContent("Save Canvas Asset As"));
				}
				else
				{
					menu.AddItem(new GUIContent("Save Canvas Asset"), false, SaveCanvas);
					menu.AddItem(new GUIContent("Save Canvas Asset As"), false, SaveCanvasAs);
				}
				menu.AddSeparator("");
#endif

				// Bundled example canvases (CanvasStorageManager), loaded from Resources on demand.
				// Replaces NEF's Load/Save Canvas to Scene: scene saves made at runtime land in the
				// play-mode / player scene and are lost on exit, so they never persisted here.
				List<string> examples = CanvasStorageManager.Examples;
				if (examples.Count == 0)
					menu.AddDisabledItem(new GUIContent("Load Example"));
				else foreach (string example in examples)
					menu.AddItem(new GUIContent("Load Example/" + CanvasStorageManager.DisplayName(example)), false, LoadExampleCallback, example);

				// Show dropdown
				menu.Show(new Vector2(5, toolbarHeight));
			}
			curToolbarHeight = Mathf.Max(curToolbarHeight, GUILayoutUtility.GetLastRect().yMax);

			GUILayout.Space(10);
			GUILayout.FlexibleSpace();

			GUILayout.Label(new GUIContent("" + canvasCache.nodeCanvas.saveName + " (" + (canvasCache.nodeCanvas.livesInScene ? "Scene Save" : "Asset Save") + ")", 
											"Opened Canvas path: " + canvasCache.nodeCanvas.savePath), NodeEditorGUI.toolbarLabel);
			GUILayout.Label("Type: " + canvasCache.typeData.DisplayString, NodeEditorGUI.toolbarLabel);
			curToolbarHeight = Mathf.Max(curToolbarHeight, GUILayoutUtility.GetLastRect().yMax);

			GUI.backgroundColor = new Color(1, 0.3f, 0.3f, 1);
			if (GUILayout.Button("Force Re-init", NodeEditorGUI.toolbarButton, GUILayout.Width(100)))
			{
				NodeEditor.ReInit(true);
				canvasCache.nodeCanvas.Validate();
			}
#if !UNITY_EDITOR
			GUILayout.Space(5);
			if (GUILayout.Button("Quit", NodeEditorGUI.toolbarButton, GUILayout.Width(100)))
				Application.Quit ();
#endif
			curToolbarHeight = Mathf.Max(curToolbarHeight, GUILayoutUtility.GetLastRect().yMax);
			GUI.backgroundColor = Color.white;

			GUILayout.EndHorizontal();
			GUILayout.EndArea();
			if (Event.current.type == EventType.Repaint)
				toolbarHeight = curToolbarHeight;
		}

		public void DrawModalPanel()
		{
			if (showModalPanel)
			{
				if (modalPanelContent == null)
					return;
				GUILayout.BeginArea(modalPanelRect, NodeEditorGUI.nodeBox);
				modalPanelContent.Invoke();
				GUILayout.EndArea();
			}
		}

#endregion

#region Menu Callbacks

		private void NewNodeCanvas(Type canvasType)
		{
			canvasCache.NewNodeCanvas(canvasType);
		}

#if UNITY_EDITOR
		private void LoadCanvas()
		{
			string path = UnityEditor.EditorUtility.OpenFilePanel("Load Node Canvas", "Assets/PatternSystem/Resources/CanvasSaves", "asset");
			if (!path.Contains(Application.dataPath))
			{
				if (!string.IsNullOrEmpty(path))
					ShowNotification(new GUIContent("You should select an asset inside your project folder!"));
			}
			else
				canvasCache.LoadNodeCanvas(path);
		}

		private void ReloadCanvas()
		{
			string path = canvasCache.nodeCanvas.savePath;
			if (!string.IsNullOrEmpty(path))
			{
				if (path.StartsWith("SCENE/"))
					canvasCache.LoadSceneNodeCanvas(path.Substring(6));
				else
					canvasCache.LoadNodeCanvas(path);
				ShowNotification(new GUIContent("Canvas Reloaded!"));
			}
			else
				ShowNotification(new GUIContent("Cannot reload canvas as it has not been saved yet!"));
		}

		private void SaveCanvas()
		{
			string path = canvasCache.nodeCanvas.savePath;
			if (!string.IsNullOrEmpty(path))
			{
				if (path.StartsWith("SCENE/"))
					canvasCache.SaveSceneNodeCanvas(path.Substring(6));
				else
					canvasCache.SaveNodeCanvas(path);
				ShowNotification(new GUIContent("Canvas Saved!"));
			}
			else
				ShowNotification(new GUIContent("No save location found. Use 'Save As'!"));
		}

		private void SaveCanvasAs()
		{
			string panelPath = "Assets/PatternSystem/Resources/CanvasSaves";
			if (canvasCache.nodeCanvas != null && !string.IsNullOrEmpty(canvasCache.nodeCanvas.savePath))
				panelPath = canvasCache.nodeCanvas.savePath;
			string path = UnityEditor.EditorUtility.SaveFilePanelInProject("Save Node Canvas", "Node Canvas", "asset", "", panelPath);
			if (!string.IsNullOrEmpty(path))
				canvasCache.SaveNodeCanvas(path);
		}
#endif

		private void LoadExampleCallback(object resourcePath)
		{
			string path = (string)resourcePath;
			if (!canvasCache.LoadResourceNodeCanvas(path))
			{
				Debug.LogError("Load Example: no canvas at Resources path '" + path + "' (moved or renamed?)");
				ShowNotification(new GUIContent("Example '" + CanvasStorageManager.DisplayName(path) + "' not found"));
			}
		}

		private void ImportCanvasCallback(string formatID)
		{
			IOFormat = ImportExportManager.ParseFormat(formatID);
			if (IOFormat.RequiresLocationGUI)
			{
				ImportLocationGUI = IOFormat.ImportLocationArgsGUI;
				modalPanelContent = ImportCanvasGUI;
				showModalPanel = true;
			}
			else if (IOFormat.ImportLocationArgsSelection(out IOLocationArgs))
				canvasCache.SetCanvas(ImportExportManager.ImportCanvas(IOFormat, IOLocationArgs));
		}

		private void ImportCanvasGUI()
		{
			if (ImportLocationGUI != null)
			{
				bool? state = ImportLocationGUI(ref IOLocationArgs);
				if (state == null)
					return;

				if (state == true)
					canvasCache.SetCanvas(ImportExportManager.ImportCanvas(IOFormat, IOLocationArgs));

				ImportLocationGUI = null;
				modalPanelContent = null;
				showModalPanel = false;
			}
			else
				showModalPanel = false;
		}

		private void ExportCanvasCallback(string formatID)
		{
			IOFormat = ImportExportManager.ParseFormat(formatID);
			if (IOFormat.RequiresLocationGUI)
			{
				ExportLocationGUI = IOFormat.ExportLocationArgsGUI;
				modalPanelContent = ExportCanvasGUI;
				showModalPanel = true;
			}
			else if (IOFormat.ExportLocationArgsSelection(canvasCache.nodeCanvas.saveName, out IOLocationArgs))
				ImportExportManager.ExportCanvas(canvasCache.nodeCanvas, IOFormat, IOLocationArgs);
		}

		private void ExportCanvasGUI()
		{
			if (ExportLocationGUI != null)
			{
				bool? state = ExportLocationGUI(canvasCache.nodeCanvas.saveName, ref IOLocationArgs);
				if (state == null)
					return;

				if (state == true)
					ImportExportManager.ExportCanvas(canvasCache.nodeCanvas, IOFormat, IOLocationArgs);

				ImportLocationGUI = null;
				modalPanelContent = null;
				showModalPanel = false;
			}
			else
				showModalPanel = false;
		}

#endregion
	}
}
