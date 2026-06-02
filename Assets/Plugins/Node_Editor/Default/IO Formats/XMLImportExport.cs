using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Reflection;
using UnityEngine;

namespace NodeEditorFramework.IO
{
	public class XMLImportExport : StructuredImportExportFormat
	{
		public override string FormatIdentifier { get { return "XML"; } }
		public override string FormatExtension { get { return "xml"; } }

		public override void ExportData(CanvasData data, params object[] args)
		{
			if (args == null || args.Length != 1 || args[0].GetType() != typeof(string))
				throw new ArgumentException("Location Arguments");
			string path = (string)args[0];

			XmlDocument saveDoc = new XmlDocument();
			XmlDeclaration decl = saveDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
			saveDoc.InsertBefore(decl, saveDoc.DocumentElement);

			// CANVAS

			XmlElement canvas = saveDoc.CreateElement("NodeCanvas");
			canvas.SetAttribute("type", data.type.FullName);
			saveDoc.AppendChild(canvas);

			// EDITOR STATES

			XmlElement editorStates = saveDoc.CreateElement("EditorStates");
			canvas.AppendChild(editorStates);
			foreach (EditorStateData stateData in data.editorStates)
			{
				XmlElement editorState = saveDoc.CreateElement("EditorState");
				editorState.SetAttribute("selected", stateData.selectedNode != null ? stateData.selectedNode.nodeID.ToString() : "");
				editorState.SetAttribute("pan", stateData.panOffset.x + "," + stateData.panOffset.y);
				editorState.SetAttribute("zoom", stateData.zoom.ToString());
				editorStates.AppendChild(editorState);
			}

			// GROUPS

			XmlElement groups = saveDoc.CreateElement("Groups");
			canvas.AppendChild(groups);
			foreach (GroupData groupData in data.groups)
			{
				XmlElement group = saveDoc.CreateElement("Group");
				group.SetAttribute("name", groupData.name);
				group.SetAttribute("rect", groupData.rect.x + "," + groupData.rect.y + "," + groupData.rect.width + "," + groupData.rect.height);
				group.SetAttribute("color", groupData.color.r + "," + groupData.color.g + "," + groupData.color.b + "," + groupData.color.a);
				groups.AppendChild(group);
			}

			// NODES

			XmlElement nodes = saveDoc.CreateElement("Nodes");
			canvas.AppendChild(nodes);
			foreach (NodeData nodeData in data.nodes.Values)
			{
				XmlElement node = saveDoc.CreateElement("Node");
				node.SetAttribute("name", nodeData.name);
				node.SetAttribute("ID", nodeData.nodeID.ToString());
				node.SetAttribute("type", nodeData.typeID);
				node.SetAttribute("pos", nodeData.nodePos.x + "," + nodeData.nodePos.y);
				nodes.AppendChild(node);

				// NODE PORTS

				foreach (PortData portData in nodeData.connectionPorts)
				{
					XmlElement port = saveDoc.CreateElement("Port");
					port.SetAttribute("ID", portData.portID.ToString());
					port.SetAttribute("name", portData.name);
					port.SetAttribute("dynamic", portData.dynamic.ToString());
					if (portData.dynamic)
					{ // Serialize dynamic port
						port.SetAttribute("type", portData.dynaType.FullName);
						foreach (string fieldName in portData.port.AdditionalDynamicKnobData())
							SerializeFieldToXML(port, portData.port, fieldName); // Serialize all dynamic knob variables
					}
					node.AppendChild(port);
				}

				// NODE VARIABLES

				foreach (VariableData varData in nodeData.variables)
				{ // Serialize all node variables
					if (varData.refObject != null)
					{ // Serialize reference-type variables as 'Variable' element
						XmlElement variable = saveDoc.CreateElement("Variable");
						variable.SetAttribute("name", varData.name);
						variable.SetAttribute("refID", varData.refObject.refID.ToString());
						node.AppendChild(variable);
					}
					else // Serialize value-type fields in-line
						SerializeFieldToXML(node, nodeData.node, varData.name);
				}
			}

			// CONNECTIONS

			XmlElement connections = saveDoc.CreateElement("Connections");
			canvas.AppendChild(connections);
			foreach (ConnectionData connectionData in data.connections)
			{
				XmlElement connection = saveDoc.CreateElement("Connection");
				connection.SetAttribute("port1ID", connectionData.port1.portID.ToString());
				connection.SetAttribute("port2ID", connectionData.port2.portID.ToString());
				connections.AppendChild(connection);
			}

			// OBJECTS

			XmlElement objects = saveDoc.CreateElement("Objects");
			canvas.AppendChild(objects);
			foreach (ObjectData objectData in data.objects.Values)
			{
				XmlElement obj = saveDoc.CreateElement("Object");
				obj.SetAttribute("refID", objectData.refID.ToString());
				obj.SetAttribute("type", objectData.data.GetType().FullName);
				objects.AppendChild(obj);
				if (SerializeObjectToXML(obj, objectData.data) == null)
					objects.RemoveChild(obj); // Non-serializable; leave no empty element behind (already logged)
			}

			// WRITE

			Directory.CreateDirectory(Path.GetDirectoryName(path));
			using (XmlTextWriter writer = new XmlTextWriter(path, Encoding.UTF8))
			{
				writer.Formatting = Formatting.Indented;
				writer.Indentation = 1;
				writer.IndentChar = '\t';
				saveDoc.Save(writer);
			}
		}

		public override CanvasData ImportData(params object[] args)
		{
			if (args == null || args.Length != 1 || args[0].GetType() != typeof(string))
				throw new ArgumentException("Location Arguments");
			string path = (string)args[0];

			using (FileStream fs = new FileStream(path, FileMode.Open))
			{
				XmlDocument data = new XmlDocument();
				data.Load(fs);

				// CANVAS

				string canvasName = Path.GetFileNameWithoutExtension(path);
				XmlElement xmlCanvas = (XmlElement)data.SelectSingleNode("//NodeCanvas");
				Type canvasType = NodeCanvasManager.GetCanvasTypeData(xmlCanvas.GetAttribute("type")).CanvasType;
				if (canvasType == null)
					throw new XmlException("Could not find NodeCanvas of type '" + xmlCanvas.GetAttribute("type") + "'!");
				CanvasData canvasData = new CanvasData(canvasType, canvasName);
				Dictionary<int, PortData> ports = new Dictionary<int, PortData>();

				// OBJECTS

				IEnumerable<XmlElement> xmlObjects = xmlCanvas.SelectNodes("Objects/Object").OfType<XmlElement>();
				foreach (XmlElement xmlObject in xmlObjects)
				{
					int refID = GetIntegerAttribute(xmlObject, "refID");
					string typeName = xmlObject.GetAttribute("type");
					Type type = ResolveType(typeName);
					if (type == null)
					{ // Skip rather than abort the whole import; referencing variables stay at their defaults
						Debug.LogWarning("[XMLImport] Could not resolve type '" + typeName + "' for object refID " + refID + "; skipping it.");
						continue;
					}
					if (typeof(UnityEngine.Object).IsAssignableFrom(type))
					{ // GPU/asset objects (RenderTexture, Texture, Material...) can't be reconstructed from XML
					  // (the generated reader NREs in UnityEngine.Object.set_name). Skip; node rebuilds at runtime.
						Debug.LogWarning("[XMLImport] Skipping UnityEngine.Object reference of type '" + typeName + "' (refID " + refID + "); these aren't supported by XML serialization.");
						continue;
					}
					object obj;
					try
					{ // Defense in depth: a single malformed object must not abort the whole import
						obj = DeserializeObjectFromXML(xmlObject, type);
					}
					catch (Exception e)
					{
						Debug.LogWarning("[XMLImport] Failed to deserialize object refID " + refID + " of type '" + typeName + "': " + e.Message + "; skipping it.");
						continue;
					}
					if (obj == null)
					{ // Serialization may have failed on export (logged then), leaving an empty Object element
						Debug.LogWarning("[XMLImport] Could not deserialize object refID " + refID + " of type '" + typeName + "'; skipping it.");
						continue;
					}
					if (canvasData.objects.ContainsKey(refID))
					{ // Duplicate refID (corrupt/hand-edited or legacy file) -- keep the first
						Debug.LogWarning("[XMLImport] Duplicate object refID " + refID + "; ignoring later occurrence.");
						continue;
					}
					ObjectData objData = new ObjectData(refID, obj);
					canvasData.objects.Add(refID, objData);
				}

				// NODES

				IEnumerable<XmlElement> xmlNodes = xmlCanvas.SelectNodes("Nodes/Node").OfType<XmlElement>();
				foreach (XmlElement xmlNode in xmlNodes)
				{
					string name = xmlNode.GetAttribute("name");
					int nodeID = GetIntegerAttribute(xmlNode, "ID");
					string typeID = xmlNode.GetAttribute("type");
					Vector2 nodePos = GetVectorAttribute(xmlNode, "pos");
					// Record
					NodeData node = new NodeData(name, typeID, nodeID, nodePos);
					canvasData.nodes.Add(nodeID, node);

					// NODE PORTS

					IEnumerable<XmlElement> xmlConnectionPorts = xmlNode.SelectNodes("Port").OfType<XmlElement>();
					foreach (XmlElement xmlPort in xmlConnectionPorts)
					{
						int portID = GetIntegerAttribute(xmlPort, "ID");
						string portName = xmlPort.GetAttribute("name");
						if (string.IsNullOrEmpty(portName)) // Fallback to old save
							portName = xmlPort.GetAttribute("varName");
						bool dynamic = GetBooleanAttribute(xmlPort, "dynamic", false);
						PortData portData;
						if (!dynamic) // Record static port
							portData = new PortData(node, portName, portID);
						else
						{ // Deserialize dynamic port
							string typeName = xmlPort.GetAttribute("type");
							Type portType = ResolveType(typeName);
							if (portType == null || (portType != typeof(ConnectionPort) && !portType.IsSubclassOf(typeof(ConnectionPort))))
								continue; // Invalid type stored
							ConnectionPort port = (ConnectionPort)ScriptableObject.CreateInstance(portType);
							port.name = portName;
							foreach (XmlElement portVar in xmlPort.ChildNodes.OfType<XmlElement>())
								DeserializeFieldFromXML(portVar, port);
							portData = new PortData(node, port, portID);
						}
						node.connectionPorts.Add(portData);
						ports.Add(portID, portData);
					}

					// NODE VARIABLES
					
					foreach (XmlElement variable in xmlNode.ChildNodes.OfType<XmlElement>())
					{ // Deserialize all value-type variables
						if (variable.Name != "Variable" && variable.Name != "Port")
						{
							string varName = variable.GetAttribute("name");
							object varValue = DeserializeFieldFromXML(variable, node.type, null);
							VariableData varData = new VariableData(varName);
							varData.value = varValue;
							node.variables.Add(varData);
						}
					}

					IEnumerable<XmlElement> xmlVariables = xmlNode.SelectNodes("Variable").OfType<XmlElement>();
					foreach (XmlElement xmlVariable in xmlVariables)
					{ // Deserialize all reference-type variables (and also value type variables in old save files)
						string varName = xmlVariable.GetAttribute("name");
						VariableData varData = new VariableData(varName);
						if (xmlVariable.HasAttribute("refID"))
						{ // Read reference-type variables from objects
							int refID = GetIntegerAttribute(xmlVariable, "refID");
							ObjectData objData;
							if (canvasData.objects.TryGetValue(refID, out objData))
								varData.refObject = objData;
						}
						else
						{ // Read value-type variable (old save file only) TODO: Remove
							string typeName = xmlVariable.GetAttribute("type");
							Type type = ResolveType(typeName);
							if (type != null)
								varData.value = DeserializeObjectFromXML(xmlVariable, type);
							else
								Debug.LogWarning("[XMLImport] Could not resolve type '" + typeName + "' for variable '" + varName + "'.");
						}
						node.variables.Add(varData);
					}
				}

				// CONNECTIONS

				IEnumerable<XmlElement> xmlConnections = xmlCanvas.SelectNodes("Connections/Connection").OfType<XmlElement>();
				foreach (XmlElement xmlConnection in xmlConnections)
				{
					int port1ID = GetIntegerAttribute(xmlConnection, "port1ID");
					int port2ID = GetIntegerAttribute(xmlConnection, "port2ID");
					PortData port1, port2;
					if (ports.TryGetValue(port1ID, out port1) && ports.TryGetValue(port2ID, out port2))
						canvasData.RecordConnection(port1, port2);
				}

				// GROUPS

				IEnumerable<XmlElement> xmlGroups = xmlCanvas.SelectNodes("Groups/Group").OfType<XmlElement>();
				foreach (XmlElement xmlGroup in xmlGroups)
				{
					string name = xmlGroup.GetAttribute("name");
					Rect rect = GetRectAttribute(xmlGroup, "rect");
					Color color = GetColorAttribute(xmlGroup, "color");
					canvasData.groups.Add(new GroupData(name, rect, color));
				}

				// EDITOR STATES

				IEnumerable<XmlElement> xmlEditorStates = xmlCanvas.SelectNodes("EditorStates/EditorState").OfType<XmlElement>();
				List<EditorStateData> editorStates = new List<EditorStateData>();
				foreach (XmlElement xmlEditorState in xmlEditorStates)
				{
					Vector2 pan = GetVectorAttribute(xmlEditorState, "pan");
					float zoom;
					if (!float.TryParse(xmlEditorState.GetAttribute("zoom"), out zoom))
						zoom = 1;
					// Selected Node
					NodeData selectedNode = null;
					int selectedNodeID;
					if (int.TryParse(xmlEditorState.GetAttribute("selected"), out selectedNodeID))
						selectedNode = canvasData.FindNode(selectedNodeID);
					// Create state
					editorStates.Add(new EditorStateData(selectedNode, pan, zoom));
				}
				canvasData.editorStates = editorStates.ToArray();

				return canvasData;
			}
		}

		#region Utility

		/// <summary>
		/// Resolves a Type from a stored type name, searching every loaded assembly.
		/// Type.GetType(name) only looks in mscorlib and the *calling* assembly (here the framework,
		/// Assembly-CSharp-firstpass), so a bare FullName for a user type in Assembly-CSharp (e.g.
		/// SecretFire.TextureSynth.RadioButtonSet) would fail to load. Returns null if not found.
		/// Handles both plain FullNames (legacy exports) and assembly-qualified names.
		/// </summary>
		private static Type ResolveType(string typeName)
		{
			if (string.IsNullOrEmpty(typeName))
				return null;
			Type type = Type.GetType(typeName); // Fast path: AQNs and framework/mscorlib types
			if (type != null)
				return type;
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				type = assembly.GetType(typeName);
				if (type != null)
					return type;
			}
			return null;
		}

		private XmlElement SerializeFieldToXML(XmlElement parent, object obj, string fieldName)
		{
			Type type = obj.GetType();
			FieldInfo field = type.GetField(fieldName);
			if (field == null)
			{
				Debug.LogWarning("Failed to find field " + fieldName + " on type " + type.Name);
				return null;
			}
			object fieldValue = field.GetValue(obj);
			XmlElement serializedValue = SerializeObjectToXML(parent, fieldValue);
			if (serializedValue != null)
			{
				serializedValue.SetAttribute("name", fieldName);
				return serializedValue;
			}
			return null;
		}

		private object DeserializeFieldFromXML(XmlElement xmlElement, object obj)
		{
			Type type = obj.GetType();
			return DeserializeFieldFromXML(xmlElement, type, obj);
		}

		private object DeserializeFieldFromXML(XmlElement xmlElement, Type type, object obj = null)
		{
			string fieldName = xmlElement.GetAttribute("name");
			FieldInfo field = type.GetField(fieldName);
			if (field == null)
			{
				Debug.LogWarning("Failed to find field " + fieldName + " on type " + type.Name);
				return null;
			}
			object fieldValue = DeserializeObjectFromXML(xmlElement, field.FieldType, false);
			if (obj != null)
				field.SetValue(obj, fieldValue);
			return fieldValue;
		}

		private XmlElement SerializeObjectToXML(XmlElement parent, object obj)
		{
			// TODO: Need to handle asset references
			// Because of runtime compability, always try to embed objects
			// If that fails, try to find references to assets (e.g. for textures)
			if (obj is UnityEngine.Object)
			{ // GPU/asset objects (RenderTexture, Texture, Material...) cannot round-trip through
			  // XmlSerializer -- it may appear to write one, but importing it NREs in set_name. Skip
			  // it; the referencing field is left at its default and rebuilt by the node at runtime.
				Debug.LogWarning("[XMLExport] Skipping UnityEngine.Object reference of type '" + obj.GetType().FullName + "' (asset/GPU references aren't supported by XML serialization).");
				return null;
			}
			if (customConverters.TryGetValue(obj.GetType(), out ICustomXmlConverter converter))
			{ // Types XmlSerializer mishandles (e.g. Gradient, AnimationCurve) get an explicit converter
				XmlElement element = parent.OwnerDocument.CreateElement(obj.GetType().Name);
				parent.AppendChild(element);
				converter.Write(element, obj);
				return element;
			}
			try
			{ // Try to embed object
				XmlSerializer serializer = new XmlSerializer(obj.GetType());
				XPathNavigator navigator = parent.CreateNavigator();
				using (XmlWriter writer = navigator.AppendChild())
				{
					writer.WriteWhitespace("");
					serializer.Serialize(writer, obj);
				}
				return (XmlElement)parent.LastChild;
			}
			catch (Exception e)
			{ // Most common cause: the type (or a type it contains) has no public parameterless
			  // constructor, which XmlSerializer requires. Caller drops the reference; on import the
			  // field is left at its default (and nodes that derive it rebuild it in DoInit).
				Debug.LogWarning("[XMLExport] Could not serialize type '" + obj.GetType().FullName + "': " + e.Message);
				return null;
			}
		}

		private object DeserializeObjectFromXML(XmlElement xmlElement, Type type, bool isParent = true)
		{
			if (isParent && !xmlElement.HasChildNodes)
				return null;
			XmlNode dataNode = isParent ? xmlElement.FirstChild : xmlElement;
			if (dataNode is XmlElement dataElement && customConverters.TryGetValue(type, out ICustomXmlConverter converter))
				return converter.Read(dataElement);
			XmlSerializer serializer = new XmlSerializer(type);
			XPathNavigator navigator = dataNode.CreateNavigator();
			using (XmlReader reader = navigator.ReadSubtree())
				return serializer.Deserialize(reader);
		}

		public delegate bool TryParseHandler<T>(string value, out T result);
		private T GetAttribute<T>(XmlElement element, string attribute, TryParseHandler<T> handler, T defaultValue)
		{
			T result;
			if (handler(element.GetAttribute(attribute), out result))
				return result;
			return defaultValue;
		}
		private T GetAttribute<T>(XmlElement element, string attribute, TryParseHandler<T> handler)
		{
			T result;
			if (!handler(element.GetAttribute(attribute), out result))
				throw new XmlException("Invalid " + typeof(T).Name + " " + attribute + " for element " + element.Name + "!");
			return result;
		}

		private int GetIntegerAttribute(XmlElement element, string attribute, bool throwIfInvalid = true)
		{
			int result = 0;
			if (!int.TryParse(element.GetAttribute(attribute), out result) && throwIfInvalid)
				throw new XmlException("Invalid Int " + attribute + " for element " + element.Name + "!");
			return result;
		}

		private float GetFloatAttribute(XmlElement element, string attribute, bool throwIfInvalid = true)
		{
			float result = 0;
			if (!float.TryParse(element.GetAttribute(attribute), out result) && throwIfInvalid)
				throw new XmlException("Invalid Float " + attribute + " for element " + element.Name + "!");
			return result;
		}

		private bool GetBooleanAttribute(XmlElement element, string attribute, bool throwIfInvalid = true)
		{
			bool result = false;
			if (!bool.TryParse(element.GetAttribute(attribute), out result) && throwIfInvalid)
				throw new XmlException("Invalid Bool " + attribute + " for element " + element.Name + "!");
			return result;
		}

		private Vector2 GetVectorAttribute(XmlElement element, string attribute, bool throwIfInvalid = false)
		{
			string[] vecString = element.GetAttribute(attribute).Split(',');
			Vector2 vector = new Vector2(0, 0);
			float vecX, vecY;
			if (vecString.Length == 2 && float.TryParse(vecString[0], out vecX) && float.TryParse(vecString[1], out vecY))
				vector = new Vector2(vecX, vecY);
			else if (throwIfInvalid)
				throw new XmlException("Invalid Vector2 " + attribute + " for element " + element.Name + "!");
			return vector;
		}

		private Color GetColorAttribute(XmlElement element, string attribute, bool throwIfInvalid = false)
		{
			string[] vecString = element.GetAttribute(attribute).Split(',');
			Color color = Color.white;
			float colR, colG, colB, colA;
			if (vecString.Length == 4 && float.TryParse(vecString[0], out colR) && float.TryParse(vecString[1], out colG) && float.TryParse(vecString[2], out colB) && float.TryParse(vecString[3], out colA))
				color = new Color(colR, colG, colB, colA);
			else if (throwIfInvalid)
				throw new XmlException("Invalid Color " + attribute + " for element " + element.Name + "!");
			return color;
		}

		private Rect GetRectAttribute(XmlElement element, string attribute, bool throwIfInvalid = false)
		{
			string[] vecString = element.GetAttribute(attribute).Split(',');
			Rect rect = new Rect(0, 0, 100, 100);
			float x, y, w, h;
			if (vecString.Length == 4 && float.TryParse(vecString[0], out x) && float.TryParse(vecString[1], out y) && float.TryParse(vecString[2], out w) && float.TryParse(vecString[3], out h))
				rect = new Rect(x, y, w, h);
			else if (throwIfInvalid)
				throw new XmlException("Invalid Rect " + attribute + " for element " + element.Name + "!");
			return rect;
		}

		// --- Custom type converters ---
		// Some Unity types (e.g. Gradient, AnimationCurve) have a public parameterless ctor so
		// XmlSerializer accepts them, yet it does not faithfully round-trip their data -- you get a
		// default value back. Register an explicit converter for such types; it takes precedence over
		// the generic XmlSerializer path in Serialize/DeserializeObjectFromXML. Add entries as needed.
		private interface ICustomXmlConverter
		{
			void Write(XmlElement element, object obj);
			object Read(XmlElement element);
		}

		private static readonly Dictionary<Type, ICustomXmlConverter> customConverters = new Dictionary<Type, ICustomXmlConverter>
		{
			{ typeof(Gradient), new GradientXmlConverter() },
			{ typeof(AnimationCurve), new AnimationCurveXmlConverter() },
		};

		// Invariant-culture float round-trip ("R" = exact) so values survive comma-decimal locales.
		private static string FloatToXml(float f) { return f.ToString("R", CultureInfo.InvariantCulture); }
		private static float FloatFromXml(string s) { float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f); return f; }

		private class GradientXmlConverter : ICustomXmlConverter
		{
			public void Write(XmlElement element, object obj)
			{
				Gradient gradient = (Gradient)obj;
				XmlDocument doc = element.OwnerDocument;
				element.SetAttribute("mode", gradient.mode.ToString());
				foreach (GradientColorKey key in gradient.colorKeys)
				{
					XmlElement k = doc.CreateElement("ColorKey");
					k.SetAttribute("time", FloatToXml(key.time));
					k.SetAttribute("r", FloatToXml(key.color.r));
					k.SetAttribute("g", FloatToXml(key.color.g));
					k.SetAttribute("b", FloatToXml(key.color.b));
					element.AppendChild(k);
				}
				foreach (GradientAlphaKey key in gradient.alphaKeys)
				{
					XmlElement k = doc.CreateElement("AlphaKey");
					k.SetAttribute("time", FloatToXml(key.time));
					k.SetAttribute("a", FloatToXml(key.alpha));
					element.AppendChild(k);
				}
			}

			public object Read(XmlElement element)
			{
				List<GradientColorKey> colorKeys = new List<GradientColorKey>();
				List<GradientAlphaKey> alphaKeys = new List<GradientAlphaKey>();
				foreach (XmlElement k in element.ChildNodes.OfType<XmlElement>())
				{
					if (k.Name == "ColorKey")
						colorKeys.Add(new GradientColorKey(
							new Color(FloatFromXml(k.GetAttribute("r")), FloatFromXml(k.GetAttribute("g")), FloatFromXml(k.GetAttribute("b"))),
							FloatFromXml(k.GetAttribute("time"))));
					else if (k.Name == "AlphaKey")
						alphaKeys.Add(new GradientAlphaKey(FloatFromXml(k.GetAttribute("a")), FloatFromXml(k.GetAttribute("time"))));
				}
				Gradient gradient = new Gradient();
				if (colorKeys.Count > 0 && alphaKeys.Count > 0)
					gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
				if (Enum.TryParse(element.GetAttribute("mode"), out GradientMode mode))
					gradient.mode = mode;
				return gradient;
			}
		}

		private class AnimationCurveXmlConverter : ICustomXmlConverter
		{
			public void Write(XmlElement element, object obj)
			{
				AnimationCurve curve = (AnimationCurve)obj;
				XmlDocument doc = element.OwnerDocument;
				element.SetAttribute("preWrapMode", curve.preWrapMode.ToString());
				element.SetAttribute("postWrapMode", curve.postWrapMode.ToString());
				foreach (Keyframe kf in curve.keys)
				{
					XmlElement k = doc.CreateElement("Key");
					k.SetAttribute("time", FloatToXml(kf.time));
					k.SetAttribute("value", FloatToXml(kf.value));
					k.SetAttribute("inTangent", FloatToXml(kf.inTangent));
					k.SetAttribute("outTangent", FloatToXml(kf.outTangent));
					k.SetAttribute("inWeight", FloatToXml(kf.inWeight));
					k.SetAttribute("outWeight", FloatToXml(kf.outWeight));
					k.SetAttribute("weightedMode", kf.weightedMode.ToString());
					element.AppendChild(k);
				}
			}

			public object Read(XmlElement element)
			{
				List<Keyframe> keys = new List<Keyframe>();
				foreach (XmlElement k in element.ChildNodes.OfType<XmlElement>())
				{
					if (k.Name != "Key") continue;
					Keyframe kf = new Keyframe(
						FloatFromXml(k.GetAttribute("time")), FloatFromXml(k.GetAttribute("value")),
						FloatFromXml(k.GetAttribute("inTangent")), FloatFromXml(k.GetAttribute("outTangent")),
						FloatFromXml(k.GetAttribute("inWeight")), FloatFromXml(k.GetAttribute("outWeight")));
					if (Enum.TryParse(k.GetAttribute("weightedMode"), out WeightedMode wm))
						kf.weightedMode = wm;
					keys.Add(kf);
				}
				AnimationCurve curve = new AnimationCurve(keys.ToArray());
				if (Enum.TryParse(element.GetAttribute("preWrapMode"), out WrapMode pre))
					curve.preWrapMode = pre;
				if (Enum.TryParse(element.GetAttribute("postWrapMode"), out WrapMode post))
					curve.postWrapMode = post;
				return curve;
			}
		}

		#endregion
	}
}