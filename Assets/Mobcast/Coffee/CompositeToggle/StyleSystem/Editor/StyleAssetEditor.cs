using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Mobcast.Coffee.Toggles;
using System;
using System.IO;

namespace Mobcast.Coffee.Toggles
{
	[CustomEditor(typeof(StyleAsset), true)]
	public class StyleAssetEditor : Editor
	{
		static StyleAsset lastChecked;

		static List<Style> targetSheets = new List<Style>();

		static readonly Type s_TypeRt = typeof(RectTransform);
		static HashSet<string> s_RectTransformCommonProperties = new HashSet<string>
		{
			PropertyEditor.ConvertToMethodIdentifier(s_TypeRt.GetProperty("anchorMin").GetSetMethod(), s_TypeRt),
			PropertyEditor.ConvertToMethodIdentifier(s_TypeRt.GetProperty("anchorMax").GetSetMethod(), s_TypeRt),
			PropertyEditor.ConvertToMethodIdentifier(s_TypeRt.GetProperty("pivot").GetSetMethod(), s_TypeRt),
			PropertyEditor.ConvertToMethodIdentifier(s_TypeRt.GetProperty("anchoredPosition3D").GetSetMethod(), s_TypeRt),
			PropertyEditor.ConvertToMethodIdentifier(s_TypeRt.GetProperty("sizeDelta").GetSetMethod(), s_TypeRt),
		};

		static bool s_IsShowProperties = true;

		public static bool isShowProperties
		{
			get { return s_IsShowProperties; }
			set
			{
				if (s_IsShowProperties == value)
					return;

				s_IsShowProperties = value;
				EditorPrefs.SetBool(typeof(StyleAssetEditor).FullName + ".isShowProperties", s_IsShowProperties);
			}
		}

		[InitializeOnLoadMethod]
		static void hohoho()
		{
			// When add/remove property, reflesh style.
			PropertyEditor.onPropertyChanged += () => targetSheets.ForEach(s => s.LoadStyle());
			s_IsShowProperties = EditorPrefs.GetBool(typeof(StyleAssetEditor).FullName + ".isShowProperties", true);
		}

		void OnEnable()
		{
			UpdateChangeListeners(target as StyleAsset);
		}


		void OnDisable()
		{
			UpdateChangeListeners(null);
		}

		public static void UpdateChangeListeners(StyleAsset asset, bool onChanged = true)
		{
			if (lastChecked == asset)
				return;
			
			targetSheets.Clear();
			lastChecked = asset;
			if (asset)
			{
				targetSheets = Resources.FindObjectsOfTypeAll<Style>()
					.Where(x => x.styleAsset && x.styleAsset.Contains(asset))
					.ToList();

				if(onChanged)
					PropertyEditor.onPropertyChanged();
			}
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
//			DrawStyle(null, target as StyleAsset);


			// Toolbar.
			EditorGUI.BeginChangeCheck();
			DrawStyleToolbar(null, target as StyleAsset);

			// Draw style detail.
			if (isShowProperties && target)
			{
				DrawProperties(target as StyleAsset);
			}

			// When style has changed, apply the style to gameObject.
			if (EditorGUI.EndChangeCheck() && target)
			{
				EditorUtility.SetDirty(target);
				PropertyEditor.onPropertyChanged();
			}
			serializedObject.ApplyModifiedProperties();
		}

		/// <summary>
		/// Draws the style sheet toolbar.
		/// </summary>
		/// <param name="behavior">Target behavior.</param>
		/// <param name="styleSheetAsset">Style sheet asset.</param>
		public static void DrawStyleToolbar(Style style, StyleAsset styleAsset)
		{
//			bool changed = GUI.changed;

			using (new EditorGUI.DisabledGroupScope(!styleAsset))
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				GUILayout.FlexibleSpace ();
				if (GUILayout.Toggle(isShowProperties, new GUIContent("Show Detal"), EditorStyles.toolbarButton) != isShowProperties)
				{
					isShowProperties = !isShowProperties;
				}
			
				if (GUILayout.Button("Bake", EditorStyles.toolbarButton))
				{
					PropertyEditor.Bake(styleAsset.GetPropertyEnumerator());
				}

				if (GUILayout.Button("Add Property", EditorStyles.toolbarPopup))
				{
					IEnumerable<System.Type> availableTypes = Enumerable.Empty<System.Type>();
					if (style)
						availableTypes = availableTypes.Concat(style.GetComponents<Component>()
							.Where(x => x != style)
							.Select(x => x.GetType()));

					if (styleAsset)
						availableTypes = availableTypes.Concat(styleAsset.GetPropertyEnumerator()
						.Where(x => x.methodInfo != null)
						.Select(x => x.methodInfo.ReflectedType));

					var menu = PropertyEditor.CreateTargetMenu(style, styleAsset.properties, availableTypes, 1);
					AppendAvailableOptionToMenu(menu, style, styleAsset.properties).ShowAsContext();
				}
			}
//			GUI.changed = changed;
		}


		static GenericMenu AppendAvailableOptionToMenu(GenericMenu menu, Style style, List<Property> activedTargets)
		{
			// Options.
			menu.AddSeparator("");
			menu.AddDisabledItem(new GUIContent("Options"));

			HashSet<string> ids = new HashSet<string>(activedTargets.Select(x => x.methodId));
			bool rtCommonProp = s_RectTransformCommonProperties.All(ids.Contains);

			menu.AddItem(new GUIContent("RectTransform Common Properties"),
				rtCommonProp,
				() =>
				{
					foreach (var id in s_RectTransformCommonProperties)
					{
						if (rtCommonProp && ids.Contains(id))
							PropertyEditor.DeactivatePropetyTarget(id, activedTargets);
						else if (!rtCommonProp && !ids.Contains(id))
							PropertyEditor.ActivatePropetyTarget(style, activedTargets, PropertyEditor.ConvertToMethodInfo(id), id, 1);
					}
				});
			return menu;
		}

		/// <summary>
		/// Draws the style asset field.
		/// </summary>
		/// <param name="property">Property.</param>
		/// <param name="onSelect">On select.</param>
		public static void DrawStyleAssetField(SerializedProperty property, Action<UnityEngine.Object> onSelect)
		{
			Rect r = EditorGUILayout.GetControlRect();

			// Object field.
			Rect rField = new Rect(r.x, r.y, r.width - 16, r.height);
			EditorGUI.BeginChangeCheck();
			EditorGUI.PropertyField(rField, property);
			if (EditorGUI.EndChangeCheck())
				onSelect(property.objectReferenceValue);

			// Popup to select style asset in project.
			Rect rPopup = new Rect(r.x + rField.width, r.y + 4, 16, r.height - 4);
			if (GUI.Button(rPopup, EditorGUIUtility.FindTexture("icon dropdown"), EditorStyles.label))
			{
				// Create style asset.
				GenericMenu menu = new GenericMenu();
				menu.AddItem(new GUIContent("Create New Style Asset"), false, () =>
					{
						// Open save file dialog.
						string filename = AssetDatabase.GenerateUniqueAssetPath("Assets/New Style Asset.asset");
						string path = EditorUtility.SaveFilePanelInProject("Create New Style Asset", Path.GetFileName(filename), "asset", "");
						if (path.Length == 0)
							return;

						// Create and save a new builder asset.
						StyleAsset styleAsset = ScriptableObject.CreateInstance(typeof(StyleAsset)) as StyleAsset;
						AssetDatabase.CreateAsset(styleAsset, path);
						AssetDatabase.SaveAssets();
						EditorGUIUtility.PingObject(styleAsset);
						onSelect(styleAsset);
					});
				menu.AddSeparator("");

				// Unselect style asset.
				menu.AddItem(
					new GUIContent("-"),
					!property.hasMultipleDifferentValues && !property.objectReferenceValue,
					() => onSelect(null)
				);

				// Select style asset.
				foreach (string path in AssetDatabase.FindAssets ("t:" + typeof(StyleAsset).Name).Select (x => AssetDatabase.GUIDToAssetPath (x)))
				{
					string assetName = Path.GetFileNameWithoutExtension(path);
					string displayedName = assetName.Replace(" - ", "/");
					bool active = !property.hasMultipleDifferentValues && property.objectReferenceValue && (property.objectReferenceValue.name == assetName);
					menu.AddItem(
						new GUIContent(displayedName),
						active,
						x => onSelect(AssetDatabase.LoadAssetAtPath((string)x, typeof(StyleAsset))),
						path
					);
				}

				menu.ShowAsContext();
			}
		}

		/*
		public static void DrawStyle(Style style, StyleAsset styleAsset)
		{
			// Toolbar.
			EditorGUI.BeginChangeCheck();
			DrawStyleToolbar(style, styleAsset);

			// Draw style detail.
			if (isShowProperties && styleAsset)
			{
				DrawProperties(styleAsset);
			}

			// When style has changed, apply the style to gameObject.
			if (EditorGUI.EndChangeCheck() && styleAsset)
			{
				EditorUtility.SetDirty(styleAsset);
				PropertyEditor.onPropertyChanged();
			}
		}*/

		/// <summary>
		/// Draws the style full.
		/// </summary>
		/// <returns>The style full.</returns>
		/// <param name="styleSheetAsset">Style sheet asset.</param>
		public static StyleAsset DrawProperties(StyleAsset styleAsset)
		{
			if (!styleAsset)
				return styleAsset;

			StyleAsset ret = styleAsset;

			// Draw all sheet.
			int depth = 0;
			foreach (var validStyleAsset in styleAsset.GetStyleAssetEnumerator())
			{
				EditorGUI.BeginDisabledGroup(0 < depth);

				GUILayout.Space(-5);
				EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));

				IEnumerable<Property> properties = validStyleAsset.properties;
				HashSet<string> ids = new HashSet<string>(properties.Select(x => x.methodId));


				// Included all RectTransform common properties in style.
				if (s_RectTransformCommonProperties.All(ids.Contains))
				{
					
					EditorGUILayout.LabelField("RectTransform Common Properties", EditorStyles.boldLabel);
					PropertyEditor.abbreviation = true;
					EditorGUI.indentLevel++;
					// Draw RectTransform common properties.
					foreach (var property in properties.Where(x=>s_RectTransformCommonProperties.Contains(x.methodId)))
					{
						if (property.parameterList != null)
							property.parameterList.FitSize(1);
						PropertyEditor.DrawPropertyField(property, 0, true);
					}
					EditorGUI.indentLevel--;
					PropertyEditor.abbreviation = false;
					properties = properties.Where(x => !s_RectTransformCommonProperties.Contains(x.methodId));
					GUILayout.Space(8);
				}

				// Draw all properties.
				foreach (var property in properties)
				{
					if (property.parameterList != null)
						property.parameterList.FitSize(1);
					PropertyEditor.DrawPropertyField(property, 0, true);
				}

				// If current sheet is main, draw base style sheet.
				using (new EditorGUI.DisabledGroupScope(validStyleAsset != styleAsset))
				{
					var so = new SerializedObject(validStyleAsset);
					var spStyleAsset = so.FindProperty("m_BaseStyleAsset");
					if (validStyleAsset == styleAsset || spStyleAsset.objectReferenceValue)
					{

						so.Update();

						GUILayout.Space(22);
						DrawStyleAssetField(spStyleAsset,
							style =>
							{
								spStyleAsset.objectReferenceValue = style;
								spStyleAsset.serializedObject.ApplyModifiedProperties();
							});
					}
				}
				EditorGUI.EndDisabledGroup();
				depth++;
			}

			while (0 < depth--)
				EditorGUILayout.EndVertical();
				
			return ret;
		}

		/// <summary>
		/// Draws the style sheet field.
		/// </summary>
		/// <returns>The style sheet field.</returns>
		/// <param name="label">Label.</param>
		/// <param name="style">Style.</param>
		static StyleAsset DrawStyleAssetField(string label, StyleAsset style, Action<StyleAsset> onSelect = null)
		{
			Rect r = EditorGUILayout.GetControlRect();
//			GUI.Label(r, GUIContent.none, "dockarea");

			Rect rField = new Rect(r.x, r.y, r.width - 20, r.height);
			var ret = EditorGUI.ObjectField(rField, label, style, typeof(StyleAsset), false) as StyleAsset;

			Rect rPopup = new Rect(r.x + rField.width, r.y + 4, 20, r.height - 4);
			if (GUI.Button(rPopup, EditorGUIUtility.FindTexture("icon dropdown"), EditorStyles.label))
			{
				GenericMenu menu = new GenericMenu();

				// Create style asset.
				menu.AddItem(new GUIContent("Create New Style Asset"), false, () =>
					{
						// Open save file dialog.
						string filename = AssetDatabase.GenerateUniqueAssetPath("Assets/New Style Asset.asset");
						string path = EditorUtility.SaveFilePanelInProject("Create New Style Asset", Path.GetFileName(filename), "asset", "");
						if (path.Length == 0)
							return;

						// Create and save a new builder asset.
						StyleAsset styleAsset = ScriptableObject.CreateInstance(typeof(StyleAsset)) as StyleAsset;
						AssetDatabase.CreateAsset(styleAsset, path);
						AssetDatabase.SaveAssets();
						EditorGUIUtility.PingObject(styleAsset);
						onSelect(styleAsset);
					});
				menu.AddSeparator("");

				menu.AddItem(new GUIContent("-"), !style, () => onSelect(null));

				// Select style asset.
				foreach (string path in AssetDatabase.FindAssets ("t:" + typeof(StyleAsset).Name).Select (x => AssetDatabase.GUIDToAssetPath (x)))
				{
					string displayName = Path.GetFileNameWithoutExtension(path);
					menu.AddItem(
						new GUIContent(displayName),
						style && (style.name == displayName),
						x => onSelect(AssetDatabase.LoadAssetAtPath((string)x, typeof(StyleAsset)) as StyleAsset),
						path
					);
				}

				menu.ShowAsContext();
			}
			return ret;
		}


//		/// <summary>
//		/// Applies the style to targets.
//		/// </summary>
//		public static void ApplyStyleToTargets()
//		{
//			foreach (var sheet in targetSheets)
//			{
//				if (sheet)
//					sheet.LoadStyle();
//			}
//		}
	}

}