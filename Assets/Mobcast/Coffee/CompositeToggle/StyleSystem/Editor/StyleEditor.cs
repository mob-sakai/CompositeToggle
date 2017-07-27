using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

using Mobcast.Coffee.Toggles;
using System;
using System.Linq;

namespace Mobcast.Coffee.Toggles
{
	[CustomEditor(typeof(Style), true)]
	[CanEditMultipleObjects]
	public class StyleEditor : Editor
	{
		Style current;
		static ReorderableList roIgnoreProperties;

		void OnEnable()
		{
			current = target as Style;

			StyleAssetEditor.UpdateChangeListeners(current.styleAsset, false);

			// 
			if (roIgnoreProperties == null)
			{
				roIgnoreProperties = new ReorderableList(new List<string>(), typeof(string));
				roIgnoreProperties.drawElementCallback += (rect, index, isActive, isFocused) =>
				{
					var sp = roIgnoreProperties.serializedProperty.GetArrayElementAtIndex(index);
					EditorGUI.LabelField(rect, PropertyEditor.GetMethodPath(sp.stringValue));
				};
				roIgnoreProperties.onAddDropdownCallback += (buttonRect, list) =>
				{
					GenericMenu menu = new GenericMenu();
					var spIgnoreProperties = list.serializedProperty;
					var ignoredIds = new HashSet<string>(
						                 Enumerable.Range(0, spIgnoreProperties.arraySize)
													.Select(i => spIgnoreProperties.GetArrayElementAtIndex(i).stringValue)
					                 );

					Style style = list.serializedProperty.serializedObject.targetObject as Style;
					foreach (Property property in style.styleAsset.GetPropertyEnumerator())
					{
						// Property is ignored?
						string id = property.methodId;
						if (ignoredIds.Contains(id))
							continue;

						menu.AddItem(
							new GUIContent(PropertyEditor.GetMethodPath(id)),
							false,
							() =>
							{
								spIgnoreProperties.InsertArrayElementAtIndex(spIgnoreProperties.arraySize);
								spIgnoreProperties.GetArrayElementAtIndex(spIgnoreProperties.arraySize - 1).stringValue = id;
								spIgnoreProperties.serializedObject.ApplyModifiedProperties();
							}
						);
					}
					menu.DropDown(buttonRect);

				};
				roIgnoreProperties.headerHeight = 0;
				roIgnoreProperties.elementHeight = 18;
			}
		}

		void OnDisable()
		{
			StyleAssetEditor.UpdateChangeListeners(null);
		}

		public override void OnInspectorGUI()
		{
			GUILayout.Space(8);
			serializedObject.Update();

			// Style Asset.
			var spStyleAsset = serializedObject.FindProperty("m_StyleAsset");
			StyleAssetEditor.DrawStyleAssetField(spStyleAsset, style =>
				{
					spStyleAsset.objectReferenceValue = style;
					spStyleAsset.serializedObject.ApplyModifiedProperties();
					EditorApplication.delayCall += () => StyleAssetEditor.UpdateChangeListeners(style as StyleAsset);
				});

			// Style Properties.
			if (!spStyleAsset.hasMultipleDifferentValues)
			{
				EditorGUI.BeginChangeCheck();

				// Draw Toolbar.
				using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
				{
					using (var cc = new EditorGUI.DisabledGroupScope(!current.styleAsset))
					{
						// Save style property from target.
						if (GUILayout.Button(new GUIContent("Save"), EditorStyles.toolbarButton))
						{
							foreach (var property in current.styleAsset.properties)
							{
								property.parameterList.FitSize(1);
								PropertyEditor.FillArgumentsByGetter(current, property.methodInfo, property.parameterList);
							}
							PropertyEditor.onPropertyChanged();
						}

						// Load style property to target.
						if (GUILayout.Button(new GUIContent("Load"), EditorStyles.toolbarButton))
						{
							current.LoadStyle();
						}
					}

					GUILayout.FlexibleSpace();

					// Default Toolbar.
					StyleAssetEditor.DrawStyleToolbar(current, current.styleAsset);
				}

				// Draw style detail.
				if (StyleAssetEditor.isShowProperties && current.styleAsset)
				{
					StyleAssetEditor.DrawProperties(current.styleAsset);
				}

				// When style has changed, apply the style to gameObject.
				if (EditorGUI.EndChangeCheck() && current.styleAsset)
				{
					Debug.Log("hogehoge");
					EditorUtility.SetDirty(current.styleAsset);
					PropertyEditor.onPropertyChanged();
				}
			}
			// Not support multiple StyleAssets.
			else
			{
				EditorGUILayout.HelpBox("Multi-StyleAssets editing is not supported.", MessageType.None);
			}

			// Ignore Properties.
			GUILayout.Space(6);
			EditorGUILayout.LabelField("Ignore Properties", EditorStyles.boldLabel);
			roIgnoreProperties.serializedProperty = serializedObject.FindProperty("m_IgnoreProperties");
			roIgnoreProperties.DoLayoutList();

			serializedObject.ApplyModifiedProperties();

		}
	}
}